namespace TaskManagement.Domain.Entities;

public sealed class CollaborationMessageReaction
{
    public Guid Id { get; set; }
    public Guid ChannelMessageId { get; set; }
    public ChannelMessage ChannelMessage { get; set; } = null!;
    public Guid UserId { get; set; }
    public User User { get; set; } = null!;
    public string Emoji { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
}
