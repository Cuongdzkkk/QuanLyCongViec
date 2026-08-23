namespace TaskManagement.Application.DTOs.Collaboration;

public sealed record CallChatMessageDto(
    Guid MessageId,
    Guid CallSessionId,
    string RoomId,
    Guid SenderUserId,
    string SenderName,
    string Content,
    DateTime CreatedAt,
    string? ClientMessageId = null);
