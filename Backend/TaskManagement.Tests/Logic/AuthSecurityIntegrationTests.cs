using System.Net;
using System.ComponentModel.DataAnnotations;
using System.Text.Json;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Options;
using Moq;
using TaskManagement.API.Controllers;
using TaskManagement.Application.Configuration;
using TaskManagement.Application.DTOs.Auth;
using TaskManagement.Application.Interfaces;
using TaskManagement.Domain.Entities;
using TaskManagement.Infrastructure.Data;
using TaskManagement.Infrastructure.Services;

namespace TaskManagement.Tests.Logic;

public sealed class AuthSecurityIntegrationTests : IDisposable
{
    private readonly ApplicationDbContext _context;
    private readonly IMemoryCache _cache;
    private readonly OtpService _otpService;
    private readonly Mock<IEmailService> _emailService = new();

    public AuthSecurityIntegrationTests()
    {
        var dbOptions = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        _context = new ApplicationDbContext(dbOptions);
        _cache = new MemoryCache(new MemoryCacheOptions());
        _otpService = new OtpService(_cache, Options.Create(new OtpSecurityOptions()));
        _emailService.Setup(service => service.SendOtpEmailAsync(It.IsAny<string>(), It.IsAny<string>()))
            .Returns(Task.CompletedTask);
    }

    [Fact]
    public async Task SendOtp_ExistingAndMissingEmail_ReturnSamePublicResponse()
    {
        _context.Users.Add(new User
        {
            Id = Guid.NewGuid(),
            Email = "existing@example.com",
            FullName = "Existing User",
            PasswordHash = "unused",
            IsActive = true,
            IsDeleted = false
        });
        await _context.SaveChangesAsync();
        var controller = CreateController();

        var existing = await controller.SendOtp(new SendOtpRequestDto
        {
            Email = " EXISTING@example.com ",
            Purpose = "forgot-password"
        });
        var missing = await controller.SendOtp(new SendOtpRequestDto
        {
            Email = "missing@example.com",
            Purpose = "forgot-password"
        });

        existing.Should().BeOfType<OkObjectResult>();
        missing.Should().BeOfType<OkObjectResult>();
        JsonSerializer.Serialize(((OkObjectResult)existing).Value)
            .Should().Be(JsonSerializer.Serialize(((OkObjectResult)missing).Value));
        _emailService.Verify(service => service.SendOtpEmailAsync("existing@example.com", It.IsAny<string>()), Times.Once);
        _emailService.Verify(service => service.SendOtpEmailAsync("missing@example.com", It.IsAny<string>()), Times.Never);
    }

    [Fact]
    public async Task SendOtp_RegisterExistingAccount_ReturnsConflictWithoutIssuingOtp()
    {
        _context.Users.Add(new User
        {
            Id = Guid.NewGuid(),
            Email = "registered@example.com",
            FullName = "Registered User",
            PasswordHash = "password-hash",
            IsActive = true,
            IsDeleted = false
        });
        await _context.SaveChangesAsync();
        var controller = CreateController();

        var result = await controller.SendOtp(new SendOtpRequestDto
        {
            Email = "registered@example.com",
            Purpose = "register"
        });

        result.Should().BeOfType<ConflictObjectResult>().Which.StatusCode
            .Should().Be(StatusCodes.Status409Conflict);
        JsonSerializer.Serialize(((ConflictObjectResult)result).Value)
            .Should().Contain("statusCode").And.Contain("message");
        _emailService.Verify(service => service.SendOtpEmailAsync(
            "registered@example.com", It.IsAny<string>()), Times.Never);
        _otpService.StoreOtp("registered@example.com", "123456", "127.0.0.1")
            .Issued.Should().BeTrue();
    }

