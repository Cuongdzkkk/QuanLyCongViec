using System.Text.Json;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Moq;
using TaskManagement.API.Controllers;
using TaskManagement.API.Extensions;
using TaskManagement.Application.Auth;
using TaskManagement.Application.DTOs.Auth;
using TaskManagement.Application.Interfaces;
using TaskManagement.Domain.Entities;
using TaskManagement.Infrastructure.Data;
using TaskManagement.Infrastructure.Services;

namespace TaskManagement.Tests.Logic;

public sealed class GoogleAuthTests
{
    private const string SubjectA = "GOOGLE_SUBJECT_A";
    private const string SubjectB = "GOOGLE_SUBJECT_B";

    [Fact]
    public async Task ValidCredential_CreatesUserAndPersistentExternalIdentity()
    {
        await using var context = CreateContext();
        var service = CreateService(context, Identity(SubjectA, "new@example.com", "Google Name"));

        var result = await service.GoogleLoginAsync(new GoogleLoginRequestDto { Credential = "valid-id-token" });

        result.response.Email.Should().Be("new@example.com");
        result.response.FullName.Should().Be("Google Name");
        result.response.AccessToken.Should().StartWith("access:");
        result.refreshToken.Should().StartWith("refresh:");
        var user = await context.Users.SingleAsync();
        user.PasswordHash.Should().BeEmpty();
        user.IsActive.Should().BeTrue();
        var login = await context.ExternalLogins.SingleAsync();
        login.UserId.Should().Be(user.Id);
        login.Provider.Should().Be("Google");
        login.ProviderSubject.Should().Be(SubjectA);
        login.ProviderEmail.Should().Be("new@example.com");
    }

    [Fact]
    public async Task RepeatedLogin_SameSubjectReturnsSameUserWithoutDuplicates()
    {
        await using var context = CreateContext();
        var validator = new MutableGoogleValidator(Identity(SubjectA, "first@example.com", "First Name"));
        var service = CreateService(context, validator);
        var first = await service.GoogleLoginAsync(new GoogleLoginRequestDto { Credential = "credential-one" });
        validator.Identity = Identity(SubjectA, "renamed@example.com", "Changed By Google");

        var second = await service.GoogleLoginAsync(new GoogleLoginRequestDto { Credential = "credential-two" });

        second.response.Id.Should().Be(first.response.Id);
        second.response.Email.Should().Be("first@example.com");
        second.response.FullName.Should().Be("First Name");
        (await context.Users.CountAsync()).Should().Be(1);
        (await context.ExternalLogins.CountAsync()).Should().Be(1);
        (await context.RefreshTokens.CountAsync()).Should().Be(2);
    }

    [Fact]
    public async Task ExistingCanonicalEmailWithoutLink_ReturnsConflictAndPreservesAccount()
    {
        await using var context = CreateContext();
        var role = new Role { Id = Guid.NewGuid(), Name = "Manager" };
        var user = User("existing@example.com", active: true, deleted: false, passwordHash: "password-hash");
        context.AddRange(role, user, new UserRole { UserId = user.Id, RoleId = role.Id });
        await context.SaveChangesAsync();
        var service = CreateService(context, Identity(SubjectA, "existing@example.com", "Untrusted Rename"));

        var action = () => service.GoogleLoginAsync(new GoogleLoginRequestDto { Credential = "valid-id-token" });

        await action.Should().ThrowAsync<GoogleAccountConflictException>();
        var unchanged = await context.Users.Include(item => item.UserRoles).SingleAsync();
        unchanged.PasswordHash.Should().Be("password-hash");
        unchanged.FullName.Should().Be("Existing User");
        unchanged.UserRoles.Should().ContainSingle();
        (await context.ExternalLogins.CountAsync()).Should().Be(0);
    }

    [Fact]
    public async Task DeletedEmailWithoutLink_IsNotRecreated()
    {
        await using var context = CreateContext();
        context.Users.Add(User("deleted@example.com", active: false, deleted: true));
        await context.SaveChangesAsync();
        var service = CreateService(context, Identity(SubjectA, "deleted@example.com"));

        var action = () => service.GoogleLoginAsync(new GoogleLoginRequestDto { Credential = "valid-id-token" });

        await action.Should().ThrowAsync<GoogleAccountForbiddenException>();
        (await context.Users.CountAsync(user => user.Email == "deleted@example.com")).Should().Be(1);
        (await context.ExternalLogins.CountAsync()).Should().Be(0);
    }

