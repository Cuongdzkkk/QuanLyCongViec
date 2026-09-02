using System.Text.Json;
using System.Text.Json.Serialization;
using System.Net.Http.Json;
using TaskManagement.Application.Auth;
using TaskManagement.Application.Interfaces;

namespace TaskManagement.Infrastructure.Services;

public sealed class GoogleAuthorizationCodeExchange : IGoogleAuthorizationCodeExchange
{
    private const string TokenEndpoint = "https://oauth2.googleapis.com/token";
    private readonly IHttpClientFactory _httpClientFactory;

    public GoogleAuthorizationCodeExchange(IHttpClientFactory httpClientFactory)
    {
        _httpClientFactory = httpClientFactory;
    }

    public async Task<string> ExchangeAsync(
        string code,
        string clientId,
        string clientSecret,
        string redirectUri,
        CancellationToken cancellationToken = default)
    {
        HttpResponseMessage response;
        try
        {
            response = await _httpClientFactory.CreateClient("GoogleAuth").PostAsync(
                TokenEndpoint,
                new FormUrlEncodedContent(new Dictionary<string, string>
                {
                    ["code"] = code,
                    ["client_id"] = clientId,
                    ["client_secret"] = clientSecret,
                    ["redirect_uri"] = redirectUri,
                    ["grant_type"] = "authorization_code"
                }),
                cancellationToken);
        }
        catch (HttpRequestException)
        {
            throw new GoogleProviderUnavailableException();
        }

        using (response)
        {
            GoogleTokenResponse? payload;
            try
            {
                payload = await response.Content.ReadFromJsonAsync<GoogleTokenResponse>(cancellationToken: cancellationToken);
            }
            catch (JsonException)
            {
                throw new GoogleProviderUnavailableException();
            }

            if (!response.IsSuccessStatusCode || string.IsNullOrWhiteSpace(payload?.IdToken))
            {
                if (response.StatusCode is System.Net.HttpStatusCode.BadRequest or System.Net.HttpStatusCode.Unauthorized)
                    throw new GoogleCredentialException();

                throw new GoogleProviderUnavailableException();
            }

            return payload.IdToken;
        }
    }

    private sealed class GoogleTokenResponse
    {
        [JsonPropertyName("id_token")]
        public string? IdToken { get; set; }
    }
}
