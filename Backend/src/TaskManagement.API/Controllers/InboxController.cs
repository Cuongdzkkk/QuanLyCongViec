using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using TaskManagement.Application.DTOs.WorkTask;
using TaskManagement.Application.Interfaces;
using TaskManagement.Domain.Entities;
using TaskManagement.Infrastructure.Data;

namespace TaskManagement.API.Controllers
{
    [ApiController]
    [Route("api/inbox")]
    [Authorize]
    public class InboxController : ControllerBase
    {
        private readonly ApplicationDbContext _context;
        private readonly IWorkTaskService _workTaskService;
        private readonly IAiIntegrationService _aiIntegrationService;

        public InboxController(
            ApplicationDbContext context,
            IWorkTaskService workTaskService,
            IAiIntegrationService aiIntegrationService)
        {
            _context = context;
            _workTaskService = workTaskService;
            _aiIntegrationService = aiIntegrationService;
        }

        [HttpGet]
        public async Task<IActionResult> GetInbox([FromQuery] string? source)
        {
            var userId = GetUserId();
            if (userId == null) return Unauthorized();

            var query = _context.InboxItems
                .AsNoTracking()
                .Where(item => item.UserId == userId.Value);

            if (!string.IsNullOrWhiteSpace(source) && source != "all")
            {
                query = query.Where(item => item.Source == source);
            }

            var items = await query
                .OrderByDescending(item => item.StartsAt ?? item.CreatedAt)
                .Take(100)
                .Select(item => new
                {
                    item.Id,
                    item.Source,
                    item.Provider,
                    item.ExternalId,
                    item.Title,
                    item.Content,
                    item.Location,
                    item.StartsAt,
                    item.EndsAt,
                    item.IsRead,
                    item.CreatedTaskId,
                    CreatedTask = item.CreatedTask == null ? null : new
                    {
                        item.CreatedTask.Id,
                        item.CreatedTask.SequenceId,
                        item.CreatedTask.Title,
                        ProjectId = item.CreatedTask.ProjectId,
                        ProjectName = item.CreatedTask.Project.Name,
                        StatusName = item.CreatedTask.TaskStatus.Name,
                        AssigneeName = item.CreatedTask.AssignedUser != null ? item.CreatedTask.AssignedUser.FullName : null
                    },
                    item.CreatedAt,
                    item.UpdatedAt
                })
                .ToListAsync();

            return Ok(new { statusCode = 200, data = items });
        }

        [HttpGet("{id:guid}")]
        public async Task<IActionResult> GetInboxItem(Guid id)
        {
            var userId = GetUserId();
            if (userId == null) return Unauthorized();

            var item = await _context.InboxItems
                .AsNoTracking()
                .Where(inboxItem => inboxItem.Id == id && inboxItem.UserId == userId.Value)
                .Select(inboxItem => new
                {
                    inboxItem.Id,
                    inboxItem.Source,
                    inboxItem.Provider,
                    inboxItem.ExternalId,
                    inboxItem.Title,
                    inboxItem.Content,
                    inboxItem.Location,
                    inboxItem.StartsAt,
                    inboxItem.EndsAt,
                    inboxItem.IsRead,
                    inboxItem.CreatedTaskId,
                    CreatedTask = inboxItem.CreatedTask == null ? null : new
                    {
                        inboxItem.CreatedTask.Id,
                        inboxItem.CreatedTask.SequenceId,
                        inboxItem.CreatedTask.Title,
                        ProjectId = inboxItem.CreatedTask.ProjectId,
                        ProjectName = inboxItem.CreatedTask.Project.Name,
                        StatusName = inboxItem.CreatedTask.TaskStatus.Name,
                        AssigneeName = inboxItem.CreatedTask.AssignedUser != null ? inboxItem.CreatedTask.AssignedUser.FullName : null
                    },
                    inboxItem.CreatedAt,
                    inboxItem.UpdatedAt
                })
                .FirstOrDefaultAsync();

            if (item == null) return NotFound(new { message = "Không tìm thấy mục inbox" });
            return Ok(new { statusCode = 200, data = item });
        }

