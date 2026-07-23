using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Security.Claims;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using TaskManagement.API.Hubs;
using TaskManagement.API.Filters;
using TaskManagement.API.Security;
using TaskManagement.Domain.Entities;
using TaskManagement.Infrastructure.Data;

namespace TaskManagement.API.Controllers
{
    [ApiController]
    [Route("api")]
    [Authorize]
    public class CommentsController : ControllerBase
    {
        private const string ReactionMarkerPrefix = "<!--reactions:";
        private const string ReactionMarkerSuffix = "-->";

        private readonly ApplicationDbContext _context;
        private readonly IHubContext<NotificationHub> _notificationHub;
        private readonly IWebHostEnvironment _env;

        public CommentsController(ApplicationDbContext context, IHubContext<NotificationHub> notificationHub, IWebHostEnvironment env)
        {
            _context = context;
            _notificationHub = notificationHub;
            _env = env;
        }

        private async Task<bool> TaskBelongsToProjectAsync(Guid projectId, Guid taskId)
        {
            return await _context.WorkTasks.AnyAsync(task => task.Id == taskId && task.ProjectId == projectId && !task.IsDeleted);
        }

        private async Task TriggerNotificationEventAsync(string relativePath, object payload)
        {
            var authorizationHeader = Request.Headers.Authorization.ToString();
            if (string.IsNullOrWhiteSpace(authorizationHeader) || string.IsNullOrWhiteSpace(Request.Host.Value))
            {
                return;
            }

            try
            {
                using var client = new HttpClient
                {
                    BaseAddress = new Uri($"{Request.Scheme}://{Request.Host}")
                };
                client.DefaultRequestHeaders.TryAddWithoutValidation("Authorization", authorizationHeader);

                using var content = new StringContent(
                    JsonSerializer.Serialize(payload),
                    Encoding.UTF8,
                    "application/json");

                await client.PostAsync(relativePath, content);
            }
            catch
            {
                // Notification event failures must not block comment creation.
            }
        }

        private Guid? GetUserId()
        {
            var claim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            return Guid.TryParse(claim, out var id) ? id : null;
        }

        private static (string Content, Dictionary<string, int> Reactions) ParseCommentContent(string? rawContent)
        {
            var content = rawContent ?? string.Empty;
            var markerStart = content.LastIndexOf(ReactionMarkerPrefix, StringComparison.Ordinal);
            if (markerStart < 0)
            {
                return (content, new Dictionary<string, int>());
            }

            var markerEnd = content.IndexOf(ReactionMarkerSuffix, markerStart, StringComparison.Ordinal);
            if (markerEnd < 0)
            {
                return (content, new Dictionary<string, int>());
            }

            var jsonStart = markerStart + ReactionMarkerPrefix.Length;
            var json = content.Substring(jsonStart, markerEnd - jsonStart);
            var cleanContent = content.Remove(markerStart, markerEnd + ReactionMarkerSuffix.Length - markerStart).TrimEnd();

            try
            {
                var reactions = JsonSerializer.Deserialize<Dictionary<string, int>>(json) ?? new Dictionary<string, int>();
                return (cleanContent, reactions);
            }
            catch
            {
                return (cleanContent, new Dictionary<string, int>());
            }
        }

        private static string ComposeCommentContent(string? content, IDictionary<string, int>? reactions)
        {
            var cleanContent = content ?? string.Empty;
            if (reactions == null || reactions.Count == 0)
            {
                return cleanContent;
            }

            return $"{cleanContent}{ReactionMarkerPrefix}{JsonSerializer.Serialize(reactions)}{ReactionMarkerSuffix}";
        }

        private static string SanitizeRichHtml(string? html)
        {
            if (string.IsNullOrWhiteSpace(html))
            {
                return string.Empty;
            }

            var sanitized = html;

            // Remove executable/scriptable blocks first.
            sanitized = Regex.Replace(
                sanitized,
                @"<(script|style|iframe|object|embed|link|meta)[^>]*>[\s\S]*?</\1>",
                string.Empty,
                RegexOptions.IgnoreCase);

            // Strip inline event handlers such as onclick=...
            sanitized = Regex.Replace(
                sanitized,
                @"\son\w+\s*=\s*(""[^""]*""|'[^']*'|[^\s>]+)",
                string.Empty,
                RegexOptions.IgnoreCase);

            // Prevent javascript: URLs in href/src.
            sanitized = Regex.Replace(
                sanitized,
                @"\s(href|src)\s*=\s*(['""])\s*javascript:[\s\S]*?\2",
                string.Empty,
                RegexOptions.IgnoreCase);

            return sanitized.Trim();
        }

