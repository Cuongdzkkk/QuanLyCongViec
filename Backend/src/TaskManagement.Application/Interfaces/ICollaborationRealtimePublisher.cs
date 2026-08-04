using TaskManagement.Application.DTOs.Collaboration;

namespace TaskManagement.Application.Interfaces;

public interface ICollaborationRealtimePublisher
{
    Task PublishChannelMessageCreatedAsync(
        ChannelMessageDto message,
        CancellationToken cancellationToken = default);

    Task PublishDirectMessageCreatedAsync(
        DirectMessageDto message,
        CancellationToken cancellationToken = default);

    Task PublishReadStateChangedAsync(
        Guid userId,
        CollaborationReadStateDto state,
        CancellationToken cancellationToken = default);
}