    [Fact]
    public async Task SendOtp_RegisterIncompleteInvitePlaceholder_RemainsSendable()
    {
        _context.Users.Add(new User
        {
            Id = Guid.NewGuid(),
            Email = "invited-placeholder@example.com",
            FullName = "Invited Placeholder",
            PasswordHash = string.Empty,
            IsActive = false,
            IsDeleted = false
        });
        await _context.SaveChangesAsync();
        var controller = CreateController();

        var result = await controller.SendOtp(new SendOtpRequestDto
        {
            Email = "invited-placeholder@example.com",
            Purpose = "register"
        });

        result.Should().BeOfType<OkObjectResult>();
        _emailService.Verify(service => service.SendOtpEmailAsync(
            "invited-placeholder@example.com", It.IsAny<string>()), Times.Once);
    }

    [Fact]
    public async Task SendOtp_ProviderFailure_ReturnsUnavailableAndRollsBackOtp()
    {
        const string email = "provider-failure@example.com";
        _emailService.Reset();
        _emailService.Setup(service => service.SendOtpEmailAsync(email, It.IsAny<string>()))
            .ThrowsAsync(new InvalidOperationException("provider failure details"));
        var controller = CreateController();

        var result = await controller.SendOtp(new SendOtpRequestDto
        {
            Email = email,
            Purpose = "register"
        });

        result.Should().BeOfType<ObjectResult>().Which.StatusCode
            .Should().Be(StatusCodes.Status503ServiceUnavailable);
        JsonSerializer.Serialize(((ObjectResult)result).Value)
            .Should().NotContain("provider failure details");
        _otpService.StoreOtp(email, "654321", "127.0.0.1")
            .Issued.Should().BeTrue();
    }

    [Fact]
    public async Task SendOtp_ResendWithinCooldown_Returns429()
    {
        _context.Users.Add(new User
        {
            Id = Guid.NewGuid(),
            Email = "cooldown-api@example.com",
            FullName = "Cooldown User",
            PasswordHash = "unused",
            IsActive = true,
            IsDeleted = false
        });
        await _context.SaveChangesAsync();
        var controller = CreateController();
        var request = new SendOtpRequestDto { Email = "cooldown-api@example.com", Purpose = "reset" };

        var firstSend = await controller.SendOtp(request);
        firstSend.Should().BeOfType<OkObjectResult>();
        JsonSerializer.Serialize(((OkObjectResult)firstSend).Value)
            .Should().Contain("resendCooldownSeconds");
        var resend = await controller.SendOtp(request);

        resend.Should().BeOfType<ObjectResult>().Which.StatusCode
            .Should().Be(StatusCodes.Status429TooManyRequests);
        controller.Response.Headers["Retry-After"].ToString().Should().Be("60");
    }

    [Fact]
    public async Task SendOtp_DifferentEmails_DoNotShareCooldown()
    {
        var controller = CreateController();

        var first = await controller.SendOtp(new SendOtpRequestDto
        {
            Email = "first-registration@example.com",
            Purpose = "register"
        });
        var second = await controller.SendOtp(new SendOtpRequestDto
        {
            Email = "second-registration@example.com",
            Purpose = "register"
        });

        first.Should().BeOfType<OkObjectResult>();
        second.Should().BeOfType<OkObjectResult>();
    }

    [Fact]
    public void VerifyOtp_FifthWrongAttempt_Returns429()
    {
        _otpService.StoreOtp("locked-api@example.com", "123456", "127.0.0.1").Issued.Should().BeTrue();
        var controller = CreateController();
        IActionResult? result = null;

        for (var attempt = 0; attempt < 5; attempt++)
        {
            result = controller.VerifyOtp(new VerifyOtpRequestDto
            {
                Email = "locked-api@example.com",
                OtpCode = "000000"
            });
        }

        result.Should().BeOfType<ObjectResult>().Which.StatusCode
            .Should().Be(StatusCodes.Status429TooManyRequests);
    }

    [Fact]
    public void VerifiedOtp_IsExchangedForOpaqueSingleUseToken()
    {
        _otpService.StoreOtp("token@example.com", "123456").Issued.Should().BeTrue();
        _otpService.ValidateOtp("token@example.com", "123456").IsValid.Should().BeTrue();

        var token = _otpService.IssueVerificationToken("token@example.com");

        token.Should().NotMatchRegex("^[0-9]{6}$");
        _otpService.ValidateOtp("token@example.com", token).IsValid.Should().BeTrue();
        _otpService.ValidateOtp("token@example.com", token).IsValid.Should().BeFalse();
    }

