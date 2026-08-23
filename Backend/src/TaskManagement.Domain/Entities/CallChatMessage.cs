namespace TaskManagement.Domain.Entities;

public sealed class CallChatMessage
{
    public Guid Id { get; set; }
    public Guid CallSessionId { get; set; }
    public string RoomId { get; set; } = string.Empty;
    public Guid SenderUserId { get; set; }
    public string Content { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
}
