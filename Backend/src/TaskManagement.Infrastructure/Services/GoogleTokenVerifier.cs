using System.Text.Json;
using Google.Apis.Auth;
using TaskManagement.Application.Auth;
using TaskManagement.Application.Interfaces;

namespace TaskManagement.Infrastructure.Services;

public sealed class GoogleTokenVerifier : IGoogleTokenVerifier
{
    public async Task<GoogleTokenClaims> VerifyAsync(
        string credential,
        string clientId,
        CancellationToken cancellationToken = default)
    {
        var payload = await GoogleJsonWebSignature.ValidateAsync(
            credential,
            new GoogleJsonWebSignature.ValidationSettings
            {
                Audience = new[] { clientId }
            });

        return new GoogleTokenClaims(
            payload.Subject,
            payload.Email,
            payload.EmailVerified,
            payload.Name,
            payload.Picture,
            payload.Issuer,
            NormalizeAudiences(payload.Audience),
            payload.ExpirationTimeSeconds);
    }

    private static IReadOnlyCollection<string> NormalizeAudiences(object? value)
    {
        if (value is string audience)
        {
            return new[] { audience };
        }

        if (value is IEnumerable<string> audiences)
        {
            return audiences.ToArray();
        }

        if (value is JsonElement { ValueKind: JsonValueKind.String } jsonString)
        {
            return new[] { jsonString.GetString() ?? string.Empty };
        }

        if (value is JsonElement { ValueKind: JsonValueKind.Array } jsonArray)
        {
            return jsonArray.EnumerateArray()
                .Where(item => item.ValueKind == JsonValueKind.String)
                .Select(item => item.GetString() ?? string.Empty)
                .ToArray();
        }

        return Array.Empty<string>();
    }
}
