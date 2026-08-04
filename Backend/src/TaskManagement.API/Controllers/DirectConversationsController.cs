using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TaskManagement.Application.DTOs.Collaboration;
using TaskManagement.Application.DTOs.Common;
using TaskManagement.Application.Interfaces;
using TaskManagement.API.Security;
using TaskManagement.API.Services;

namespace TaskManagement.API.Controllers;

[ApiController]
[Authorize]
[Route("api/direct-conversations")]
public sealed class DirectConversationsController : ControllerBase
{
    private readonly IDirectConversationService _service;
    private readonly ICollaborationReadStateService _readStateService;
    private readonly ICollaborationRealtimePublisher _realtimePublisher;
    private readonly ICollaborationAttachmentStorage _attachmentStorage;

    public DirectConversationsController(
        IDirectConversationService service,
        ICollaborationReadStateService readStateService,
        ICollaborationRealtimePublisher realtimePublisher,
        ICollaborationAttachmentStorage attachmentStorage)
    {
        _service = service;
        _readStateService = readStateService;
        _realtimePublisher = realtimePublisher;
        _attachmentStorage = attachmentStorage;
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
    [Consumes("application/json")]
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
            await PublishCreatedAsync(result, cancellationToken);
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

    [HttpPost("{conversationId:guid}/messages")]
    [Consumes("multipart/form-data")]
    [RequestSizeLimit(53 * 1024 * 1024)]
    public async Task<IActionResult> SendWithAttachments(
        Guid conversationId,
        [FromForm] CollaborationMessageForm request,
        CancellationToken cancellationToken = default)
    {
        if (!TryGetCurrentUserId(out var userId)) return Unauthorized();
        if (request.Files.Count is < 1 or > 5)
            return BadRequest(ApiResponse<object>.Error("A message must contain between 1 and 5 attachments."));

        IReadOnlyList<PendingCollaborationAttachmentDto> stored = [];
        var persisted = false;
        try
        {
            var validated = new List<ValidatedUpload>(request.Files.Count);
            foreach (var file in request.Files)
                validated.Add(await UploadSecurity.ReadCollaborationFileAsync(file, cancellationToken));
            stored = await _attachmentStorage.StoreAsync(validated, cancellationToken);
            var result = await _service.SendWithAttachmentsAsync(
                conversationId, userId, request.Content, stored, cancellationToken);
            persisted = true;
            await PublishCreatedAsync(result, cancellationToken);
            return StatusCode(
                StatusCodes.Status201Created,
                ApiResponse<DirectMessageDto>.Created(result, "Message sent."));
        }
        catch (InvalidDataException exception)
        {
            if (!persisted) _attachmentStorage.Delete(stored);
            return BadRequest(ApiResponse<object>.Error(exception.Message));
        }
        catch (ArgumentException exception)
        {
            if (!persisted) _attachmentStorage.Delete(stored);
            return BadRequest(ApiResponse<object>.Error(exception.Message));
        }
        catch (DirectConversationNotFoundException exception)
        {
            if (!persisted) _attachmentStorage.Delete(stored);
            return NotFound(ApiResponse<object>.Error(exception.Message, 404));
        }
        catch (DirectParticipantNotFoundException exception)
        {
            if (!persisted) _attachmentStorage.Delete(stored);
            return NotFound(ApiResponse<object>.Error(exception.Message, 404));
        }
        catch
        {
            if (!persisted) _attachmentStorage.Delete(stored);
            return StatusCode(500, ApiResponse<object>.Error("The attachment message could not be stored.", 500));
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

    private async Task PublishCreatedAsync(
        DirectMessageDto result,
        CancellationToken cancellationToken)
    {
        await _realtimePublisher.PublishDirectMessageCreatedAsync(result, cancellationToken);
        var unreadUpdates = await _readStateService.GetDirectUnreadUpdatesForMessageAsync(
            result.MessageId, cancellationToken);
        foreach (var update in unreadUpdates)
            await _realtimePublisher.PublishReadStateChangedAsync(
                update.UserId, update.State, cancellationToken);
    }
}
