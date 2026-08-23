using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Serialization;
using TaskManagement.Application.Interfaces;

namespace TaskManagement.Infrastructure.Services;

public sealed class GoogleCalendarIntegrationService : IGoogleCalendarIntegrationService
{
    private const string AuthorizationEndpoint = "https://accounts.google.com/o/oauth2/v2/auth";
    private const string TokenEndpoint = "https://oauth2.googleapis.com/token";
    private const string UserInfoEndpoint = "https://openidconnect.googleapis.com/v1/userinfo";
    private const string CalendarEndpoint = "https://www.googleapis.com/calendar/v3/calendars/primary/events";
    private const string RevokeEndpoint = "https://oauth2.googleapis.com/revoke";
    private readonly IHttpClientFactory _httpClientFactory;

    public GoogleCalendarIntegrationService(IHttpClientFactory httpClientFactory)
    {
        _httpClientFactory = httpClientFactory;
    }

    public string BuildAuthorizationUrl(string clientId, string redirectUri, string state, string codeChallenge)
    {
        var scope = Uri.EscapeDataString("openid email profile https://www.googleapis.com/auth/calendar.readonly");
        return AuthorizationEndpoint +
            $"?client_id={Uri.EscapeDataString(clientId)}" +
            $"&redirect_uri={Uri.EscapeDataString(redirectUri)}" +
            "&response_type=code" +
            $"&scope={scope}" +
            "&access_type=offline" +
            "&prompt=consent" +
            $"&code_challenge={Uri.EscapeDataString(codeChallenge)}" +
            "&code_challenge_method=S256" +
            $"&state={Uri.EscapeDataString(state)}";
    }

