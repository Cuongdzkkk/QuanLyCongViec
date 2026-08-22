using TaskManagement.Application.DTOs.Collaboration;

namespace TaskManagement.Application.Interfaces;

public interface ICallTranscriptionProvider
{
    bool IsConfigured { get; }

    Task<CallTranscriptionResult?> TranscribeAsync(
        CallAudioChunk chunk,
        CancellationToken cancellationToken = default);
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
