using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using System;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;
using TaskManagement.API.Hubs;
using TaskManagement.API.Realtime;
using TaskManagement.Infrastructure.Data;

namespace TaskManagement.API.Controllers
{
    [ApiController]
    [Route("api/notifications")]
    [Authorize]
    public class NotificationsController : ControllerBase
    {
        private readonly ApplicationDbContext _context;
        private readonly IHubContext<KanbanHub>? _hub;

        public NotificationsController(ApplicationDbContext context, IHubContext<KanbanHub>? hub = null)
        {
            _context = context;
            _hub = hub;
        }

        private Task PublishChangedAsync(Guid userId, string action, Guid entityId) =>
            _hub?.PublishUserEntityChangedAsync(userId, "Notification", action, entityId)
            ?? Task.CompletedTask;

        private Guid? GetUserId()
        {
            var claim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            return Guid.TryParse(claim, out var id) ? id : null;
        }

        [HttpGet]
        public async Task<IActionResult> GetNotifications([FromQuery] bool? unreadOnly)
        {
            var userId = GetUserId();
            if (userId == null) return Unauthorized();

            var query = _context.Notifications
                .AsNoTracking()
                .Where(n => n.UserId == userId.Value)
                .Where(n => n.CreatedAt >= DateTime.UtcNow.AddDays(-30));

            if (unreadOnly == true)
            {
                query = query.Where(n => !n.IsRead);
            }

            var notifications = await query
                .OrderByDescending(n => n.CreatedAt)
                .Take(50)
                .Select(n => new
                {
                    n.Id,
                    n.Title,
                    n.Content,
                    n.NotificationType,
                    n.LinkUrl,
                    n.IsRead,
                    n.RelatedTaskId,
                    n.RelatedProjectId,
                    n.CollaborationChannelId,
                    n.ChannelMessageId,
                    n.TriggeredByUserId,
                    TriggeredByName = n.TriggeredByUser != null ? n.TriggeredByUser.FullName : null,
                    TriggeredByAvatar = n.TriggeredByUser != null ? n.TriggeredByUser.AvatarUrl : null,
                    n.CreatedAt
                })
                .ToListAsync();

            return Ok(new
            {
                statusCode = 200,
                data = notifications,
                unreadCount = notifications.Count(n => !n.IsRead)
            });
        }

        [HttpGet("unread-count")]
        public async Task<IActionResult> GetUnreadCount()
        {
            var userId = GetUserId();
            if (userId == null) return Unauthorized();

            var count = await _context.Notifications.CountAsync(n => n.UserId == userId.Value && !n.IsRead);
            return Ok(new { statusCode = 200, data = count });
        }

        [HttpPut("{id}/read")]
        public async Task<IActionResult> MarkAsRead(Guid id)
        {
            var userId = GetUserId();
            if (userId == null) return Unauthorized();

            var notification = await _context.Notifications.FirstOrDefaultAsync(n => n.Id == id && n.UserId == userId.Value);
            if (notification == null) return NotFound();

            notification.IsRead = true;
            await _context.SaveChangesAsync();
            await PublishChangedAsync(userId.Value, "updated", notification.Id);
            return Ok(new { statusCode = 200, message = "Marked as read." });
        }

        [HttpPut("read-all")]
        public async Task<IActionResult> MarkAllAsRead()
        {
            var userId = GetUserId();
            if (userId == null) return Unauthorized();

            var unread = await _context.Notifications
                .Where(n => n.UserId == userId.Value && !n.IsRead)
                .ToListAsync();

            foreach (var notification in unread)
            {
                notification.IsRead = true;
            }

            await _context.SaveChangesAsync();
            await PublishChangedAsync(userId.Value, "reconcile", Guid.Empty);
            return Ok(new { statusCode = 200, message = "Marked all as read." });
        }

        [HttpPost("events/task-assigned")]
        public async Task<IActionResult> CreateTaskAssignedNotification([FromBody] TaskAssignedNotificationRequest request)
        {
            var actorId = GetUserId();
            if (actorId == null) return Unauthorized();
            if (request.AssigneeUserId == Guid.Empty || request.AssigneeUserId == actorId.Value) return Ok(new { statusCode = 200, message = "Skipped." });

            var notification = new TaskManagement.Domain.Entities.Notification
            {
                Id = Guid.NewGuid(),
                UserId = request.AssigneeUserId,
                Title = request.ProjectName ?? "Project",
                Content = $"{request.ActorName ?? "System"} assigned you to {request.TaskTitle ?? "a work item"}",
                NotificationType = "TASK_ASSIGNED",
                RelatedTaskId = request.TaskId,
                RelatedProjectId = request.ProjectId,
                TriggeredByUserId = actorId.Value,
                LinkUrl = $"/space/{request.ProjectId}?task={request.TaskId}",
                CreatedAt = DateTime.UtcNow,
                IsRead = false
            };
            _context.Notifications.Add(notification);

            await _context.SaveChangesAsync();
            await PublishChangedAsync(request.AssigneeUserId, "created", notification.Id);
            return Ok(new { statusCode = 200, message = "Created." });
        }

