namespace TaskManagement.Domain.Entities;

public sealed class DirectConversationReadState
{
    public Guid ConversationId { get; set; }
    public DirectConversation Conversation { get; set; } = null!;
    public Guid UserId { get; set; }
    public User User { get; set; } = null!;
    public Guid? LastReadMessageId { get; set; }
    public DirectMessage? LastReadMessage { get; set; }
    public DateTime LastReadAt { get; set; }
}
