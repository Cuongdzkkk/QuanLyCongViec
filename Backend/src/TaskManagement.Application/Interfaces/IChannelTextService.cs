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
        CancellationToken cancellationToken = default,
        Guid? replyToMessageId = null);

    Task<IReadOnlyList<ChannelMemberSuggestionDto>> SearchMembersAsync(
        Guid channelId,
        Guid userId,
        string? query,
        int limit,
        CancellationToken cancellationToken = default);

    Task<ChannelMessagePageDto> SearchAsync(Guid channelId, Guid userId, string query, int page, int pageSize, CancellationToken cancellationToken = default);
    Task<ChannelMessageReactionChangeDto> AddReactionAsync(Guid channelId, Guid messageId, Guid userId, string emoji, CancellationToken cancellationToken = default);
    Task<ChannelMessageReactionChangeDto> RemoveReactionAsync(Guid channelId, Guid messageId, Guid userId, string emoji, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<ChannelPinnedMessageDto>> GetPinsAsync(Guid channelId, Guid userId, CancellationToken cancellationToken = default);
    Task<ChannelMessagePinChangeDto> PinAsync(Guid channelId, Guid messageId, Guid userId, CancellationToken cancellationToken = default);
    Task<ChannelMessagePinChangeDto> UnpinAsync(Guid channelId, Guid messageId, Guid userId, CancellationToken cancellationToken = default);
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

public sealed class ChannelManageForbiddenException : Exception
{
    public ChannelManageForbiddenException() : base("You do not have permission to manage this channel.") { }
}
