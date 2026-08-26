using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TaskManagement.Application.DTOs.Collaboration;
using TaskManagement.Application.Interfaces;

namespace TaskManagement.API.Controllers;

[ApiController]
[Authorize]
[Route("api/projects/{projectId:guid}/voice-channels/{voiceChannelId}/calls")]
public sealed class CallCapabilitiesController : ControllerBase
{
    private readonly ICallRoomAuthorizationService _authorization;
    private readonly ICallTranscriptionProvider _transcription;
    private readonly IMeetingAiAnalysisService? _meetingAi;

    public CallCapabilitiesController(
        ICallRoomAuthorizationService authorization,
        ICallTranscriptionProvider transcription,
        IMeetingAiAnalysisService? meetingAi = null)
    {
        _authorization = authorization;
        _transcription = transcription;
        _meetingAi = meetingAi;
    }

    [HttpGet("capabilities")]
    public async Task<ActionResult<MeetingCapabilitiesDto>> Get(
        Guid projectId,
        string voiceChannelId,
        CancellationToken cancellationToken = default)
    {
        if (!TryGetCurrentUserId(out var userId)) return Unauthorized();
        if (string.IsNullOrWhiteSpace(voiceChannelId) || voiceChannelId.Length > 200) return BadRequest();

        try
        {
            await _authorization.AuthorizeVoiceRoomJoinAsync(projectId, userId, cancellationToken);
            return Ok(new MeetingCapabilitiesDto(
                _transcription.IsConfigured,
                _transcription.ProviderName,
                _transcription.SupportedLanguages,
                _meetingAi?.IsEnabled == true,
                _meetingAi?.IsConfigured == true));
        }
        catch (UnauthorizedAccessException)
        {
            return Forbid();
        }
    }

    private bool TryGetCurrentUserId(out Guid userId)
    {
        var value = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? User.FindFirstValue("sub");
        return Guid.TryParse(value, out userId) && userId != Guid.Empty;
    }
}
