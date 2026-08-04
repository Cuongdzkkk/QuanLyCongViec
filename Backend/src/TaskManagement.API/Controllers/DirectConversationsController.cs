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
    private readonly ICollaborationReadStateService _readStateService;
    private readonly ICollaborationRealtimePublisher _realtimePublisher;

    public DirectConversationsController(
        IDirectConversationService service,
        ICollaborationReadStateService readStateService,
        ICollaborationRealtimePublisher realtimePublisher)
    {
        _service = service;
        _readStateService = readStateService;
        _realtimePublisher = realtimePublisher;
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
            await _realtimePublisher.PublishDirectMessageCreatedAsync(
                result,
                cancellationToken);
            var unreadUpdates = await _readStateService
                .GetDirectUnreadUpdatesForMessageAsync(
                    result.MessageId,
                    cancellationToken);
            foreach (var update in unreadUpdates)
            {
                await _realtimePublisher.PublishReadStateChangedAsync(
                    update.UserId,
                    update.State,
                    cancellationToken);
            }
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

    [HttpPost("{conversationId:guid}/read")]
    public async Task<IActionResult> MarkRead(
        Guid conversationId,
        [FromBody] MarkCollaborationReadRequestDto request,
        CancellationToken cancellationToken = default)
    {
        if (!TryGetCurrentUserId(out var userId)) return Unauthorized();
        if (request.MessageId == Guid.Empty)
            return BadRequest(ApiResponse<object>.Error("MessageId is required."));
        try
        {
            var result = await _readStateService.MarkDirectConversationReadAsync(
                conversationId, userId, request.MessageId, cancellationToken);
            await _realtimePublisher.PublishReadStateChangedAsync(
                userId, result, cancellationToken);
            return Ok(ApiResponse<CollaborationReadStateDto>.Success(result));
        }
        catch (DirectConversationNotFoundException exception)
        {
            return NotFound(ApiResponse<object>.Error(exception.Message, 404));
        }
        catch (DirectParticipantNotFoundException exception)
        {
            return NotFound(ApiResponse<object>.Error(exception.Message, 404));
        }
        catch (CollaborationMessageNotFoundException exception)
        {
            return NotFound(ApiResponse<object>.Error(exception.Message, 404));
        }
    }

    private bool TryGetCurrentUserId(out Guid userId) =>
        Guid.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out userId);
}
