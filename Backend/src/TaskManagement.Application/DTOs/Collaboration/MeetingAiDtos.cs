namespace TaskManagement.Application.DTOs.Collaboration;

public sealed record MeetingAiEvidenceItemDto(
    string Text,
    IReadOnlyList<Guid> EvidenceChunkIds);

public sealed record MeetingAiActionItemDto(
    string Task,
    string? ProposedOwner,
    DateTimeOffset? ProposedDeadline,
    string? ProposedPriority,
    IReadOnlyList<Guid> EvidenceChunkIds);

public sealed record MeetingAiCompactStateDto(
    string MeetingSummaryDraft,
    IReadOnlyList<MeetingAiEvidenceItemDto> Decisions,
    IReadOnlyList<MeetingAiActionItemDto> ActionItems,
    IReadOnlyList<MeetingAiEvidenceItemDto> Risks,
    IReadOnlyList<MeetingAiEvidenceItemDto> Blockers,
    IReadOnlyList<MeetingAiEvidenceItemDto> OpenQuestions);

public sealed record MeetingAiEvidenceReferenceDto(
    Guid TranscriptChunkId,
    string SpeakerDisplayName,
    DateTimeOffset Timestamp,
    string Excerpt);

public sealed record MeetingAiReportDto(
    Guid CallSessionId,
    Guid ProjectId,
    string VoiceChannelId,
    string Status,
    int ProcessedTranscriptChunkCount,
    MeetingAiCompactStateDto State,
    DateTimeOffset UpdatedAt,
    DateTimeOffset? CompletedAt,
    IReadOnlyList<MeetingAiEvidenceReferenceDto>? Evidence = null,
    bool AutoCreatesTasks = false);
