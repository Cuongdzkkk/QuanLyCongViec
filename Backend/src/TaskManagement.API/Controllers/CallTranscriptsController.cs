using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TaskManagement.Application.DTOs.Collaboration;
using TaskManagement.Application.Interfaces;

namespace TaskManagement.API.Controllers;

[ApiController]
[Authorize]
[Route("api/projects/{projectId:guid}/voice-channels/{voiceChannelId}/calls/{callSessionId:guid}/transcript")]
public sealed class CallTranscriptsController : ControllerBase
{
    private readonly ICallRoomAuthorizationService _authorization;
    private readonly ICallTranscriptService _transcripts;

    public CallTranscriptsController(
        ICallRoomAuthorizationService authorization,
        ICallTranscriptService transcripts)
    {
        _authorization = authorization;
        _transcripts = transcripts;
    }

    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<CallTranscriptChunkDto>>> Get(
        Guid projectId,
        string voiceChannelId,
        Guid callSessionId,
        CancellationToken cancellationToken = default)
    {
        if (!TryGetCurrentUserId(out var userId)) return Unauthorized();
        if (string.IsNullOrWhiteSpace(voiceChannelId) || voiceChannelId.Length > 200) return BadRequest();

        try
        {
            await _authorization.AuthorizeVoiceRoomJoinAsync(projectId, userId, cancellationToken);
            return Ok(await _transcripts.GetAsync(projectId, voiceChannelId, callSessionId, cancellationToken));
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
