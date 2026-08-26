using System.Collections.Concurrent;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using TaskManagement.Application.Common;
using TaskManagement.Application.DTOs.Collaboration;
using TaskManagement.Application.Interfaces;
using TaskManagement.Infrastructure.AI;
using TaskManagement.Infrastructure.Data;

namespace TaskManagement.Infrastructure.Services;

public sealed class MeetingAiAnalysisService : IMeetingAiAnalysisService
{
    private const int MaximumTranscriptTextLength = 1200;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<MeetingAiAnalysisService> _logger;
    private readonly ConcurrentDictionary<Guid, Task> _pipelines = new();
    private readonly JsonSerializerOptions _json = new(JsonSerializerDefaults.Web)
    {
        PropertyNameCaseInsensitive = true
    };

    public MeetingAiAnalysisService(
        IServiceScopeFactory scopeFactory,
        IConfiguration configuration,
        ILogger<MeetingAiAnalysisService> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
        IsEnabled = configuration.GetValue("MeetingAi:Enabled", false);
        IsConfigured = IsEnabled &&
                       !string.IsNullOrWhiteSpace(configuration["ZenMux:ApiKey"]);
        TranscriptChunkSize = Math.Clamp(configuration.GetValue("MeetingAi:TranscriptChunkSize", 8), 4, 20);
    }

    public bool IsEnabled { get; }
    public bool IsConfigured { get; }
    public string ProviderName => IsConfigured ? "ZenMux" : "Unavailable";
    public int TranscriptChunkSize { get; }

    public void QueueIncremental(CallTranscriptChunkDto transcriptChunk)
    {
        if (!IsConfigured || transcriptChunk.CallSessionId == Guid.Empty) return;
        Enqueue(transcriptChunk.CallSessionId, force: false);
    }

    public void QueueFinalize(Guid callSessionId)
    {
        if (!IsConfigured || callSessionId == Guid.Empty) return;
        Enqueue(callSessionId, force: true);
    }

    public void QueueFinalizeRoom(string roomId)
    {
        if (!IsConfigured || string.IsNullOrWhiteSpace(roomId)) return;
        _ = ResolveAndFinalizeRoomAsync(roomId);
    }

    public async Task<MeetingAiReportDto?> GetAsync(Guid callSessionId, CancellationToken cancellationToken = default)
    {
        if (_pipelines.TryGetValue(callSessionId, out var pending))
            await pending.WaitAsync(cancellationToken);
        await using var scope = _scopeFactory.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var report = await context.MeetingAiReports.AsNoTracking()
            .SingleOrDefaultAsync(item => item.CallSessionId == callSessionId, cancellationToken);
        if (report is null) return null;
        var state = DeserializeState(report.StateJson);
        var evidenceIds = GetEvidenceIds(state);
        var evidenceChunks = evidenceIds.Count == 0
            ? []
            : await context.CallTranscriptChunks.AsNoTracking()
                .Where(item => item.CallSessionId == callSessionId && evidenceIds.Contains(item.Id))
                .OrderBy(item => item.StartedAt)
                .ToListAsync(cancellationToken);
        return ToDto(report, state, evidenceChunks);
    }

    private void Enqueue(Guid callSessionId, bool force)
    {
        _pipelines.AddOrUpdate(
            callSessionId,
            _ => RunSafelyAsync(callSessionId, force),
            (_, previous) => previous.ContinueWith(
                _ => RunSafelyAsync(callSessionId, force),
                CancellationToken.None,
                TaskContinuationOptions.ExecuteSynchronously,
                TaskScheduler.Default).Unwrap());
    }

    private async Task ResolveAndFinalizeRoomAsync(string roomId)
    {
        try
        {
            if (!TryParseRoomId(roomId, out var projectId, out var voiceChannelId))
            {
                _logger.LogWarning("Unable to parse meeting roomId={RoomId} for AI finalization", roomId);
                return;
            }

            await using var scope = _scopeFactory.CreateAsyncScope();
            var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var latestSessionId = await context.CallTranscriptChunks.AsNoTracking()
                .Where(item => item.ProjectId == projectId && item.VoiceChannelId == voiceChannelId)
                    .OrderByDescending(item => item.CreatedAt)
                    .Select(item => item.CallSessionId)
                    .FirstOrDefaultAsync();
            if (latestSessionId != Guid.Empty) Enqueue(latestSessionId, force: true);
        }
        catch (Exception exception)
        {
            _logger.LogWarning(exception, "Unable to finalize meeting AI report for roomId={RoomId}", roomId);
        }
    }

