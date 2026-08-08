using System.ComponentModel.DataAnnotations;

namespace TaskManagement.Application.DTOs.Collaboration;

public sealed class CreateCollaborationChannelRequestDto
{
    [Required]
    [MaxLength(100)]
    public string Name { get; set; } = string.Empty;

    [MaxLength(500)]
    public string? Description { get; set; }

    [MaxLength(20)]
    public string Visibility { get; set; } = "Private";
}

public sealed record CollaborationChannelDto(
    Guid ChannelId,
    string Name,
    string? Description,
    Guid WorkspaceId,
    Guid ProjectId,
    string Visibility,
    bool IsMember,
    bool CanRead,
    bool CanSend,
    bool CanManage,
    DateTime CreatedAt,
    DateTime UpdatedAt,
    int UnreadCount = 0,
    Guid? LastReadMessageId = null);

public sealed record CollaborationChannelPageDto(
    IReadOnlyList<CollaborationChannelDto> Items,
    int Page,
    int PageSize,
    int TotalCount,
    string Ordering);

public sealed record ProvisionCollaborationChannelResult(
    CollaborationChannelDto Channel,
    bool Created);
