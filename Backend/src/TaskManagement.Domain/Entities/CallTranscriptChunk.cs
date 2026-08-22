namespace TaskManagement.Domain.Entities;

public sealed class CallTranscriptChunk
{
    public Guid Id { get; set; }
    public Guid CallSessionId { get; set; }
    public Guid ProjectId { get; set; }
    public string VoiceChannelId { get; set; } = string.Empty;
    public Guid SpeakerUserId { get; set; }
    public string SpeakerDisplayName { get; set; } = string.Empty;
    public DateTimeOffset StartedAt { get; set; }
    public DateTimeOffset EndedAt { get; set; }
    public string Text { get; set; } = string.Empty;
    public double? Confidence { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
}
