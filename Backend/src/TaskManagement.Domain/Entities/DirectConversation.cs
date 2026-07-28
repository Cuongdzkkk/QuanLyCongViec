namespace TaskManagement.Domain.Entities;

public sealed class DirectConversation
{
    public Guid Id { get; set; }
    public Guid WorkspaceId { get; set; }
    public Workspace Workspace { get; set; } = null!;
    public Guid UserLowId { get; set; }
    public User UserLow { get; set; } = null!;
    public Guid UserHighId { get; set; }
    public User UserHigh { get; set; } = null!;
    public DateTime CreatedAt { get; set; }
    public DateTime? LastMessageAt { get; set; }
    public ICollection<DirectConversationParticipant> Participants { get; set; } =
        new List<DirectConversationParticipant>();
    public ICollection<DirectMessage> Messages { get; set; } = new List<DirectMessage>();
}