    [Fact]
    public async Task Registration_SendVerifyRegisterLogin_Succeeds()
    {
        const string email = "full-registration@example.com";
        string? sentOtp = null;
        _emailService.Reset();
        _emailService.Setup(service => service.SendOtpEmailAsync(email, It.IsAny<string>()))
            .Callback<string, string>((_, otp) => sentOtp = otp)
            .Returns(Task.CompletedTask);

        var jwt = new Mock<IJwtService>();
        jwt.Setup(service => service.GenerateAccessToken(It.IsAny<User>(), It.IsAny<IList<string>>()))
            .Returns("access-token");
        jwt.Setup(service => service.GenerateRefreshToken()).Returns("refresh-token");
        var authService = new AuthService(
            _context,
            jwt.Object,
            Mock.Of<IConfiguration>(),
            _otpService,
            _emailService.Object);
        var controller = CreateController(authService);

        var send = await controller.SendOtp(new SendOtpRequestDto
        {
            Email = email,
            Purpose = "register"
        });
        send.Should().BeOfType<OkObjectResult>();
        sentOtp.Should().NotBeNullOrWhiteSpace();

        var verify = controller.VerifyOtp(new VerifyOtpRequestDto
        {
            Email = email,
            OtpCode = sentOtp!
        });
        verify.Should().BeOfType<OkObjectResult>();
        using var tokenJson = JsonDocument.Parse(JsonSerializer.Serialize(((OkObjectResult)verify).Value));
        var verificationToken = tokenJson.RootElement.GetProperty("otpToken").GetString();
        verificationToken.Should().NotBeNullOrWhiteSpace();

        var register = await controller.Register(new RegisterRequestDto
        {
            Email = email,
            FullName = "Full Registration User",
            Password = "Password123!",
            OtpToken = verificationToken!
        });
        register.Should().BeOfType<OkObjectResult>();
        (await _context.Users.SingleAsync(user => user.Email == email)).FullName
            .Should().Be("Full Registration User");

        var login = await authService.LoginAsync(new LoginRequestDto
        {
            Email = email,
            Password = "Password123!"
        });
        login.response.Should().NotBeNull();
    }

    [Fact]
    public async Task Register_MissingOrInvalidOtp_ReturnsControlled400()
    {
        var authService = new AuthService(
            _context,
            Mock.Of<IJwtService>(),
            Mock.Of<IConfiguration>(),
            _otpService,
            _emailService.Object);
        var controller = CreateController(authService);

        var missing = await controller.Register(new RegisterRequestDto
        {
            Email = "missing-token@example.com",
            FullName = "Missing Token User",
            Password = "Password123!",
            OtpToken = string.Empty
        });
        var invalid = await controller.Register(new RegisterRequestDto
        {
            Email = "invalid-token@example.com",
            FullName = "Invalid Token User",
            Password = "Password123!",
            OtpToken = "invalid-token"
        });

        missing.Should().BeOfType<BadRequestObjectResult>();
        invalid.Should().BeOfType<BadRequestObjectResult>();
    }

    [Fact]
    public void RegisterDto_RequiresOpaqueVerificationToken()
    {
        var validToken = _otpService.IssueVerificationToken("dto-token@example.com");
        var valid = new RegisterRequestDto
        {
            Email = "dto-token@example.com",
            FullName = "DTO Token User",
            Password = "Password123!",
            OtpToken = validToken
        };
        var missing = new RegisterRequestDto
        {
            Email = "dto-token@example.com",
            FullName = "DTO Token User",
            Password = "Password123!"
        };
        var rawOtp = new RegisterRequestDto
        {
            Email = "dto-token@example.com",
            FullName = "DTO Token User",
            Password = "Password123!",
            OtpToken = "123456"
        };
        var malformed = new RegisterRequestDto
        {
            Email = "dto-token@example.com",
            FullName = "DTO Token User",
            Password = "Password123!",
            OtpToken = new string('!', 43)
        };

        static bool IsValid(RegisterRequestDto request)
        {
            return Validator.TryValidateObject(
                request,
                new ValidationContext(request),
                new List<ValidationResult>(),
                validateAllProperties: true);
        }

        IsValid(valid).Should().BeTrue();
        IsValid(missing).Should().BeFalse();
        IsValid(rawOtp).Should().BeFalse();
        IsValid(malformed).Should().BeFalse();
    }

