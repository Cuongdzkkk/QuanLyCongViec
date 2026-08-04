using System.ComponentModel.DataAnnotations;

namespace TaskManagement.Application.DTOs.Collaboration;

public sealed class SendChannelMessageRequestDto
{
    [Required]
    [MaxLength(4000)]
    public string Content { get; set; } = string.Empty;
}

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
    IReadOnlyList<CollaborationAttachmentDto>? Attachments = null);

public sealed record ChannelMessagePageDto(
    IReadOnlyList<ChannelMessageDto> Items,
    int Page,
    int PageSize,
    int TotalCount,
    string Ordering);
