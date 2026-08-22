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
    private readonly ICallTranscriptionProvider? _transcriptionProvider;
    private readonly ICallTranscriptService? _transcripts;

    public CallHub(ICallRoomRegistry rooms, ICallRoomAuthorizationService authorization)
        : this(rooms, authorization, null, null)
    {
    }

    public CallHub(
        ICallRoomRegistry rooms,
        ICallRoomAuthorizationService authorization,
        ICallTranscriptionProvider? transcriptionProvider,
        ICallTranscriptService? transcripts)
    {
        _rooms = rooms;
        _authorization = authorization;
        _transcriptionProvider = transcriptionProvider;
        _transcripts = transcripts;
    }

    public async Task<CallRoomSnapshotDto> JoinVoiceRoom(string? projectId, string? voiceChannelId)
    {
        var userId = GetCurrentUserId();
        var parsedProjectId = ParseGuid(projectId, "INVALID_PROJECT_ID");
        var roomId = BuildRoomId(parsedProjectId, voiceChannelId);
        await _authorization.AuthorizeVoiceRoomJoinAsync(parsedProjectId, userId, Context.ConnectionAborted);
        var profile = await _authorization.GetParticipantProfileAsync(userId, Context.ConnectionAborted);
        var previousAiState = _rooms.GetAiState(roomId);
        var result = _rooms.Join(new CallRoomParticipant(
            roomId, Context.ConnectionId, userId, profile.DisplayName, profile.AvatarUrl, true, false, false));
        if (result.RoomFull) throw new HubException("CALL_ROOM_FULL");

        await Groups.AddToGroupAsync(Context.ConnectionId, roomId, Context.ConnectionAborted);
        await Clients.OthersInGroup(roomId).SendAsync(
            CallRealtimeEvents.ParticipantJoined,
            new CallParticipantJoinedDto(result.JoinedParticipant!),
            Context.ConnectionAborted);
        var nextAiState = result.Snapshot.AiState;
        if (previousAiState.State == CallAiStates.Active && nextAiState.State == CallAiStates.PausedConsent)
        {
            await Clients.Group(roomId).SendAsync(
                CallRealtimeEvents.AiTranscriptionPaused,
                new CallAiStateChangedDto(nextAiState),
                Context.ConnectionAborted);
        }
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

    public async Task RequestAiTranscription(string? roomId)
    {
        var normalizedRoomId = NormalizeRoomId(roomId);
        var state = _rooms.RequestAiTranscription(normalizedRoomId, Context.ConnectionId);
        var dto = new CallAiStateChangedDto(ToDto(state));
        await Clients.Group(normalizedRoomId).SendAsync(CallRealtimeEvents.AiConsentRequested, dto, Context.ConnectionAborted);
        await Clients.Group(normalizedRoomId).SendAsync(CallRealtimeEvents.CallAiStateChanged, dto, Context.ConnectionAborted);
    }

    public async Task RespondToAiConsent(
        string? roomId,
        string? callSessionId,
        long consentGeneration,
        bool accepted)
    {
        var normalizedRoomId = NormalizeRoomId(roomId);
        var sessionId = ParseGuid(callSessionId, "INVALID_CALL_SESSION_ID");
        var state = _rooms.RespondToAiConsent(
            normalizedRoomId, Context.ConnectionId, sessionId, consentGeneration, accepted);
        var dto = new CallAiStateChangedDto(ToDto(state));
        await Clients.Group(normalizedRoomId).SendAsync(
            accepted ? CallRealtimeEvents.AiParticipantAccepted : CallRealtimeEvents.AiParticipantDeclined,
            dto,
            Context.ConnectionAborted);
        await Clients.Group(normalizedRoomId).SendAsync(CallRealtimeEvents.CallAiStateChanged, dto, Context.ConnectionAborted);
        if (state.State == CallAiStates.Active)
            await Clients.Group(normalizedRoomId).SendAsync(CallRealtimeEvents.AiTranscriptionStarted, dto, Context.ConnectionAborted);
        else if (!accepted)
            await Clients.Group(normalizedRoomId).SendAsync(CallRealtimeEvents.AiTranscriptionStopped, dto, Context.ConnectionAborted);
    }

    public async Task StopAiTranscription(string? roomId)
    {
        var normalizedRoomId = NormalizeRoomId(roomId);
        var state = _rooms.StopAiTranscription(normalizedRoomId, Context.ConnectionId);
        var dto = new CallAiStateChangedDto(ToDto(state));
        await Clients.Group(normalizedRoomId).SendAsync(CallRealtimeEvents.AiTranscriptionStopped, dto, Context.ConnectionAborted);
        await Clients.Group(normalizedRoomId).SendAsync(CallRealtimeEvents.CallAiStateChanged, dto, Context.ConnectionAborted);
    }

    public async Task SubmitCallAudioChunk(
        string? roomId,
        string? callSessionId,
        long consentGeneration,
        string? mimeType,
        byte[]? audioBytes,
        DateTimeOffset startedAt,
        DateTimeOffset endedAt)
    {
        var normalizedRoomId = NormalizeRoomId(roomId);
        var sessionId = ParseGuid(callSessionId, "INVALID_CALL_SESSION_ID");
        if (audioBytes is null || audioBytes.Length == 0 || audioBytes.Length > 256 * 1024)
            throw new HubException("INVALID_AUDIO_CHUNK");
        if (string.IsNullOrWhiteSpace(mimeType) || !mimeType.StartsWith("audio/", StringComparison.OrdinalIgnoreCase))
            throw new HubException("INVALID_AUDIO_MIME_TYPE");
        if (endedAt < startedAt || endedAt - startedAt > TimeSpan.FromSeconds(15))
            throw new HubException("INVALID_AUDIO_TIMESTAMP");
        if (!_rooms.TryAuthorizeTranscription(normalizedRoomId, Context.ConnectionId, sessionId, consentGeneration, out var participant))
            throw new HubException("AI_TRANSCRIPTION_NOT_ACTIVE");
        if (_transcriptionProvider is null || _transcripts is null)
            throw new HubException("CALL_TRANSCRIPTION_NOT_CONFIGURED");

        var source = new CallAudioChunk(
            participant.CallSessionId,
            normalizedRoomId,
            participant.UserId,
            participant.DisplayName,
            mimeType.Trim(),
            audioBytes,
            startedAt,
            endedAt,
            participant.ConsentGeneration);
        try
        {
            var result = await _transcriptionProvider.TranscribeAsync(source, Context.ConnectionAborted);
            if (result is null || string.IsNullOrWhiteSpace(result.Text)) return;
            if (!_rooms.TryAuthorizeTranscription(normalizedRoomId, Context.ConnectionId, sessionId, consentGeneration, out _)) return;

            var transcript = await _transcripts.AppendAsync(source, result, Context.ConnectionAborted);
            if (transcript is not null)
            {
                await Clients.Group(normalizedRoomId).SendAsync(
                    CallRealtimeEvents.CallTranscriptChunkAdded,
                    transcript,
                    Context.ConnectionAborted);
            }
        }
        finally
        {
            Array.Clear(source.AudioBytes, 0, source.AudioBytes.Length);
        }
    }

    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        foreach (var participant in _rooms.RemoveConnection(Context.ConnectionId))
        {
            var nextAiState = _rooms.GetAiState(participant.RoomId);
            await Clients.OthersInGroup(participant.RoomId).SendAsync(
                CallRealtimeEvents.ParticipantLeft,
                new CallParticipantLeftDto(participant.ConnectionId, participant.UserId));
            await PublishAiStateTransitionAsync(participant.RoomId, nextAiState, Context.ConnectionAborted);
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
        var previousAiState = _rooms.GetAiState(roomId);
        var participant = _rooms.Leave(roomId, Context.ConnectionId);
        if (participant == null) return;
        await Groups.RemoveFromGroupAsync(Context.ConnectionId, roomId, cancellationToken);
        await Clients.OthersInGroup(roomId).SendAsync(
            CallRealtimeEvents.ParticipantLeft,
            new CallParticipantLeftDto(participant.ConnectionId, participant.UserId), cancellationToken);
        await PublishAiStateTransitionAsync(roomId, previousAiState, cancellationToken);
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

    private static CallAiStateDto ToDto(CallAiSessionSnapshot state) => new(
        state.State,
        state.CallSessionId,
        state.ConsentGeneration,
        state.Participants);

    private async Task PublishAiStateTransitionAsync(
        string roomId,
        CallAiSessionSnapshot nextState,
        CancellationToken cancellationToken)
    {
        var dto = new CallAiStateChangedDto(ToDto(nextState));
        if (nextState.State == CallAiStates.Active)
            await Clients.Group(roomId).SendAsync(CallRealtimeEvents.AiTranscriptionStarted, dto, cancellationToken);
        else if (nextState.State == CallAiStates.PausedConsent)
            await Clients.Group(roomId).SendAsync(CallRealtimeEvents.AiTranscriptionPaused, dto, cancellationToken);
        await Clients.Group(roomId).SendAsync(CallRealtimeEvents.CallAiStateChanged, dto, cancellationToken);
    }
}