    [Theory]
    [InlineData(false, false)]
    [InlineData(true, true)]
    public async Task InactiveOrDeletedLinkedUser_DoesNotReceiveTokens(bool active, bool deleted)
    {
        await using var context = CreateContext();
        var user = User("blocked@example.com", active, deleted);
        context.AddRange(user, Login(user, SubjectA));
        await context.SaveChangesAsync();
        var jwt = JwtMock();
        var service = CreateService(context, Identity(SubjectA, "blocked@example.com"), jwt);

        var action = () => service.GoogleLoginAsync(new GoogleLoginRequestDto { Credential = "valid-id-token" });

        await action.Should().ThrowAsync<GoogleAccountForbiddenException>();
        jwt.Verify(item => item.GenerateAccessToken(It.IsAny<User>(), It.IsAny<IList<string>>()), Times.Never);
        (await context.RefreshTokens.CountAsync()).Should().Be(0);
    }

    [Fact]
    public async Task ProviderSubjectWinsOverConflictingGoogleEmail()
    {
        await using var context = CreateContext();
        var userA = User("user-a@example.com");
        var userB = User("user-b@example.com");
        context.AddRange(userA, userB, Login(userA, SubjectA), Login(userB, SubjectB));
        await context.SaveChangesAsync();
        var service = CreateService(context, Identity(SubjectA, "user-b@example.com", "Spoofed Profile"));

        var result = await service.GoogleLoginAsync(new GoogleLoginRequestDto { Credential = "valid-id-token" });

        result.response.Id.Should().Be(userA.Id);
        result.response.Email.Should().Be("user-a@example.com");
        (await context.Users.CountAsync()).Should().Be(2);
    }

    [Fact]
    public async Task LinkedPasswordAccount_PreservesPasswordRoleAndMembership()
    {
        await using var context = CreateContext();
        var role = new Role { Id = Guid.NewGuid(), Name = "Manager" };
        var user = User("hybrid@example.com", passwordHash: "existing-password-hash");
        var workspaceId = Guid.NewGuid();
        context.AddRange(
            role,
            user,
            Login(user, SubjectA),
            new UserRole { UserId = user.Id, RoleId = role.Id },
            new Workspace
            {
                Id = workspaceId,
                Name = "Protected Workspace",
                Slug = $"protected-{Guid.NewGuid():N}",
                OwnerId = user.Id,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            },
            new WorkspaceMember
            {
                WorkspaceId = workspaceId,
                UserId = user.Id,
                WorkspaceRole = "OWNER",
                IsActive = true
            });
        await context.SaveChangesAsync();
        var service = CreateService(context, Identity(SubjectA, "hybrid@example.com", "Changed Name"));

        var result = await service.GoogleLoginAsync(new GoogleLoginRequestDto { Credential = "valid-id-token" });

        result.response.SystemRoles.Should().Contain("Manager");
        var unchanged = await context.Users.SingleAsync();
        unchanged.PasswordHash.Should().Be("existing-password-hash");
        unchanged.FullName.Should().Be("Existing User");
        (await context.WorkspaceMembers.SingleAsync()).WorkspaceRole.Should().Be("OWNER");
    }

