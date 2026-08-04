namespace TaskManagement.Application.DTOs.Collaboration;

public sealed record CreateDirectConversationRequestDto(Guid ParticipantUserId);
public sealed record SendDirectMessageRequestDto(string? Content);
public sealed record DirectParticipantDto(Guid UserId, string DisplayName, string? AvatarUrl);
public sealed record DirectConversationDto(
    Guid ConversationId,
    DirectParticipantDto OtherParticipant,
    string? LastMessagePreview,
    DateTime? LastMessageAt,
    DateTime CreatedAt,
    int UnreadCount = 0,
    Guid? LastReadMessageId = null);
public sealed record DirectConversationPageDto(
    IReadOnlyList<DirectConversationDto> Items,
    int Page,
    int PageSize,
    int TotalCount,
    string Ordering);
public sealed record DirectMessageSenderDto(Guid UserId, string DisplayName, string? AvatarUrl);
public sealed record DirectMessageDto(
    Guid MessageId,
    Guid ConversationId,
    string Content,
    DirectMessageSenderDto Sender,
    DateTime CreatedAt,
    IReadOnlyList<CollaborationAttachmentDto>? Attachments = null);
public sealed record DirectMessagePageDto(
    IReadOnlyList<DirectMessageDto> Items,
    int Page,
    int PageSize,
    int TotalCount,
    string Ordering);
