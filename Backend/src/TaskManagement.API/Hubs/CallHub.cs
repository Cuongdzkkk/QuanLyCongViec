using System.Collections.Concurrent;
using System.Security.Claims;
using System.Text.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Logging.Abstractions;
using TaskManagement.Application.DTOs.Collaboration;
using TaskManagement.Application.Diagnostics;
using TaskManagement.Application.Interfaces;
using TaskManagement.API.Services;

namespace TaskManagement.API.Hubs;

[Authorize]
public sealed class CallHub : Hub
{
    public const string Route = "/hubs/call";
    public const int MaximumReceiveMessageSize = 128 * 1024;
    public const int MaximumSdpUtf8Bytes = 96 * 1024;
    private const string CaptionTransportCountersKey = "CallHub.CaptionTransportCounters";

    private readonly ICallRoomRegistry _rooms;
    private readonly ICallRoomAuthorizationService _authorization;
    private readonly ICallTranscriptionProvider _transcriptionProvider;
    private readonly ICallChatService _callChat;
    private readonly ICallCaptionResultDispatcher _captionResults;
    private readonly IMeetingAiAnalysisService? _meetingAi;
    private readonly ILogger<CallHub> _logger;

    public CallHub(
        ICallRoomRegistry rooms,
        ICallRoomAuthorizationService authorization,
        ICallTranscriptionProvider transcriptionProvider,
        ICallChatService callChat,
        ICallCaptionResultDispatcher captionResults,
        ILogger<CallHub>? logger = null,
        IMeetingAiAnalysisService? meetingAi = null)
    {
        _rooms = rooms;
        _authorization = authorization;
        _transcriptionProvider = transcriptionProvider;
        _callChat = callChat;
        _captionResults = captionResults;
        _meetingAi = meetingAi;
        _logger = logger ?? NullLogger<CallHub>.Instance;
    }

    public async Task<CallRoomSnapshotDto> JoinVoiceRoom(string? projectId, string? voiceChannelId)
    {
        Trace("JOIN_BEGIN", nameof(JoinVoiceRoom), roomId: voiceChannelId);
        var userId = GetCurrentUserId();
        var parsedProjectId = ParseGuid(projectId, "INVALID_PROJECT_ID");
        var roomId = BuildRoomId(parsedProjectId, voiceChannelId);
        await _authorization.AuthorizeVoiceRoomJoinAsync(parsedProjectId, userId, Context.ConnectionAborted);
        var profile = await _authorization.GetParticipantProfileAsync(userId, Context.ConnectionAborted);
        var previousAiState = _rooms.GetAiState(roomId);
        var isExistingConnection = _rooms.IsParticipantInRoom(roomId, Context.ConnectionId);
        var result = _rooms.Join(new CallRoomParticipant(
            roomId, Context.ConnectionId, userId, profile.DisplayName, profile.AvatarUrl, true, false, false));
        if (result.RoomFull) throw new HubException("CALL_ROOM_FULL");

        await Groups.AddToGroupAsync(Context.ConnectionId, roomId, Context.ConnectionAborted);
        foreach (var replaced in result.ReplacedParticipants)
        {
            await Groups.RemoveFromGroupAsync(replaced.ConnectionId, roomId, Context.ConnectionAborted);
            await Clients.Group(roomId).SendAsync(
                CallRealtimeEvents.ParticipantLeft,
                new CallParticipantLeftDto(replaced.ConnectionId, replaced.UserId),
                Context.ConnectionAborted);
        }
        if (!isExistingConnection)
        {
            await Clients.OthersInGroup(roomId).SendAsync(
                CallRealtimeEvents.ParticipantJoined,
                new CallParticipantJoinedDto(result.JoinedParticipant!),
                Context.ConnectionAborted);
        }
        var nextAiState = result.Snapshot.AiState;
        if (previousAiState.State == CallAiStates.Active && nextAiState.State == CallAiStates.PausedConsent)
        {
            await StopRoomTranscriptionAsync(roomId, Context.ConnectionAborted);
            await Clients.Group(roomId).SendAsync(
                CallRealtimeEvents.AiTranscriptionPaused,
                new CallAiStateChangedDto(nextAiState),
                Context.ConnectionAborted);
        }
        Trace("JOIN_ACK", nameof(JoinVoiceRoom), roomId, result.Snapshot.AiState.CallSessionId, result.Snapshot.Participants.Count, "ok");
        return result.Snapshot with
        {
            Transcription = new CallTranscriptionCapabilitiesDto(
                _transcriptionProvider.IsConfigured,
                _transcriptionProvider.ProviderName,
                _transcriptionProvider.SupportedLanguages,
                _transcriptionProvider.DefaultLanguage,
                _meetingAi?.IsConfigured == true,
                _meetingAi?.ProviderName ?? "Unavailable",
                _meetingAi?.TranscriptChunkSize ?? 8)
        };
    }

