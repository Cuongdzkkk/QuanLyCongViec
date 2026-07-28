using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TaskManagement.Application.DTOs.Collaboration;
using TaskManagement.Application.DTOs.Common;
using TaskManagement.Application.Interfaces;

namespace TaskManagement.API.Controllers;

[ApiController]
[Authorize]
[Route("api/direct-conversations")]
public sealed class DirectConversationsController : ControllerBase
{
    private readonly IDirectConversationService _service;

    public DirectConversationsController(IDirectConversationService service)
    {
        _service = service;
    }

    [HttpPost]
    public async Task<IActionResult> FindOrCreate(
        [FromBody] CreateDirectConversationRequestDto request,
        CancellationToken cancellationToken = default)
    {
        if (!TryGetCurrentUserId(out var userId)) return Unauthorized();
        try
        {
            var result = await _service.FindOrCreateAsync(
                userId, request.ParticipantUserId, cancellationToken);
            return Ok(ApiResponse<DirectConversationDto>.Success(result));
        }
        catch (ArgumentException exception)
        {
            return BadRequest(ApiResponse<object>.Error(exception.Message));
        }
        catch (DirectParticipantNotFoundException exception)
        {
            return NotFound(ApiResponse<object>.Error(exception.Message, 404));
        }
    }

    [HttpGet]
    public async Task<IActionResult> List(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 50,
        CancellationToken cancellationToken = default)
    {
        if (!TryGetCurrentUserId(out var userId)) return Unauthorized();
        try
        {
            return Ok(ApiResponse<DirectConversationPageDto>.Success(
                await _service.ListAsync(userId, page, pageSize, cancellationToken)));
        }
        catch (ArgumentOutOfRangeException exception)
        {
            return BadRequest(ApiResponse<object>.Error(exception.Message));
        }
        catch (DirectParticipantNotFoundException exception)
        {
            return NotFound(ApiResponse<object>.Error(exception.Message, 404));
        }
    }

    [HttpGet("{conversationId:guid}/messages")]
    public async Task<IActionResult> GetHistory(
        Guid conversationId,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 50,
        CancellationToken cancellationToken = default)
    {
        if (!TryGetCurrentUserId(out var userId)) return Unauthorized();
        try
        {
            return Ok(ApiResponse<DirectMessagePageDto>.Success(
                await _service.GetHistoryAsync(
                    conversationId, userId, page, pageSize, cancellationToken)));
        }
        catch (ArgumentOutOfRangeException exception)
        {
            return BadRequest(ApiResponse<object>.Error(exception.Message));
        }
        catch (DirectConversationNotFoundException exception)
        {
            return NotFound(ApiResponse<object>.Error(exception.Message, 404));
        }
        catch (DirectParticipantNotFoundException exception)
        {
            return NotFound(ApiResponse<object>.Error(exception.Message, 404));
        }
    }

    [HttpPost("{conversationId:guid}/messages")]
    public async Task<IActionResult> Send(
        Guid conversationId,
        [FromBody] SendDirectMessageRequestDto request,
        CancellationToken cancellationToken = default)
    {
        if (!TryGetCurrentUserId(out var userId)) return Unauthorized();
        try
        {
            var result = await _service.SendAsync(
                conversationId, userId, request.Content, cancellationToken);
            return StatusCode(
                StatusCodes.Status201Created,
                ApiResponse<DirectMessageDto>.Created(result, "Message sent."));
        }
        catch (ArgumentException exception)
        {
            return BadRequest(ApiResponse<object>.Error(exception.Message));
        }
        catch (DirectConversationNotFoundException exception)
        {
            return NotFound(ApiResponse<object>.Error(exception.Message, 404));
        }
        catch (DirectParticipantNotFoundException exception)
        {
            return NotFound(ApiResponse<object>.Error(exception.Message, 404));
        }
    }

    private bool TryGetCurrentUserId(out Guid userId) =>
        Guid.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out userId);
}
