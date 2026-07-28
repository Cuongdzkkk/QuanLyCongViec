using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TaskManagement.Application.DTOs.Collaboration;
using TaskManagement.Application.DTOs.Common;
using TaskManagement.Application.Interfaces;

namespace TaskManagement.API.Controllers;

[ApiController]
[Authorize]
[Route("api/projects/{projectId:guid}/channels")]
public sealed class CollaborationChannelsController : ControllerBase
{
    private readonly ICollaborationChannelService _channelService;

    public CollaborationChannelsController(ICollaborationChannelService channelService)
    {
        _channelService = channelService;
    }

    [HttpGet]
    public async Task<IActionResult> Discover(
        Guid projectId,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 50,
        CancellationToken cancellationToken = default)
    {
        if (!TryGetCurrentUserId(out var userId)) return Unauthorized();
        try
        {
            var result = await _channelService.DiscoverAsync(
                projectId,
                userId,
                page,
                pageSize,
                cancellationToken);
            return Ok(ApiResponse<CollaborationChannelPageDto>.Success(result));
        }
        catch (ArgumentOutOfRangeException exception)
        {
            return BadRequest(ApiResponse<object>.Error(exception.Message));
        }
        catch (CollaborationProjectNotFoundException exception)
        {
            return NotFound(ApiResponse<object>.Error(exception.Message, 404));
        }
    }

    [HttpPost]
    public async Task<IActionResult> Create(
        Guid projectId,
        [FromBody] CreateCollaborationChannelRequestDto request,
        [FromHeader(Name = "Idempotency-Key")] string? idempotencyKey,
        CancellationToken cancellationToken = default)
    {
        if (!TryGetCurrentUserId(out var userId)) return Unauthorized();
        try
        {
            var result = await _channelService.CreateAsync(
                projectId,
                userId,
                request,
                idempotencyKey,
                cancellationToken);
            if (!result.Created)
                return Ok(ApiResponse<CollaborationChannelDto>.Success(
                    result.Channel,
                    "Channel already provisioned."));

            return StatusCode(
                StatusCodes.Status201Created,
                ApiResponse<CollaborationChannelDto>.Created(
                    result.Channel,
                    "Channel created."));
        }
        catch (ArgumentException exception)
        {
            return BadRequest(ApiResponse<object>.Error(exception.Message));
        }
        catch (CollaborationProjectNotFoundException exception)
        {
            return NotFound(ApiResponse<object>.Error(exception.Message, 404));
        }
        catch (CollaborationChannelForbiddenException exception)
        {
            return StatusCode(
                StatusCodes.Status403Forbidden,
                ApiResponse<object>.Error(exception.Message, 403));
        }
        catch (CollaborationChannelConflictException exception)
        {
            return Conflict(ApiResponse<object>.Error(exception.Message, 409));
        }
    }

    private bool TryGetCurrentUserId(out Guid userId) =>
        Guid.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out userId);
}