    public async Task LeaveVoiceRoom(string? projectId, string? voiceChannelId)
    {
        var roomId = BuildRoomId(ParseGuid(projectId, "INVALID_PROJECT_ID"), voiceChannelId);
        await LeaveRoomAsync(roomId, Context.ConnectionAborted);
    }

    public Task SendWebRtcOffer(string? roomId, string? targetConnectionId, object? description) =>
        RelaySignalAsync(roomId, targetConnectionId, description, CallRealtimeEvents.WebRtcOffer,
            (sender, target, payload) => new CallOfferDto(sender.ConnectionId, sender.UserId, target.ConnectionId, payload), nameof(SendWebRtcOffer));

    public Task SendWebRtcAnswer(string? roomId, string? targetConnectionId, object? description) =>
        RelaySignalAsync(roomId, targetConnectionId, description, CallRealtimeEvents.WebRtcAnswer,
            (sender, target, payload) => new CallAnswerDto(sender.ConnectionId, sender.UserId, target.ConnectionId, payload), nameof(SendWebRtcAnswer));

    public Task SendIceCandidate(string? roomId, string? targetConnectionId, object? candidate) =>
        RelaySignalAsync(roomId, targetConnectionId, candidate, CallRealtimeEvents.IceCandidate,
            (sender, target, payload) => new CallIceCandidateDto(sender.ConnectionId, sender.UserId, target.ConnectionId, payload), nameof(SendIceCandidate));

    public async Task<IReadOnlyList<CallChatMessageDto>> GetCallChatHistory(string? roomId, int limit = 100)
    {
        var normalizedRoomId = NormalizeRoomId(roomId);
        if (!_rooms.TryGetCallSessionId(normalizedRoomId, Context.ConnectionId, out var callSessionId))
            throw new HubException("NOT_IN_CALL_ROOM");
        return await _callChat.GetHistoryAsync(normalizedRoomId, callSessionId, limit, Context.ConnectionAborted);
    }

    public async Task SendCallMessage(string? roomId, string? content, string? clientMessageId = null)
    {
        var normalizedRoomId = NormalizeRoomId(roomId);
        if (!_rooms.TryGetParticipant(normalizedRoomId, Context.ConnectionId, out var participant) ||
            !_rooms.TryGetCallSessionId(normalizedRoomId, Context.ConnectionId, out var callSessionId))
            throw new HubException("NOT_IN_CALL_ROOM");
        if (string.IsNullOrWhiteSpace(content) || content.Trim().Length > 4000)
            throw new HubException("INVALID_CALL_MESSAGE");

        var message = await _callChat.CreateAsync(
            normalizedRoomId,
            callSessionId,
            participant.UserId,
            participant.DisplayName,
            content,
            clientMessageId,
            Context.ConnectionAborted);
        await Clients.Group(normalizedRoomId).SendAsync(
            CallRealtimeEvents.CallMessageCreated,
            message,
            Context.ConnectionAborted);
    }

    public async Task PublishParticipantMediaState(string? roomId, CallParticipantMediaStateDto? state)
    {
        Trace("MEDIA_STATE_BEGIN", nameof(PublishParticipantMediaState), roomId, payloadSize: EstimatePayloadBytes(state));
        if (state == null) throw new HubException("INVALID_MEDIA_STATE");
        var normalizedRoomId = NormalizeRoomId(roomId);
        if (!_rooms.TryUpdateMediaState(normalizedRoomId, Context.ConnectionId, state, out var participant))
            throw new HubException("NOT_IN_CALL_ROOM");
        await Clients.OthersInGroup(normalizedRoomId).SendAsync(
            CallRealtimeEvents.ParticipantMediaStateChanged,
            new CallParticipantMediaStateChangedDto(participant.ConnectionId, participant.UserId, state),
            Context.ConnectionAborted);
        Trace("MEDIA_STATE_OK", nameof(PublishParticipantMediaState), normalizedRoomId, result: "ok");
    }

