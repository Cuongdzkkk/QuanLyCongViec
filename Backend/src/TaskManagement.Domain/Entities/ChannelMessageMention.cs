namespace TaskManagement.Domain.Entities;

public sealed class ChannelMessageMention
{
    public Guid Id { get; set; }
    public Guid ChannelMessageId { get; set; }
    public ChannelMessage ChannelMessage { get; set; } = null!;
    public Guid MentionedUserId { get; set; }
    public User MentionedUser { get; set; } = null!;
    public int StartIndex { get; set; }
    public int Length { get; set; }
    public string DisplayText { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
}