    public async Task<GoogleTokenResult> ExchangeCodeAsync(string code, string clientId, string clientSecret, string redirectUri, string codeVerifier, CancellationToken cancellationToken = default)
    {
        using var response = await _httpClientFactory.CreateClient("GoogleCalendar").PostAsync(TokenEndpoint, new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["code"] = code,
            ["client_id"] = clientId,
            ["client_secret"] = clientSecret,
            ["redirect_uri"] = redirectUri,
            ["grant_type"] = "authorization_code",
            ["code_verifier"] = codeVerifier
        }), cancellationToken);
        return await ReadTokenResponseAsync(response, cancellationToken);
    }

    public async Task<GoogleAccountIdentity> GetAccountIdentityAsync(string accessToken, CancellationToken cancellationToken = default)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, UserInfoEndpoint);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        using var response = await _httpClientFactory.CreateClient("GoogleCalendar").SendAsync(request, cancellationToken);
        if (!response.IsSuccessStatusCode) throw new GoogleProviderException("Google account lookup failed.");
        GoogleUserInfoResponse? payload;
        try
        {
            payload = await response.Content.ReadFromJsonAsync<GoogleUserInfoResponse>(cancellationToken: cancellationToken);
        }
        catch (JsonException)
        {
            throw new GoogleProviderException("Google account lookup failed.");
        }
        return new GoogleAccountIdentity(payload?.Subject, payload?.Email);
    }

    public async Task<GoogleTokenResult> RefreshAccessTokenAsync(string clientId, string clientSecret, string refreshToken, CancellationToken cancellationToken = default)
    {
        using var response = await _httpClientFactory.CreateClient("GoogleCalendar").PostAsync(TokenEndpoint, new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["client_id"] = clientId,
            ["client_secret"] = clientSecret,
            ["refresh_token"] = refreshToken,
            ["grant_type"] = "refresh_token"
        }), cancellationToken);
        return await ReadTokenResponseAsync(response, cancellationToken);
    }

    public async Task<IReadOnlyList<GoogleCalendarEventResult>> GetEventsAsync(string accessToken, DateTime timeMin, DateTime timeMax, CancellationToken cancellationToken = default)
    {
        var url = CalendarEndpoint +
            $"?singleEvents=true&orderBy=startTime&timeMin={Uri.EscapeDataString(timeMin.ToString("O"))}&timeMax={Uri.EscapeDataString(timeMax.ToString("O"))}";
        using var request = new HttpRequestMessage(HttpMethod.Get, url);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        using var response = await _httpClientFactory.CreateClient("GoogleCalendar").SendAsync(request, cancellationToken);
        if (!response.IsSuccessStatusCode) throw new GoogleProviderException("Google Calendar request failed.");
        GoogleCalendarEventsResponse? payload;
        try
        {
            payload = await response.Content.ReadFromJsonAsync<GoogleCalendarEventsResponse>(cancellationToken: cancellationToken);
        }
        catch (JsonException)
        {
            throw new GoogleProviderException("Google Calendar request failed.");
        }
        return payload?.Items?.Where(item => !string.IsNullOrWhiteSpace(item.Id)).Select(item => new GoogleCalendarEventResult(
            item.Id!, item.Summary, item.Description, item.Location, ParseDate(item.Start), ParseDate(item.End))).ToList()
            ?? new List<GoogleCalendarEventResult>();
    }

    public async Task RevokeTokenAsync(string token, CancellationToken cancellationToken = default)
    {
        using var response = await _httpClientFactory.CreateClient("GoogleCalendar").PostAsync(RevokeEndpoint, new FormUrlEncodedContent(new Dictionary<string, string> { ["token"] = token }), cancellationToken);
        if (!response.IsSuccessStatusCode && response.StatusCode != System.Net.HttpStatusCode.BadRequest)
            throw new GoogleProviderException("Google token revocation failed.");
    }

    private static async Task<GoogleTokenResult> ReadTokenResponseAsync(HttpResponseMessage response, CancellationToken cancellationToken)
    {
        GoogleTokenResponse? payload;
        try
        {
            payload = await response.Content.ReadFromJsonAsync<GoogleTokenResponse>(cancellationToken: cancellationToken);
        }
        catch (JsonException)
        {
            throw new GoogleProviderException("Google OAuth request failed.");
        }
        if (!response.IsSuccessStatusCode || payload == null || string.IsNullOrWhiteSpace(payload.AccessToken))
            throw new GoogleProviderException(payload?.Error == "invalid_grant" ? "Google authorization requires reconnect." : "Google OAuth request failed.", payload?.Error == "invalid_grant");
        return new GoogleTokenResult(payload.AccessToken, payload.RefreshToken, payload.ExpiresIn, payload.Scope);
    }

    private static DateTime? ParseDate(GoogleDateTime? value)
    {
        if (value == null) return null;
        if (DateTimeOffset.TryParse(value.DateTime, out var dateTime)) return dateTime.UtcDateTime;
        if (DateTime.TryParse(value.Date, out var date)) return DateTime.SpecifyKind(date.Date, DateTimeKind.Utc);
        return null;
    }

    public static string CreateCodeVerifier()
        => Convert.ToBase64String(RandomNumberGenerator.GetBytes(64)).Replace("+", "-").Replace("/", "_").TrimEnd('=');

    public static string CreateCodeChallenge(string verifier)
        => Convert.ToBase64String(SHA256.HashData(System.Text.Encoding.ASCII.GetBytes(verifier))).Replace("+", "-").Replace("/", "_").TrimEnd('=');

    private sealed class GoogleTokenResponse
    {
        [JsonPropertyName("access_token")] public string AccessToken { get; set; } = string.Empty;
        [JsonPropertyName("refresh_token")] public string? RefreshToken { get; set; }
        [JsonPropertyName("expires_in")] public int ExpiresIn { get; set; }
        [JsonPropertyName("scope")] public string? Scope { get; set; }
        [JsonPropertyName("error")] public string? Error { get; set; }
    }

    private sealed class GoogleUserInfoResponse
    {
        [JsonPropertyName("sub")] public string? Subject { get; set; }
        [JsonPropertyName("email")] public string? Email { get; set; }
    }

    private sealed class GoogleCalendarEventsResponse
    {
        [JsonPropertyName("items")] public List<GoogleCalendarEvent> Items { get; set; } = new();
    }

    private sealed class GoogleCalendarEvent
    {
        [JsonPropertyName("id")] public string? Id { get; set; }
        [JsonPropertyName("summary")] public string? Summary { get; set; }
        [JsonPropertyName("description")] public string? Description { get; set; }
        [JsonPropertyName("location")] public string? Location { get; set; }
        [JsonPropertyName("start")] public GoogleDateTime? Start { get; set; }
        [JsonPropertyName("end")] public GoogleDateTime? End { get; set; }
    }

    private sealed class GoogleDateTime
    {
        [JsonPropertyName("dateTime")] public string? DateTime { get; set; }
        [JsonPropertyName("date")] public string? Date { get; set; }
    }
}

public sealed class GoogleProviderException : Exception
{
    public GoogleProviderException(string message, bool reconnectRequired = false) : base(message) => ReconnectRequired = reconnectRequired;
    public bool ReconnectRequired { get; }
}
