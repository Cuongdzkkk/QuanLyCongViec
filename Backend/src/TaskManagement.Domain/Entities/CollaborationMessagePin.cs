namespace TaskManagement.Domain.Entities;

public sealed class CollaborationMessagePin
{
    public Guid Id { get; set; }
    public Guid ChannelMessageId { get; set; }
    public ChannelMessage ChannelMessage { get; set; } = null!;
    public Guid PinnedByUserId { get; set; }
    public User PinnedByUser { get; set; } = null!;
    public DateTime PinnedAt { get; set; }
}
