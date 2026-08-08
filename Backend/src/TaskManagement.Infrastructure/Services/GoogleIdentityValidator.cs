using Microsoft.Extensions.Configuration;
using System.Net.Mail;
using TaskManagement.Application.Auth;
using TaskManagement.Application.Common;
using TaskManagement.Application.Interfaces;

namespace TaskManagement.Infrastructure.Services;

public sealed class GoogleIdentityValidator : IGoogleIdentityValidator
{
    private static readonly HashSet<string> AllowedIssuers = new(StringComparer.Ordinal)
    {
        "accounts.google.com",
        "https://accounts.google.com"
    };

    private readonly IConfiguration _configuration;
    private readonly IGoogleTokenVerifier _tokenVerifier;

    public GoogleIdentityValidator(
        IConfiguration configuration,
        IGoogleTokenVerifier? tokenVerifier = null)
    {
        _configuration = configuration;
        _tokenVerifier = tokenVerifier ?? new GoogleTokenVerifier();
    }

    public async Task<GoogleIdentity> ValidateAsync(
        string credential,
        CancellationToken cancellationToken = default)
    {
        if (!_configuration.GetValue<bool>("Google:Enabled"))
        {
            throw new GoogleProviderUnavailableException();
        }

        var clientId = _configuration["Google:ClientId"]?.Trim();
        if (string.IsNullOrWhiteSpace(clientId))
        {
            throw new GoogleProviderUnavailableException();
        }

        try
        {
            var payload = await _tokenVerifier.VerifyAsync(
                credential,
                clientId,
                cancellationToken);

            if (string.IsNullOrWhiteSpace(payload.Subject) ||
                payload.Subject.Length > 255 ||
                string.IsNullOrWhiteSpace(payload.Email) ||
                !payload.EmailVerified ||
                !AllowedIssuers.Contains(payload.Issuer) ||
                !payload.Audiences.Contains(clientId, StringComparer.Ordinal) ||
                payload.ExpirationTimeSeconds is null ||
                payload.ExpirationTimeSeconds <= DateTimeOffset.UtcNow.ToUnixTimeSeconds())
            {
                throw new GoogleCredentialException();
            }

            var email = EmailCanonicalizer.Normalize(payload.Email);
            if (string.IsNullOrWhiteSpace(email) ||
                email.Length > 450 ||
                !MailAddress.TryCreate(email, out var parsedEmail) ||
                !string.Equals(parsedEmail.Address, email, StringComparison.OrdinalIgnoreCase))
            {
                throw new GoogleCredentialException();
            }

            return new GoogleIdentity(
                payload.Subject.Trim(),
                email,
                string.IsNullOrWhiteSpace(payload.DisplayName) ? email : payload.DisplayName.Trim(),
                string.IsNullOrWhiteSpace(payload.AvatarUrl) ? null : payload.AvatarUrl);
        }
        catch (GoogleCredentialException)
        {
            throw;
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (HttpRequestException)
        {
            throw new GoogleProviderUnavailableException();
        }
        catch (Exception)
        {
            throw new GoogleCredentialException();
        }
    }
}