    public async Task SetRaiseHand(string? roomId, bool raised)
    {
        var normalizedRoomId = NormalizeRoomId(roomId);
        if (!_rooms.TryUpdateHand(normalizedRoomId, Context.ConnectionId, raised, out var participant)) throw new HubException("NOT_IN_CALL_ROOM");
        await Clients.Group(normalizedRoomId).SendAsync(CallRealtimeEvents.ParticipantHandChanged,
            new CallParticipantHandChangedDto(participant.ConnectionId, participant.UserId, participant.HandRaised), Context.ConnectionAborted);
    }

    public async Task SendCallReaction(string? roomId, string? emoji)
    {
        var normalizedRoomId = NormalizeRoomId(roomId);
        var allowed = new[] { "👍", "👏", "😂", "❤️", "🎉", "😮" };
        if (string.IsNullOrWhiteSpace(emoji) || !allowed.Contains(emoji)) throw new HubException("INVALID_REACTION");
        if (!_rooms.TryGetParticipant(normalizedRoomId, Context.ConnectionId, out var participant)) throw new HubException("NOT_IN_CALL_ROOM");
        await Clients.Group(normalizedRoomId).SendAsync(CallRealtimeEvents.CallReactionAdded,
            new CallReactionDto(Guid.NewGuid().ToString("N"), participant.ConnectionId, participant.UserId, participant.DisplayName, emoji, DateTimeOffset.UtcNow), Context.ConnectionAborted);
    }

    public async Task PublishSpeakerState(string? roomId, bool speaking)
    {
        var normalizedRoomId = NormalizeRoomId(roomId);
        if (!_rooms.TryUpdateSpeaking(normalizedRoomId, Context.ConnectionId, speaking, out var participant)) throw new HubException("NOT_IN_CALL_ROOM");
        await Clients.OthersInGroup(normalizedRoomId).SendAsync(CallRealtimeEvents.ParticipantSpeakerChanged,
            new CallParticipantSpeakerChangedDto(participant.ConnectionId, participant.UserId, participant.IsSpeaking), Context.ConnectionAborted);
    }

    public async Task MuteParticipant(string? roomId, string? targetConnectionId)
    {
        var normalizedRoomId = NormalizeRoomId(roomId);
        if (!_rooms.IsHostOrCoHost(normalizedRoomId, Context.ConnectionId) || string.IsNullOrWhiteSpace(targetConnectionId) || !_rooms.IsParticipantInRoom(normalizedRoomId, targetConnectionId)) throw new HubException("CALL_HOST_REQUIRED");
        await Clients.Client(targetConnectionId).SendAsync(CallRealtimeEvents.ForceMuteParticipant, Context.ConnectionAborted);
    }

    public async Task LowerParticipantHand(string? roomId, string? targetConnectionId)
    {
        var normalizedRoomId = NormalizeRoomId(roomId);
        if (!_rooms.IsHostOrCoHost(normalizedRoomId, Context.ConnectionId) || string.IsNullOrWhiteSpace(targetConnectionId) || !_rooms.TryUpdateHand(normalizedRoomId, targetConnectionId, false, out var target)) throw new HubException("CALL_HOST_REQUIRED");
        await Clients.Group(normalizedRoomId).SendAsync(CallRealtimeEvents.ParticipantHandChanged, new CallParticipantHandChangedDto(target.ConnectionId, target.UserId, false), Context.ConnectionAborted);
    }

    public async Task RemoveParticipant(string? roomId, string? targetConnectionId)
    {
        var normalizedRoomId = NormalizeRoomId(roomId);
        if (!_rooms.IsHostOrCoHost(normalizedRoomId, Context.ConnectionId) || string.IsNullOrWhiteSpace(targetConnectionId) || !_rooms.IsParticipantInRoom(normalizedRoomId, targetConnectionId)) throw new HubException("CALL_HOST_REQUIRED");
        await Clients.Client(targetConnectionId).SendAsync(CallRealtimeEvents.ForceRemovedFromCall, Context.ConnectionAborted);
    }

