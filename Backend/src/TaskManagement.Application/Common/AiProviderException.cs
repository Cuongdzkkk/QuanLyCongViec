namespace TaskManagement.Application.Common;

public enum AiProviderErrorKind
{
    RateLimited,
    Unavailable
}

public sealed class AiProviderException : Exception
{
    public AiProviderErrorKind Kind { get; }
    public int? RetryAfterSeconds { get; }

    public AiProviderException(AiProviderErrorKind kind, int? retryAfterSeconds = null, Exception? innerException = null)
        : base(
            kind == AiProviderErrorKind.RateLimited
                ? "Dịch vụ AI đang bận. Vui lòng thử lại sau."
                : "Dịch vụ AI tạm thời không khả dụng. Vui lòng thử lại sau.",
            innerException)
    {
        Kind = kind;
        RetryAfterSeconds = retryAfterSeconds is > 0 ? retryAfterSeconds : null;
    }
}
