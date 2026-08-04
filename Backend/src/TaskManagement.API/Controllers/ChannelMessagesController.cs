using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TaskManagement.Application.DTOs.Collaboration;
using TaskManagement.Application.DTOs.Common;
using TaskManagement.Application.Interfaces;

namespace TaskManagement.API.Controllers;

[ApiController]
[Authorize]
[Route("api/channels/{channelId:guid}/messages")]
public sealed class ChannelMessagesController : ControllerBase
{
    private readonly IChannelTextService _channelTextService;
    private readonly ICollaborationReadStateService _readStateService;
    private readonly ICollaborationRealtimePublisher _realtimePublisher;

    public ChannelMessagesController(
        IChannelTextService channelTextService,
        ICollaborationReadStateService readStateService,
        ICollaborationRealtimePublisher realtimePublisher)
    {
        _channelTextService = channelTextService;
        _readStateService = readStateService;
        _realtimePublisher = realtimePublisher;
    }

    [HttpGet]
    public async Task<IActionResult> GetHistory(
        Guid channelId,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 50,
        CancellationToken cancellationToken = default)
    {
        if (!TryGetCurrentUserId(out var userId)) return Unauthorized();
        try
        {
            var result = await _channelTextService.GetHistoryAsync(
                channelId,
                userId,
                page,
                pageSize,
                cancellationToken);
            return Ok(ApiResponse<ChannelMessagePageDto>.Success(result));
        }
        catch (ArgumentOutOfRangeException exception)
        {
            return BadRequest(ApiResponse<object>.Error(exception.Message));
        }
        catch (ChannelNotFoundException exception)
        {
            return NotFound(ApiResponse<object>.Error(exception.Message, 404));
        }
    }

    [HttpPost]
    public async Task<IActionResult> Send(
        Guid channelId,
        [FromBody] SendChannelMessageRequestDto request,
        CancellationToken cancellationToken = default)
    {
        if (!TryGetCurrentUserId(out var userId)) return Unauthorized();
        try
        {
            var result = await _channelTextService.SendAsync(
                channelId,
                userId,
                request.Content,
                cancellationToken);
            await _realtimePublisher.PublishChannelMessageCreatedAsync(
                result,
                cancellationToken);
            var unreadUpdates = await _readStateService
                .GetChannelUnreadUpdatesForMessageAsync(
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
                ApiResponse<ChannelMessageDto>.Created(result, "Message sent."));
        }
        catch (ArgumentException exception)
        {
            return BadRequest(ApiResponse<object>.Error(exception.Message));
        }
        catch (ChannelNotFoundException exception)
        {
            return NotFound(ApiResponse<object>.Error(exception.Message, 404));
        }
        catch (ChannelSendForbiddenException exception)
        {
            return StatusCode(
                StatusCodes.Status403Forbidden,
                ApiResponse<object>.Error(exception.Message, 403));
        }
    }

    [HttpPost("/api/channels/{channelId:guid}/read")]
    public async Task<IActionResult> MarkRead(
        Guid channelId,
        [FromBody] MarkCollaborationReadRequestDto request,
        CancellationToken cancellationToken = default)
    {
        if (!TryGetCurrentUserId(out var userId)) return Unauthorized();
        if (request.MessageId == Guid.Empty)
            return BadRequest(ApiResponse<object>.Error("MessageId is required."));
        try
        {
            var result = await _readStateService.MarkChannelReadAsync(
                channelId, userId, request.MessageId, cancellationToken);
            await _realtimePublisher.PublishReadStateChangedAsync(
                userId, result, cancellationToken);
            return Ok(ApiResponse<CollaborationReadStateDto>.Success(result));
        }
        catch (ChannelNotFoundException exception)
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