    public async Task RequestAiTranscription(string? roomId)
    {
        var normalizedRoomId = NormalizeRoomId(roomId);
        if (_transcriptionProvider?.IsConfigured != true)
        {
            await Clients.Caller.SendAsync(
                CallRealtimeEvents.AiTranscriptionUnavailable,
                new CallTranscriptionErrorDto("BLOCKED_CONFIG", "Live call transcription is not configured."),
                Context.ConnectionAborted);
            return;
        }
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
        await StopRoomTranscriptionAsync(normalizedRoomId, Context.ConnectionAborted);
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
        DateTimeOffset endedAt,
        string? language = null)
    {
        var roomMetadata = DescribeCaptionRoom(roomId);
        var userId = Context.User?.FindFirstValue(ClaimTypes.NameIdentifier) ?? Context.User?.FindFirstValue("sub") ?? "unknown";
        _logger.LogInformation(
            "[CAPTION_SERVER] event=AUDIO_HANDLER_ENTER connectionId={ConnectionId} userId={UserId} callSessionId={CallSessionId} projectId={ProjectId} voiceChannelId={VoiceChannelId} language={Language} payloadBytes={PayloadBytes}",
            Context.ConnectionId,
            userId,
            DescribeCaptionGuid(callSessionId),
            roomMetadata.ProjectId,
            roomMetadata.VoiceChannelId,
            language ?? "",
            audioBytes?.Length ?? 0);

        var normalizedRoomId = NormalizeRoomId(roomId);
        if (!Guid.TryParse(callSessionId, out var sessionId) || sessionId == Guid.Empty)
        {
            LogCaptionReject("invalid_call_session", audioBytes?.Length ?? 0);
            throw new HubException("INVALID_CALL_SESSION_ID");
        }
        if (audioBytes is null || audioBytes.Length == 0)
        {
            LogCaptionReject("invalid_payload", audioBytes?.Length ?? 0);
            throw new HubException("INVALID_AUDIO_CHUNK");
        }
        if (audioBytes.Length > 256 * 1024)
        {
            LogCaptionReject("payload_too_large", audioBytes.Length);
            throw new HubException("INVALID_AUDIO_CHUNK");
        }
        if (string.IsNullOrWhiteSpace(mimeType) || !mimeType.StartsWith("audio/", StringComparison.OrdinalIgnoreCase))
        {
            LogCaptionReject("invalid_format", audioBytes.Length);
            throw new HubException("INVALID_AUDIO_MIME_TYPE");
        }
        if (endedAt < startedAt || endedAt - startedAt > TimeSpan.FromSeconds(15))
        {
            LogCaptionReject("invalid_payload", audioBytes.Length);
            throw new HubException("INVALID_AUDIO_TIMESTAMP");
        }
        var normalizedLanguage = string.IsNullOrWhiteSpace(language)
            ? _transcriptionProvider.DefaultLanguage
            : language.Trim().ToLowerInvariant();
        if (!_transcriptionProvider.SupportedLanguages.Contains(normalizedLanguage, StringComparer.OrdinalIgnoreCase))
        {
            LogCaptionReject("invalid_language", audioBytes.Length);
            throw new HubException("UNSUPPORTED_TRANSCRIPTION_LANGUAGE");
        }
        if (!_rooms.TryAuthorizeTranscription(normalizedRoomId, Context.ConnectionId, sessionId, consentGeneration, out var participant))
        {
            LogCaptionReject("not_joined", audioBytes.Length);
            throw new HubException("AI_TRANSCRIPTION_NOT_ACTIVE");
        }
        if (_transcriptionProvider is null)
        {
            LogCaptionReject("provider_unavailable", audioBytes.Length);
            throw new HubException("CALL_TRANSCRIPTION_NOT_CONFIGURED");
        }

        var chunkIndex = NextCaptionTransportChunkIndex(
            normalizedRoomId,
            participant.CallSessionId,
            participant.UserId,
            participant.ConsentGeneration);
        LogCaptionTransportDiagnostic(
            roomMetadata,
            participant.CallSessionId,
            normalizedLanguage,
            chunkIndex,
            audioBytes);

        var connectionId = Context.ConnectionId;
        var source = new CallAudioChunk(
            participant.CallSessionId,
            normalizedRoomId,
            participant.UserId,
            participant.DisplayName,
            mimeType.Trim(),
            audioBytes,
            startedAt,
            endedAt,
            participant.ConsentGeneration,
            connectionId,
            normalizedLanguage);
        _logger.LogInformation(
            "[CAPTION_SERVER] event=AUDIO_HANDLER_ACCEPT connectionId={ConnectionId} callSessionId={CallSessionId} projectId={ProjectId} voiceChannelId={VoiceChannelId} language={Language} payloadBytes={PayloadBytes}",
            Context.ConnectionId,
            participant.CallSessionId,
            roomMetadata.ProjectId,
            roomMetadata.VoiceChannelId,
            normalizedLanguage,
            audioBytes.Length);
        try
        {
            _logger.LogInformation(
                "[CAPTION_SERVER] event=DEEPGRAM_SEND_BEGIN connectionId={ConnectionId} callSessionId={CallSessionId} payloadBytes={PayloadBytes}",
                Context.ConnectionId,
                participant.CallSessionId,
                audioBytes.Length);
            if (_transcriptionProvider is ICallStreamingTranscriptionProvider streamingProvider)
            {
                var rooms = _rooms;
                await streamingProvider.SubmitAsync(
                    source,
                    _captionResults.DeliverAsync,
                    () => rooms.TryAuthorizeTranscription(normalizedRoomId, connectionId, sessionId, consentGeneration, out _),
                    Context.ConnectionAborted);
            }
            else
            {
                var result = await _transcriptionProvider.TranscribeAsync(source, Context.ConnectionAborted);
                if (result is not null)
                    await _captionResults.DeliverAsync(source, result);
            }
            _logger.LogInformation(
                "[CAPTION_SERVER] event=DEEPGRAM_SEND_OK connectionId={ConnectionId} callSessionId={CallSessionId} payloadBytes={PayloadBytes}",
                Context.ConnectionId,
                participant.CallSessionId,
                audioBytes.Length);
        }
        catch (CallTranscriptionProviderUnavailableException)
        {
            LogCaptionReject("provider_unavailable", audioBytes.Length);
            _logger.LogWarning(
                "[CAPTION_SERVER] event=DEEPGRAM_SEND_FAIL connectionId={ConnectionId} callSessionId={CallSessionId} exceptionType={ExceptionType} safeMessage={SafeMessage}",
                Context.ConnectionId,
                participant.CallSessionId,
                nameof(CallTranscriptionProviderUnavailableException),
                "transcription provider unavailable");
            await NotifyTranscriptionFailureAsync(normalizedRoomId, "BLOCKED_CONFIG", "Live call transcription is not configured.");
        }
        catch (Exception exception) when (!Context.ConnectionAborted.IsCancellationRequested)
        {
            _logger.LogError(
                exception,
                "[CAPTION_SERVER] event=DEEPGRAM_SEND_FAIL connectionId={ConnectionId} callSessionId={CallSessionId} exceptionType={ExceptionType} safeMessage={SafeMessage}",
                Context.ConnectionId,
                participant.CallSessionId,
                exception.GetType().FullName,
                SafeCaptionExceptionMessage(exception));
            await StopRoomTranscriptionAsync(normalizedRoomId, Context.ConnectionAborted);
            await NotifyTranscriptionFailureAsync(normalizedRoomId, "PROVIDER_ERROR", "Không thể ghi biên bản cuộc gọi lúc này.");
        }
        finally
        {
            Array.Clear(source.AudioBytes, 0, source.AudioBytes.Length);
        }
    }

