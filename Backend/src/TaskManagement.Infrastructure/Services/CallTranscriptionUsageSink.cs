using Microsoft.Extensions.Logging;
using TaskManagement.Application.DTOs.Collaboration;
using TaskManagement.Application.Interfaces;

namespace TaskManagement.Infrastructure.Services;

public sealed class CallTranscriptionUsageSink : ICallTranscriptionUsageSink
{
    private readonly ILogger<CallTranscriptionUsageSink> _logger;

    public CallTranscriptionUsageSink(ILogger<CallTranscriptionUsageSink> logger) => _logger = logger;

    public Task RecordAsync(CallTranscriptionUsage usage, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation(
            "Call STT usage provider={Provider} session={CallSessionId} speaker={SpeakerUserId} audioSeconds={AudioSeconds} model={Model}",
            usage.Provider,
            usage.CallSessionId,
            usage.SpeakerUserId,
            usage.AudioSeconds,
            usage.Model);
        return Task.CompletedTask;
    }
}