        [HttpPost("events/task-status-changed")]
        public async Task<IActionResult> CreateTaskStatusNotification([FromBody] TaskStatusNotificationRequest request)
        {
            var actorId = GetUserId();
            if (actorId == null) return Unauthorized();

            var targetUserIds = await _context.TaskAssignments
                .Where(ta => ta.WorkTaskId == request.TaskId && ta.Status && ta.UserId != actorId.Value)
                .Select(ta => ta.UserId)
                .Distinct()
                .ToListAsync();

            foreach (var targetUserId in targetUserIds)
            {
                _context.Notifications.Add(new TaskManagement.Domain.Entities.Notification
                {
                    Id = Guid.NewGuid(),
                    UserId = targetUserId,
                    Title = request.ProjectName ?? "Project",
                    Content = $"{request.ActorName ?? "System"} changed {request.TaskTitle ?? "a work item"} to {request.StatusName ?? "updated"}",
                    NotificationType = "TASK_STATUS_CHANGED",
                    RelatedTaskId = request.TaskId,
                    RelatedProjectId = request.ProjectId,
                    TriggeredByUserId = actorId.Value,
                    LinkUrl = $"/space/{request.ProjectId}?task={request.TaskId}",
                    CreatedAt = DateTime.UtcNow,
                    IsRead = false
                });
            }

            await _context.SaveChangesAsync();
            await Task.WhenAll(targetUserIds.Select(id => PublishChangedAsync(id, "created", Guid.Empty)));
            return Ok(new { statusCode = 200, message = "Created.", count = targetUserIds.Count });
        }

        [HttpPost("events/comment-added")]
        public async Task<IActionResult> CreateCommentNotification([FromBody] CommentNotificationRequest request)
        {
            var actorId = GetUserId();
            if (actorId == null) return Unauthorized();

            var targetUserIds = await _context.TaskAssignments
                .Where(ta => ta.WorkTaskId == request.TaskId && ta.Status && ta.UserId != actorId.Value)
                .Select(ta => ta.UserId)
                .Distinct()
                .ToListAsync();

            foreach (var targetUserId in targetUserIds)
            {
                _context.Notifications.Add(new TaskManagement.Domain.Entities.Notification
                {
                    Id = Guid.NewGuid(),
                    UserId = targetUserId,
                    Title = request.ProjectName ?? "Project",
                    Content = $"{request.ActorName ?? "System"} commented on {request.TaskTitle ?? "a work item"}",
                    NotificationType = "COMMENT_ADDED",
                    RelatedTaskId = request.TaskId,
                    RelatedProjectId = request.ProjectId,
                    TriggeredByUserId = actorId.Value,
                    LinkUrl = $"/space/{request.ProjectId}?task={request.TaskId}",
                    CreatedAt = DateTime.UtcNow,
                    IsRead = false
                });
            }

            await _context.SaveChangesAsync();
            await Task.WhenAll(targetUserIds.Select(id => PublishChangedAsync(id, "created", Guid.Empty)));
            return Ok(new { statusCode = 200, message = "Created.", count = targetUserIds.Count });
        }

        [HttpPost("events/task-reminded")]
        public async Task<IActionResult> CreateTaskRemindedNotification([FromBody] TaskRemindedNotificationRequest request)
        {
            var actorId = GetUserId();
            if (actorId == null) return Unauthorized();
            if (request.AssigneeUserId == Guid.Empty) return BadRequest("AssigneeUserId is required.");

            if (request.AssigneeUserId == actorId.Value) 
            {
                return Ok(new { skipped = true, message = "Không gửi nhắc việc cho chính bạn." });
            }

            var task = await _context.WorkTasks.FirstOrDefaultAsync(t => t.Id == request.TaskId && t.ProjectId == request.ProjectId);
            if (task == null) return NotFound("Task not found in the specified project.");

            var isAssigned = await _context.TaskAssignments.AnyAsync(ta => ta.WorkTaskId == request.TaskId && ta.UserId == request.AssigneeUserId && ta.Status);
            if (!isAssigned)
            {
                return BadRequest("Assignee is not assigned to this task or is inactive.");
            }

            var notificationId = Guid.NewGuid();
            _context.Notifications.Add(new TaskManagement.Domain.Entities.Notification
            {
                Id = notificationId,
                UserId = request.AssigneeUserId,
                Title = request.ProjectName ?? "Nhắc việc",
                Content = $"{request.ActorName ?? "Quản lý"} đã nhắc bạn xử lý công việc: {request.TaskTitle ?? task.Title}",
                NotificationType = "TASK_REMINDED",
                RelatedTaskId = request.TaskId,
                RelatedProjectId = request.ProjectId,
                TriggeredByUserId = actorId.Value,
                LinkUrl = $"/space/{request.ProjectId}?task={request.TaskId}",
                CreatedAt = DateTime.UtcNow,
                IsRead = false
            });

            await _context.SaveChangesAsync();
            await PublishChangedAsync(request.AssigneeUserId, "created", notificationId);
            return Ok(new
            {
                statusCode = 200,
                message = "Đã gửi nhắc việc.",
                data = new { notificationId }
            });
        }
    }

    public class TaskAssignedNotificationRequest
    {
        public Guid ProjectId { get; set; }
        public Guid TaskId { get; set; }
        public Guid AssigneeUserId { get; set; }
        public string? ProjectName { get; set; }
        public string? TaskTitle { get; set; }
        public string? ActorName { get; set; }
    }

    public class TaskStatusNotificationRequest
    {
        public Guid ProjectId { get; set; }
        public Guid TaskId { get; set; }
        public string? ProjectName { get; set; }
        public string? TaskTitle { get; set; }
        public string? StatusName { get; set; }
        public string? ActorName { get; set; }
    }

    public class CommentNotificationRequest
    {
        public Guid ProjectId { get; set; }
        public Guid TaskId { get; set; }
        public string? ProjectName { get; set; }
        public string? TaskTitle { get; set; }
        public string? ActorName { get; set; }
    }

    public class TaskRemindedNotificationRequest
    {
        public Guid ProjectId { get; set; }
        public Guid TaskId { get; set; }
        public Guid AssigneeUserId { get; set; }
        public string? ProjectName { get; set; }
        public string? TaskTitle { get; set; }
        public string? ActorName { get; set; }
    }
}
