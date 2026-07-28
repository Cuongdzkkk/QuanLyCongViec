using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using TaskManagement.Application.DTOs.Collaboration;
using TaskManagement.Application.Interfaces;

namespace TaskManagement.API.Hubs;

[Authorize]
public sealed class ChatHub : Hub
{
    public const string Route = "/hubs/chat";

    private readonly ICollaborationRealtimeAuthorizationService _authorization;
    private readonly ILogger<ChatHub> _logger;

    public ChatHub(
        ICollaborationRealtimeAuthorizationService authorization,
        ILogger<ChatHub> logger)
    {
        _authorization = authorization;
        _logger = logger;
    }

    public override Task OnConnectedAsync()
    {
        var userId = GetCurrentUserId();
        _logger.LogInformation(
            "Chat connection established for user {UserId} on connection {ConnectionId}",
            userId,
            Context.ConnectionId);
        return base.OnConnectedAsync();
    }

    public override Task OnDisconnectedAsync(Exception? exception)
    {
        var userId = TryGetCurrentUserId(out var parsedUserId)
            ? parsedUserId.ToString()
            : "unknown";
        _logger.LogInformation(
            "Chat connection ended for user {UserId} on connection {ConnectionId}",
            userId,
            Context.ConnectionId);
        return base.OnDisconnectedAsync(exception);
    }

    public Task JoinChannel(string? channelId) =>
        JoinAuthorizedGroupAsync(
            channelId,
            ChatRealtimeGroups.Channel,
            _authorization.AuthorizeChannelJoinAsync,
            "CHANNEL_NOT_FOUND_OR_FORBIDDEN",
            "channel");

    public Task LeaveChannel(string? channelId) =>
        LeaveGroupAsync(channelId, ChatRealtimeGroups.Channel, "channel");

    public Task JoinDirectConversation(string? conversationId) =>
        JoinAuthorizedGroupAsync(
            conversationId,
            ChatRealtimeGroups.DirectConversation,
            _authorization.AuthorizeDirectConversationJoinAsync,
            "CONVERSATION_NOT_FOUND_OR_FORBIDDEN",
            "direct-conversation");

    public Task LeaveDirectConversation(string? conversationId) =>
        LeaveGroupAsync(
            conversationId,
            ChatRealtimeGroups.DirectConversation,
            "direct-conversation");

    private async Task JoinAuthorizedGroupAsync(
        string? rawId,
        Func<Guid, string> groupNameFactory,
        Func<Guid, Guid, CancellationToken, Task> authorize,
        string forbiddenCode,
        string category)
    {
        var entityId = ParseId(rawId);
        var userId = GetCurrentUserId();
        try
        {
            await authorize(entityId, userId, Context.ConnectionAborted);
            await Groups.AddToGroupAsync(
                Context.ConnectionId,
                groupNameFactory(entityId),
                Context.ConnectionAborted);
            _logger.LogInformation(
                "Chat group joined by user {UserId} on connection {ConnectionId} for {Category}",
                userId,
                Context.ConnectionId,
                category);
        }
        catch (CollaborationRealtimeUserInactiveException)
        {
            throw new HubException("USER_INACTIVE");
        }
        catch (ChannelNotFoundException)
        {
            throw new HubException(forbiddenCode);
        }
        catch (DirectConversationNotFoundException)
        {
            throw new HubException(forbiddenCode);
        }
        catch (DirectParticipantNotFoundException)
        {
            throw new HubException(forbiddenCode);
        }
        catch (HubException)
        {
            throw;
        }
        catch
        {
            _logger.LogWarning(
                "Chat group join failed for user {UserId} on connection {ConnectionId} for {Category}",
                userId,
                Context.ConnectionId,
                category);
            throw new HubException("JOIN_FAILED");
        }
    }

    private async Task LeaveGroupAsync(
        string? rawId,
        Func<Guid, string> groupNameFactory,
        string category)
    {
        var entityId = ParseId(rawId);
        var userId = GetCurrentUserId();
        try
        {
            await Groups.RemoveFromGroupAsync(
                Context.ConnectionId,
                groupNameFactory(entityId),
                Context.ConnectionAborted);
            _logger.LogInformation(
                "Chat group left by user {UserId} on connection {ConnectionId} for {Category}",
                userId,
                Context.ConnectionId,
                category);
        }
        catch
        {
            _logger.LogWarning(
                "Chat group leave failed for user {UserId} on connection {ConnectionId} for {Category}",
                userId,
                Context.ConnectionId,
                category);
            throw new HubException("JOIN_FAILED");
        }
    }

    private Guid GetCurrentUserId()
    {
        if (!TryGetCurrentUserId(out var userId))
            throw new HubException("AUTH_REQUIRED");
        return userId;
    }

    private bool TryGetCurrentUserId(out Guid userId)
    {
        userId = Guid.Empty;
        return Context.User?.Identity?.IsAuthenticated == true &&
            Guid.TryParse(
                Context.User.FindFirstValue(ClaimTypes.NameIdentifier),
                out userId);
    }

    private static Guid ParseId(string? rawId)
    {
        if (!Guid.TryParse(rawId, out var entityId) || entityId == Guid.Empty)
            throw new HubException("INVALID_ID");
        return entityId;
    }
}
