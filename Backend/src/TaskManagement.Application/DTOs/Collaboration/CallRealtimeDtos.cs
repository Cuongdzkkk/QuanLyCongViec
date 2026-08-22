namespace TaskManagement.Application.DTOs.Collaboration;

public static class CallRealtimeEvents
{
    public const string ParticipantJoined = nameof(ParticipantJoined);
    public const string ParticipantLeft = nameof(ParticipantLeft);
    public const string WebRtcOffer = nameof(WebRtcOffer);
    public const string WebRtcAnswer = nameof(WebRtcAnswer);
    public const string IceCandidate = nameof(IceCandidate);
    public const string ParticipantMediaStateChanged = nameof(ParticipantMediaStateChanged);
    public const string CallRoomStateChanged = nameof(CallRoomStateChanged);
}

public sealed record CallParticipantDto(
    string ConnectionId,
    Guid UserId,
    string DisplayName,
    string? AvatarUrl,
    bool MicrophoneEnabled,
    bool CameraEnabled,
    bool ScreenSharing);

public sealed record CallRoomSnapshotDto(
    string RoomId,
    IReadOnlyList<CallParticipantDto> Participants,
    int MaximumParticipants);

public sealed record CallParticipantMediaStateDto(
    bool MicrophoneEnabled,
    bool CameraEnabled,
    bool ScreenSharing);

public sealed record CallParticipantMediaStateChangedDto(
    string ConnectionId,
    Guid UserId,
    CallParticipantMediaStateDto State);

public sealed record CallParticipantJoinedDto(CallParticipantDto Participant);

public sealed record CallParticipantLeftDto(
    string ConnectionId,
    Guid UserId);

public sealed record CallOfferDto(
    string FromConnectionId,
    Guid FromUserId,
    string TargetConnectionId,
    object Description);

public sealed record CallAnswerDto(
    string FromConnectionId,
    Guid FromUserId,
    string TargetConnectionId,
    object Description);

public sealed record CallIceCandidateDto(
    string FromConnectionId,
    Guid FromUserId,
    string TargetConnectionId,
    object Candidate);
