using System.ComponentModel.DataAnnotations;

namespace TaskManagement.Application.DTOs.Collaboration;

public static class CollaborationReadResourceTypes
{
    public const string Channel = "channel";
    public const string DirectConversation = "dm";
}

public sealed class MarkCollaborationReadRequestDto
{
    [Required]
    public Guid MessageId { get; set; }
}

public sealed record CollaborationReadStateDto(
    string ResourceType,
    Guid ResourceId,
    Guid? LastReadMessageId,
    DateTime? LastReadAt,
    int UnreadCount);

public sealed record CollaborationReadStateDeliveryDto(
    Guid UserId,
    CollaborationReadStateDto State);
