using System;
using System.Threading;
using System.Threading.Tasks;
using TaskManagement.Application.DTOs.AI;

namespace TaskManagement.Application.Interfaces;

public interface IAiChannelAnalysisService
{
    Task<AiChannelAnalysisResponseDto> AnalyzeAsync(
        Guid userId,
        Guid channelId,
        AiChannelAnalysisRequestDto request,
        CancellationToken cancellationToken = default);
}

public sealed class AiChannelRequestInProgressException : Exception
{
    public AiChannelRequestInProgressException() : base("AI request is already in progress.") { }
}

public sealed class AiChannelRequestAlreadyCompletedException : Exception
{
    public AiChannelRequestAlreadyCompletedException() : base("AI request already completed; use a new request id to create a new analysis.") { }
}