        private static object MapComment(Comment comment)
        {
            var parsed = ParseCommentContent(comment.Content);
            return new
            {
                comment.Id,
                Content = parsed.Content,
                Reactions = parsed.Reactions,
                comment.ParentCommentId,
                comment.CreatedAt,
                comment.UpdatedAt,
                UserId = comment.UserId,
                FullName = comment.User.FullName ?? comment.User.Email,
                AvatarUrl = comment.User.AvatarUrl,
                Attachments = comment.CommentAttachments.Select(a => new
                {
                    a.Id,
                    a.FileName,
                    FileUrl = $"/api/private-attachments/comments/{a.Id}",
                    a.ContentType,
                    a.FileSize,
                    a.CreatedAt
                }).ToList()
            };
        }

        [HttpGet("{entityType}/{entityId}")]
        [HttpGet("comments/{entityType}/{entityId}")]
        public async Task<IActionResult> GetCommentsPolymorphic(string entityType, Guid entityId)
        {
            var allowedEntityTypes = new[]
            {
                "WorkTask", "Project", "Goal", "Risk", "Lesson", "Decision",
                "GoalUpdate", "ProjectUpdate",
                "GoalLesson", "GoalRisk", "GoalDecision",
                "ProjectLesson", "ProjectRisk", "ProjectDecision"
            };
            if (!allowedEntityTypes.Contains(entityType))
            {
                return BadRequest(new { message = "Loại Entity không hợp lệ." });
            }

            var comments = await _context.Comments
                .Where(c => c.EntityType == entityType && c.EntityId == entityId && !c.IsDeleted)
                .Include(c => c.User)
                .Include(c => c.CommentAttachments)
                .OrderBy(c => c.CreatedAt)
                .ToListAsync();

            return Ok(new { statusCode = 200, message = "Success", data = comments.Select(MapComment).ToList() });
        }

        [HttpGet("projects/{projectId}/WorkTasks/{taskId}/comments")]
        [ProjectAuthorize("")]
        public async Task<IActionResult> GetComments(Guid projectId, Guid taskId)
        {
            if (!await TaskBelongsToProjectAsync(projectId, taskId))
            {
                return NotFound(new { message = "Task khong ton tai trong du an nay." });
            }

            var comments = await _context.Comments
                .Where(c => c.EntityType == "WorkTask" && c.EntityId == taskId && !c.IsDeleted)
                .Include(c => c.User)
                .Include(c => c.CommentAttachments)
                .OrderBy(c => c.CreatedAt)
                .ToListAsync();

            return Ok(new { statusCode = 200, message = "Success", data = comments.Select(MapComment).ToList() });
        }

