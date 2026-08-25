namespace TaskManagement.Application.DTOs.Collaboration;

public sealed record CallAudioChunk(
    Guid CallSessionId,
    string RoomId,
    Guid SpeakerUserId,
    string SpeakerDisplayName,
    string MimeType,
    byte[] AudioBytes,
    DateTimeOffset StartedAt,
    DateTimeOffset EndedAt,
    long ConsentGeneration,
    string? SpeakerConnectionId = null,
    string Language = "vi");

public sealed record CallTranscriptionResult(
    string Text,
    DateTimeOffset StartedAt,
    DateTimeOffset EndedAt,
    double? Confidence,
    bool IsFinal = true,
    bool IsUtteranceFinal = true,
    string Provider = "",
    double? AudioSeconds = null);

public sealed record CallTranscriptionUsage(
    string Provider,
    Guid CallSessionId,
    Guid SpeakerUserId,
    double AudioSeconds,
    string? Model);

public sealed record CallTranscriptInterimDto(
    Guid CallSessionId,
    Guid SpeakerUserId,
    string SpeakerDisplayName,
    DateTimeOffset StartedAt,
    DateTimeOffset EndedAt,
    string Text,
    double? Confidence);
