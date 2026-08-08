namespace TaskManagement.Domain.Entities;

public sealed class CollaborationChannelMember
{
    public Guid ChannelId { get; set; }
    public CollaborationChannel Channel { get; set; } = null!;
    public Guid UserId { get; set; }
    public User User { get; set; } = null!;
    public DateTime JoinedAt { get; set; }
    public DateTime? LeftAt { get; set; }
    public bool IsActive { get; set; } = true;
    public bool CanSendMessages { get; set; } = true;
}
