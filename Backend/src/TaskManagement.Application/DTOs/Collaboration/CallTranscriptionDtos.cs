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
    long ConsentGeneration);

public sealed record CallTranscriptionResult(
    string Text,
    DateTimeOffset StartedAt,
    DateTimeOffset EndedAt,
    double? Confidence);
