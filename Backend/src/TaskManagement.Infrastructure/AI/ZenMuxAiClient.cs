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

    public ZenMuxResponseDiagnostics? LastResponseDiagnostics { get; private set; }

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
        int? maxCompletionTokens = null,
        bool disableReasoning = false)
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

        if (disableReasoning)
        {
            payload["reasoning"] = new { enabled = false };
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
            var result = ParseResponse(responseBody, response);
            LastResponseDiagnostics = result.Diagnostics;
            return result;
        }
    }

    public async Task<ZenMuxTranscriptionResult> TranscribeAudioAsync(
        string languageMode,
        string audioFormat,
        byte[] audioBytes,
        CancellationToken cancellationToken = default)
    {
        var apiKey = _configuration["ZenMux:ApiKey"];
        if (string.IsNullOrWhiteSpace(apiKey))
        {
            throw new AiProviderException(AiProviderErrorKind.Unavailable);
        }

        var baseUrl = (_configuration["ZenMux:BaseUrl"] ?? "https://zenmux.ai/api/v1").TrimEnd('/');
        var model = _configuration["ZenMux:TranscriptionModel"] ?? "qwen/qwen3-asr-flash";
        var payload = new Dictionary<string, object?>
        {
            ["model"] = model,
            ["input_audio"] = new
            {
                data = Convert.ToBase64String(audioBytes),
                format = audioFormat
            }
        };

        if (languageMode is "vi" or "en")
        {
            payload["language"] = languageMode;
        }

        using var request = new HttpRequestMessage(HttpMethod.Post, $"{baseUrl}/audio/transcriptions");
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
                if (response.StatusCode == HttpStatusCode.BadRequest)
                {
                    throw new AiTranscriptionProviderException(AiTranscriptionProviderErrorKind.InvalidRequest);
                }

                if (response.StatusCode is HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden)
                {
                    throw new AiTranscriptionProviderException(AiTranscriptionProviderErrorKind.Authentication);
                }

                throw new AiProviderException(
                    response.StatusCode == HttpStatusCode.TooManyRequests
                        ? AiProviderErrorKind.RateLimited
                        : AiProviderErrorKind.Unavailable,
                    GetRetryAfterSeconds(response));
            }

            var responseBody = await response.Content.ReadAsStringAsync(cancellationToken);
            return ParseTranscriptionResponse(responseBody);
        }
    }

    private ZenMuxChatResult ParseResponse(string responseBody, HttpResponseMessage response)
    {
        try
        {
            using var doc = JsonDocument.Parse(responseBody);
            var root = doc.RootElement;
            var choiceCount = root.TryGetProperty("choices", out var choices) &&
                              choices.ValueKind == JsonValueKind.Array
                ? choices.GetArrayLength()
                : 0;
            var choice = choiceCount > 0 ? choices[0] : default;
            var message = choice.ValueKind == JsonValueKind.Object &&
                          choice.TryGetProperty("message", out var messageElement)
                ? messageElement
                : default;
            var content = message.ValueKind == JsonValueKind.Object &&
                          message.TryGetProperty("content", out var contentElement)
                ? ExtractContent(contentElement)
                : string.Empty;
            var diagnostics = new ZenMuxResponseDiagnostics(
                (int)response.StatusCode,
                root.TryGetProperty("id", out _) ||
                response.Headers.Contains("x-request-id") ||
                response.Headers.Contains("request-id"),
                choice.ValueKind == JsonValueKind.Object && choice.TryGetProperty("finish_reason", out var finishReason)
                    ? finishReason.GetString()
                    : null,
                choiceCount,
                content.Length > 0,
                content.Length,
                message.ValueKind == JsonValueKind.Object && message.TryGetProperty("tool_calls", out _),
                message.ValueKind == JsonValueKind.Object &&
                (message.TryGetProperty("reasoning", out _) || message.TryGetProperty("reasoning_content", out _)),
                message.ValueKind == JsonValueKind.Object && message.TryGetProperty("refusal", out _),
                root.TryGetProperty("error", out _));

            long totalTokens = 0;
            if (root.TryGetProperty("usage", out var usage) &&
                usage.TryGetProperty("total_tokens", out var totalTokensElement) &&
                totalTokensElement.TryGetInt64(out var parsedTokens))
            {
                totalTokens = parsedTokens;
            }

            return new ZenMuxChatResult(content, totalTokens, diagnostics);
        }
        catch (JsonException exception)
        {
            throw new AiProviderException(AiProviderErrorKind.Unavailable, innerException: exception);
        }
    }

    private ZenMuxTranscriptionResult ParseTranscriptionResponse(string responseBody)
    {
        try
        {
            using var doc = JsonDocument.Parse(responseBody);
            var root = doc.RootElement;
            if (root.ValueKind != JsonValueKind.Object)
            {
                throw new JsonException("Transcription response must be an object.");
            }

            var text = root.TryGetProperty("text", out var textElement) &&
                       textElement.ValueKind == JsonValueKind.String
                ? textElement.GetString() ?? string.Empty
                : string.Empty;
            var totalTokens = root.TryGetProperty("usage", out var usage) &&
                              usage.ValueKind == JsonValueKind.Object &&
                              usage.TryGetProperty("total_tokens", out var totalTokensElement) &&
                              totalTokensElement.TryGetInt64(out var parsedTokens)
                ? parsedTokens
                : 0;

            return new ZenMuxTranscriptionResult(text, totalTokens);
        }
        catch (JsonException exception)
        {
            throw new AiProviderException(AiProviderErrorKind.Unavailable, innerException: exception);
        }
    }

    private static string ExtractContent(JsonElement content)
    {
        if (content.ValueKind == JsonValueKind.String)
            return content.GetString() ?? string.Empty;
        if (content.ValueKind == JsonValueKind.Array)
        {
            var parts = content.EnumerateArray()
                .Select(item => item.ValueKind == JsonValueKind.String
                    ? item.GetString()
                    : item.ValueKind == JsonValueKind.Object &&
                      item.TryGetProperty("text", out var text) &&
                      text.ValueKind == JsonValueKind.String
                        ? text.GetString()
                        : null)
                .Where(item => !string.IsNullOrEmpty(item));
            return string.Join(string.Empty, parts);
        }
        if (content.ValueKind == JsonValueKind.Object &&
            content.TryGetProperty("text", out var objectText) &&
            objectText.ValueKind == JsonValueKind.String)
        {
            return objectText.GetString() ?? string.Empty;
        }
        return string.Empty;
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

public sealed record ZenMuxChatResult(string Text, long TotalTokens, ZenMuxResponseDiagnostics Diagnostics);

public sealed record ZenMuxTranscriptionResult(string Text, long TotalTokens);

public sealed record ZenMuxResponseDiagnostics(
    int HttpStatus,
    bool RequestIdPresent,
    string? FinishReason,
    int ChoiceCount,
    bool ContentPresent,
    int ContentLength,
    bool ToolCallsPresent,
    bool ReasoningPresent,
    bool RefusalPresent,
    bool ErrorPresent);
