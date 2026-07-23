using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.Configuration;
using TaskManagement.Infrastructure.Services;

namespace TaskManagement.Infrastructure.AI;

public sealed class ZenMuxAiClient
{
    private readonly HttpClient _httpClient;
    private readonly IConfiguration _configuration;
    private readonly JsonSerializerOptions _jsonOptions = new(JsonSerializerDefaults.Web);

    public ZenMuxAiClient(HttpClient httpClient, IConfiguration configuration)
    {
        _httpClient = httpClient;
        _configuration = configuration;
    }

    public async Task<ZenMuxChatResult> GenerateTextAsync(
        string prompt,
        string systemInstruction,
        bool forceJson = false,
        double? temperature = null,
        CancellationToken cancellationToken = default)
    {
        var apiKey = _configuration["ZenMux:ApiKey"];
        if (string.IsNullOrWhiteSpace(apiKey))
        {
            throw new InvalidOperationException("Chua cau hinh ZenMux API key. Hay cau hinh ZenMux:ApiKey bang bien moi truong hoac secret store.");
        }

        var baseUrl = (_configuration["ZenMux:BaseUrl"] ?? "https://zenmux.ai/api/v1").TrimEnd('/');
        var model = _configuration["ZenMux:Model"] ?? "deepseek/deepseek-v4-flash";
        var endpoint = $"{baseUrl}/chat/completions";

        var payload = new Dictionary<string, object?>
        {
            ["model"] = model,
            ["messages"] = new object[]
            {
                new { role = "system", content = systemInstruction },
                new { role = "user", content = AiSafetyGuard.RedactSecrets(prompt) }
            },
            ["temperature"] = temperature ?? (forceJson ? 0.2 : 0.5)
        };

        if (forceJson)
        {
            payload["response_format"] = new { type = "json_object" };
        }

        using var request = new HttpRequestMessage(HttpMethod.Post, endpoint);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", apiKey.Trim());
        request.Content = JsonContent.Create(payload, options: _jsonOptions);

        using var response = await _httpClient.SendAsync(request, cancellationToken);
        var responseBody = await response.Content.ReadAsStringAsync(cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            throw new InvalidOperationException(BuildProviderError(response.StatusCode, responseBody));
        }

        return ParseResponse(responseBody);
    }

    private ZenMuxChatResult ParseResponse(string responseBody)
    {
        using var doc = JsonDocument.Parse(responseBody);
        var root = doc.RootElement;
        var text = "";

        if (root.TryGetProperty("choices", out var choices) &&
            choices.ValueKind == JsonValueKind.Array &&
            choices.GetArrayLength() > 0 &&
            choices[0].TryGetProperty("message", out var message) &&
            message.TryGetProperty("content", out var content))
        {
            text = content.GetString() ?? "";
        }

        long totalTokens = 0;
        if (root.TryGetProperty("usage", out var usage) &&
            usage.TryGetProperty("total_tokens", out var totalTokensElement) &&
            totalTokensElement.TryGetInt64(out var parsedTokens))
        {
            totalTokens = parsedTokens;
        }

        return new ZenMuxChatResult(text, totalTokens);
    }

    private static string BuildProviderError(HttpStatusCode statusCode, string responseBody)
    {
        return (int)statusCode switch
        {
            401 => "ZenMux API key is invalid, expired, or not authorized.",
            402 => "ZenMux account has insufficient credits or billing is required.",
            429 => "ZenMux rate limit reached. Retry later.",
            408 or 504 => "ZenMux API timed out. Retry later.",
            >= 500 => "ZenMux API is temporarily unavailable.",
            _ => $"ZenMux API rejected the request ({(int)statusCode}): {ExtractSafeErrorMessage(responseBody)}"
        };
    }

    private static string ExtractSafeErrorMessage(string responseBody)
    {
        if (string.IsNullOrWhiteSpace(responseBody)) return "No additional error details.";

        try
        {
            using var doc = JsonDocument.Parse(responseBody);
            var root = doc.RootElement;
            if (root.TryGetProperty("error", out var error))
            {
                if (error.ValueKind == JsonValueKind.Object &&
                    error.TryGetProperty("message", out var message))
                {
                    return Limit(AiSafetyGuard.RedactSecrets(message.GetString()), 240);
                }

                if (error.ValueKind == JsonValueKind.String)
                {
                    return Limit(AiSafetyGuard.RedactSecrets(error.GetString()), 240);
                }
            }

            if (root.TryGetProperty("message", out var rootMessage))
            {
                return Limit(AiSafetyGuard.RedactSecrets(rootMessage.GetString()), 240);
            }
        }
        catch (JsonException)
        {
            return Limit(AiSafetyGuard.RedactSecrets(responseBody), 240);
        }

        return "No additional error details.";
    }

    private static string Limit(string? value, int maxLength)
    {
        var text = string.IsNullOrWhiteSpace(value) ? "No additional error details." : value.Trim();
        return text.Length <= maxLength ? text : text[..maxLength];
    }
}

public sealed record ZenMuxChatResult(string Text, long TotalTokens);
