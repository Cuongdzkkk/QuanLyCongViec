using TaskManagement.Application.DTOs.Collaboration;

namespace TaskManagement.Application.Interfaces;

public sealed record CallRoomParticipant(
    string RoomId,
    string ConnectionId,
    Guid UserId,
    string DisplayName,
    string? AvatarUrl,
    bool MicrophoneEnabled,
    bool CameraEnabled,
    bool ScreenSharing);

public sealed record CallRoomJoinResult(
    bool Accepted,
    bool RoomFull,
    CallRoomSnapshotDto Snapshot,
    CallParticipantDto? JoinedParticipant);

public sealed record CallAiSessionSnapshot(
    string State,
    Guid CallSessionId,
    long ConsentGeneration,
    IReadOnlyList<CallAiParticipantConsentDto> Participants);

public sealed record CallTranscriptionParticipant(
    Guid CallSessionId,
    long ConsentGeneration,
    Guid UserId,
    string DisplayName);

public interface ICallRoomRegistry
{
    const int MaximumParticipants = 6;

    CallRoomJoinResult Join(CallRoomParticipant participant);

    CallRoomParticipant? Leave(string roomId, string connectionId);

    IReadOnlyList<CallRoomParticipant> RemoveConnection(string connectionId);

    bool TryGetParticipant(string roomId, string connectionId, out CallRoomParticipant participant);

    bool TryUpdateMediaState(
        string roomId,
        string connectionId,
        CallParticipantMediaStateDto state,
        out CallRoomParticipant participant);

    bool IsParticipantInRoom(string roomId, string connectionId);

    IReadOnlyList<CallRoomParticipant> GetRoomParticipants(string roomId);

    CallAiSessionSnapshot GetAiState(string roomId);

    CallAiSessionSnapshot RequestAiTranscription(string roomId, string connectionId);

    CallAiSessionSnapshot RespondToAiConsent(
        string roomId,
        string connectionId,
        Guid callSessionId,
        long consentGeneration,
        bool accepted);

    CallAiSessionSnapshot StopAiTranscription(string roomId, string connectionId);

    bool TryAuthorizeTranscription(
        string roomId,
        string connectionId,
        Guid callSessionId,
        long consentGeneration,
        out CallTranscriptionParticipant participant);

    static CallParticipantDto ToDto(CallRoomParticipant participant) => new(
        participant.ConnectionId,
        participant.UserId,
        participant.DisplayName,
        participant.AvatarUrl,
        participant.MicrophoneEnabled,
        participant.CameraEnabled,
        participant.ScreenSharing);
}
