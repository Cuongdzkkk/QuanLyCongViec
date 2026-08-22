using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using TaskManagement.Application.Common;
using TaskManagement.Application.DTOs.AI;
using TaskManagement.Application.DTOs.Common;
using TaskManagement.Application.Interfaces;

namespace TaskManagement.API.Controllers;

[ApiController]
[Authorize]
[Route("api/channels/{channelId:guid}/ai")]
public sealed class ChannelAiController : ControllerBase
{
    private readonly IAiChannelAnalysisService _analysisService;

    public ChannelAiController(IAiChannelAnalysisService analysisService)
    {
        _analysisService = analysisService;
    }

    [HttpPost("analysis")]
    [EnableRateLimiting("AiGeneration")]
    public async Task<IActionResult> Analyze(
        Guid channelId,
        [FromBody] AiChannelAnalysisRequestDto request,
        CancellationToken cancellationToken = default)
    {
        if (!TryGetCurrentUserId(out var userId)) return Unauthorized();
        try
        {
            var result = await _analysisService.AnalyzeAsync(userId, channelId, request, cancellationToken);
            return Ok(ApiResponse<AiChannelAnalysisResponseDto>.Success(result));
        }
        catch (ChannelNotFoundException exception)
        {
            return NotFound(ApiResponse<object>.Error(exception.Message, 404));
        }
        catch (AiCreditsExhaustedException)
        {
            return StatusCode(402, ApiResponse<object>.Error(
                "Không đủ credit AI để phân tích channel. Hãy thử lại sau khi có thêm credit.", 402));
        }
        catch (AiProviderException exception)
        {
            return StatusCode(503, ApiResponse<object>.Error(exception.Message, 503));
        }
        catch (AiChannelRequestAlreadyCompletedException exception)
        {
            return Conflict(ApiResponse<object>.Error(exception.Message, 409));
        }
        catch (AiChannelRequestInProgressException exception)
        {
            return Conflict(ApiResponse<object>.Error(exception.Message, 409));
        }
        catch (ArgumentException exception)
        {
            return BadRequest(ApiResponse<object>.Error(exception.Message));
        }
    }

    private bool TryGetCurrentUserId(out Guid userId)
    {
        var value = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? User.FindFirstValue("sub");
        return Guid.TryParse(value, out userId) && userId != Guid.Empty;
    }
}
