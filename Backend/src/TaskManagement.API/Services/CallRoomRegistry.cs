using TaskManagement.Application.DTOs.Collaboration;
using TaskManagement.Application.Interfaces;

namespace TaskManagement.API.Services;

public sealed class CallRoomRegistry : ICallRoomRegistry
{
    private readonly object _gate = new();
    private readonly Dictionary<string, RoomState> _rooms = new(StringComparer.Ordinal);

    public CallRoomJoinResult Join(CallRoomParticipant participant)
    {
        lock (_gate)
        {
            if (!_rooms.TryGetValue(participant.RoomId, out var room))
            {
                room = new RoomState(Guid.NewGuid());
                _rooms[participant.RoomId] = room;
            }

            var existingConnections = room.Participants.Values
                .Where(item => item.UserId == participant.UserId)
                .ToArray();
            var isNewUser = existingConnections.Length == 0;
            if (isNewUser && room.Participants.Values.Select(item => item.UserId).Distinct().Count() >= ICallRoomRegistry.MaximumParticipants)
                return new(false, true, Snapshot(participant.RoomId, room), null, []);

            var previousRole = existingConnections.FirstOrDefault()?.Role;
            var replacedConnections = existingConnections
                .Where(existing => existing.ConnectionId != participant.ConnectionId)
                .ToArray();
            var previousConsent = existingConnections
                .Select(existing => room.Consents.TryGetValue(existing.ConnectionId, out var consent) ? consent : null)
                .FirstOrDefault(consent => consent is not null);
            foreach (var existing in existingConnections)
            {
                room.Participants.Remove(existing.ConnectionId);
                room.Consents.Remove(existing.ConnectionId);
            }

            var joinedParticipant = participant with
            {
                Role = previousRole ?? (room.Participants.Count == 0 ? "host" : participant.Role)
            };
            room.Participants[joinedParticipant.ConnectionId] = joinedParticipant;
            if (room.AiState == CallAiStates.Active && isNewUser)
            {
                room.ConsentGeneration++;
                room.AiState = CallAiStates.PausedConsent;
                room.Consents[joinedParticipant.ConnectionId] = new ConsentEntry(CallConsentStatuses.Pending, null);
            }
            else if (room.AiState == CallAiStates.WaitingForConsent && isNewUser)
            {
                room.Consents[joinedParticipant.ConnectionId] = new ConsentEntry(CallConsentStatuses.Pending, null);
            }
            else if (!isNewUser && previousConsent is not null)
            {
                room.Consents[joinedParticipant.ConnectionId] = previousConsent;
            }

            return new(
                true,
                false,
                Snapshot(participant.RoomId, room),
                ICallRoomRegistry.ToDto(joinedParticipant),
                replacedConnections.Select(ICallRoomRegistry.ToDto).ToArray());
        }
    }

    public CallRoomParticipant? Leave(string roomId, string connectionId)
    {
        lock (_gate)
        {
            if (!_rooms.TryGetValue(roomId, out var room) || !room.Participants.Remove(connectionId, out var participant)) return null;
            room.Consents.Remove(connectionId);
            ResumeIfAllRemainingParticipantsConsented(room);
            if (room.Participants.Count == 0) _rooms.Remove(roomId);
            return participant;
        }
    }

    public IReadOnlyList<CallRoomParticipant> RemoveConnection(string connectionId)
    {
        lock (_gate)
        {
            var removed = new List<CallRoomParticipant>();
            foreach (var (roomId, room) in _rooms.ToArray())
            {
                if (!room.Participants.Remove(connectionId, out var participant)) continue;
                room.Consents.Remove(connectionId);
                ResumeIfAllRemainingParticipantsConsented(room);
                removed.Add(participant);
                if (room.Participants.Count == 0) _rooms.Remove(roomId);
            }
            return removed;
        }
    }

    public bool TryGetParticipant(string roomId, string connectionId, out CallRoomParticipant participant)
    {
        lock (_gate)
        {
            if (_rooms.TryGetValue(roomId, out var room) && room.Participants.TryGetValue(connectionId, out participant!)) return true;
            participant = null!;
            return false;
        }
    }

