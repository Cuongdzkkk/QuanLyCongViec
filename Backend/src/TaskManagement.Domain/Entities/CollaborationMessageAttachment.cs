namespace TaskManagement.Domain.Entities;

public sealed class CollaborationMessageAttachment
{
    public Guid Id { get; set; }
    public Guid? ChannelMessageId { get; set; }
    public ChannelMessage? ChannelMessage { get; set; }
    public Guid? DirectMessageId { get; set; }
    public DirectMessage? DirectMessage { get; set; }
    public string StorageKey { get; set; } = string.Empty;
    public string OriginalFileName { get; set; } = string.Empty;
    public string ContentType { get; set; } = string.Empty;
    public long SizeBytes { get; set; }
    public Guid UploadedByUserId { get; set; }
    public User? UploadedByUser { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
