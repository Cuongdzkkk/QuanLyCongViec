using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.DependencyInjection;
using TaskManagement.API.Hubs;
using TaskManagement.Application.DTOs.Collaboration;
using TaskManagement.Application.Interfaces;

namespace TaskManagement.API.Services;

public interface ICallCaptionResultDispatcher
{
    Task DeliverAsync(CallAudioChunk source, CallTranscriptionResult result);
}

public sealed class CallCaptionResultDispatcher : ICallCaptionResultDispatcher
{
    private readonly ICallRoomRegistry _rooms;
    private readonly IHubContext<CallHub> _hubContext;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IMeetingAiAnalysisService? _meetingAi;

    public CallCaptionResultDispatcher(
        ICallRoomRegistry rooms,
        IHubContext<CallHub> hubContext,
        IServiceScopeFactory scopeFactory,
        IMeetingAiAnalysisService? meetingAi = null)
    {
        _rooms = rooms;
        _hubContext = hubContext;
        _scopeFactory = scopeFactory;
        _meetingAi = meetingAi;
    }

    public async Task DeliverAsync(CallAudioChunk source, CallTranscriptionResult result)
    {
        if (string.IsNullOrWhiteSpace(result.Text) || string.IsNullOrWhiteSpace(source.SpeakerConnectionId)) return;
        if (!_rooms.TryAuthorizeTranscription(
                source.RoomId,
                source.SpeakerConnectionId,
                source.CallSessionId,
                source.ConsentGeneration,
                out _)) return;

        if (!result.IsFinal)
        {
            await _hubContext.Clients.Group(source.RoomId).SendAsync(
                CallRealtimeEvents.CallTranscriptInterim,
                new CallTranscriptInterimDto(
                    source.CallSessionId,
                    source.SpeakerUserId,
                    source.SpeakerDisplayName,
                    result.StartedAt,
                    result.EndedAt,
                    result.Text.Trim(),
                    result.Confidence));
            return;
        }

        await using var scope = _scopeFactory.CreateAsyncScope();
        var transcripts = scope.ServiceProvider.GetRequiredService<ICallTranscriptService>();
        var transcript = await transcripts.AppendAsync(source, result);
        if (transcript is null) return;

        _meetingAi?.QueueIncremental(transcript);
        await _hubContext.Clients.Group(source.RoomId).SendAsync(
            CallRealtimeEvents.CallTranscriptChunkAdded,
            transcript);
    }
}