    [Fact]
    public async Task Register_VerificationTokenIsBoundToEmailAndSingleUse()
    {
        const string verifiedEmail = "verified-token@example.com";
        var token = _otpService.IssueVerificationToken(verifiedEmail);
        var authService = new AuthService(
            _context,
            Mock.Of<IJwtService>(),
            Mock.Of<IConfiguration>(),
            _otpService,
            _emailService.Object);

        await Assert.ThrowsAsync<InvalidOperationException>(() => authService.RegisterAsync(new RegisterRequestDto
        {
            Email = "different-email@example.com",
            FullName = "Different Email",
            Password = "Password123!",
            OtpToken = token
        }));

        await authService.RegisterAsync(new RegisterRequestDto
        {
            Email = verifiedEmail,
            FullName = "Verified Token User",
            Password = "Password123!",
            OtpToken = token
        });

        await Assert.ThrowsAsync<InvalidOperationException>(() => authService.RegisterAsync(new RegisterRequestDto
        {
            Email = verifiedEmail,
            FullName = "Verified Token User",
            Password = "Password123!",
            OtpToken = token
        }));
    }

    [Fact]
    public async Task Register_ExpiredVerificationTokenIsRejected()
    {
        using var shortLivedCache = new MemoryCache(new MemoryCacheOptions());
        var shortLivedOtpService = new OtpService(shortLivedCache, Options.Create(new OtpSecurityOptions
        {
            VerificationTokenExpirationSeconds = 1
        }));
        const string email = "expired-token@example.com";
        var token = shortLivedOtpService.IssueVerificationToken(email);
        await Task.Delay(1200);
        var authService = new AuthService(
            _context,
            Mock.Of<IJwtService>(),
            Mock.Of<IConfiguration>(),
            shortLivedOtpService,
            _emailService.Object);

        await Assert.ThrowsAsync<InvalidOperationException>(() => authService.RegisterAsync(new RegisterRequestDto
        {
            Email = email,
            FullName = "Expired Token User",
            Password = "Password123!",
            OtpToken = token
        }));
    }

    [Fact]
    public void RegisterDto_RejectsValuesLongerThanBackendLimits()
    {
        var request = new RegisterRequestDto
        {
            Email = "long-values@example.com",
            FullName = new string('N', 101),
            Password = new string('P', 101),
            OtpToken = "123456"
        };
        var validationResults = new List<ValidationResult>();

        var isValid = Validator.TryValidateObject(
            request,
            new ValidationContext(request),
            validationResults,
            validateAllProperties: true);

        isValid.Should().BeFalse();
        validationResults.Should().Contain(item => item.MemberNames.Contains(nameof(RegisterRequestDto.FullName)));
        validationResults.Should().Contain(item => item.MemberNames.Contains(nameof(RegisterRequestDto.Password)));
    }

    private AuthController CreateController(IAuthService? authService = null)
    {
        var httpContext = new DefaultHttpContext();
        httpContext.Connection.RemoteIpAddress = IPAddress.Parse("127.0.0.1");
        return new AuthController(
            authService ?? Mock.Of<IAuthService>(),
            _otpService,
            _emailService.Object,
            _context,
            Mock.Of<IConfiguration>(),
            Mock.Of<IGoogleAuthorizationCodeExchange>(),
            Mock.Of<IGoogleLoginOAuthStateStore>())
        {
            ControllerContext = new ControllerContext { HttpContext = httpContext }
        };
    }

    public void Dispose()
    {
        _context.Dispose();
        _cache.Dispose();
    }
}
