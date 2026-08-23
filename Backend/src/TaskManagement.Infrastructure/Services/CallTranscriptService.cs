using Microsoft.EntityFrameworkCore;
using TaskManagement.Application.DTOs.Collaboration;
using TaskManagement.Application.Interfaces;
using TaskManagement.Infrastructure.Data;

namespace TaskManagement.Infrastructure.Services;

public sealed class CallTranscriptService : ICallTranscriptService
{
    private readonly ApplicationDbContext _context;

    public CallTranscriptService(ApplicationDbContext context) => _context = context;

    public async Task<CallTranscriptChunkDto?> AppendAsync(
        CallAudioChunk source,
        CallTranscriptionResult result,
        CancellationToken cancellationToken = default)
    {
        var text = result.Text?.Trim();
        if (string.IsNullOrWhiteSpace(text)) return null;
        var chunk = new Domain.Entities.CallTranscriptChunk
        {
            Id = Guid.NewGuid(),
            CallSessionId = source.CallSessionId,
            ProjectId = ParseProjectId(source.RoomId),
            VoiceChannelId = ParseVoiceChannelId(source.RoomId),
            SpeakerUserId = source.SpeakerUserId,
            SpeakerDisplayName = source.SpeakerDisplayName,
            StartedAt = result.StartedAt,
            EndedAt = result.EndedAt < result.StartedAt ? result.StartedAt : result.EndedAt,
            Text = text,
            Confidence = result.Confidence,
            CreatedAt = DateTimeOffset.UtcNow
        };
        _context.CallTranscriptChunks.Add(chunk);
        await _context.SaveChangesAsync(cancellationToken);
        return ToDto(chunk);
    }

    public async Task<IReadOnlyList<CallTranscriptChunkDto>> GetAsync(
        Guid projectId,
        string voiceChannelId,
        Guid callSessionId,
        CancellationToken cancellationToken = default)
    {
        var normalizedVoiceChannelId = NormalizeVoiceChannelId(voiceChannelId);
        return await _context.CallTranscriptChunks.AsNoTracking()
            .Where(chunk => chunk.ProjectId == projectId &&
                            chunk.VoiceChannelId == normalizedVoiceChannelId &&
                            chunk.CallSessionId == callSessionId)
            .OrderBy(chunk => chunk.StartedAt)
            .ThenBy(chunk => chunk.CreatedAt)
            .Select(chunk => new CallTranscriptChunkDto(
                chunk.Id,
                chunk.CallSessionId,
                chunk.ProjectId,
                chunk.VoiceChannelId,
                chunk.SpeakerUserId,
                chunk.SpeakerDisplayName,
                chunk.StartedAt,
                chunk.EndedAt,
                chunk.Text,
                chunk.Confidence))
            .ToListAsync(cancellationToken);
    }

    private static CallTranscriptChunkDto ToDto(Domain.Entities.CallTranscriptChunk chunk) => new(
        chunk.Id,
        chunk.CallSessionId,
        chunk.ProjectId,
        chunk.VoiceChannelId,
        chunk.SpeakerUserId,
        chunk.SpeakerDisplayName,
        chunk.StartedAt,
        chunk.EndedAt,
        chunk.Text,
        chunk.Confidence);

    private static Guid ParseProjectId(string roomId)
    {
        var parts = roomId.Split(':', StringSplitOptions.RemoveEmptyEntries);
        return parts.Length >= 2 && Guid.TryParseExact(parts[1], "N", out var projectId)
            ? projectId
            : throw new ArgumentException("Invalid call room.", nameof(roomId));
    }

    private static string ParseVoiceChannelId(string roomId)
    {
        var parts = roomId.Split(':', StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length < 4 || !string.Equals(parts[0], "project", StringComparison.Ordinal) || !string.Equals(parts[2], "voice", StringComparison.Ordinal))
            throw new ArgumentException("Invalid call room.", nameof(roomId));
        var marker = $"project:{parts[1]}:voice:";
        return roomId.StartsWith(marker, StringComparison.Ordinal)
            ? NormalizeVoiceChannelId(roomId[marker.Length..])
            : throw new ArgumentException("Invalid call room.", nameof(roomId));
    }

    private static string NormalizeVoiceChannelId(string value) =>
        !string.IsNullOrWhiteSpace(value) && value.Length <= 200 ? value.Trim() : throw new ArgumentException("Invalid voice channel.", nameof(value));
}
