using System.Text;
using System.Text.RegularExpressions;

namespace TaskManagement.Infrastructure.Services;

public static partial class AiSafetyGuard
{
    private const string Redacted = "[REDACTED]";

    public static string RedactSecrets(string? value)
    {
        if (string.IsNullOrEmpty(value)) return value ?? string.Empty;

        var result = PrivateKeyRegex().Replace(value, "-----BEGIN PRIVATE KEY-----\n[REDACTED]\n-----END PRIVATE KEY-----");
        result = BearerRegex().Replace(result, "$1" + Redacted);
        result = ApiKeyRegex().Replace(result, "$1" + Redacted);
        result = PasswordRegex().Replace(result, "$1" + Redacted);
        result = ConnectionStringPasswordRegex().Replace(result, "$1" + Redacted);
        result = OtpRegex().Replace(result, "$1" + Redacted);
        return result;
    }

    public static string WrapUntrustedText(string content, string sourceId, string sourceName)
    {
        var safeId = Regex.Replace(sourceId ?? string.Empty, "[^A-Za-z0-9_-]", string.Empty);
        var safeName = Path.GetFileName(sourceName ?? "source");
        var builder = new StringBuilder();
        builder.AppendLine($"<untrusted-source id=\"{safeId}\" name=\"{safeName}\">");
        builder.AppendLine(RedactSecrets(content));
        builder.AppendLine("</untrusted-source>");
        return builder.ToString();
    }

    public static string SafeProviderError(int statusCode) => statusCode switch
    {
        429 => "AI provider rate limit reached. Retry later.",
        408 or 504 => "AI provider timed out. Retry later.",
        _ when statusCode >= 500 => "AI provider is temporarily unavailable.",
        _ => "AI provider rejected the request."
    };

    [GeneratedRegex(@"-----BEGIN (?:RSA |EC |OPENSSH )?PRIVATE KEY-----[\s\S]*?-----END (?:RSA |EC |OPENSSH )?PRIVATE KEY-----", RegexOptions.IgnoreCase)]
    private static partial Regex PrivateKeyRegex();

    [GeneratedRegex(@"(?i)(\bBearer\s+)[A-Za-z0-9._~+\-/=]{8,}")]
    private static partial Regex BearerRegex();

    [GeneratedRegex(@"(?i)(\b(?:api[_ -]?key|secret|token)\s*[:=]\s*)[^\s,;]{6,}")]
    private static partial Regex ApiKeyRegex();

    [GeneratedRegex(@"(?i)(\bpassword\s*[:=]\s*)[^\s,;]{4,}")]
    private static partial Regex PasswordRegex();

    [GeneratedRegex(@"(?i)(\b(?:Password|Pwd)\s*=\s*)[^;\r\n]+")]
    private static partial Regex ConnectionStringPasswordRegex();

    [GeneratedRegex(@"(?i)(\bOTP\s*[:=]\s*)\d{6}\b")]
    private static partial Regex OtpRegex();
}
