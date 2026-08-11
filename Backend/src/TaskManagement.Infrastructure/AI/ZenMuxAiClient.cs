using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.Configuration;
using TaskManagement.Application.Common;
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
        CancellationToken cancellationToken = default,
        int? maxCompletionTokens = null)
    {
        var apiKey = _configuration["ZenMux:ApiKey"];
        if (string.IsNullOrWhiteSpace(apiKey))
        {
            throw new AiProviderException(AiProviderErrorKind.Unavailable);
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

        if (maxCompletionTokens is > 0)
        {
            payload["max_completion_tokens"] = maxCompletionTokens;
        }

        using var request = new HttpRequestMessage(HttpMethod.Post, endpoint);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", apiKey.Trim());
        request.Content = JsonContent.Create(payload, options: _jsonOptions);

        HttpResponseMessage response;
        try
        {
            response = await _httpClient.SendAsync(request, cancellationToken);
        }
        catch (TaskCanceledException ex) when (!cancellationToken.IsCancellationRequested)
        {
            throw new AiProviderException(AiProviderErrorKind.Unavailable, innerException: ex);
        }
        catch (HttpRequestException ex)
        {
            throw new AiProviderException(AiProviderErrorKind.Unavailable, innerException: ex);
        }

        using (response)
        {
            if (!response.IsSuccessStatusCode)
            {
                throw new AiProviderException(
                    response.StatusCode == HttpStatusCode.TooManyRequests
                        ? AiProviderErrorKind.RateLimited
                        : AiProviderErrorKind.Unavailable,
                    GetRetryAfterSeconds(response));
            }

            var responseBody = await response.Content.ReadAsStringAsync(cancellationToken);
            return ParseResponse(responseBody);
        }
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

    private static int? GetRetryAfterSeconds(HttpResponseMessage response)
    {
        var retryAfter = response.Headers.RetryAfter;
        if (retryAfter?.Delta is { } delta)
        {
            return Math.Max(1, (int)Math.Ceiling(delta.TotalSeconds));
        }

        return retryAfter?.Date is { } date
            ? Math.Max(1, (int)Math.Ceiling((date - DateTimeOffset.UtcNow).TotalSeconds))
            : null;
    }
}

public sealed record ZenMuxChatResult(string Text, long TotalTokens);