        [HttpGet("create-task-options")]
        public async Task<IActionResult> GetCreateTaskOptions()
        {
            var userId = GetUserId();
            if (userId == null) return Unauthorized();

            var projects = await _context.ProjectMembers
                .AsNoTracking()
                .Where(member => member.UserId == userId.Value
                    && member.Status
                    && !member.Project.IsDeleted
                    && !member.Project.IsArchived)
                .OrderBy(member => member.Project.Name)
                .Select(member => new
                {
                    id = member.ProjectId,
                    name = member.Project.Name,
                    key = member.Project.Identifier,
                    workspaceId = member.Project.WorkspaceId,
                    role = member.ProjectRole
                })
                .ToListAsync();

            return Ok(new { statusCode = 200, data = new { projects } });
        }

        [HttpPatch("{id:guid}/read")]
        public async Task<IActionResult> MarkRead(Guid id)
        {
            var userId = GetUserId();
            if (userId == null) return Unauthorized();

            var item = await _context.InboxItems.FirstOrDefaultAsync(inboxItem => inboxItem.Id == id && inboxItem.UserId == userId.Value);
            if (item == null) return NotFound(new { message = "Không tìm thấy mục inbox" });

            item.IsRead = true;
            item.UpdatedAt = DateTime.UtcNow;
            await _context.SaveChangesAsync();

            return Ok(new { statusCode = 200, data = new { item.Id, item.IsRead } });
        }

        [HttpPost("{id:guid}/create-task")]
        public async Task<IActionResult> CreateTask(Guid id, [FromBody] CreateTaskFromInboxRequest? request)
        {
            var userId = GetUserId();
            if (userId == null) return Unauthorized();

            var item = await _context.InboxItems.FirstOrDefaultAsync(inboxItem => inboxItem.Id == id && inboxItem.UserId == userId.Value);
            if (item == null) return NotFound(new { message = "Không tìm thấy mục inbox" });

            if (item.CreatedTaskId.HasValue)
            {
                return Conflict(new { message = "Mục inbox này đã tạo task rồi", taskId = item.CreatedTaskId.Value });
            }

            var projectId = request?.ProjectId;
            if (projectId == null)
            {
                return BadRequest(new { message = "Bạn cần chọn hoặc tạo project trước khi tạo task từ inbox." });
            }

            var created = await CreateWorkTaskFromInboxItem(userId.Value, item, projectId.Value);
            await _context.SaveChangesAsync();

            return Ok(new { statusCode = 200, data = created });
        }

        [HttpPost("create-tasks")]
        public async Task<IActionResult> CreateTasks([FromBody] CreateTasksFromInboxRequest? request)
        {
            var userId = GetUserId();
            if (userId == null) return Unauthorized();

            if (request?.ProjectId == null)
            {
                return BadRequest(new { message = "Bạn cần chọn project trước khi tạo nhiều task từ inbox." });
            }

            var requestedIds = (request.InboxItemIds ?? new List<Guid>())
                .Where(id => id != Guid.Empty)
                .Distinct()
                .ToList();

            if (requestedIds.Count == 0)
            {
                return BadRequest(new { message = "Hãy chọn ít nhất một mục inbox chưa tạo task." });
            }

            var items = await _context.InboxItems
                .Where(item => item.UserId == userId.Value && requestedIds.Contains(item.Id))
                .ToListAsync();

            var created = new List<WorkTaskResponseDto>();
            var skippedAlreadyCreated = items.Where(item => item.CreatedTaskId.HasValue).Select(item => item.Id).ToList();

            foreach (var item in items.Where(item => !item.CreatedTaskId.HasValue))
            {
                created.Add(await CreateWorkTaskFromInboxItem(userId.Value, item, request.ProjectId.Value));
            }

            await _context.SaveChangesAsync();

            var foundIds = items.Select(item => item.Id).ToHashSet();
            var missingIds = requestedIds.Where(id => !foundIds.Contains(id)).ToList();

            return Ok(new
            {
                statusCode = 200,
                data = new
                {
                    created,
                    createdCount = created.Count,
                    skippedAlreadyCreated,
                    missingIds
                }
            });
        }
        [HttpGet("{id:guid}/ai/summary")]
        public async Task<IActionResult> Summarize(Guid id)
        {
            var userId = GetUserId();
            if (userId == null) return Unauthorized();
            try
            {
                var result = await _aiIntegrationService.SummarizeInboxItemAsync(id, userId.Value);
                return Ok(new { statusCode = 200, data = result });
            }
            catch (InvalidOperationException ex)
            {
                return StatusCode(502, new { statusCode = 502, message = ex.Message });
            }
        }

