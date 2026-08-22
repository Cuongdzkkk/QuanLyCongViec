namespace TaskManagement.Application.DTOs.Collaboration;

public static class ChatRealtimeEvents
{
    public const string ChannelMessageCreated = nameof(ChannelMessageCreated);
    public const string DirectMessageCreated = nameof(DirectMessageCreated);
    public const string CollaborationReadStateChanged = nameof(CollaborationReadStateChanged);
    public const string CollaborationMentionCreated = nameof(CollaborationMentionCreated);
    public const string ChannelMessageReactionChanged = nameof(ChannelMessageReactionChanged);
    public const string ChannelMessagePinChanged = nameof(ChannelMessagePinChanged);
}

public static class ChatRealtimeGroups
{
    public static string Channel(Guid channelId) => $"channel:{channelId:D}";
    public static string DirectConversation(Guid conversationId) => $"dm:{conversationId:D}";
}

public sealed record ChannelMessageCreatedEventDto(
    Guid MessageId,
    Guid ChannelId,
    string Content,
    ChannelMessageSenderDto Sender,
    DateTime CreatedAt,
    IReadOnlyList<CollaborationAttachmentDto>? Attachments = null,
    IReadOnlyList<ChannelMessageMentionDto>? Mentions = null,
    ChannelMessageQuoteDto? ReplyTo = null,
    IReadOnlyList<ChannelMessageReactionDto>? Reactions = null,
    bool IsPinned = false);

public sealed record DirectMessageCreatedEventDto(
    Guid MessageId,
    Guid ConversationId,
    string Content,
    DirectMessageSenderDto Sender,
    DateTime CreatedAt,
    IReadOnlyList<CollaborationAttachmentDto>? Attachments = null);
