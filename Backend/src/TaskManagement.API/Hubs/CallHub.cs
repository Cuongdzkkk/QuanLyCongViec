using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using TaskManagement.Application.DTOs.Collaboration;
using TaskManagement.Application.Interfaces;

namespace TaskManagement.API.Hubs;

[Authorize]
public sealed class CallHub : Hub
{
    public const string Route = "/hubs/call";

    private readonly ICallRoomRegistry _rooms;
    private readonly ICallRoomAuthorizationService _authorization;

    public CallHub(ICallRoomRegistry rooms, ICallRoomAuthorizationService authorization)
    {
        _rooms = rooms;
        _authorization = authorization;
    }

    public async Task<CallRoomSnapshotDto> JoinVoiceRoom(string? projectId, string? voiceChannelId)
    {
        var userId = GetCurrentUserId();
        var parsedProjectId = ParseGuid(projectId, "INVALID_PROJECT_ID");
        var roomId = BuildRoomId(parsedProjectId, voiceChannelId);
        await _authorization.AuthorizeVoiceRoomJoinAsync(parsedProjectId, userId, Context.ConnectionAborted);
        var profile = await _authorization.GetParticipantProfileAsync(userId, Context.ConnectionAborted);
        var result = _rooms.Join(new CallRoomParticipant(
            roomId, Context.ConnectionId, userId, profile.DisplayName, profile.AvatarUrl, true, false, false));
        if (result.RoomFull) throw new HubException("CALL_ROOM_FULL");

        await Groups.AddToGroupAsync(Context.ConnectionId, roomId, Context.ConnectionAborted);
        await Clients.OthersInGroup(roomId).SendAsync(
            CallRealtimeEvents.ParticipantJoined,
            new CallParticipantJoinedDto(result.JoinedParticipant!),
            Context.ConnectionAborted);
        return result.Snapshot;
    }

    public async Task LeaveVoiceRoom(string? projectId, string? voiceChannelId)
    {
        var roomId = BuildRoomId(ParseGuid(projectId, "INVALID_PROJECT_ID"), voiceChannelId);
        await LeaveRoomAsync(roomId, Context.ConnectionAborted);
    }

    public Task SendWebRtcOffer(string? roomId, string? targetConnectionId, object? description) =>
        RelaySignalAsync(roomId, targetConnectionId, description, CallRealtimeEvents.WebRtcOffer,
            (sender, target, payload) => new CallOfferDto(sender.ConnectionId, sender.UserId, target.ConnectionId, payload));

    public Task SendWebRtcAnswer(string? roomId, string? targetConnectionId, object? description) =>
        RelaySignalAsync(roomId, targetConnectionId, description, CallRealtimeEvents.WebRtcAnswer,
            (sender, target, payload) => new CallAnswerDto(sender.ConnectionId, sender.UserId, target.ConnectionId, payload));

    public Task SendIceCandidate(string? roomId, string? targetConnectionId, object? candidate) =>
        RelaySignalAsync(roomId, targetConnectionId, candidate, CallRealtimeEvents.IceCandidate,
            (sender, target, payload) => new CallIceCandidateDto(sender.ConnectionId, sender.UserId, target.ConnectionId, payload));

    public async Task PublishParticipantMediaState(string? roomId, CallParticipantMediaStateDto? state)
    {
        if (state == null) throw new HubException("INVALID_MEDIA_STATE");
        var normalizedRoomId = NormalizeRoomId(roomId);
        if (!_rooms.TryUpdateMediaState(normalizedRoomId, Context.ConnectionId, state, out var participant))
            throw new HubException("NOT_IN_CALL_ROOM");
        await Clients.OthersInGroup(normalizedRoomId).SendAsync(
            CallRealtimeEvents.ParticipantMediaStateChanged,
            new CallParticipantMediaStateChangedDto(participant.ConnectionId, participant.UserId, state),
            Context.ConnectionAborted);
    }

    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        foreach (var participant in _rooms.RemoveConnection(Context.ConnectionId))
        {
            await Clients.OthersInGroup(participant.RoomId).SendAsync(
                CallRealtimeEvents.ParticipantLeft,
                new CallParticipantLeftDto(participant.ConnectionId, participant.UserId));
        }
        await base.OnDisconnectedAsync(exception);
    }

    private async Task RelaySignalAsync(
        string? roomId,
        string? targetConnectionId,
        object? payload,
        string eventName,
        Func<CallRoomParticipant, CallRoomParticipant, object, object> envelope)
    {
        if (payload == null) throw new HubException("INVALID_SIGNAL");
        var normalizedRoomId = NormalizeRoomId(roomId);
        if (string.IsNullOrWhiteSpace(targetConnectionId) ||
            !_rooms.TryGetParticipant(normalizedRoomId, Context.ConnectionId, out var sender) ||
            !_rooms.TryGetParticipant(normalizedRoomId, targetConnectionId, out var target))
            throw new HubException("NOT_IN_CALL_ROOM");

        await Clients.Client(target.ConnectionId).SendAsync(
            eventName, envelope(sender, target, payload), Context.ConnectionAborted);
    }

    private async Task LeaveRoomAsync(string roomId, CancellationToken cancellationToken)
    {
        var participant = _rooms.Leave(roomId, Context.ConnectionId);
        if (participant == null) return;
        await Groups.RemoveFromGroupAsync(Context.ConnectionId, roomId, cancellationToken);
        await Clients.OthersInGroup(roomId).SendAsync(
            CallRealtimeEvents.ParticipantLeft,
            new CallParticipantLeftDto(participant.ConnectionId, participant.UserId), cancellationToken);
    }

    private Guid GetCurrentUserId() =>
        Guid.TryParse(Context.User?.FindFirstValue(ClaimTypes.NameIdentifier), out var userId)
            ? userId
            : throw new HubException("AUTH_REQUIRED");

    private static Guid ParseGuid(string? value, string code) =>
        Guid.TryParse(value, out var parsed) && parsed != Guid.Empty
            ? parsed
            : throw new HubException(code);

    private static string BuildRoomId(Guid projectId, string? voiceChannelId)
    {
        if (string.IsNullOrWhiteSpace(voiceChannelId) || voiceChannelId.Length > 200)
            throw new HubException("INVALID_VOICE_ROOM");
        return $"project:{projectId:N}:voice:{voiceChannelId.Trim()}";
    }

    private static string NormalizeRoomId(string? roomId) =>
        !string.IsNullOrWhiteSpace(roomId) && roomId.Length <= 300
            ? roomId
            : throw new HubException("INVALID_VOICE_ROOM");
}