    public bool TryUpdateMediaState(string roomId, string connectionId, CallParticipantMediaStateDto state, out CallRoomParticipant participant)
    {
        lock (_gate)
        {
            if (!_rooms.TryGetValue(roomId, out var room) || !room.Participants.TryGetValue(connectionId, out var current))
            {
                participant = null!;
                return false;
            }
            participant = current with
            {
                MicrophoneEnabled = state.MicrophoneEnabled,
                CameraEnabled = state.CameraEnabled,
                ScreenSharing = state.ScreenSharing
            };
            room.Participants[connectionId] = participant;
            return true;
        }
    }

    public bool IsParticipantInRoom(string roomId, string connectionId)
    {
        lock (_gate) return _rooms.TryGetValue(roomId, out var room) && room.Participants.ContainsKey(connectionId);
    }

    public bool TryUpdateHand(string roomId, string connectionId, bool raised, out CallRoomParticipant participant) =>
        TryUpdateParticipant(roomId, connectionId, current => current with { HandRaised = raised }, out participant);

    public bool TryUpdateSpeaking(string roomId, string connectionId, bool speaking, out CallRoomParticipant participant) =>
        TryUpdateParticipant(roomId, connectionId, current => current with { IsSpeaking = speaking }, out participant);

    public bool IsHostOrCoHost(string roomId, string connectionId)
    {
        lock (_gate) return _rooms.TryGetValue(roomId, out var room) && room.Participants.TryGetValue(connectionId, out var participant) && participant.Role is "host" or "cohost";
    }

    private bool TryUpdateParticipant(string roomId, string connectionId, Func<CallRoomParticipant, CallRoomParticipant> update, out CallRoomParticipant participant)
    {
        lock (_gate)
        {
            if (!_rooms.TryGetValue(roomId, out var room) || !room.Participants.TryGetValue(connectionId, out var current)) { participant = null!; return false; }
            participant = update(current);
            room.Participants[connectionId] = participant;
            return true;
        }
    }

    public IReadOnlyList<CallRoomParticipant> GetRoomParticipants(string roomId)
    {
        lock (_gate) return _rooms.TryGetValue(roomId, out var room) ? room.Participants.Values.ToArray() : [];
    }

    public CallAiSessionSnapshot GetAiState(string roomId)
    {
        lock (_gate)
        {
            return _rooms.TryGetValue(roomId, out var room) ? ToAiSnapshot(room) : EmptyAiSnapshot();
        }
    }

    public CallAiSessionSnapshot RequestAiTranscription(string roomId, string connectionId)
    {
        lock (_gate)
        {
            var room = RequireParticipant(roomId, connectionId);
            room.ConsentGeneration++;
            room.AiState = CallAiStates.WaitingForConsent;
            room.Consents.Clear();
            foreach (var participant in room.Participants.Keys)
                room.Consents[participant] = new ConsentEntry(CallConsentStatuses.Pending, null);
            return ToAiSnapshot(room);
        }
    }

    public CallAiSessionSnapshot RespondToAiConsent(
        string roomId,
        string connectionId,
        Guid callSessionId,
        long consentGeneration,
        bool accepted)
    {
        lock (_gate)
        {
            var room = RequireParticipant(roomId, connectionId);
            ValidateGeneration(room, callSessionId, consentGeneration);
            if (!room.Consents.ContainsKey(connectionId) || room.AiState == CallAiStates.Off)
                throw new InvalidOperationException("AI_CONSENT_NOT_REQUESTED");

            room.Consents[connectionId] = new ConsentEntry(
                accepted ? CallConsentStatuses.Accepted : CallConsentStatuses.Declined,
                DateTimeOffset.UtcNow);
            if (!accepted)
            {
                room.AiState = CallAiStates.Off;
            }
            else if (room.Consents.Values.All(item => item.Status == CallConsentStatuses.Accepted))
            {
                room.AiState = CallAiStates.Active;
            }
            else
            {
                room.AiState = CallAiStates.WaitingForConsent;
            }

            return ToAiSnapshot(room);
        }
    }

