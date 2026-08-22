using TaskManagement.Application.DTOs.Collaboration;

namespace TaskManagement.Application.Interfaces;

public interface ICallTranscriptionProvider
{
    bool IsConfigured { get; }

    Task<CallTranscriptionResult?> TranscribeAsync(
        CallAudioChunk chunk,
        CancellationToken cancellationToken = default);
}

public interface ICallStreamingTranscriptionProvider : ICallTranscriptionProvider
{
    Task SubmitAsync(
        CallAudioChunk chunk,
        Func<CallAudioChunk, CallTranscriptionResult, Task> onResult,
        Func<bool> canContinue,
        CancellationToken cancellationToken = default);

    Task StopAsync(
        string roomId,
        Guid callSessionId,
        Guid speakerUserId,
        long consentGeneration,
        CancellationToken cancellationToken = default);

    Task StopRoomAsync(string roomId, CancellationToken cancellationToken = default);
}

public interface ICallTranscriptionUsageSink
{
    Task RecordAsync(CallTranscriptionUsage usage, CancellationToken cancellationToken = default);
}

public interface ICallTranscriptService
{
    Task<CallTranscriptChunkDto?> AppendAsync(
        CallAudioChunk source,
        CallTranscriptionResult result,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<CallTranscriptChunkDto>> GetAsync(
        Guid projectId,
        string voiceChannelId,
        Guid callSessionId,
        CancellationToken cancellationToken = default);
}

public sealed class CallTranscriptionProviderUnavailableException : InvalidOperationException
{
    public CallTranscriptionProviderUnavailableException()
        : base("Live call transcription is not configured.")
    {
    }
}
