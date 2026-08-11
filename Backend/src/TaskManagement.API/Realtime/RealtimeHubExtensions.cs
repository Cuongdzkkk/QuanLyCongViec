using Microsoft.AspNetCore.SignalR;
using TaskManagement.API.Hubs;

namespace TaskManagement.API.Realtime;

public static class RealtimeHubExtensions
{
    public static Task PublishUserEntityChangedAsync(
        this IHubContext<KanbanHub> hub,
        Guid userId,
        string entityType,
        string action,
        Guid entityId,
        object? data = null,
        CancellationToken cancellationToken = default)
    {
        return hub.Clients.Group($"user:{userId}").SendAsync(
            "EntityChanged",
            new
            {
                eventId = Guid.NewGuid(),
                entityType,
                action,
                entityId,
                userId,
                occurredAt = DateTime.UtcNow,
                data
            },
            cancellationToken);
    }

    public static Task PublishAuthenticatedEntityChangedAsync(
        this IHubContext<KanbanHub> hub,
        string entityType,
        string action,
        Guid entityId,
        object? data = null,
        CancellationToken cancellationToken = default)
    {
        return hub.Clients.All.SendAsync(
            "EntityChanged",
            new
            {
                eventId = Guid.NewGuid(),
                entityType,
                action,
                entityId,
                occurredAt = DateTime.UtcNow,
                data
            },
            cancellationToken);
    }

    public static Task PublishEntityChangedAsync(
        this IHubContext<KanbanHub> hub,
        Guid projectId,
        string entityType,
        string action,
        Guid entityId,
        object? data = null,
        object? version = null,
        CancellationToken cancellationToken = default)
    {
        return hub.Clients.Group(projectId.ToString()).SendAsync(
            "EntityChanged",
            new
            {
                eventId = Guid.NewGuid(),
                entityType,
                action,
                entityId,
                projectId,
                version,
                occurredAt = DateTime.UtcNow,
                data
            },
            cancellationToken);
    }

    public static Task PublishWorkspaceEntityChangedAsync(
        this IHubContext<KanbanHub> hub,
        Guid workspaceId,
        string entityType,
        string action,
        Guid entityId,
        object? data = null,
        object? version = null,
        CancellationToken cancellationToken = default)
    {
        return hub.Clients.Group($"workspace:{workspaceId}").SendAsync(
            "EntityChanged",
            new
            {
                eventId = Guid.NewGuid(),
                entityType,
                action,
                entityId,
                workspaceId,
                version,
                occurredAt = DateTime.UtcNow,
                data
            },
            cancellationToken);
    }
}
