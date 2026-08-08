using TaskManagement.Application.Auth;

namespace TaskManagement.Application.Interfaces;

public interface IGoogleTokenVerifier
{
    Task<GoogleTokenClaims> VerifyAsync(
        string credential,
        string clientId,
        CancellationToken cancellationToken = default);
}
