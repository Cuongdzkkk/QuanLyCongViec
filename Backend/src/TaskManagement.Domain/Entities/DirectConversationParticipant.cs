namespace TaskManagement.Domain.Entities;

public sealed class DirectConversationParticipant
{
    public Guid ConversationId { get; set; }
    public DirectConversation Conversation { get; set; } = null!;
    public Guid UserId { get; set; }
    public User User { get; set; } = null!;
    public DateTime JoinedAt { get; set; }
}