    private static bool TryParseRoomId(string roomId, out Guid projectId, out string voiceChannelId)
    {
        const string projectPrefix = "project:";
        const string voiceSeparator = ":voice:";
        projectId = Guid.Empty;
        voiceChannelId = "";
        if (!roomId.StartsWith(projectPrefix, StringComparison.OrdinalIgnoreCase)) return false;
        var separatorIndex = roomId.IndexOf(voiceSeparator, projectPrefix.Length, StringComparison.OrdinalIgnoreCase);
        if (separatorIndex < 0) return false;
        var projectValue = roomId[projectPrefix.Length..separatorIndex];
        voiceChannelId = roomId[(separatorIndex + voiceSeparator.Length)..];
        return Guid.TryParse(projectValue, out projectId) && projectId != Guid.Empty && !string.IsNullOrWhiteSpace(voiceChannelId);
    }

    private async Task RunSafelyAsync(Guid callSessionId, bool force)
    {
        try
        {
            await ProcessAsync(callSessionId, force);
        }
        catch (Exception exception)
        {
            _logger.LogWarning(exception, "Meeting AI analysis failed for callSessionId={CallSessionId}", callSessionId);
        }
    }

    private async Task ProcessAsync(Guid callSessionId, bool force)
    {
        await using var scope = _scopeFactory.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var provider = scope.ServiceProvider.GetRequiredService<ZenMuxAiClient>();
        var chunks = await context.CallTranscriptChunks.AsNoTracking()
            .Where(item => item.CallSessionId == callSessionId)
            .OrderBy(item => item.StartedAt)
            .ThenBy(item => item.CreatedAt)
            .ToListAsync();
        if (chunks.Count == 0) return;

        var report = await context.MeetingAiReports.SingleOrDefaultAsync(item => item.CallSessionId == callSessionId);
        var processed = Math.Clamp(report?.ProcessedTranscriptChunkCount ?? 0, 0, chunks.Count);
        var state = report is null ? EmptyState() : DeserializeState(report.StateJson);
        if (!force && chunks.Count - processed < TranscriptChunkSize) return;

        do
        {
            var window = chunks.Skip(processed).Take(TranscriptChunkSize).ToList();
            if (window.Count == 0) break;
            if (!force && window.Count < TranscriptChunkSize) break;

            var prompt = BuildPrompt(state, window);
            try
            {
                var result = await provider.GenerateTextAsync(
                    prompt,
                    MeetingSystemInstruction,
                    forceJson: true,
                    temperature: 0.1,
                    maxCompletionTokens: 1800,
                    disableReasoning: true);
                state = NormalizeState(JsonSerializer.Deserialize<MeetingAiCompactStateDto>(result.Text, _json), chunks.Take(processed + window.Count));
            }
            catch (AiProviderException)
            {
                report ??= CreateReport(chunks[0]);
                report.Status = "UNAVAILABLE";
                report.UpdatedAt = DateTimeOffset.UtcNow;
                context.Update(report);
                await context.SaveChangesAsync();
                return;
            }
            catch (JsonException exception)
            {
                _logger.LogWarning(exception, "Meeting AI returned invalid JSON for callSessionId={CallSessionId}", callSessionId);
                report ??= CreateReport(chunks[0]);
                report.Status = "ERROR";
                report.UpdatedAt = DateTimeOffset.UtcNow;
                context.Update(report);
                await context.SaveChangesAsync();
                return;
            }

            processed += window.Count;
            report ??= CreateReport(chunks[0]);
            report.ProcessedTranscriptChunkCount = processed;
            report.StateJson = JsonSerializer.Serialize(state, _json);
            report.Status = force && processed >= chunks.Count ? "COMPLETED" : "PROCESSING";
            report.UpdatedAt = DateTimeOffset.UtcNow;
            report.CompletedAt = report.Status == "COMPLETED" ? report.UpdatedAt : null;
            context.Update(report);
            await context.SaveChangesAsync();
        } while (force && processed < chunks.Count);
    }

    private string BuildPrompt(MeetingAiCompactStateDto state, IReadOnlyList<Domain.Entities.CallTranscriptChunk> chunks)
    {
        var input = new
        {
            currentState = state,
            newFinalTranscriptSegments = chunks.Select(chunk => new
            {
                id = chunk.Id,
                speaker = chunk.SpeakerDisplayName,
                timestamp = chunk.StartedAt,
                text = Limit(chunk.Text, MaximumTranscriptTextLength)
            })
        };
        return JsonSerializer.Serialize(input, _json);
    }

    private MeetingAiCompactStateDto NormalizeState(
        MeetingAiCompactStateDto? value,
        IEnumerable<Domain.Entities.CallTranscriptChunk> availableChunks)
    {
        var validEvidence = availableChunks.Select(item => item.Id).ToHashSet();
        value ??= EmptyState();
        return new(
            Limit(value.MeetingSummaryDraft, 4000),
            NormalizeEvidence(value.Decisions, validEvidence),
            NormalizeActions(value.ActionItems, validEvidence),
            NormalizeEvidence(value.Risks, validEvidence),
            NormalizeEvidence(value.Blockers, validEvidence),
            NormalizeEvidence(value.OpenQuestions, validEvidence));
    }

