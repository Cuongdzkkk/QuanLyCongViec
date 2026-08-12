using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Moq;
using TaskManagement.Application.Auth;
using TaskManagement.Application.DTOs.Auth;
using TaskManagement.Application.Interfaces;
using TaskManagement.Domain.Entities;
using TaskManagement.Infrastructure.Data;
using TaskManagement.Infrastructure.Services;

namespace TaskManagement.Tests.Logic;

public sealed class GoogleAuthSqlServerTests
{
    private const string Email = "google-concurrency@sprinta.test";
    private const string Subject = "GOOGLE_SUBJECT_CONCURRENT";
    private static string ConnectionString => SqlServerTestConfiguration.ConnectionString("SprintAGoogleAuth01Integration");

    [Fact]
    [Trait("Category", "SqlServerIntegration")]
    public async Task ConcurrentFirstLogin_CreatesOneUserAndOneProviderRecord()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseSqlServer(ConnectionString, sql => sql.EnableRetryOnFailure())
            .Options;

        await using (var setup = new ApplicationDbContext(options))
        {
            await setup.Database.EnsureCreatedAsync();
            await CleanupAsync(setup);
        }

        try
        {
            var results = await Task.WhenAll(
                LoginAsync(options),
                LoginAsync(options));

            results[0].Id.Should().Be(results[1].Id);
            await using var verification = new ApplicationDbContext(options);
            (await verification.Users.CountAsync(user => user.Email == Email)).Should().Be(1);
            (await verification.ExternalLogins.CountAsync(login =>
                login.Provider == "Google" &&
                login.ProviderSubject == Subject)).Should().Be(1);
        }
        finally
        {
            await using var cleanup = new ApplicationDbContext(options);
            await CleanupAsync(cleanup);
        }
    }

    private static async Task<AuthResponseDto> LoginAsync(
        DbContextOptions<ApplicationDbContext> options)
    {
        await using var context = new ApplicationDbContext(options);
        var jwt = new Mock<IJwtService>();
        jwt.Setup(item => item.GenerateAccessToken(It.IsAny<User>(), It.IsAny<IList<string>>()))
            .Returns((User user, IList<string> _) => $"access:{user.Id:N}");
        jwt.Setup(item => item.GenerateRefreshToken())
            .Returns(() => $"refresh:{Guid.NewGuid():N}");
        var service = new AuthService(
            context,
            jwt.Object,
            new ConfigurationBuilder().Build(),
            Mock.Of<IOtpService>(),
            Mock.Of<IEmailService>(),
            new FixedValidator());

        var result = await service.GoogleLoginAsync(new GoogleLoginRequestDto
        {
            Credential = "test-google-id-token"
        });
        return result.response;
    }

    private static async Task CleanupAsync(ApplicationDbContext context)
    {
        var userIds = await context.Users
            .Where(user => user.Email == Email)
            .Select(user => user.Id)
            .ToListAsync();
        if (userIds.Count == 0) return;

        await context.RefreshTokens
            .Where(token => userIds.Contains(token.UserId))
            .ExecuteDeleteAsync();
        await context.ExternalLogins
            .Where(login => userIds.Contains(login.UserId))
            .ExecuteDeleteAsync();
        await context.Users
            .Where(user => userIds.Contains(user.Id))
            .ExecuteDeleteAsync();
    }

    private sealed class FixedValidator : IGoogleIdentityValidator
    {
        public Task<GoogleIdentity> ValidateAsync(
            string credential,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(new GoogleIdentity(Subject, Email, "Concurrent Google User", null));
    }
}
