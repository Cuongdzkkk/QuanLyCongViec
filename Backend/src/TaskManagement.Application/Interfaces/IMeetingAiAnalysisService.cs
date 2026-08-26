using TaskManagement.Application.DTOs.Collaboration;

namespace TaskManagement.Application.Interfaces;

public interface IMeetingAiAnalysisService
{
    bool IsEnabled { get; }
    bool IsConfigured { get; }
    string ProviderName { get; }
    int TranscriptChunkSize { get; }

    void QueueIncremental(CallTranscriptChunkDto transcriptChunk);
    void QueueFinalize(Guid callSessionId);
    void QueueFinalizeRoom(string roomId);
    Task<MeetingAiReportDto?> GetAsync(Guid callSessionId, CancellationToken cancellationToken = default);
}
