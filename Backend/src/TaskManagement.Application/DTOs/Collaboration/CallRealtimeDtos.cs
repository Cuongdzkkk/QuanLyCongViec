namespace TaskManagement.Application.DTOs.Collaboration;

public static class CallRealtimeEvents
{
    public const string ParticipantJoined = nameof(ParticipantJoined);
    public const string ParticipantLeft = nameof(ParticipantLeft);
    public const string WebRtcOffer = nameof(WebRtcOffer);
    public const string WebRtcAnswer = nameof(WebRtcAnswer);
    public const string IceCandidate = nameof(IceCandidate);
    public const string ParticipantMediaStateChanged = nameof(ParticipantMediaStateChanged);
    public const string ParticipantHandChanged = nameof(ParticipantHandChanged);
    public const string CallReactionAdded = nameof(CallReactionAdded);
    public const string ParticipantSpeakerChanged = nameof(ParticipantSpeakerChanged);
    public const string ForceMuteParticipant = nameof(ForceMuteParticipant);
    public const string ForceRemovedFromCall = nameof(ForceRemovedFromCall);
    public const string CallRoomStateChanged = nameof(CallRoomStateChanged);
    public const string AiConsentRequested = nameof(AiConsentRequested);
    public const string AiParticipantAccepted = nameof(AiParticipantAccepted);
    public const string AiParticipantDeclined = nameof(AiParticipantDeclined);
    public const string AiTranscriptionStarted = nameof(AiTranscriptionStarted);
    public const string AiTranscriptionPaused = nameof(AiTranscriptionPaused);
    public const string AiTranscriptionStopped = nameof(AiTranscriptionStopped);
    public const string AiTranscriptionUnavailable = nameof(AiTranscriptionUnavailable);
    public const string AiTranscriptionError = nameof(AiTranscriptionError);
    public const string CallAiStateChanged = nameof(CallAiStateChanged);
    public const string CallTranscriptChunkAdded = nameof(CallTranscriptChunkAdded);
    public const string CallTranscriptInterim = nameof(CallTranscriptInterim);
}

public sealed record CallTranscriptionErrorDto(string Code, string Message);

public static class CallAiStates
{
    public const string Off = "OFF";
    public const string WaitingForConsent = "WAITING_FOR_CONSENT";
    public const string Active = "ACTIVE";
    public const string PausedConsent = "PAUSED_CONSENT";
    public const string Stopping = "STOPPING";
    public const string Error = "ERROR";
}

public static class CallConsentStatuses
{
    public const string Pending = "PENDING";
    public const string Accepted = "ACCEPTED";
    public const string Declined = "DECLINED";
}

public sealed record CallParticipantDto(
    string ConnectionId,
    Guid UserId,
    string DisplayName,
    string? AvatarUrl,
    bool MicrophoneEnabled,
    bool CameraEnabled,
    bool ScreenSharing,
    bool HandRaised = false,
    bool IsSpeaking = false,
    string Role = "participant");

public sealed record CallParticipantHandChangedDto(string ConnectionId, Guid UserId, bool HandRaised);
public sealed record CallReactionDto(string Id, string ConnectionId, Guid UserId, string DisplayName, string Emoji, DateTimeOffset CreatedAt);
public sealed record CallParticipantSpeakerChangedDto(string ConnectionId, Guid UserId, bool IsSpeaking);

public sealed record CallRoomSnapshotDto(
    string RoomId,
    IReadOnlyList<CallParticipantDto> Participants,
    int MaximumParticipants,
    CallAiStateDto AiState);

public sealed record CallAiParticipantConsentDto(
    Guid UserId,
    string DisplayName,
    string ConsentStatus,
    DateTimeOffset? RespondedAt);

public sealed record CallAiStateDto(
    string State,
    Guid CallSessionId,
    long ConsentGeneration,
    IReadOnlyList<CallAiParticipantConsentDto> Participants);

public sealed record CallAiStateChangedDto(CallAiStateDto State);

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

public sealed record CallTranscriptChunkDto(
    Guid Id,
    Guid CallSessionId,
    Guid ProjectId,
    string VoiceChannelId,
    Guid SpeakerUserId,
    string SpeakerDisplayName,
    DateTimeOffset StartedAt,
    DateTimeOffset EndedAt,
    string Text,
    double? Confidence);
