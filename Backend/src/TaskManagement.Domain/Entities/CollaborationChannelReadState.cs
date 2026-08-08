namespace TaskManagement.Domain.Entities;

public sealed class CollaborationChannelReadState
{
    public Guid ChannelId { get; set; }
    public CollaborationChannel Channel { get; set; } = null!;
    public Guid UserId { get; set; }
    public User User { get; set; } = null!;
    public Guid? LastReadMessageId { get; set; }
    public ChannelMessage? LastReadMessage { get; set; }
    public DateTime LastReadAt { get; set; }
}
