namespace TaskManagement.Application.Auth;

public sealed record GoogleTokenClaims(
    string Subject,
    string Email,
    bool EmailVerified,
    string? DisplayName,
    string? AvatarUrl,
    string Issuer,
    IReadOnlyCollection<string> Audiences,
    long? ExpirationTimeSeconds);
