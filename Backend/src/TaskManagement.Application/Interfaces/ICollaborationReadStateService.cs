using TaskManagement.Application.DTOs.Collaboration;

namespace TaskManagement.Application.Interfaces;

public interface ICollaborationReadStateService
{
    Task<CollaborationReadStateDto> MarkChannelReadAsync(
        Guid channelId,
        Guid userId,
        Guid messageId,
        CancellationToken cancellationToken = default);

    Task<CollaborationReadStateDto> MarkDirectConversationReadAsync(
        Guid conversationId,
        Guid userId,
        Guid messageId,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<CollaborationReadStateDeliveryDto>>
        GetChannelUnreadUpdatesForMessageAsync(
            Guid messageId,
            CancellationToken cancellationToken = default);

    Task<IReadOnlyList<CollaborationReadStateDeliveryDto>>
        GetDirectUnreadUpdatesForMessageAsync(
            Guid messageId,
            CancellationToken cancellationToken = default);
}

public sealed class CollaborationMessageNotFoundException : Exception
{
    public CollaborationMessageNotFoundException() : base("Message was not found in this resource.") { }
}
