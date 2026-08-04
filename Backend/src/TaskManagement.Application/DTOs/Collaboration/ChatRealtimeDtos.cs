namespace TaskManagement.Application.DTOs.Collaboration;

public static class ChatRealtimeEvents
{
    public const string ChannelMessageCreated = nameof(ChannelMessageCreated);
    public const string DirectMessageCreated = nameof(DirectMessageCreated);
    public const string CollaborationReadStateChanged = nameof(CollaborationReadStateChanged);
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
    DateTime CreatedAt);

public sealed record DirectMessageCreatedEventDto(
    Guid MessageId,
    Guid ConversationId,
    string Content,
    DirectMessageSenderDto Sender,
    DateTime CreatedAt);
