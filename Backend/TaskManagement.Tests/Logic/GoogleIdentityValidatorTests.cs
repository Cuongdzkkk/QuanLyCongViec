using FluentAssertions;
using Microsoft.Extensions.Configuration;
using Moq;
using TaskManagement.Application.Auth;
using TaskManagement.Application.Interfaces;
using TaskManagement.Infrastructure.Services;

namespace TaskManagement.Tests.Logic;

public sealed class GoogleIdentityValidatorTests
{
    private const string ClientId = "sprinta-test.apps.googleusercontent.com";

    [Fact]
    public async Task ValidClaims_UseConfiguredAudienceAndReturnGoogleIdentity()
    {
        var verifier = new Mock<IGoogleTokenVerifier>();
        verifier.Setup(item => item.VerifyAsync("signed-id-token", ClientId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Claims());
        var validator = Validator(verifier.Object);

        var identity = await validator.ValidateAsync("signed-id-token");

        identity.Subject.Should().Be("GOOGLE_SUBJECT_A");
        identity.Email.Should().Be("verified@example.com");
        verifier.VerifyAll();
    }

    [Fact]
    public async Task WrongAudience_IsRejected()
    {
        await AssertInvalid(Claims(audiences: new[] { "another-client-id" }));
    }

    [Fact]
    public async Task WrongIssuer_IsRejected()
    {
        await AssertInvalid(Claims(issuer: "https://attacker.invalid"));
    }

    [Fact]
    public async Task ExpiredToken_IsRejected()
    {
        await AssertInvalid(Claims(expiration: DateTimeOffset.UtcNow.AddMinutes(-1).ToUnixTimeSeconds()));
    }

    [Fact]
    public async Task UnverifiedEmail_IsRejected()
    {
        await AssertInvalid(Claims(emailVerified: false));
    }

    [Fact]
    public async Task InvalidEmailFormat_IsRejected()
    {
        await AssertInvalid(Claims(email: "not-an-email"));
    }

    [Fact]
    public async Task SignatureValidationFailure_IsSanitizedAsInvalidCredential()
    {
        var verifier = new Mock<IGoogleTokenVerifier>();
        verifier.Setup(item => item.VerifyAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("provider detail that must not escape"));
        var validator = Validator(verifier.Object);

        var action = () => validator.ValidateAsync("tampered-token");

        var exception = await action.Should().ThrowAsync<GoogleCredentialException>();
        exception.Which.Message.Should().NotContain("provider detail");
    }

    [Fact]
    public async Task ProviderNetworkFailure_IsReportedAsUnavailable()
    {
        var verifier = new Mock<IGoogleTokenVerifier>();
        verifier.Setup(item => item.VerifyAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new HttpRequestException("network detail"));
        var validator = Validator(verifier.Object);

        var action = () => validator.ValidateAsync("signed-id-token");

        await action.Should().ThrowAsync<GoogleProviderUnavailableException>();
    }

    [Fact]
    public async Task DisabledProvider_DoesNotAttemptTokenVerification()
    {
        var verifier = new Mock<IGoogleTokenVerifier>(MockBehavior.Strict);
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Google:Enabled"] = "false",
                ["Google:ClientId"] = ClientId
            })
            .Build();
        var validator = new GoogleIdentityValidator(configuration, verifier.Object);

        var action = () => validator.ValidateAsync("signed-id-token");

        await action.Should().ThrowAsync<GoogleProviderUnavailableException>();
        verifier.VerifyNoOtherCalls();
    }

    private static async Task AssertInvalid(GoogleTokenClaims claims)
    {
        var verifier = new Mock<IGoogleTokenVerifier>();
        verifier.Setup(item => item.VerifyAsync(It.IsAny<string>(), ClientId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(claims);
        var validator = Validator(verifier.Object);

        var action = () => validator.ValidateAsync("signed-id-token");

        await action.Should().ThrowAsync<GoogleCredentialException>();
    }

    private static GoogleIdentityValidator Validator(IGoogleTokenVerifier verifier)
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Google:Enabled"] = "true",
                ["Google:ClientId"] = ClientId
            })
            .Build();
        return new GoogleIdentityValidator(configuration, verifier);
    }

    private static GoogleTokenClaims Claims(
        string email = "verified@example.com",
        bool emailVerified = true,
        string issuer = "https://accounts.google.com",
        IReadOnlyCollection<string>? audiences = null,
        long? expiration = null) =>
        new(
            "GOOGLE_SUBJECT_A",
            email,
            emailVerified,
            "Verified User",
            null,
            issuer,
            audiences ?? new[] { ClientId },
            expiration ?? DateTimeOffset.UtcNow.AddMinutes(5).ToUnixTimeSeconds());
}
