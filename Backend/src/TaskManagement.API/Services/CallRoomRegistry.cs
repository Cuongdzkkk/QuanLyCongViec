using TaskManagement.Application.DTOs.Collaboration;
using TaskManagement.Application.Interfaces;

namespace TaskManagement.API.Services;

public sealed class CallRoomRegistry : ICallRoomRegistry
{
    private readonly object _gate = new();
    private readonly Dictionary<string, Dictionary<string, CallRoomParticipant>> _rooms = new(StringComparer.Ordinal);

    public CallRoomJoinResult Join(CallRoomParticipant participant)
    {
        lock (_gate)
        {
            if (!_rooms.TryGetValue(participant.RoomId, out var room))
            {
                room = new Dictionary<string, CallRoomParticipant>(StringComparer.Ordinal);
                _rooms[participant.RoomId] = room;
            }

            var isNewUser = room.Values.All(item => item.UserId != participant.UserId);
            if (isNewUser && room.Values.Select(item => item.UserId).Distinct().Count() >= ICallRoomRegistry.MaximumParticipants)
                return new(false, true, Snapshot(participant.RoomId, room.Values), null);

            room[participant.ConnectionId] = participant;
            return new(true, false, Snapshot(participant.RoomId, room.Values), ICallRoomRegistry.ToDto(participant));
        }
    }

    public CallRoomParticipant? Leave(string roomId, string connectionId)
    {
        lock (_gate)
        {
            if (!_rooms.TryGetValue(roomId, out var room) || !room.Remove(connectionId, out var participant)) return null;
            if (room.Count == 0) _rooms.Remove(roomId);
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
                if (!room.Remove(connectionId, out var participant)) continue;
                removed.Add(participant);
                if (room.Count == 0) _rooms.Remove(roomId);
            }
            return removed;
        }
    }

    public bool TryGetParticipant(string roomId, string connectionId, out CallRoomParticipant participant)
    {
        lock (_gate)
        {
            if (_rooms.TryGetValue(roomId, out var room) && room.TryGetValue(connectionId, out participant!)) return true;
            participant = null!;
            return false;
        }
    }

    public bool TryUpdateMediaState(string roomId, string connectionId, CallParticipantMediaStateDto state, out CallRoomParticipant participant)
    {
        lock (_gate)
        {
            if (!_rooms.TryGetValue(roomId, out var room) || !room.TryGetValue(connectionId, out var current))
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
            room[connectionId] = participant;
            return true;
        }
    }

    public bool IsParticipantInRoom(string roomId, string connectionId)
    {
        lock (_gate) return _rooms.TryGetValue(roomId, out var room) && room.ContainsKey(connectionId);
    }

    public IReadOnlyList<CallRoomParticipant> GetRoomParticipants(string roomId)
    {
        lock (_gate) return _rooms.TryGetValue(roomId, out var room) ? room.Values.ToArray() : [];
    }

    private static CallRoomSnapshotDto Snapshot(string roomId, IEnumerable<CallRoomParticipant> participants) => new(
        roomId, participants.Select(ICallRoomRegistry.ToDto).ToArray(), ICallRoomRegistry.MaximumParticipants);
}
