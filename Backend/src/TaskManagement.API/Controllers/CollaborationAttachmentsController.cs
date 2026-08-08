using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using TaskManagement.API.Services;
using TaskManagement.Application.Interfaces;
using TaskManagement.Infrastructure.Data;

namespace TaskManagement.API.Controllers;

[ApiController]
[Authorize]
[Route("api/collaboration-attachments")]
public sealed class CollaborationAttachmentsController : ControllerBase
{
    private readonly ApplicationDbContext _context;
    private readonly IChannelTextService _channelTextService;
    private readonly IDirectConversationService _directConversationService;
    private readonly ICollaborationAttachmentStorage _storage;

    public CollaborationAttachmentsController(
        ApplicationDbContext context,
        IChannelTextService channelTextService,
        IDirectConversationService directConversationService,
        ICollaborationAttachmentStorage storage)
    {
        _context = context;
        _channelTextService = channelTextService;
        _directConversationService = directConversationService;
        _storage = storage;
    }

    [HttpGet("{attachmentId:guid}/content")]
    public async Task<IActionResult> Download(
        Guid attachmentId,
        CancellationToken cancellationToken = default)
    {
        if (!Guid.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var userId))
            return Unauthorized();
        var attachment = await _context.CollaborationMessageAttachments.AsNoTracking()
            .Where(item => item.Id == attachmentId)
            .Select(item => new
            {
                item.ChannelMessageId,
                ChannelId = item.ChannelMessage != null
                    ? item.ChannelMessage.CollaborationChannelId
                    : null,
                item.DirectMessageId,
                ConversationId = item.DirectMessage != null
                    ? item.DirectMessage.ConversationId
                    : null,
                item.StorageKey,
                item.OriginalFileName,
                item.ContentType
            })
            .SingleOrDefaultAsync(cancellationToken);
        if (attachment == null) return NotFound();

        try
        {
            if (attachment.ChannelMessageId != null && attachment.ChannelId != null)
                await _channelTextService.GetHistoryAsync(
                    attachment.ChannelId.Value, userId, 1, 1, cancellationToken);
            else if (attachment.DirectMessageId != null && attachment.ConversationId != null)
                await _directConversationService.GetHistoryAsync(
                    attachment.ConversationId.Value, userId, 1, 1, cancellationToken);
            else
                return NotFound();
        }
        catch (ChannelNotFoundException) { return NotFound(); }
        catch (DirectConversationNotFoundException) { return NotFound(); }
        catch (DirectParticipantNotFoundException) { return NotFound(); }

        string path;
        try { path = _storage.ResolvePath(attachment.StorageKey); }
        catch (InvalidDataException) { return NotFound(); }
        if (!System.IO.File.Exists(path)) return NotFound();

        Response.Headers.CacheControl = "private, no-store";
        Response.Headers.XContentTypeOptions = "nosniff";
        Response.Headers.ContentSecurityPolicy = "sandbox; default-src 'none'";
        return PhysicalFile(
            path,
            attachment.ContentType,
            attachment.OriginalFileName,
            enableRangeProcessing: true);
    }
}