        [HttpPost("{entityType}/{entityId}")]
        [HttpPost("comments/{entityType}/{entityId}")]
        public async Task<IActionResult> CreateCommentPolymorphic(string entityType, Guid entityId, [FromForm] string content, [FromForm] Guid? parentCommentId, [FromForm] List<IFormFile>? files)
        {
            var allowedEntityTypes = new[]
            {
                "WorkTask", "Project", "Goal", "Risk", "Lesson", "Decision",
                "GoalUpdate", "ProjectUpdate",
                "GoalLesson", "GoalRisk", "GoalDecision",
                "ProjectLesson", "ProjectRisk", "ProjectDecision"
            };
            if (!allowedEntityTypes.Contains(entityType))
            {
                return BadRequest(new { message = "Loại Entity không hợp lệ." });
            }

            var userId = GetUserId();
            if (userId == null) return Unauthorized();

            if (string.IsNullOrWhiteSpace(content) && (files == null || files.Count == 0))
            {
                return BadRequest(new { message = "Comment phai co noi dung hoac file dinh kem." });
            }

            var comment = new Comment
            {
                Id = Guid.NewGuid(),
                EntityId = entityId,
                EntityType = entityType,
                UserId = userId.Value,
                Content = SanitizeRichHtml(content),
                ParentCommentId = parentCommentId,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };
            _context.Comments.Add(comment);

            if (files != null && files.Count > 0)
            {
                var uploadsDir = Path.Combine(_env.ContentRootPath, "uploads", "comments");
                if (!Directory.Exists(uploadsDir)) Directory.CreateDirectory(uploadsDir);

                foreach (var file in files)
                {
                    ValidatedUpload validated;
                    try { validated = await UploadSecurity.ReadPrivateFileAsync(file); }
                    catch (InvalidDataException exception) { return BadRequest(new { message = exception.Message }); }
                    var uniqueName = $"{Guid.NewGuid():N}{validated.Extension}";
                    var filePath = UploadSecurity.ResolveUnderRoot(uploadsDir, uniqueName);
                    await System.IO.File.WriteAllBytesAsync(filePath, validated.Bytes);

                    _context.CommentAttachments.Add(new CommentAttachment
                    {
                        Id = Guid.NewGuid(),
                        CommentId = comment.Id,
                        UploadedByUserId = userId.Value,
                        FileName = validated.OriginalFileName,
                        FileUrl = $"/uploads/comments/{uniqueName}",
                        ContentType = validated.MimeType,
                        FileSize = validated.Bytes.LongLength,
                        CreatedAt = DateTime.UtcNow
                    });
                }
            }

            _context.SiteAuditLogs.Add(new SiteAuditLog
            {
                EntityId = entityId,
                EntityType = entityType,
                Action = "AddComment",
                NewValue = SanitizeRichHtml(content),
                UserId = userId.Value,
                CreatedAt = DateTime.UtcNow
            });

            var followerIds = await _context.EntityFollowers
                .Where(f => f.EntityType == entityType && f.EntityId == entityId && f.UserId != userId.Value)
                .Select(f => f.UserId)
                .Distinct()
                .ToListAsync();

            foreach (var followerId in followerIds)
            {
                _context.Notifications.Add(new Notification
                {
                    Id = Guid.NewGuid(),
                    UserId = followerId,
                    Title = "Co binh luan moi",
                    Content = SanitizeRichHtml(content),
                    LinkUrl = entityType == "Goal" ? $"/home/goals/{entityId}" : entityType == "Project" ? $"/home/projects/{entityId}" : null,
                    CreatedAt = DateTime.UtcNow,
                    NotificationType = "COMMENT_ADDED",
                    TriggeredByUserId = userId.Value
                });
            }

            await _context.SaveChangesAsync();

            var createdComment = await _context.Comments
                .Where(c => c.Id == comment.Id)
                .Include(c => c.User)
                .Include(c => c.CommentAttachments)
                .FirstOrDefaultAsync();

            return Ok(new
            {
                statusCode = 201,
                message = "Da them binh luan.",
                data = createdComment == null ? null : MapComment(createdComment)
            });
        }

