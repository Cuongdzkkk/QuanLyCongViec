using TaskManagement.Application.DTOs.Collaboration;

namespace TaskManagement.Application.Interfaces;

public interface IChannelTextService
{
    Task<ChannelMessagePageDto> GetHistoryAsync(
        Guid channelId,
        Guid userId,
        int page,
        int pageSize,
        CancellationToken cancellationToken = default);

    Task<ChannelMessageDto> SendAsync(
        Guid channelId,
        Guid userId,
        string? content,
        CancellationToken cancellationToken = default);

    Task<ChannelMessageDto> SendWithAttachmentsAsync(
        Guid channelId,
        Guid userId,
        string? content,
        IReadOnlyList<PendingCollaborationAttachmentDto> attachments,
        CancellationToken cancellationToken = default);

    Task<SendChannelMessageResult> SendWithMentionsAsync(
        Guid channelId,
        Guid userId,
        string? content,
        IReadOnlyList<ChannelMessageMentionRequestDto> mentions,
        IReadOnlyList<PendingCollaborationAttachmentDto> attachments,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<ChannelMemberSuggestionDto>> SearchMembersAsync(
        Guid channelId,
        Guid userId,
        string? query,
        int limit,
        CancellationToken cancellationToken = default);
}

public sealed class ChannelNotFoundException : Exception
{
    public ChannelNotFoundException() : base("Channel was not found.") { }
}

public sealed class ChannelSendForbiddenException : Exception
{
    public ChannelSendForbiddenException() : base("You do not have permission to send messages to this channel.") { }
}

public sealed class ChannelMentionForbiddenException : Exception
{
    public ChannelMentionForbiddenException()
        : base("One or more mentioned users are not active members of this channel.") { }
}
