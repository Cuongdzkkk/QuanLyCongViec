namespace TaskManagement.Application.Common;

public enum AiTranscriptionProviderErrorKind
{
    InvalidRequest,
    Authentication
}

public sealed class AiTranscriptionProviderException : Exception
{
    public AiTranscriptionProviderErrorKind Kind { get; }

    public AiTranscriptionProviderException(AiTranscriptionProviderErrorKind kind)
        : base(kind == AiTranscriptionProviderErrorKind.InvalidRequest
            ? "Yêu cầu nhận dạng giọng nói không hợp lệ."
            : "Dịch vụ nhận dạng giọng nói chưa được cấu hình đúng.")
    {
        Kind = kind;
    }
}
