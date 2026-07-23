using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using TaskManagement.API.Security;
using TaskManagement.Infrastructure.Data;

namespace TaskManagement.API.Controllers;

[ApiController]
[Authorize]
[Route("api/private-attachments")]
public sealed class PrivateAttachmentsController : ControllerBase
{
    private readonly ApplicationDbContext _context;
    private readonly IWebHostEnvironment _environment;

    public PrivateAttachmentsController(ApplicationDbContext context, IWebHostEnvironment environment)
    {
        _context = context;
        _environment = environment;
    }

    [HttpGet("comments/{attachmentId:guid}")]
    public async Task<IActionResult> DownloadCommentAttachment(Guid attachmentId)
    {
        if (!Guid.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var userId)) return Unauthorized();
        var attachment = await _context.CommentAttachments.AsNoTracking()
            .Include(item => item.Comment)
            .SingleOrDefaultAsync(item => item.Id == attachmentId && !item.Comment.IsDeleted);
        if (attachment == null) return NotFound();

        Guid? projectId = attachment.Comment.EntityType switch
        {
            "Project" => attachment.Comment.EntityId,
            "WorkTask" => await _context.WorkTasks.AsNoTracking()
                .Where(task => task.Id == attachment.Comment.EntityId && !task.IsDeleted)
                .Select(task => (Guid?)task.ProjectId)
                .SingleOrDefaultAsync(),
            _ => null
        };
        var workspaceId = projectId.HasValue
            ? await _context.Projects.AsNoTracking().Where(project => project.Id == projectId).Select(project => (Guid?)project.WorkspaceId).SingleOrDefaultAsync()
            : null;
        var allowed = projectId.HasValue && workspaceId.HasValue &&
            await _context.ProjectMembers.AsNoTracking().AnyAsync(member => member.ProjectId == projectId && member.UserId == userId && member.Status) &&
            await _context.WorkspaceMembers.AsNoTracking().AnyAsync(member => member.WorkspaceId == workspaceId && member.UserId == userId && member.IsActive);
        if (!allowed && attachment.Comment.UserId != userId) return Forbid();

        var storedName = Path.GetFileName(attachment.FileUrl);
        var root = Path.Combine(_environment.ContentRootPath, "uploads", "comments");
        string path;
        try { path = UploadSecurity.ResolveUnderRoot(root, storedName); }
        catch (InvalidDataException) { return NotFound(); }
        if (!System.IO.File.Exists(path)) return NotFound();
        Response.Headers.CacheControl = "private, no-store";
        return PhysicalFile(path, attachment.ContentType, attachment.FileName, enableRangeProcessing: true);
    }
}