        [HttpGet("{id:guid}/ai/suggest-task")]
        public async Task<IActionResult> SuggestTask(Guid id)
        {
            var userId = GetUserId();
            if (userId == null) return Unauthorized();
            try
            {
                var result = await _aiIntegrationService.SuggestTaskFromInboxItemAsync(id, userId.Value);
                return Ok(new { statusCode = 200, data = result });
            }
            catch (InvalidOperationException ex)
            {
                return StatusCode(502, new { statusCode = 502, message = ex.Message });
            }
        }

        [HttpGet("{id:guid}/ai/suggest-related-task")]
        public async Task<IActionResult> SuggestRelatedTask(Guid id)
        {
            var userId = GetUserId();
            if (userId == null) return Unauthorized();
            try
            {
                var result = await _aiIntegrationService.SuggestRelatedTaskAsync(id, userId.Value);
                return Ok(new { statusCode = 200, data = result });
            }
            catch (InvalidOperationException ex)
            {
                return StatusCode(502, new { statusCode = 502, message = ex.Message });
            }
        }

        private Guid? GetUserId()
        {
            var claim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            return Guid.TryParse(claim, out var id) ? id : null;
        }

        private async Task<WorkTaskResponseDto> CreateWorkTaskFromInboxItem(Guid userId, InboxItem item, Guid projectId)
        {
            var created = await _workTaskService.CreateAsync(userId, new CreateWorkTaskDto
            {
                ProjectId = projectId,
                Title = BuildTaskTitle(item),
                Description = BuildTaskDescription(item),
                TypeName = "Task",
                Priority = item.StartsAt.HasValue ? 2 : 3,
                StoryPoints = 0,
                DueDate = item.StartsAt
            });

            item.CreatedTaskId = created.Id;
            item.IsRead = true;
            item.UpdatedAt = DateTime.UtcNow;

            return created;
        }

        private static string BuildTaskTitle(InboxItem item)
        {
            var title = string.IsNullOrWhiteSpace(item.Title) ? "Untitled inbox item" : item.Title.Trim();

            return item.Source switch
            {
                "calendar" => title.StartsWith("[Calendar]", StringComparison.OrdinalIgnoreCase) ? title : $"[Calendar] {title}",
                "email" => title.StartsWith("[Email]", StringComparison.OrdinalIgnoreCase) ? title : $"[Email] {title}",
                "slack" => title.StartsWith("[Slack]", StringComparison.OrdinalIgnoreCase) ? title : $"[Slack] {title}",
                _ => title
            };
        }

        private static string BuildTaskDescription(InboxItem item)
        {
            var lines = new List<string>
            {
                $"Nguồn tích hợp: {item.Provider} / {item.Source}",
                $"Tiêu đề gốc: {(string.IsNullOrWhiteSpace(item.Title) ? "(không có tiêu đề)" : item.Title.Trim())}"
            };

            if (item.StartsAt.HasValue) lines.Add($"Bắt đầu: {item.StartsAt.Value:yyyy-MM-dd HH:mm}");
            if (item.EndsAt.HasValue) lines.Add($"Kết thúc: {item.EndsAt.Value:yyyy-MM-dd HH:mm}");
            if (!string.IsNullOrWhiteSpace(item.Location)) lines.Add($"Địa điểm: {item.Location.Trim()}");

            lines.Add("");
            lines.Add("Nội dung gốc:");
            lines.Add(string.IsNullOrWhiteSpace(item.Content) ? "(không có mô tả)" : item.Content.Trim());

            return string.Join(Environment.NewLine, lines);
        }
    }

    public class CreateTaskFromInboxRequest
    {
        public Guid? ProjectId { get; set; }
    }

    public class CreateTasksFromInboxRequest
    {
        public Guid? ProjectId { get; set; }
        public List<Guid>? InboxItemIds { get; set; }
    }
}