        [HttpPost("projects/{projectId}/WorkTasks/{taskId}/comments")]
        [ProjectAuthorize("")]
        public async Task<IActionResult> CreateComment(Guid projectId, Guid taskId, [FromForm] string content, [FromForm] Guid? parentCommentId, [FromForm] List<IFormFile>? files)
        {
            var userId = GetUserId();
            if (userId == null) return Unauthorized();

            if (string.IsNullOrWhiteSpace(content) && (files == null || files.Count == 0))
            {
                return BadRequest(new { message = "Comment phai co noi dung hoac file dinh kem." });
            }

            var task = await _context.WorkTasks.FirstOrDefaultAsync(t => t.Id == taskId && t.ProjectId == projectId && !t.IsDeleted);
            if (task == null)
            {
                return NotFound(new { message = "Task khong ton tai." });
            }

            var comment = new Comment
            {
                Id = Guid.NewGuid(),
                EntityId = taskId,
                EntityType = "WorkTask",
                UserId = userId.Value,
                Content = SanitizeRichHtml(content),
                ParentCommentId = parentCommentId,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };
            _context.Comments.Add(comment);

            if (files != null && files.Count > 0)
            {
                var uploadsDir = Path.Combine(_env.ContentRootPath, "uploads", "comments");
                if (!Directory.Exists(uploadsDir))
                {
                    Directory.CreateDirectory(uploadsDir);
                }

                foreach (var file in files)
                {
                    ValidatedUpload validated;
                    try { validated = await UploadSecurity.ReadPrivateFileAsync(file); }
                    catch (InvalidDataException exception) { return BadRequest(new { message = exception.Message }); }
                    var uniqueName = $"{Guid.NewGuid():N}{validated.Extension}";
                    var filePath = UploadSecurity.ResolveUnderRoot(uploadsDir, uniqueName);
                    await System.IO.File.WriteAllBytesAsync(filePath, validated.Bytes);

                    _context.CommentAttachments.Add(new CommentAttachment
                    {
                        Id = Guid.NewGuid(),
                        CommentId = comment.Id,
                        UploadedByUserId = userId.Value,
                        FileName = validated.OriginalFileName,
                        FileUrl = $"/uploads/comments/{uniqueName}",
                        ContentType = validated.MimeType,
                        FileSize = validated.Bytes.LongLength,
                        CreatedAt = DateTime.UtcNow
                    });
                }
            }

            _context.SiteAuditLogs.Add(new SiteAuditLog
            {
                EntityId = taskId,
                EntityType = "WorkTask",
                Action = "AddComment",
                NewValue = SanitizeRichHtml(content),
                UserId = userId.Value,
                CreatedAt = DateTime.UtcNow
            });

            await _context.SaveChangesAsync();

            var currentUser = await _context.Users.FindAsync(userId.Value);
            var projectName = await _context.Projects
                .Where(project => project.Id == projectId)
                .Select(project => project.Name)
                .FirstOrDefaultAsync();

            if (!string.IsNullOrWhiteSpace(projectName))
            {
                await TriggerNotificationEventAsync("/api/notifications/events/comment-added", new
                {
                    ProjectId = projectId,
                    TaskId = taskId,
                    ProjectName = projectName,
                    ActorName = currentUser?.FullName ?? currentUser?.Email ?? "System",
                    TaskTitle = task.Title
                });
            }

            var createdComment = await _context.Comments
                .Where(c => c.Id == comment.Id)
                .Include(c => c.User)
                .Include(c => c.CommentAttachments)
                .FirstOrDefaultAsync();

            return Ok(new
            {
                statusCode = 201,
                message = "Da them binh luan.",
                data = createdComment == null ? null : MapComment(createdComment)
            });
        }

        [HttpPost("projects/{projectId}/WorkTasks/{taskId}/comments/{commentId}/reactions")]
        [ProjectAuthorize("")]
        public async Task<IActionResult> AddReaction(Guid projectId, Guid taskId, Guid commentId, [FromBody] AddReactionRequest request)
        {
            var userId = GetUserId();
            if (userId == null) return Unauthorized();

            if (string.IsNullOrWhiteSpace(request.Emoji))
            {
                return BadRequest(new { message = "Emoji is required." });
            }

            if (!await TaskBelongsToProjectAsync(projectId, taskId))
            {
                return NotFound(new { message = "Task khong ton tai trong du an nay." });
            }

            var comment = await _context.Comments
                .Include(c => c.User)
                .Include(c => c.CommentAttachments)
                .FirstOrDefaultAsync(c => c.Id == commentId && c.EntityType == "WorkTask" && c.EntityId == taskId && !c.IsDeleted);

            if (comment == null)
            {
                return NotFound(new { message = "Comment khong ton tai." });
            }

            var parsed = ParseCommentContent(comment.Content);
            if (!parsed.Reactions.ContainsKey(request.Emoji))
            {
                parsed.Reactions[request.Emoji] = 0;
            }

            parsed.Reactions[request.Emoji] += 1;
            comment.Content = ComposeCommentContent(parsed.Content, parsed.Reactions);
            comment.UpdatedAt = DateTime.UtcNow;

            await _context.SaveChangesAsync();

            return Ok(new
            {
                statusCode = 200,
                message = "Da them reaction.",
                data = new
                {
                    comment.Id,
                    Reactions = parsed.Reactions
                }
            });
        }