    private static IReadOnlyList<MeetingAiEvidenceItemDto> NormalizeEvidence(
        IEnumerable<MeetingAiEvidenceItemDto>? values,
        HashSet<Guid> validEvidence) => (values ?? [])
        .Where(item => !string.IsNullOrWhiteSpace(item.Text))
        .Select(item => new MeetingAiEvidenceItemDto(
            Limit(item.Text, 800),
            (item.EvidenceChunkIds ?? []).Where(validEvidence.Contains).Distinct().Take(5).ToArray()))
        .DistinctBy(item => item.Text, StringComparer.OrdinalIgnoreCase)
        .Take(30)
        .ToArray();

    private static IReadOnlyList<MeetingAiActionItemDto> NormalizeActions(
        IEnumerable<MeetingAiActionItemDto>? values,
        HashSet<Guid> validEvidence) => (values ?? [])
        .Where(item => !string.IsNullOrWhiteSpace(item.Task))
        .Select(item => new MeetingAiActionItemDto(
            Limit(item.Task, 800),
            LimitNullable(item.ProposedOwner, 200),
            item.ProposedDeadline,
            NormalizePriority(item.ProposedPriority),
            (item.EvidenceChunkIds ?? []).Where(validEvidence.Contains).Distinct().Take(5).ToArray()))
        .DistinctBy(item => item.Task, StringComparer.OrdinalIgnoreCase)
        .Take(30)
        .ToArray();

    private MeetingAiCompactStateDto DeserializeState(string json)
    {
        try { return JsonSerializer.Deserialize<MeetingAiCompactStateDto>(json, _json) ?? EmptyState(); }
        catch (JsonException) { return EmptyState(); }
    }

    private MeetingAiReportDto ToDto(
        Domain.Entities.MeetingAiReport report,
        MeetingAiCompactStateDto state,
        IReadOnlyList<Domain.Entities.CallTranscriptChunk> evidenceChunks) => new(
        report.CallSessionId,
        report.ProjectId,
        report.VoiceChannelId,
        report.Status,
        report.ProcessedTranscriptChunkCount,
        state,
        report.UpdatedAt,
        report.CompletedAt,
        evidenceChunks.Select(item => new MeetingAiEvidenceReferenceDto(
            item.Id,
            item.SpeakerDisplayName,
            item.StartedAt,
            Limit(item.Text, 240))).ToArray(),
        false);

    private static HashSet<Guid> GetEvidenceIds(MeetingAiCompactStateDto state) => state.Decisions
        .SelectMany(item => item.EvidenceChunkIds)
        .Concat(state.ActionItems.SelectMany(item => item.EvidenceChunkIds))
        .Concat(state.Risks.SelectMany(item => item.EvidenceChunkIds))
        .Concat(state.Blockers.SelectMany(item => item.EvidenceChunkIds))
        .Concat(state.OpenQuestions.SelectMany(item => item.EvidenceChunkIds))
        .ToHashSet();

    private static Domain.Entities.MeetingAiReport CreateReport(Domain.Entities.CallTranscriptChunk firstChunk) => new()
    {
        Id = Guid.NewGuid(),
        CallSessionId = firstChunk.CallSessionId,
        ProjectId = firstChunk.ProjectId,
        VoiceChannelId = firstChunk.VoiceChannelId,
        Status = "PROCESSING",
        StateJson = JsonSerializer.Serialize(EmptyState(), new JsonSerializerOptions(JsonSerializerDefaults.Web)),
        UpdatedAt = DateTimeOffset.UtcNow
    };

    private static MeetingAiCompactStateDto EmptyState() => new("", [], [], [], [], []);
    private static string Limit(string? value, int maximum) => string.IsNullOrWhiteSpace(value) ? "" : value.Trim()[..Math.Min(value.Trim().Length, maximum)];
    private static string? LimitNullable(string? value, int maximum) => string.IsNullOrWhiteSpace(value) ? null : Limit(value, maximum);
    private static string? NormalizePriority(string? value) => value?.Trim().ToUpperInvariant() is "LOW" or "MEDIUM" or "HIGH" or "URGENT" ? value.Trim().ToUpperInvariant() : null;

    private const string MeetingSystemInstruction = """
        You maintain a compact structured SprintA meeting state from FINAL transcript segments only.
        Merge the supplied currentState with only the newFinalTranscriptSegments. Do not restart from zero.
        Return one JSON object matching currentState exactly: meetingSummaryDraft, decisions, actionItems, risks, blockers, openQuestions.
        Each evidence item has text and evidenceChunkIds. Each action item has task, proposedOwner, proposedDeadline, proposedPriority, evidenceChunkIds.
        Evidence IDs must come from the supplied transcript segments. Deduplicate semantically equivalent items.
        Never claim that a WorkItem was created. Action items are proposals requiring explicit user approval.
        Keep the summary concise and do not include raw audio, interim hypotheses, or secrets.
        """;
}
