namespace TaskManagement.Domain.Entities;

public sealed class MeetingAiReport
{
    public Guid Id { get; set; }
    public Guid CallSessionId { get; set; }
    public Guid ProjectId { get; set; }
    public string VoiceChannelId { get; set; } = string.Empty;
    public string Status { get; set; } = "PROCESSING";
    public int ProcessedTranscriptChunkCount { get; set; }
    public string StateJson { get; set; } = "{}";
    public DateTimeOffset UpdatedAt { get; set; }
    public DateTimeOffset? CompletedAt { get; set; }
}