        [HttpPut("projects/{projectId}/WorkTasks/{taskId}/comments/{commentId}")]
        [ProjectAuthorize("")]
        public async Task<IActionResult> UpdateComment(Guid projectId, Guid taskId, Guid commentId, [FromBody] UpdateCommentRequest request)
        {
            var userId = GetUserId();
            if (userId == null) return Unauthorized();

            if (!await TaskBelongsToProjectAsync(projectId, taskId))
            {
                return NotFound(new { message = "Task khong ton tai trong du an nay." });
            }

            var comment = await _context.Comments
                .FirstOrDefaultAsync(c => c.Id == commentId && c.EntityType == "WorkTask" && c.EntityId == taskId && !c.IsDeleted);

            if (comment == null) return NotFound(new { message = "Comment khong ton tai." });
            if (comment.UserId != userId.Value) return Forbid();

            var parsed = ParseCommentContent(comment.Content);
            comment.Content = ComposeCommentContent(SanitizeRichHtml(request.Content), parsed.Reactions);
            comment.UpdatedAt = DateTime.UtcNow;

            await _context.SaveChangesAsync();
            return Ok(new { statusCode = 200, message = "Da cap nhat binh luan." });
        }

        [HttpDelete("projects/{projectId}/WorkTasks/{taskId}/comments/{commentId}")]
        [ProjectAuthorize("")]
        public async Task<IActionResult> DeleteCommentNested(Guid projectId, Guid taskId, Guid commentId)
        {
            if (!await TaskBelongsToProjectAsync(projectId, taskId))
            {
                return NotFound(new { message = "Task khong ton tai trong du an nay." });
            }

            return await DeleteComment(commentId);
        }

        [HttpDelete("projects/{projectId}/WorkTasks/{taskId}/comments/{commentId}/attachments/{attachmentId}")]
        [ProjectAuthorize("")]
        public async Task<IActionResult> DeleteCommentAttachment(Guid projectId, Guid taskId, Guid commentId, Guid attachmentId)
        {
            var userId = GetUserId();
            if (userId == null) return Unauthorized();

            if (!await TaskBelongsToProjectAsync(projectId, taskId))
            {
                return NotFound(new { message = "Task khong ton tai trong du an nay." });
            }

            var comment = await _context.Comments
                .FirstOrDefaultAsync(c => c.Id == commentId && c.EntityType == "WorkTask" && c.EntityId == taskId && !c.IsDeleted);
            if (comment == null) return NotFound(new { message = "Comment khong ton tai." });
            if (comment.UserId != userId.Value) return Forbid();

            var attachment = await _context.CommentAttachments
                .FirstOrDefaultAsync(a => a.Id == attachmentId && a.CommentId == commentId);
            if (attachment == null) return NotFound(new { message = "Attachment khong ton tai." });

            _context.CommentAttachments.Remove(attachment);
            await _context.SaveChangesAsync();

            var attachmentRoot = Path.Combine(_env.ContentRootPath, "uploads", "comments");
            var absolutePath = UploadSecurity.ResolveUnderRoot(attachmentRoot, Path.GetFileName(attachment.FileUrl));
            if (System.IO.File.Exists(absolutePath))
            {
                System.IO.File.Delete(absolutePath);
            }

            return Ok(new { statusCode = 200, message = "Da xoa attachment." });
        }

        [HttpDelete("comments/{commentId}")]
        public async Task<IActionResult> DeleteComment(Guid commentId)
        {
            var userId = GetUserId();
            if (userId == null) return Unauthorized();

            var comment = await _context.Comments.FindAsync(commentId);
            if (comment == null) return NotFound();
            if (comment.UserId != userId.Value) return Forbid();

            comment.IsDeleted = true;
            comment.UpdatedAt = DateTime.UtcNow;
            await _context.SaveChangesAsync();

            return Ok(new { statusCode = 200, message = "Da xoa binh luan." });
        }

        [HttpPost("upload")]
        public async Task<IActionResult> Upload([FromForm] IFormFile file, [FromForm] string? folder)
        {
            await Task.CompletedTask;
            return BadRequest(new
            {
                message = "Unscoped comment uploads are disabled. Use the scoped comment attachment endpoint or /api/Uploads/file."
            });
        }
    }

    public class UpdateCommentRequest
    {
        public string? Content { get; set; }
    }

    public class AddReactionRequest
    {
        public string Emoji { get; set; } = string.Empty;
    }
}