    private void LogCaptionReject(string reason, int payloadBytes) =>
        _logger.LogWarning(
            "[CAPTION_SERVER] event=AUDIO_HANDLER_REJECT reason={Reason} connectionId={ConnectionId} payloadBytes={PayloadBytes}",
            reason,
            Context.ConnectionId,
            payloadBytes);

    private void LogCaptionTransportDiagnostic(
        (string ProjectId, string VoiceChannelId) roomMetadata,
        Guid callSessionId,
        string? language,
        long chunkIndex,
        byte[] audioBytes)
    {
        if (!CaptionTransportDiagnostics.IsSampledChunk(chunkIndex)) return;

        var pcmSha256 = CaptionTransportDiagnostics.ComputeSha256Hex(audioBytes);
        _logger.LogInformation(
            "[CAPTION_TRANSPORT_SERVER_DIAG] event=CAPTION_TRANSPORT_SERVER_DIAG connectionId={ConnectionId} callSessionId={CallSessionId} projectId={ProjectId} voiceChannelId={VoiceChannelId} language={Language} chunkIndex={ChunkIndex} payloadBytes={PayloadBytes} pcmSha256={PcmSha256}",
            Context.ConnectionId,
            callSessionId,
            roomMetadata.ProjectId,
            roomMetadata.VoiceChannelId,
            language ?? "",
            chunkIndex,
            audioBytes.Length,
            pcmSha256);
    }

