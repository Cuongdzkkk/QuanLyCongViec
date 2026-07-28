using Microsoft.AspNetCore.SignalR;
using TaskManagement.API.Hubs;
using TaskManagement.Application.DTOs.Collaboration;
using TaskManagement.Application.Interfaces;

namespace TaskManagement.API.Services;

public sealed class ChatRealtimePublisher : ICollaborationRealtimePublisher
{
    private readonly IHubContext<ChatHub> _hubContext;
    private readonly ILogger<ChatRealtimePublisher> _logger;

    public ChatRealtimePublisher(
        IHubContext<ChatHub> hubContext,
        ILogger<ChatRealtimePublisher> logger)
    {
        _hubContext = hubContext;
        _logger = logger;
    }

    public Task PublishChannelMessageCreatedAsync(
        ChannelMessageDto message,
        CancellationToken cancellationToken = default) =>
        PublishSafelyAsync(
            ChatRealtimeGroups.Channel(message.ChannelId),
            ChatRealtimeEvents.ChannelMessageCreated,
            new ChannelMessageCreatedEventDto(
                message.MessageId,
                message.ChannelId,
                message.Content,
                message.Sender,
                message.CreatedAt),
            message.MessageId,
            cancellationToken);

    public Task PublishDirectMessageCreatedAsync(
        DirectMessageDto message,
        CancellationToken cancellationToken = default) =>
        PublishSafelyAsync(
            ChatRealtimeGroups.DirectConversation(message.ConversationId),
            ChatRealtimeEvents.DirectMessageCreated,
            new DirectMessageCreatedEventDto(
                message.MessageId,
                message.ConversationId,
                message.Content,
                message.Sender,
                message.CreatedAt),
            message.MessageId,
            cancellationToken);

    private async Task PublishSafelyAsync<TPayload>(
        string groupName,
        string eventName,
        TPayload payload,
        Guid messageId,
        CancellationToken cancellationToken)
    {
        try
        {
            await _hubContext.Clients
                .Group(groupName)
                .SendAsync(eventName, payload, cancellationToken);
        }
        catch
        {
            _logger.LogWarning(
                "Realtime delivery failed for event {EventName} and message {MessageId}",
                eventName,
                messageId);
        }
    }
}