    public CallAiSessionSnapshot StopAiTranscription(string roomId, string connectionId)
    {
        lock (_gate)
        {
            var room = RequireParticipant(roomId, connectionId);
            room.AiState = CallAiStates.Off;
            room.Consents.Clear();
            return ToAiSnapshot(room);
        }
    }

    public bool TryAuthorizeTranscription(
        string roomId,
        string connectionId,
        Guid callSessionId,
        long consentGeneration,
        out CallTranscriptionParticipant participant)
    {
        lock (_gate)
        {
            participant = null!;
            if (!_rooms.TryGetValue(roomId, out var room) || room.AiState != CallAiStates.Active ||
                room.CallSessionId != callSessionId || room.ConsentGeneration != consentGeneration ||
                !room.Participants.TryGetValue(connectionId, out var current) ||
                !room.Consents.TryGetValue(connectionId, out var consent) ||
                consent.Status != CallConsentStatuses.Accepted)
                return false;

            participant = new CallTranscriptionParticipant(
                room.CallSessionId, room.ConsentGeneration, current.UserId, current.DisplayName);
            return true;
        }
    }

    private RoomState RequireParticipant(string roomId, string connectionId)
    {
        if (!_rooms.TryGetValue(roomId, out var room) || !room.Participants.ContainsKey(connectionId))
            throw new InvalidOperationException("NOT_IN_CALL_ROOM");
        return room;
    }

    private static void ValidateGeneration(RoomState room, Guid callSessionId, long consentGeneration)
    {
        if (room.CallSessionId != callSessionId || room.ConsentGeneration != consentGeneration)
            throw new InvalidOperationException("STALE_AI_CONSENT");
    }

    private static void ResumeIfAllRemainingParticipantsConsented(RoomState room)
    {
        if ((room.AiState == CallAiStates.PausedConsent || room.AiState == CallAiStates.WaitingForConsent) &&
            room.Participants.Count > 0 && room.Participants.Keys.All(connectionId =>
                room.Consents.TryGetValue(connectionId, out var consent) && consent.Status == CallConsentStatuses.Accepted))
            room.AiState = CallAiStates.Active;
    }

    private static CallRoomSnapshotDto Snapshot(string roomId, RoomState room) => new(
        roomId,
        room.Participants.Values.Select(ICallRoomRegistry.ToDto).ToArray(),
        ICallRoomRegistry.MaximumParticipants,
        ToAiDto(ToAiSnapshot(room)));

    private static CallAiStateDto ToAiDto(CallAiSessionSnapshot state) => new(
        state.State,
        state.CallSessionId,
        state.ConsentGeneration,
        state.Participants);

    private static CallAiSessionSnapshot ToAiSnapshot(RoomState room) => new(
        room.AiState,
        room.CallSessionId,
        room.ConsentGeneration,
        room.Participants.Values.Select(participant =>
        {
            var consent = room.Consents.TryGetValue(participant.ConnectionId, out var current)
                ? current
                : new ConsentEntry(CallConsentStatuses.Pending, null);
            return new CallAiParticipantConsentDto(participant.UserId, participant.DisplayName, consent.Status, consent.RespondedAt);
        }).ToArray());

    private static CallAiSessionSnapshot EmptyAiSnapshot() => new(
        CallAiStates.Off, Guid.Empty, 0, []);

    private sealed class RoomState
    {
        public RoomState(Guid callSessionId) => CallSessionId = callSessionId;

        public Guid CallSessionId { get; }
        public long ConsentGeneration { get; set; }
        public string AiState { get; set; } = CallAiStates.Off;
        public Dictionary<string, CallRoomParticipant> Participants { get; } = new(StringComparer.Ordinal);
        public Dictionary<string, ConsentEntry> Consents { get; } = new(StringComparer.Ordinal);
    }

    private sealed record ConsentEntry(string Status, DateTimeOffset? RespondedAt);
}