    private long NextCaptionTransportChunkIndex(
        string roomId,
        Guid callSessionId,
        Guid speakerUserId,
        long consentGeneration)
    {
        var items = Context.Items;
        if (items is null) return 1;
        var key = $"{roomId}\u001f{callSessionId:D}\u001f{speakerUserId:D}\u001f{consentGeneration}";
        if (items.TryGetValue(CaptionTransportCountersKey, out var value)
            && value is ConcurrentDictionary<string, long> counters)
            return counters.AddOrUpdate(key, 1, (_, current) => current + 1);

        var created = new ConcurrentDictionary<string, long>(StringComparer.Ordinal);
        var existing = items[CaptionTransportCountersKey] = created;
        return ((ConcurrentDictionary<string, long>)existing).AddOrUpdate(key, 1, (_, current) => current + 1);
    }

    private static (string ProjectId, string VoiceChannelId) DescribeCaptionRoom(string? roomId)
    {
        if (string.IsNullOrWhiteSpace(roomId)) return ("unknown", "unknown");
        var parts = roomId.Split(':', 4, StringSplitOptions.None);
        if (parts.Length == 4 && string.Equals(parts[0], "project", StringComparison.Ordinal) && string.Equals(parts[2], "voice", StringComparison.Ordinal))
            return (parts[1], parts[3].Length <= 200 ? parts[3] : "too_long");
        return ("unknown", "unknown");
    }

    private static string DescribeCaptionGuid(string? value) =>
        Guid.TryParse(value, out var parsed) && parsed != Guid.Empty ? parsed.ToString("D") : "invalid";

    private static string SafeCaptionExceptionMessage(Exception exception)
    {
        var message = exception.Message.Replace("\r", " ").Replace("\n", " ");
        var accessTokenIndex = message.IndexOf("access_token=", StringComparison.OrdinalIgnoreCase);
        if (accessTokenIndex >= 0)
        {
            var end = message.IndexOfAny(new[] { '&', ' ', '"' }, accessTokenIndex);
            message = message[..accessTokenIndex] + "access_token=[redacted]" + (end >= 0 ? message[end..] : string.Empty);
        }
        return message.Length <= 256 ? message : message[..256];
    }