    [Fact]
    public async Task EmptyCredential_IsRejectedBeforeProviderCall()
    {
        await using var context = CreateContext();
        var validator = new Mock<IGoogleIdentityValidator>(MockBehavior.Strict);
        var service = CreateService(context, validator.Object);

        var action = () => service.GoogleLoginAsync(new GoogleLoginRequestDto { Credential = " " });

        await action.Should().ThrowAsync<ArgumentException>();
        validator.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task ExtraFrontendIdentityFields_DoNotOverrideValidatedGoogleIdentity()
    {
        await using var context = CreateContext();
        var request = JsonSerializer.Deserialize<GoogleLoginRequestDto>(
            """
            {
              "credential": "valid-id-token",
              "email": "attacker@example.com",
              "userId": "USER_EXISTING",
              "fullName": "Attacker Supplied"
            }
            """)!;
        var service = CreateService(context, Identity(SubjectA, "verified@example.com", "Verified Name"));

        var result = await service.GoogleLoginAsync(request);

        result.response.Email.Should().Be("verified@example.com");
        result.response.FullName.Should().Be("Verified Name");
        (await context.Users.SingleAsync()).Email.Should().Be("verified@example.com");
    }

    [Theory]
    [InlineData("wrong-audience")]
    [InlineData("wrong-issuer")]
    [InlineData("expired")]
    [InlineData("invalid-signature")]
    [InlineData("email-not-verified")]
    public async Task InvalidGoogleValidation_IsRejectedWithoutCreatingAccount(string category)
    {
        await using var context = CreateContext();
        var validator = new Mock<IGoogleIdentityValidator>();
        validator.Setup(item => item.ValidateAsync(category, It.IsAny<CancellationToken>()))
            .ThrowsAsync(new GoogleCredentialException());
        var jwt = JwtMock();
        var service = CreateService(context, validator.Object, jwt);

        var action = () => service.GoogleLoginAsync(new GoogleLoginRequestDto { Credential = category });

        await action.Should().ThrowAsync<GoogleCredentialException>();
        (await context.Users.CountAsync()).Should().Be(0);
        (await context.ExternalLogins.CountAsync()).Should().Be(0);
        jwt.Verify(item => item.GenerateAccessToken(It.IsAny<User>(), It.IsAny<IList<string>>()), Times.Never);
    }

    [Fact]
    public void ExternalLoginModel_HasDatabaseUniquenessForSubjectAndUserProvider()
    {
        using var context = CreateContext();
        var entity = context.Model.FindEntityType(typeof(ExternalLogin))!;
        var indexes = entity.GetIndexes().Where(index => index.IsUnique)
            .Select(index => string.Join(",", index.Properties.Select(property => property.Name)))
            .ToArray();

        indexes.Should().Contain("Provider,ProviderSubject");
        indexes.Should().Contain("UserId,Provider");
    }

    [Fact]
    public async Task Controller_UsesExistingAuthResponseWithoutSecurityFields()
    {
        await using var context = CreateContext();
        var auth = new Mock<IAuthService>();
        auth.Setup(item => item.GoogleLoginAsync(It.IsAny<GoogleLoginRequestDto>()))
            .ReturnsAsync((new AuthResponseDto
            {
                AccessToken = "internal-access",
                Id = Guid.NewGuid(),
                Email = "response@example.com",
                FullName = "Response User",
                SystemRoles = new[] { "Developer" }
            }, "http-only-refresh"));
        var controller = Controller(context, auth.Object);

        var result = await controller.GoogleLogin(new GoogleLoginRequestDto { Credential = "google-id-token" });

        var ok = result.Should().BeOfType<OkObjectResult>().Subject;
        var json = JsonSerializer.Serialize(ok.Value);
        json.Should().Contain("internal-access");
        json.Should().NotContain("http-only-refresh");
        json.Should().NotContain("PasswordHash");
        json.Should().NotContain("ProviderSubject");
    }

    [Theory]
    [InlineData(typeof(ArgumentException), 400)]
    [InlineData(typeof(GoogleCredentialException), 401)]
    [InlineData(typeof(GoogleAccountForbiddenException), 403)]
    [InlineData(typeof(GoogleAccountConflictException), 409)]
    [InlineData(typeof(GoogleProviderUnavailableException), 503)]
    public async Task Controller_MapsGoogleFailuresToSanitizedStatus(Type exceptionType, int expectedStatus)
    {
        await using var context = CreateContext();
        var auth = new Mock<IAuthService>();
        auth.Setup(item => item.GoogleLoginAsync(It.IsAny<GoogleLoginRequestDto>()))
            .ThrowsAsync((Exception)Activator.CreateInstance(exceptionType, "sanitized-test-error")!);
        var controller = Controller(context, auth.Object);

        var result = await controller.GoogleLogin(new GoogleLoginRequestDto { Credential = "sensitive-google-token" });

        StatusCode(result).Should().Be(expectedStatus);
        JsonSerializer.Serialize(ResultValue(result)).Should().NotContain("sensitive-google-token");
    }

    [Fact]
    public async Task Controller_LogsCategoryWithoutCredential()
    {
        await using var context = CreateContext();
        var auth = new Mock<IAuthService>();
        auth.Setup(item => item.GoogleLoginAsync(It.IsAny<GoogleLoginRequestDto>()))
            .ThrowsAsync(new GoogleCredentialException());
        var logger = new CapturingLogger<AuthController>();
        var controller = Controller(context, auth.Object, logger);

        await controller.GoogleLogin(new GoogleLoginRequestDto { Credential = "credential-must-not-be-logged" });

        logger.Messages.Should().NotBeEmpty();
        logger.Messages.Should().OnlyContain(message => !message.Contains("credential-must-not-be-logged"));
    }

    [Fact]
    public void StartupValidation_RejectsEnabledGoogleProviderWithoutClientId()
    {
        var configuration = Configuration(new Dictionary<string, string?>
        {
            ["Jwt:SecretKey"] = "testing-only-signing-key-not-for-deployment-1234567890",
            ["Features:AIEnabled"] = "false",
            ["Google:Enabled"] = "true",
            ["Google:ClientId"] = ""
        });
        var environment = new Mock<IHostEnvironment>();
        environment.SetupGet(item => item.EnvironmentName).Returns("Testing");

        var action = () => HostingConfigurationExtensions.ValidateEnvironmentConfiguration(
            configuration,
            environment.Object,
            _ => null);

        action.Should().Throw<InvalidOperationException>()
            .WithMessage("*Google:ClientId*");
    }

    [Fact]
    public void StartupValidation_AllowsEnabledGoogleProviderWithInjectedClientId()
    {
        var configuration = Configuration(new Dictionary<string, string?>
        {
            ["Jwt:SecretKey"] = "testing-only-signing-key-not-for-deployment-1234567890",
            ["Features:AIEnabled"] = "false",
            ["Google:Enabled"] = "true",
            ["Google:ClientId"] = "test-client-id.apps.googleusercontent.com"
        });
        var environment = new Mock<IHostEnvironment>();
        environment.SetupGet(item => item.EnvironmentName).Returns("Testing");

        var action = () => HostingConfigurationExtensions.ValidateEnvironmentConfiguration(
            configuration,
            environment.Object,
            _ => null);

        action.Should().NotThrow();
    }

    private static ApplicationDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString("N"))
            .Options;
        return new ApplicationDbContext(options);
    }

    private static AuthService CreateService(
        ApplicationDbContext context,
        GoogleIdentity identity,
        Mock<IJwtService>? jwt = null) =>
        CreateService(context, new MutableGoogleValidator(identity), jwt);

    private static AuthService CreateService(
        ApplicationDbContext context,
        IGoogleIdentityValidator validator,
        Mock<IJwtService>? jwt = null)
    {
        jwt ??= JwtMock();
        return new AuthService(
            context,
            jwt.Object,
            Configuration(new Dictionary<string, string?>()),
            Mock.Of<IOtpService>(),
            Mock.Of<IEmailService>(),
            validator);
    }

    private static Mock<IJwtService> JwtMock()
    {
        var jwt = new Mock<IJwtService>();
        jwt.Setup(item => item.GenerateAccessToken(It.IsAny<User>(), It.IsAny<IList<string>>()))
            .Returns((User user, IList<string> _) => $"access:{user.Id:N}");
        jwt.Setup(item => item.GenerateRefreshToken())
            .Returns(() => $"refresh:{Guid.NewGuid():N}");
        return jwt;
    }

    private static GoogleIdentity Identity(
        string subject,
        string email,
        string name = "Google User") =>
        new(subject, email, name, "https://example.invalid/avatar.png");

    private static User User(
        string email,
        bool active = true,
        bool deleted = false,
        string passwordHash = "") =>
        new()
        {
            Id = Guid.NewGuid(),
            Email = email,
            FullName = "Existing User",
            PasswordHash = passwordHash,
            IsActive = active,
            IsDeleted = deleted,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

    private static ExternalLogin Login(User user, string subject) =>
        new()
        {
            Id = Guid.NewGuid(),
            UserId = user.Id,
            User = user,
            Provider = "Google",
            ProviderSubject = subject,
            ProviderEmail = user.Email,
            CreatedAt = DateTime.UtcNow,
            LastLoginAt = DateTime.UtcNow
        };

    private static AuthController Controller(
        ApplicationDbContext context,
        IAuthService auth,
        ILogger<AuthController>? logger = null) =>
        new(
            auth,
            Mock.Of<IOtpService>(),
            Mock.Of<IEmailService>(),
            context,
            logger)
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext()
            }
        };

    private static int StatusCode(IActionResult result) =>
        result switch
        {
            ObjectResult objectResult => objectResult.StatusCode ?? 200,
            StatusCodeResult statusCodeResult => statusCodeResult.StatusCode,
            _ => 200
        };

    private static object? ResultValue(IActionResult result) =>
        result is ObjectResult objectResult ? objectResult.Value : null;

    private static IConfiguration Configuration(Dictionary<string, string?> values) =>
        new ConfigurationBuilder().AddInMemoryCollection(values).Build();

    private sealed class MutableGoogleValidator : IGoogleIdentityValidator
    {
        public MutableGoogleValidator(GoogleIdentity identity)
        {
            Identity = identity;
        }

        public GoogleIdentity Identity { get; set; }

        public Task<GoogleIdentity> ValidateAsync(
            string credential,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(Identity);
    }

    private sealed class CapturingLogger<T> : ILogger<T>
    {
        public List<string> Messages { get; } = new();

        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(
            LogLevel logLevel,
            EventId eventId,
            TState state,
            Exception? exception,
            Func<TState, Exception?, string> formatter) =>
            Messages.Add(formatter(state, exception));
    }
}
