namespace TaskManagement.Application.Interfaces;

public interface IGoogleCalendarIntegrationService
{
    string BuildAuthorizationUrl(string clientId, string redirectUri, string state, string codeChallenge);

    Task<GoogleTokenResult> ExchangeCodeAsync(
        string code,
        string clientId,
        string clientSecret,
        string redirectUri,
        string codeVerifier,
        CancellationToken cancellationToken = default);

    Task<GoogleAccountIdentity> GetAccountIdentityAsync(
        string accessToken,
        CancellationToken cancellationToken = default);

    Task<GoogleTokenResult> RefreshAccessTokenAsync(
        string clientId,
        string clientSecret,
        string refreshToken,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<GoogleCalendarEventResult>> GetEventsAsync(
        string accessToken,
        DateTime timeMin,
        DateTime timeMax,
        CancellationToken cancellationToken = default);

    Task RevokeTokenAsync(string token, CancellationToken cancellationToken = default);
}

public sealed record GoogleTokenResult(
    string AccessToken,
    string? RefreshToken,
    int ExpiresIn,
    string? Scope);

public sealed record GoogleAccountIdentity(string? Subject, string? Email);

public sealed record GoogleCalendarEventResult(
    string Id,
    string? Summary,
    string? Description,
    string? Location,
    DateTime? StartsAt,
    DateTime? EndsAt);