    public async Task StopCallAudioStream(string? roomId, string? callSessionId, long consentGeneration)
    {
        var normalizedRoomId = NormalizeRoomId(roomId);
        var sessionId = ParseGuid(callSessionId, "INVALID_CALL_SESSION_ID");
        if (!_rooms.TryGetParticipant(normalizedRoomId, Context.ConnectionId, out var participant))
            throw new HubException("NOT_IN_CALL_ROOM");
        var state = _rooms.GetAiState(normalizedRoomId);
        if (state.CallSessionId != sessionId || state.ConsentGeneration != consentGeneration) return;
        if (_transcriptionProvider is ICallStreamingTranscriptionProvider streamingProvider)
            await streamingProvider.StopAsync(normalizedRoomId, sessionId, participant.UserId, consentGeneration, Context.ConnectionAborted);
    }

    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        Trace("DISCONNECTED_BEGIN", nameof(OnDisconnectedAsync), exception: exception);
        try
        {
            foreach (var participant in _rooms.RemoveConnection(Context.ConnectionId))
            {
                var nextAiState = _rooms.GetAiState(participant.RoomId);
                if (_rooms.GetRoomParticipants(participant.RoomId).Count == 0)
                    _meetingAi?.QueueFinalizeRoom(participant.RoomId);
                try
                {
                    await Clients.OthersInGroup(participant.RoomId).SendAsync(
                        CallRealtimeEvents.ParticipantLeft,
                        new CallParticipantLeftDto(participant.ConnectionId, participant.UserId),
                        CancellationToken.None);
                }
                catch (OperationCanceledException)
                {
                    // Teardown notifications are best effort; registry cleanup is authoritative.
                }

                try
                {
                    // The disconnect token is already cancelled by the time this callback runs.
                    // Remaining participants still need the room-state transition, so do not use
                    // the departed connection's cancellation token for this broadcast.
                    await PublishAiStateTransitionAsync(participant.RoomId, nextAiState, CancellationToken.None);
                }
                catch (OperationCanceledException)
                {
                    // SignalR may close the write side while teardown is notifying the room.
                }
            }
            await base.OnDisconnectedAsync(exception);
        }
        finally
        {
            Trace("DISCONNECTED_DONE", nameof(OnDisconnectedAsync), exception: exception);
        }
    }

    private async Task RelaySignalAsync(
        string? roomId,
        string? targetConnectionId,
        object? payload,
        string eventName,
        Func<CallRoomParticipant, CallRoomParticipant, object, object> envelope,
        string methodName)
    {
        Trace("SIGNAL_BEGIN", methodName, roomId, payloadSize: EstimatePayloadBytes(payload));
        if (payload == null) throw new HubException("INVALID_SIGNAL");
        var payloadSize = EstimatePayloadBytes(payload);
        if ((methodName == nameof(SendWebRtcOffer) || methodName == nameof(SendWebRtcAnswer)) &&
            payloadSize > MaximumSdpUtf8Bytes)
            throw new HubException("SIGNAL_DESCRIPTION_TOO_LARGE");
        var normalizedRoomId = NormalizeRoomId(roomId);
        if (string.IsNullOrWhiteSpace(targetConnectionId) ||
            !_rooms.TryGetParticipant(normalizedRoomId, Context.ConnectionId, out var sender) ||
            !_rooms.TryGetParticipant(normalizedRoomId, targetConnectionId, out var target))
            throw new HubException("NOT_IN_CALL_ROOM");

        await Clients.Client(target.ConnectionId).SendAsync(
            eventName, envelope(sender, target, payload), Context.ConnectionAborted);
        Trace("SIGNAL_OK", methodName, normalizedRoomId, _rooms.GetAiState(normalizedRoomId).CallSessionId, result: "ok");
    }

    private void Trace(
        string eventName,
        string method,
        string? roomId = null,
        Guid? callSessionId = null,
        int? participantCount = null,
        string? result = null,
        int? payloadSize = null,
        Exception? exception = null)
    {
        _logger.LogInformation(
            "[CALL_HUB_SERVER] timestamp={Timestamp} event={Event} method={Method} connectionId={ConnectionId} userId={UserId} roomId={RoomId} callSessionId={CallSessionId} participantCount={ParticipantCount} payloadBytes={PayloadBytes} result={Result} exceptionType={ExceptionType}",
            DateTimeOffset.UtcNow,
            eventName,
            method,
            Context.ConnectionId,
            Context.User?.FindFirstValue(ClaimTypes.NameIdentifier),
            roomId,
            callSessionId,
            participantCount,
            payloadSize,
            result,
            exception?.GetType().FullName);
    }

    private static int? EstimatePayloadBytes(object? payload)
    {
        if (payload is null) return null;
        try { return JsonSerializer.SerializeToUtf8Bytes(payload).Length; }
        catch (JsonException) { return null; }
    }

    private async Task LeaveRoomAsync(string roomId, CancellationToken cancellationToken)
    {
        var previousAiState = _rooms.GetAiState(roomId);
        var participant = _rooms.Leave(roomId, Context.ConnectionId);
        if (participant == null) return;
        if (_rooms.GetRoomParticipants(roomId).Count == 0)
            _meetingAi?.QueueFinalize(previousAiState.CallSessionId);
        await StopRoomTranscriptionAsync(roomId, cancellationToken);
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
        if (nextState.State != CallAiStates.Active)
            await StopRoomTranscriptionAsync(roomId, cancellationToken);
        var dto = new CallAiStateChangedDto(ToDto(nextState));
        if (nextState.State == CallAiStates.Active)
            await Clients.Group(roomId).SendAsync(CallRealtimeEvents.AiTranscriptionStarted, dto, cancellationToken);
        else if (nextState.State == CallAiStates.PausedConsent)
            await Clients.Group(roomId).SendAsync(CallRealtimeEvents.AiTranscriptionPaused, dto, cancellationToken);
        await Clients.Group(roomId).SendAsync(CallRealtimeEvents.CallAiStateChanged, dto, cancellationToken);
    }

    private async Task StopRoomTranscriptionAsync(string roomId, CancellationToken cancellationToken)
    {
        if (_transcriptionProvider is ICallStreamingTranscriptionProvider streamingProvider)
            await streamingProvider.StopRoomAsync(roomId, cancellationToken);
    }

    private Task NotifyTranscriptionFailureAsync(string roomId, string code, string message) =>
        Clients.Group(roomId).SendAsync(
            CallRealtimeEvents.AiTranscriptionError,
            new CallTranscriptionErrorDto(code, message),
            Context.ConnectionAborted);
}
