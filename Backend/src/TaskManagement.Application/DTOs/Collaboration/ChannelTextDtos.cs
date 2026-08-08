using System.ComponentModel.DataAnnotations;

namespace TaskManagement.Application.DTOs.Collaboration;

public sealed class SendChannelMessageRequestDto
{
    [Required]
    [MaxLength(4000)]
    public string Content { get; set; } = string.Empty;

    public List<ChannelMessageMentionRequestDto> Mentions { get; set; } = [];
}

public sealed class ChannelMessageMentionRequestDto
{
    public Guid UserId { get; set; }
    public int StartIndex { get; set; }
    public int Length { get; set; }
}

public sealed record ChannelMemberSuggestionDto(
    Guid UserId,
    string DisplayName,
    string? AvatarUrl);

public sealed record ChannelMessageMentionDto(
    Guid UserId,
    string DisplayText,
    int StartIndex,
    int Length);

public sealed record ChannelMessageSenderDto(
    Guid UserId,
    string DisplayName,
    string? AvatarUrl);

public sealed record ChannelMessageDto(
    Guid MessageId,
    Guid ChannelId,
    string Content,
    ChannelMessageSenderDto Sender,
    DateTime CreatedAt,
    Guid OrderingId,
    IReadOnlyList<CollaborationAttachmentDto>? Attachments = null,
    IReadOnlyList<ChannelMessageMentionDto>? Mentions = null);

public sealed record ChannelMessagePageDto(
    IReadOnlyList<ChannelMessageDto> Items,
    int Page,
    int PageSize,
    int TotalCount,
    string Ordering);

public sealed record CollaborationMentionCreatedEventDto(
    Guid NotificationId,
    Guid ChannelId,
    Guid MessageId,
    ChannelMessageSenderDto Actor,
    string Preview,
    DateTime CreatedAt);

public sealed record SendChannelMessageResult(
    ChannelMessageDto Message,
    IReadOnlyList<CollaborationMentionDelivery> MentionNotifications);

public sealed record CollaborationMentionDelivery(
    Guid RecipientUserId,
    CollaborationMentionCreatedEventDto Notification);
