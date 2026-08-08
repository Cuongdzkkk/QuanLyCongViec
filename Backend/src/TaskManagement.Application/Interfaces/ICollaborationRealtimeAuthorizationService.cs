namespace TaskManagement.Application.Interfaces;

public interface ICollaborationRealtimeAuthorizationService
{
    Task AuthorizeChannelJoinAsync(
        Guid channelId,
        Guid userId,
        CancellationToken cancellationToken = default);

    Task AuthorizeDirectConversationJoinAsync(
        Guid conversationId,
        Guid userId,
        CancellationToken cancellationToken = default);
}

public sealed class CollaborationRealtimeUserInactiveException : Exception
{
    public CollaborationRealtimeUserInactiveException() : base("The current user is inactive.") { }
}
