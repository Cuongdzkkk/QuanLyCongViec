using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.SignalR;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Globalization;
using System.IO;
using System.Net.Http;
using System.Security.Claims;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using TaskManagement.API.Filters;
using TaskManagement.Application.Common;
using TaskManagement.API.Hubs;
using TaskManagement.Application.DTOs.WorkTask;
using TaskManagement.Application.Interfaces;
using TaskManagement.Infrastructure.Data;
using TaskManagement.Domain.Entities;
using TaskManagement.Domain.Rules;

namespace TaskManagement.API.Controllers
{
    [ApiController]
    [Route("api")]
    [Authorize]
    public class WorkTasksController : ControllerBase
    {
        private readonly IWorkTaskService _workTaskService;
        private readonly IHubContext<KanbanHub> _kanbanHub;
        private static readonly string[] BaselineManagerRoles = { "PM", "PO", "SM", "PA", "PROJECT_MANAGER", "SCRUM_MASTER", "PROJECT_ADMIN" };
        private const string TaskVisibilitySettingGroup = "TaskVisibility";

        public WorkTasksController(IWorkTaskService workTaskService, IHubContext<KanbanHub> kanbanHub)
        {
            _workTaskService = workTaskService;
            _kanbanHub = kanbanHub;
        }

        private static string NormalizeStatusName(string? statusName)
        {
            if (string.IsNullOrWhiteSpace(statusName))
            {
                return "BACKLOG";
            }

            var upper = statusName.ToUpperInvariant().Replace(" ", string.Empty);
            if (upper.Contains("CANCEL"))
            {
                return "CANCELLED";
            }

            if (upper.Contains("DONE") || upper.Contains("COMPLETE"))
            {
                return "DONE";
            }

            if (upper.Contains("INREVIEW") || upper.Contains("REVIEW"))
            {
                return "IN REVIEW";
            }

            if (upper.Contains("INPROGRESS") || upper.Contains("ACTIVE"))
            {
                return "IN PROGRESS";
            }

            if (upper.Contains("TODO"))
            {
                return "TO DO";
            }

            return "BACKLOG";
        }

        private static readonly (string Name, int Position)[] DefaultTaskStatuses =
        {
            ("BACKLOG", 0),
            ("TO DO", 1),
            ("IN PROGRESS", 2),
            ("IN REVIEW", 3),
            ("DONE", 4),
            ("CANCELLED", 5)
        };

        private static string ResolveDefaultStatusColor(string statusName)
        {
            return NormalizeStatusName(statusName) switch
            {
                "TO DO" => "#3b82f6",
                "IN PROGRESS" => "#f59e0b",
                "IN REVIEW" => "#8b5cf6",
                "DONE" => "#10b981",
                "CANCELLED" => "#ef4444",
                _ => "#64748b"
            };
        }

        private static DateTime? ParseDateOnlyUtc(System.Text.Json.JsonElement property)
        {
            if (property.ValueKind == System.Text.Json.JsonValueKind.Null)
            {
                return null;
            }

            var raw = property.GetString();
            if (string.IsNullOrWhiteSpace(raw))
            {
                return null;
            }

            if (DateTime.TryParse(raw, out var parsed))
            {
                return DateTime.SpecifyKind(parsed.Date, DateTimeKind.Utc);
            }

            return null;
        }

        private static async Task<WorkTaskResponseDto?> LoadTaskResponseAsync(
            ApplicationDbContext context,
            Guid projectId,
            Guid taskId,
            Guid userId)
        {
            var task = await context.WorkTasks
                .AsNoTracking()
                .Where(wt => wt.Id == taskId && wt.ProjectId == projectId && !wt.IsDeleted)
                .Select(wt => new WorkTaskResponseDto
                {
                    Id = wt.Id,
                    ProjectId = wt.ProjectId,
                    SprintId = wt.SprintId,
                    Title = wt.Title,
                    Description = wt.Description,
                    Priority = wt.Priority,
                    StoryPoints = wt.StoryPoints,
                    StatusName = wt.TaskStatus.Name,
                    TaskStatusId = wt.TaskStatusId,
                    TaskTypeName = wt.TaskType.Name,
                    TypeName = wt.TaskType.Name,
                    TaskTypeId = wt.TaskTypeId,
                    AssigneeName = wt.AssignedUser != null ? wt.AssignedUser.FullName : null,
                    AssignedUserId = wt.AssignedUserId,
                    Assignees = wt.TaskAssignments
                        .Where(ta => ta.Status)
                        .Select(ta => new TaskAssigneeDto
                        {
                            UserId = ta.UserId,
                            FullName = ta.User.FullName,
                            Email = ta.User.Email,
                            ProgressPercent = ta.ProgressPercent,
                            ContributionWeight = ta.ContributionWeight,
                            EstimatedHours = ta.EstimatedHours,
                            TotalActualHours = ta.TotalActualHours,
                            IsBlocked = ta.BlockedByUserId.HasValue,
                            BlockedByUserId = ta.BlockedByUserId,
                            BlockReason = ta.BlockReason
                        })
                        .ToList(),
                    ModuleId = wt.IssueModules
                        .OrderBy(im => im.AssignedAt)
                        .Select(im => (Guid?)im.ModuleId)
                        .FirstOrDefault(),
                    LabelIds = wt.IssueLabels
                        .Select(il => il.LabelId)
                        .ToList(),
                    ReporterName = wt.Reporter.FullName ?? wt.Reporter.Email,
                    ReporterId = wt.ReporterId,
                    PlannedStartDate = wt.PlannedStartDate,
                    PlannedEndDate = wt.PlannedEndDate,
                    DueDate = wt.DueDate,
                    TotalEstimatedHours = wt.TotalEstimatedHours,
                    TotalActualHours = wt.TotalActualHours,
                    ParentTaskId = wt.ParentTaskId,
                    RowVersion = wt.RowVersion,
                    CreatedAt = wt.CreatedAt,
                    UpdatedAt = wt.UpdatedAt,
                    ProjectName = wt.Project.Name,
                    SortOrder = wt.SortOrder,
                    SequenceId = wt.SequenceId,
                    IsSubscribed = wt.Subscribers.Any(s => s.UserId == userId)
                })
                .FirstOrDefaultAsync();

            if (task != null)
            {
                task.StatusName = NormalizeStatusName(task.StatusName);
                var visibilitySetting = await context.SystemSettings
                    .AsNoTracking()
                    .FirstOrDefaultAsync(item => item.SettingGroup == TaskVisibilitySettingGroup && item.Key == task.Id.ToString());
                if (visibilitySetting != null)
                {
                    var visibility = ProjectExecutionRuleHelper.ParseTaskVisibility(visibilitySetting.Value);
                    task.VisibilityMode = visibility.Mode;
                    task.VisibleToRoles = visibility.Roles;
                }
            }

            return task;
        }

        private static Dictionary<string, object?> ParseNavigationConfig(string? raw)
        {
            if (string.IsNullOrWhiteSpace(raw))
            {
                return new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);
            }

            try
            {
                return JsonSerializer.Deserialize<Dictionary<string, object?>>(raw)
                    ?? new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);
            }
            catch
            {
                return new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);
            }
        }

        private static async Task<TaskVisibilityDto> LoadTaskVisibilityAsync(ApplicationDbContext context, Guid taskId)
        {
            var setting = await context.SystemSettings
                .AsNoTracking()
                .FirstOrDefaultAsync(item => item.SettingGroup == TaskVisibilitySettingGroup && item.Key == taskId.ToString());

            return setting == null
                ? ProjectExecutionRuleHelper.NormalizeTaskVisibility(new TaskVisibilityDto())
                : ProjectExecutionRuleHelper.ParseTaskVisibility(setting.Value);
        }

        private static async Task SaveTaskVisibilityAsync(
            ApplicationDbContext context,
            Guid taskId,
            string? requestedMode,
            IEnumerable<string>? requestedRoles)
        {
            var visibility = ProjectExecutionRuleHelper.NormalizeTaskVisibility(new TaskVisibilityDto
            {
                Mode = requestedMode ?? "project",
                Roles = requestedRoles?.ToList() ?? new List<string>()
            });

            var setting = await context.SystemSettings
                .FirstOrDefaultAsync(item => item.SettingGroup == TaskVisibilitySettingGroup && item.Key == taskId.ToString());

            if (setting == null)
            {
                setting = new TaskManagement.Domain.Entities.SystemSetting
                {
                    Id = Guid.NewGuid(),
                    SettingGroup = TaskVisibilitySettingGroup,
                    Key = taskId.ToString(),
                    Description = "Task visibility configuration"
                };
                context.SystemSettings.Add(setting);
            }

            setting.Value = ProjectExecutionRuleHelper.BuildTaskVisibilityPayload(visibility);
            setting.LastModifiedAt = DateTime.UtcNow;
            await context.SaveChangesAsync();
        }

        private static bool HasSystemAdminAccess(ClaimsPrincipal user)
        {
            return user.Claims
                .Where(claim => claim.Type == ClaimTypes.Role)
                .Select(claim => claim.Value)
                .Any(role =>
                    role.Equals("Admin", StringComparison.OrdinalIgnoreCase) ||
                    role.Equals("SuperAdmin", StringComparison.OrdinalIgnoreCase) ||
                    role.Equals("SystemAdmin", StringComparison.OrdinalIgnoreCase) ||
                    role.Equals("System Administrator", StringComparison.OrdinalIgnoreCase));
        }

        private static bool HasProjectManagerAccess(string? projectRole)
        {
            if (string.IsNullOrWhiteSpace(projectRole))
            {
                return false;
            }

            return BaselineManagerRoles.Contains(projectRole.Trim(), StringComparer.OrdinalIgnoreCase);
        }

        private static string BuildPlanningBaselinePayload(
            string? rawNavigationConfig,
            object payload)
        {
            var config = ParseNavigationConfig(rawNavigationConfig);
            config["planningBaseline"] = payload;
            return JsonSerializer.Serialize(config);
        }

        private async Task BroadcastTaskUpdatedAsync(Guid projectId, WorkTaskResponseDto? task)
        {
            if (task == null)
            {
                return;
            }

            var groupName = projectId.ToString();
            await _kanbanHub.Clients.Group(groupName).SendAsync("TaskUpdated", task);
            await _kanbanHub.Clients.Group(groupName).SendAsync("WorkTaskUpdated", task);
        }

        private static bool IsDoneStatusName(string? statusName)
        {
            if (string.IsNullOrWhiteSpace(statusName))
            {
                return false;
            }

            var normalized = NormalizeStatusName(statusName);
            return normalized == "DONE" || normalized.Contains("COMPLETE", StringComparison.OrdinalIgnoreCase);
        }

        private static async Task<bool> HasIncompleteSubtasksAsync(ApplicationDbContext context, Guid taskId)
        {
            return await context.WorkTasks
                .AsNoTracking()
                .Where(child => child.ParentTaskId == taskId && !child.IsDeleted)
                .AnyAsync(child =>
                    child.TaskStatus == null ||
                    !child.TaskStatus.Name.ToUpper().Contains("DONE") ||
                    child.TaskAssignments.Any(assignment => assignment.Status && assignment.ProgressPercent < 100));
        }

        private static void CompleteActiveAssignments(TaskManagement.Domain.Entities.WorkTask task)
        {
            foreach (var assignment in task.TaskAssignments.Where(item => item.Status))
            {
                assignment.ProgressPercent = 100;
                assignment.ProgressUpdatedAt = DateTime.UtcNow;
            }
        }

        private static async Task EnsureDefaultTaskStatusesAsync(ApplicationDbContext context, Guid projectId)
        {
            var existingStatuses = await context.TaskStatuses
                .Where(ts => ts.ProjectId == projectId)
                .ToListAsync();

            var existingNormalized = existingStatuses
                .Select(ts => NormalizeStatusName(ts.Name))
                .ToHashSet(StringComparer.OrdinalIgnoreCase);

            var missingStatuses = DefaultTaskStatuses
                .Where(status => !existingNormalized.Contains(status.Name))
                .ToList();

            if (!missingStatuses.Any())
            {
                return;
            }

            foreach (var status in missingStatuses)
            {
                context.TaskStatuses.Add(new TaskManagement.Domain.Entities.TaskStatus
                {
                    Id = Guid.NewGuid(),
                    ProjectId = projectId,
                    Name = status.Name,
                    ColorCode = ResolveDefaultStatusColor(status.Name),
                    Position = status.Position
                });
            }

            await context.SaveChangesAsync();
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
                // Notification event failures must not block task updates.
            }
        }

        private async Task<NotificationActorContext?> BuildNotificationActorContextAsync(
            ApplicationDbContext context,
            Guid projectId,
            Guid actorUserId,
            string taskTitle)
        {
            var projectName = await context.Projects
                .Where(project => project.Id == projectId)
                .Select(project => project.Name)
                .FirstOrDefaultAsync();

            var actorName = await context.Users
                .Where(user => user.Id == actorUserId)
                .Select(user => user.FullName ?? user.Email)
                .FirstOrDefaultAsync();

            if (string.IsNullOrWhiteSpace(projectName))
            {
                return null;
            }

            return new NotificationActorContext
            {
                ActorName = string.IsNullOrWhiteSpace(actorName) ? "System" : actorName,
                ProjectName = projectName,
                TaskTitle = taskTitle
            };
        }

        private async Task TriggerTaskAssignedNotificationsAsync(
            ApplicationDbContext context,
            Guid projectId,
            Guid taskId,
            Guid actorUserId,
            string taskTitle,
            IEnumerable<Guid> assigneeIds)
        {
            var assigneeList = assigneeIds.Distinct().ToList();
            if (!assigneeList.Any())
            {
                return;
            }

            var actorContext = await BuildNotificationActorContextAsync(context, projectId, actorUserId, taskTitle);
            if (actorContext == null)
            {
                return;
            }

            foreach (var assigneeId in assigneeList)
            {
                await TriggerNotificationEventAsync("/api/notifications/events/task-assigned", new
                {
                    AssigneeUserId = assigneeId,
                    ProjectId = projectId,
                    TaskId = taskId,
                    ProjectName = actorContext.ProjectName,
                    ActorName = actorContext.ActorName,
                    TaskTitle = actorContext.TaskTitle
                });
            }
        }

        private async Task TriggerTaskStatusChangedNotificationAsync(
            ApplicationDbContext context,
            Guid projectId,
            Guid taskId,
            Guid actorUserId,
            string taskTitle,
            string? statusName)
        {
            if (string.IsNullOrWhiteSpace(statusName))
            {
                return;
            }

            var actorContext = await BuildNotificationActorContextAsync(context, projectId, actorUserId, taskTitle);
            if (actorContext == null)
            {
                return;
            }

            await TriggerNotificationEventAsync("/api/notifications/events/task-status-changed", new
            {
                ProjectId = projectId,
                TaskId = taskId,
                ProjectName = actorContext.ProjectName,
                ActorName = actorContext.ActorName,
                TaskTitle = actorContext.TaskTitle,
                StatusName = statusName
            });
        }

        [HttpGet("projects/{projectId}/WorkTasks")]
        [ProjectAuthorize("")]
        public async Task<IActionResult> GetByProject(Guid projectId)
        {
            try
            {
                var userIdString = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
                if (!Guid.TryParse(userIdString, out Guid userId))
                {
                    return Unauthorized(new { statusCode = 401, message = "Vui lòng đăng nhập." });
                }

                var tasks = await _workTaskService.GetByProjectAsync(projectId, userId);
                return Ok(new { statusCode = 200, message = "Success", data = tasks });
            }
            catch (UnauthorizedAccessException ex)
            {
                return StatusCode(403, new { statusCode = 403, message = ex.Message });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { statusCode = 500, message = "Lỗi máy chủ nội bộ: " + ex.Message, stack = ex.ToString() });
            }
        }

        [HttpGet("tasks/my-tasks")]
        public async Task<IActionResult> GetMyTasks()
        {
            try
            {
                var userIdString = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
                if (!Guid.TryParse(userIdString, out Guid userId))
                {
                    return Unauthorized(new { statusCode = 401, message = "Token không hợp lệ." });
                }

                var tasks = await _workTaskService.GetMyTasksAsync(userId);
                return Ok(new { statusCode = 200, message = "Success", data = tasks });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { statusCode = 500, message = "Lỗi máy chủ: " + ex.Message });
            }
        }

        [HttpGet("tasks/search")]
        public async Task<IActionResult> SearchTasks([FromQuery] string? query, [FromQuery] string? status, [FromQuery] Guid? assigneeId, [FromQuery] int? priority, [FromQuery] Guid? projectId, [FromQuery] string? scope = "all")
        {
            try
            {
                var userIdString = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
                if (!Guid.TryParse(userIdString, out Guid userId))
                {
                    return Unauthorized(new { statusCode = 401, message = "Token không hợp lệ." });
                }

                var tasks = await _workTaskService.SearchTasksAsync(userId, query, status, assigneeId, priority, projectId, scope);
                return Ok(new { statusCode = 200, message = "Success", data = tasks });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { statusCode = 500, message = "Lỗi máy chủ: " + ex.Message });
            }
        }

        [HttpGet("projects/{projectId}/task-statuses")]
        [ProjectAuthorize("")]
        public async Task<IActionResult> GetProjectTaskStatuses(Guid projectId, [FromServices] ApplicationDbContext context)
        {
            try
            {
                await EnsureDefaultTaskStatusesAsync(context, projectId);

                var statuses = await context.TaskStatuses
                    .Where(ts => ts.ProjectId == projectId)
                    .OrderBy(ts => ts.Position)
                    .Select(ts => new
                    {
                        ts.Id,
                        ts.Name,
                        DisplayName = ts.Name
                    })
                    .ToListAsync();

                return Ok(new { statusCode = 200, message = "Success", data = statuses });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { statusCode = 500, message = ex.Message });
            }
        }

        [HttpPost("projects/{projectId}/task-statuses")]
        [ProjectAuthorize("PROJECT_MANAGER,PROJECT_LEAD,PM,PO,Admin")]
        public async Task<IActionResult> CreateProjectTaskStatus(Guid projectId, [FromBody] SaveTaskStatusRequest request, [FromServices] ApplicationDbContext context)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(request.Name))
                {
                    return BadRequest(new { statusCode = 400, message = "Ten trang thai khong de trong." });
                }

                await EnsureDefaultTaskStatusesAsync(context, projectId);

                var requestedName = request.Name.Trim();
                var existingNames = await context.TaskStatuses
                    .Where(status => status.ProjectId == projectId)
                    .Select(status => status.Name)
                    .ToListAsync();
                var exists = existingNames.Any(name =>
                    string.Equals(name?.Trim(), requestedName, StringComparison.OrdinalIgnoreCase));
                if (exists)
                {
                    return Conflict(new { statusCode = 409, message = "Trang thai da ton tai trong project." });
                }

                var maxPosition = await context.TaskStatuses
                    .Where(status => status.ProjectId == projectId)
                    .Select(status => (int?)status.Position)
                    .MaxAsync() ?? 0;

                var status = new TaskManagement.Domain.Entities.TaskStatus
                {
                    Id = Guid.NewGuid(),
                    ProjectId = projectId,
                    Name = requestedName,
                    ColorCode = string.IsNullOrWhiteSpace(request.ColorCode) ? ResolveDefaultStatusColor(requestedName) : request.ColorCode.Trim(),
                    Position = request.Position ?? (maxPosition + 1)
                };

                context.TaskStatuses.Add(status);
                await context.SaveChangesAsync();

                return Ok(new
                {
                    statusCode = 201,
                    message = "Da tao trang thai.",
                    data = new { status.Id, status.Name, status.ColorCode, status.Position }
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { statusCode = 500, message = ex.Message });
            }
        }

        [HttpPut("projects/{projectId}/task-statuses/{statusId}")]
        [ProjectAuthorize("PROJECT_MANAGER,PROJECT_LEAD,PM,PO,Admin")]
        public async Task<IActionResult> UpdateProjectTaskStatus(Guid projectId, Guid statusId, [FromBody] SaveTaskStatusRequest request, [FromServices] ApplicationDbContext context)
        {
            try
            {
                var status = await context.TaskStatuses.FirstOrDefaultAsync(item => item.Id == statusId && item.ProjectId == projectId);
                if (status == null)
                {
                    return NotFound(new { statusCode = 404, message = "Trang thai khong ton tai." });
                }

                if (!string.IsNullOrWhiteSpace(request.Name))
                {
                    var requestedName = request.Name.Trim();
                    var existingNames = await context.TaskStatuses
                        .Where(item => item.ProjectId == projectId && item.Id != statusId)
                        .Select(item => item.Name)
                        .ToListAsync();
                    var duplicateExists = existingNames.Any(name =>
                        string.Equals(name?.Trim(), requestedName, StringComparison.OrdinalIgnoreCase));
                    if (duplicateExists)
                    {
                        return Conflict(new { statusCode = 409, message = "Trang thai da ton tai trong project." });
                    }

                    status.Name = requestedName;
                }

                status.ColorCode = string.IsNullOrWhiteSpace(request.ColorCode) ? status.ColorCode ?? ResolveDefaultStatusColor(status.Name) : request.ColorCode.Trim();
                if (request.Position.HasValue)
                {
                    status.Position = request.Position.Value;
                }

                await context.SaveChangesAsync();

                return Ok(new
                {
                    statusCode = 200,
                    message = "Da cap nhat trang thai.",
                    data = new { status.Id, status.Name, status.ColorCode, status.Position }
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { statusCode = 500, message = ex.Message });
            }
        }

        [HttpDelete("projects/{projectId}/task-statuses/{statusId}")]
        [ProjectAuthorize("PROJECT_MANAGER,PROJECT_LEAD,PM,PO,Admin")]
        public async Task<IActionResult> DeleteProjectTaskStatus(Guid projectId, Guid statusId, [FromServices] ApplicationDbContext context)
        {
            var status = await context.TaskStatuses.FirstOrDefaultAsync(item => item.Id == statusId && item.ProjectId == projectId);
            if (status == null)
            {
                return NotFound(new { statusCode = 404, message = "Trang thai khong ton tai." });
            }

            var isInUse = await context.WorkTasks.AnyAsync(task => task.ProjectId == projectId && task.TaskStatusId == statusId && !task.IsDeleted);
            if (isInUse)
            {
                return BadRequest(new { statusCode = 400, message = "Khong the xoa trang thai dang duoc task su dung." });
            }

            context.TaskStatuses.Remove(status);
            await context.SaveChangesAsync();
            return Ok(new { statusCode = 200, message = "Da xoa trang thai." });
        }

        [HttpPost("projects/{projectId}/WorkTasks")]
        [ProjectAuthorize("")]
        public async Task<IActionResult> Create(Guid projectId, [FromBody] CreateWorkTaskDto request)
        {
            request.ProjectId = projectId;
            try
            {
                var userIdString = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
                if (string.IsNullOrEmpty(userIdString) || !Guid.TryParse(userIdString, out Guid reporterId))
                {
                    return Unauthorized(new ProblemDetails
                    {
                        Status = StatusCodes.Status401Unauthorized,
                        Title = "Authentication required",
                        Detail = "A valid authenticated reporter context is required."
                    });
                }
                
                var result = await _workTaskService.CreateAsync(reporterId, request);
                await BroadcastTaskUpdatedAsync(projectId, result);
                return CreatedAtAction(nameof(GetByProject), new { projectId }, new { statusCode = 201, message = "Tạo tác vụ thành công.", data = result });
            }
            catch (ArgumentException ex)
            {
                return BadRequest(new { statusCode = 400, message = ex.Message });
            }
            catch (InvalidOperationException ex)
            {
                return BadRequest(new { statusCode = 400, message = ex.Message });
            }
            catch (UnauthorizedAccessException ex)
            {
                return StatusCode(403, new { statusCode = 403, message = ex.Message });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { statusCode = 500, message = "Lỗi máy chủ nội bộ: " + ex.Message, stack = ex.ToString() });
            }
        }

        [HttpPut("projects/{projectId}/WorkTasks/{id}/status")]
        [ProjectAuthorize("")]
        public async Task<IActionResult> UpdateStatus(Guid projectId, Guid id, [FromBody] UpdateTaskStatusRequestDto request, [FromServices] ApplicationDbContext context)
        {
            try
            {
                if (request.RowVersion == null || request.RowVersion.Length == 0)
                {
                    return BadRequest(new { statusCode = 400, code = "TASK_VERSION_REQUIRED", message = "RowVersion is required. Reload the task before updating it." });
                }

                var userIdString = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
                if (!Guid.TryParse(userIdString, out Guid userId))
                {
                    return Unauthorized(new { statusCode = 401, message = "Vui long dang nhap." });
                }

                var previousTask = await context.WorkTasks
                    .AsNoTracking()
                    .Include(wt => wt.TaskStatus)
                    .FirstOrDefaultAsync(wt => wt.Id == id && wt.ProjectId == projectId && !wt.IsDeleted);
                if (previousTask == null)
                {
                    return NotFound(new { statusCode = 404, message = "Task khong ton tai trong du an nay." });
                }

                await _workTaskService.UpdateTaskStatusAsync(id, userId, request);

                var updatedTask = await context.WorkTasks
                    .AsNoTracking()
                    .Include(wt => wt.TaskStatus)
                    .FirstOrDefaultAsync(wt => wt.Id == id && wt.ProjectId == projectId && !wt.IsDeleted);

                if (updatedTask != null && !string.Equals(previousTask.TaskStatus?.Name, updatedTask.TaskStatus?.Name, StringComparison.OrdinalIgnoreCase))
                {
                    await TriggerTaskStatusChangedNotificationAsync(
                        context,
                        projectId,
                        id,
                        userId,
                        updatedTask.Title,
                        updatedTask.TaskStatus?.Name);
                }
                var savedTask = await LoadTaskResponseAsync(context, projectId, id, userId);
                await BroadcastTaskUpdatedAsync(projectId, savedTask);
                return Ok(new { statusCode = 200, message = "Cập nhật trạng thái tác vụ thành công.", data = savedTask });
            }
            catch (DbUpdateConcurrencyException)
            {
                var current = await LoadTaskResponseAsync(context, projectId, id, Guid.Empty);
                return Conflict(new
                {
                    statusCode = 409,
                    code = "TASK_VERSION_CONFLICT",
                    message = "Task was changed by another request. Reload before applying your changes.",
                    currentVersion = current?.RowVersion,
                    currentData = current
                });
            }
            catch (InvalidOperationException ex)
            {
                return BadRequest(new { statusCode = 400, message = ex.Message });
            }
            catch (ArgumentException ex)
            {
                return BadRequest(new { statusCode = 400, message = ex.Message });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { statusCode = 500, message = "Lỗi máy chủ nội bộ: " + ex.Message });
            }
        }

        [HttpPut("projects/{projectId}/WorkTasks/{id}")]
        [ProjectAuthorize("")]
        public async Task<IActionResult> Update(Guid projectId, Guid id, [FromBody] UpdateWorkTaskDto dto, [FromServices] ApplicationDbContext context)
        {
            try
            {
                var userIdString = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
                if (!Guid.TryParse(userIdString, out Guid userId))
                {
                    return Unauthorized(new { statusCode = 401, message = "Vui lòng đăng nhập." });
                }

                var taskExistsInProject = await context.WorkTasks
                    .AnyAsync(wt => wt.Id == id && wt.ProjectId == projectId && !wt.IsDeleted);
                if (!taskExistsInProject)
                {
                    return NotFound(new { statusCode = 404, message = "Task khong ton tai trong du an nay." });
                }

                if (dto.RowVersion == null || dto.RowVersion.Length == 0)
                {
                    return BadRequest(new { statusCode = 400, message = "Thieu RowVersion. Vui long tai lai cong viec truoc khi cap nhat." });
                }

                var previousTask = await context.WorkTasks
                    .AsNoTracking()
                    .Include(wt => wt.TaskAssignments)
                    .FirstOrDefaultAsync(wt => wt.Id == id && wt.ProjectId == projectId && !wt.IsDeleted);
                var previousAssigneeIds = new HashSet<Guid>();
                if (previousTask?.AssignedUserId is Guid previousAssignedUserId)
                {
                    previousAssigneeIds.Add(previousAssignedUserId);
                }

                if (previousTask != null)
                {
                    foreach (var assignment in previousTask.TaskAssignments.Where(ta => ta.Status))
                    {
                        previousAssigneeIds.Add(assignment.UserId);
                    }
                }

                var result = await _workTaskService.UpdateAsync(id, userId, dto);

                var updatedTask = await context.WorkTasks
                    .AsNoTracking()
                    .Include(wt => wt.TaskAssignments)
                    .FirstOrDefaultAsync(wt => wt.Id == id && wt.ProjectId == projectId && !wt.IsDeleted);

                if (updatedTask != null)
                {
                    var currentAssigneeIds = updatedTask.TaskAssignments
                        .Where(ta => ta.Status)
                        .Select(ta => ta.UserId)
                        .ToHashSet();

                    if (updatedTask.AssignedUserId is Guid currentAssignedUserId)
                    {
                        currentAssigneeIds.Add(currentAssignedUserId);
                    }

                    var newAssigneeIds = currentAssigneeIds
                        .Except(previousAssigneeIds)
                        .ToList();

                    if (newAssigneeIds.Any())
                    {
                        await TriggerTaskAssignedNotificationsAsync(
                            context,
                            projectId,
                            id,
                            userId,
                            updatedTask.Title,
                            newAssigneeIds);
                    }
                }
                await BroadcastTaskUpdatedAsync(projectId, result);
                return Ok(new { statusCode = 200, message = "Cập nhật công việc thành công.", data = result });
            }
            catch (DbUpdateConcurrencyException)
            {
                var current = await LoadTaskResponseAsync(context, projectId, id, Guid.Empty);
                return Conflict(new
                {
                    statusCode = 409,
                    code = "TASK_VERSION_CONFLICT",
                    message = "Task was changed by another request. Reload before applying your changes.",
                    currentVersion = current?.RowVersion,
                    currentData = current
                });
            }
            catch (UnauthorizedAccessException ex)
            {
                return StatusCode(403, new { statusCode = 403, message = ex.Message });
            }
            catch (InvalidOperationException ex)
            {
                return BadRequest(new { statusCode = 400, message = ex.Message });
            }
            catch (ArgumentException ex)
            {
                return BadRequest(new { statusCode = 400, message = ex.Message });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { statusCode = 500, message = "Lỗi máy chủ nội bộ: " + ex.Message });
            }
        }

        [HttpPost("projects/{projectId}/WorkTasks/{id}/subscription")]
        [ProjectAuthorize("")]
        public async Task<IActionResult> ToggleSubscription(Guid projectId, Guid id, [FromServices] ApplicationDbContext context)
        {
            try
            {
                var userIdString = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
                if (!Guid.TryParse(userIdString, out Guid userId))
                {
                    return Unauthorized(new { statusCode = 401, message = "Vui long dang nhap." });
                }

                var taskExistsInProject = await context.WorkTasks
                    .AnyAsync(wt => wt.Id == id && wt.ProjectId == projectId && !wt.IsDeleted);
                if (!taskExistsInProject)
                {
                    return NotFound(new { statusCode = 404, message = "Task khong ton tai trong du an nay." });
                }

                var isProjectMember = await context.ProjectMembers
                    .AnyAsync(pm => pm.ProjectId == projectId && pm.UserId == userId && pm.Status);
                if (!isProjectMember)
                {
                    return StatusCode(403, new { statusCode = 403, message = "Ban khong phai thanh vien cua du an nay." });
                }

                var isSubscribed = await _workTaskService.ToggleSubscriptionAsync(id, userId);
                return Ok(new
                {
                    statusCode = 200,
                    message = isSubscribed ? "Da theo doi cong viec." : "Da huy theo doi cong viec.",
                    data = new { isSubscribed }
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { statusCode = 500, message = "Loi may chu noi bo: " + ex.Message });
            }
        }

        [HttpPatch("projects/{projectId}/WorkTasks/{id}")]
        [ProjectAuthorize("")]
        public async Task<IActionResult> PartialUpdate(Guid projectId, Guid id, [FromBody] System.Text.Json.JsonElement updates, [FromServices] TaskManagement.Infrastructure.Data.ApplicationDbContext context, [FromServices] IGamificationService gamificationService)
        {
            try
            {
                var task = await context.WorkTasks
                    .Include(wt => wt.TaskAssignments)
                    .Include(wt => wt.IssueModules)
                    .Include(wt => wt.TaskStatus)
                    .Include(wt => wt.Sprint)
                    .FirstOrDefaultAsync(wt => wt.Id == id && wt.ProjectId == projectId && !wt.IsDeleted);
                if (task != null && SprintStatePolicy.IsTaskMutationLocked(task.Sprint, DateTime.UtcNow))
                    return BadRequest(new { statusCode = 400, message = "Khong the chinh sua Task cua mot Sprint da dong." });
                if (task == null) return NotFound(new { statusCode = 404, message = "Task không tồn tại." });

                var userIdString = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
                if (!Guid.TryParse(userIdString, out Guid userId))
                {
                    return Unauthorized(new { statusCode = 401, message = "Vui long dang nhap." });
                }

                var membership = await context.ProjectMembers
                    .FirstOrDefaultAsync(pm => pm.ProjectId == projectId && pm.UserId == userId && pm.Status);
                if (membership == null)
                {
                    return StatusCode(403, new { statusCode = 403, message = "Ban khong phai thanh vien cua du an nay." });
                }

                var canUpdateTask = ResourcePermissionPolicy.ProjectRoleHasPermission(
                        membership.ProjectRole,
                        ResourcePermissionCodes.ProjectWrite)
                    || task.ReporterId == userId
                    || task.AssignedUserId == userId
                    || task.TaskAssignments.Any(ta => ta.UserId == userId && ta.Status);
                if (!canUpdateTask)
                {
                    return StatusCode(403, new { statusCode = 403, message = "Ban khong co quyen sua tac vu nay." });
                }
                var oldStatusName = task.TaskStatus?.Name;
                string? newStatusName = null;
                var previousAssigneeIds = task.TaskAssignments
                    .Where(ta => ta.Status)
                    .Select(ta => ta.UserId)
                    .ToHashSet();
                if (task.AssignedUserId is Guid previousAssignedUserId)
                {
                    previousAssigneeIds.Add(previousAssignedUserId);
                }

                if (updates.TryGetProperty("title", out var titleProp) && titleProp.ValueKind != System.Text.Json.JsonValueKind.Null)
                    task.Title = titleProp.GetString() ?? task.Title;
                
                if (updates.TryGetProperty("description", out var descProp))
                    task.Description = descProp.ValueKind == System.Text.Json.JsonValueKind.Null ? null : descProp.GetString();

                if (updates.TryGetProperty("priority", out var prioProp) && prioProp.ValueKind != System.Text.Json.JsonValueKind.Null)
                    task.Priority = prioProp.GetInt32();

                if (updates.TryGetProperty("storyPoints", out var storyPointsProp) && storyPointsProp.ValueKind != System.Text.Json.JsonValueKind.Null)
                    task.StoryPoints = Math.Clamp(storyPointsProp.GetDouble(), 0, 21);

                if (updates.TryGetProperty("totalEstimatedHours", out var estimateProp) && estimateProp.ValueKind != System.Text.Json.JsonValueKind.Null)
                    task.TotalEstimatedHours = Math.Max(0, estimateProp.GetDouble());

                if (updates.TryGetProperty("sortOrder", out var sortProp) && sortProp.ValueKind != System.Text.Json.JsonValueKind.Null)
                    task.SortOrder = sortProp.GetDouble();

                if (updates.TryGetProperty("assigneeId", out var assigneeProp))
                {
                    if (assigneeProp.ValueKind == System.Text.Json.JsonValueKind.Null) task.AssignedUserId = null;
                    else if (Guid.TryParse(assigneeProp.GetString(), out Guid aId))
                    {
                        var assigneeIsMember = await context.ProjectMembers
                            .AnyAsync(pm => pm.ProjectId == projectId && pm.UserId == aId && pm.Status);
                        if (!assigneeIsMember)
                        {
                            return BadRequest(new { statusCode = 400, message = "Assignee khong thuoc du an nay." });
                        }

                        task.AssignedUserId = aId;
                    }
                }

                if (updates.TryGetProperty("assigneeIds", out var assigneeIdsProp) && assigneeIdsProp.ValueKind == System.Text.Json.JsonValueKind.Array)
                {
                    var parsedIds = assigneeIdsProp.EnumerateArray()
                        .Select(item => item.GetString())
                        .Where(value => !string.IsNullOrWhiteSpace(value) && Guid.TryParse(value, out _))
                        .Select(value => Guid.Parse(value!))
                        .Distinct()
                        .ToList();

                    if (parsedIds.Any())
                    {
                        var validAssigneeCount = await context.ProjectMembers
                            .CountAsync(pm => pm.ProjectId == projectId && pm.Status && parsedIds.Contains(pm.UserId));
                        if (validAssigneeCount != parsedIds.Count)
                        {
                            return BadRequest(new { statusCode = 400, message = "Mot hoac nhieu assignee khong thuoc du an nay." });
                        }
                    }

                    var existingAssignments = task.TaskAssignments.ToList();

                    foreach (var assignment in existingAssignments)
                    {
                        if (parsedIds.Contains(assignment.UserId))
                        {
                            assignment.Activate();
                        }
                        else
                        {
                            assignment.Status = false;
                        }
                    }

                    foreach (var assigneeId in parsedIds.Where(idValue => existingAssignments.All(ta => ta.UserId != idValue)))
                    {
                        var newAssignment = new TaskManagement.Domain.Entities.TaskAssignment
                        {
                            WorkTaskId = task.Id,
                            UserId = assigneeId,
                            Status = true
                        };
                        context.TaskAssignments.Add(newAssignment);
                        task.TaskAssignments.Add(newAssignment);
                    }

                    task.AssignedUserId = parsedIds.FirstOrDefault();
                    if (!parsedIds.Any())
                    {
                        task.AssignedUserId = null;
                    }
                }

                var progressRewardRequests = new List<(Guid AssigneeId, double OldProgress, double NewProgress)>();
                if (updates.TryGetProperty("assigneeProgress", out var assigneeProgressProp))
                {
                    var progressItems = new List<System.Text.Json.JsonElement>();
                    if (assigneeProgressProp.ValueKind == System.Text.Json.JsonValueKind.Array)
                    {
                        progressItems.AddRange(assigneeProgressProp.EnumerateArray());
                    }
                    else if (assigneeProgressProp.ValueKind == System.Text.Json.JsonValueKind.Object)
                    {
                        progressItems.Add(assigneeProgressProp);
                    }

                    foreach (var item in progressItems)
                    {
                        Guid assigneeId = Guid.Empty;
                        if (item.TryGetProperty("userId", out var userIdProp))
                        {
                            Guid.TryParse(userIdProp.GetString(), out assigneeId);
                        }

                        if (assigneeId == Guid.Empty)
                        {
                            continue;
                        }

                        var assignment = task.TaskAssignments.FirstOrDefault(ta => ta.UserId == assigneeId);
                        if (assignment == null)
                        {
                            assignment = new TaskManagement.Domain.Entities.TaskAssignment
                            {
                                WorkTaskId = task.Id,
                                UserId = assigneeId,
                                Status = true
                            };
                            context.TaskAssignments.Add(assignment);
                            task.TaskAssignments.Add(assignment);
                        }

                        assignment.Activate();

                        var oldProgress = assignment.ProgressPercent;
                        if (item.TryGetProperty("progressPercent", out var progressProp) && progressProp.ValueKind != System.Text.Json.JsonValueKind.Null)
                        {
                            var progress = progressProp.GetDouble();
                            assignment.ProgressPercent = Math.Clamp(progress, 0, 100);
                            assignment.ProgressUpdatedAt = DateTime.UtcNow;
                        }

                        if (item.TryGetProperty("contributionWeight", out var weightProp) && weightProp.ValueKind != System.Text.Json.JsonValueKind.Null)
                        {
                            assignment.ContributionWeight = Math.Max(0, weightProp.GetDouble());
                        }

                        if (item.TryGetProperty("estimatedHours", out var estimatedProp) && estimatedProp.ValueKind != System.Text.Json.JsonValueKind.Null)
                        {
                            assignment.EstimatedHours = Math.Max(0, estimatedProp.GetDouble());
                        }

                        if (item.TryGetProperty("blockedByUserId", out var blockedByProp))
                        {
                            assignment.BlockedByUserId = blockedByProp.ValueKind == System.Text.Json.JsonValueKind.Null
                                ? null
                                : Guid.TryParse(blockedByProp.GetString(), out var blockedByUserId) ? blockedByUserId : null;
                        }

                        if (item.TryGetProperty("blockReason", out var blockReasonProp))
                        {
                            assignment.BlockReason = blockReasonProp.ValueKind == System.Text.Json.JsonValueKind.Null
                                ? null
                                : blockReasonProp.GetString();
                        }

                        if (oldProgress != assignment.ProgressPercent)
                        {
                            progressRewardRequests.Add((assigneeId, oldProgress, assignment.ProgressPercent));
                        }
                    }

                    var activeAssignments = task.TaskAssignments.Where(ta => ta.Status).ToList();
                    var firstAssignee = activeAssignments.OrderByDescending(ta => ta.ProgressPercent).FirstOrDefault();
                    task.AssignedUserId = firstAssignee?.UserId;
                }

                if (updates.TryGetProperty("plannedStartDate", out var startProp))
                {
                    task.PlannedStartDate = ParseDateOnlyUtc(startProp);
                }

                if (updates.TryGetProperty("dueDate", out var dueProp))
                {
                    task.DueDate = ParseDateOnlyUtc(dueProp);
                }

                if (updates.TryGetProperty("sprintId", out var sprintProp))
                {
                    if (sprintProp.ValueKind == System.Text.Json.JsonValueKind.Null) task.SprintId = null;
                    else if (Guid.TryParse(sprintProp.GetString(), out Guid sId))
                    {
                        var targetSprint = await context.Sprints.FirstOrDefaultAsync(s => s.Id == sId && s.ProjectId == projectId);
                        if (targetSprint == null)
                        {
                            return BadRequest(new { statusCode = 400, message = "Cycle khong thuoc du an nay." });
                        }
                        if (SprintStatePolicy.IsTaskMutationLocked(targetSprint, DateTime.UtcNow))
                        {
                            return BadRequest(new { statusCode = 400, message = "Khong the chuyen Task vao mot Sprint da dong." });
                        }

                        task.SprintId = sId;
                    }
                }

                if (updates.TryGetProperty("moduleId", out var modProp))
                {
                    context.IssueModules.RemoveRange(task.IssueModules);

                    if (modProp.ValueKind != System.Text.Json.JsonValueKind.Null
                        && Guid.TryParse(modProp.GetString(), out Guid moduleId))
                    {
                        var moduleExists = await context.Modules.AnyAsync(m => m.Id == moduleId && m.ProjectId == projectId);
                        if (!moduleExists)
                        {
                            return BadRequest(new { statusCode = 400, message = "Module khong thuoc du an nay." });
                        }

                        context.IssueModules.Add(new TaskManagement.Domain.Entities.IssueModule
                        {
                            WorkTaskId = task.Id,
                            ModuleId = moduleId,
                            AssignedAt = DateTime.UtcNow
                        });
                    }
                }

                if (updates.TryGetProperty("parentId", out var parProp))
                {
                    if (parProp.ValueKind == System.Text.Json.JsonValueKind.Null) task.ParentTaskId = null;
                    else if (Guid.TryParse(parProp.GetString(), out Guid pId))
                    {
                        if (pId == task.Id)
                        {
                            return BadRequest(new { statusCode = 400, message = "Task khong the tu lam parent." });
                        }

                        var parentExists = await context.WorkTasks
                            .AnyAsync(wt => wt.Id == pId && wt.ProjectId == projectId && !wt.IsDeleted);
                        if (!parentExists)
                        {
                            return BadRequest(new { statusCode = 400, message = "Parent task khong thuoc du an nay." });
                        }

                        task.ParentTaskId = pId;
                    }
                }

                if (updates.TryGetProperty("statusName", out var statusProp) && statusProp.ValueKind != System.Text.Json.JsonValueKind.Null)
                {
                    var statusName = statusProp.GetString();
                    await EnsureDefaultTaskStatusesAsync(context, projectId);
                    var normalizedStatusName = NormalizeStatusName(statusName);
                    var projectStatuses = await context.TaskStatuses
                        .Where(ts => ts.ProjectId == projectId)
                        .ToListAsync();
                    var newStatus = projectStatuses.FirstOrDefault(ts =>
                        string.Equals(NormalizeStatusName(ts.Name), normalizedStatusName, StringComparison.OrdinalIgnoreCase));
                    if (newStatus != null)
                    {
                        var isDoneStatus = IsDoneStatusName(newStatus.Name);
                        if (isDoneStatus && await HasIncompleteSubtasksAsync(context, task.Id))
                        {
                            return BadRequest(new { statusCode = 400, message = "Chua the hoan thanh task cha khi van con sub-work item chua dat 100% hoac chua Done." });
                        }

                        if (isDoneStatus)
                        {
                            progressRewardRequests.AddRange(task.TaskAssignments
                                .Where(assignment => assignment.Status && assignment.ProgressPercent < 100)
                                .Select(assignment => (assignment.UserId, assignment.ProgressPercent, 100.0)));
                            CompleteActiveAssignments(task);
                        }

                        task.TaskStatusId = newStatus.Id;
                        newStatusName = newStatus.Name;
                    }
                    else
                    {
                        return BadRequest(new { statusCode = 400, message = $"Trạng thái '{statusName}' không hợp lệ cho dự án này." });
                    }

                }

                string? visibilityModeToSave = null;
                List<string>? visibleRolesToSave = null;
                if (updates.TryGetProperty("visibilityMode", out var visibilityModeProp))
                {
                    visibilityModeToSave = visibilityModeProp.ValueKind == JsonValueKind.Null
                        ? "project"
                        : visibilityModeProp.GetString();
                }
                if (updates.TryGetProperty("visibleToRoles", out var visibleRolesProp) && visibleRolesProp.ValueKind == JsonValueKind.Array)
                {
                    visibleRolesToSave = visibleRolesProp.EnumerateArray()
                        .Select(item => item.GetString())
                        .Where(item => !string.IsNullOrWhiteSpace(item))
                        .Cast<string>()
                        .ToList();
                }

                task.UpdatedAt = DateTime.UtcNow;
                try
                {
                    await context.SaveChangesAsync();
                    if (visibilityModeToSave != null || visibleRolesToSave != null)
                    {
                        var existingVisibility = await LoadTaskVisibilityAsync(context, task.Id);
                        await SaveTaskVisibilityAsync(
                            context,
                            task.Id,
                            visibilityModeToSave ?? existingVisibility.Mode,
                            visibleRolesToSave ?? existingVisibility.Roles);
                    }
                }
                catch (DbUpdateConcurrencyException)
                {
                    var latestTask = await LoadTaskResponseAsync(context, projectId, id, userId);

                    if (latestTask != null)
                    {
                        return Ok(new
                        {
                            statusCode = 200,
                            message = "Saved",
                            data = latestTask
                        });
                    }

                    return StatusCode(409, new
                    {
                        statusCode = 409,
                        message = "This work item was updated by another action. Please reload and try again."
                    });
                }
                try
                {
                    if (userId != Guid.Empty && !string.IsNullOrWhiteSpace(newStatusName))
                    {
                        await gamificationService.ApplyStatusChangeRewardsAsync(id, userId, oldStatusName, newStatusName);
                    }

                    if (updates.TryGetProperty("dueDate", out _))
                    {
                        await gamificationService.ApplyDueDatePenaltyAsync(id, userId);
                    }
                }
                catch
                {
                    // Post-save rewards must not fail a successful task update.
                }

                foreach (var rewardRequest in progressRewardRequests)
                {
                    try
                    {
                        await gamificationService.ApplyAssignmentProgressRewardsAsync(id, rewardRequest.AssigneeId, userId, rewardRequest.OldProgress, rewardRequest.NewProgress);
                    }
                    catch
                    {
                        // Post-save rewards must not fail a successful task update.
                    }
                }

                try
                {
                    var currentAssigneeIds = task.TaskAssignments
                        .Where(ta => ta.Status)
                        .Select(ta => ta.UserId)
                        .ToHashSet();
                    if (task.AssignedUserId is Guid currentAssignedUserId)
                    {
                        currentAssigneeIds.Add(currentAssignedUserId);
                    }

                    var newAssigneeIds = currentAssigneeIds
                        .Except(previousAssigneeIds)
                        .ToList();

                    if (newAssigneeIds.Any())
                    {
                        await TriggerTaskAssignedNotificationsAsync(
                            context,
                            projectId,
                            id,
                            userId,
                            task.Title,
                            newAssigneeIds);
                    }

                    if (!string.IsNullOrWhiteSpace(newStatusName) && !string.Equals(oldStatusName, newStatusName, StringComparison.OrdinalIgnoreCase))
                    {
                        await TriggerTaskStatusChangedNotificationAsync(
                            context,
                            projectId,
                            id,
                            userId,
                            task.Title,
                            newStatusName);
                    }
                }
                catch
                {
                    // Notifications must not fail a successful task update.
                }

                var savedTask = await LoadTaskResponseAsync(context, projectId, id, userId);
                await BroadcastTaskUpdatedAsync(projectId, savedTask);
                return Ok(new { statusCode = 200, message = "Saved", data = savedTask });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { statusCode = 500, message = ex.Message });
            }
        }

        [HttpPost("projects/{projectId}/WorkTasks/{id}/time-logs")]
        [ProjectAuthorize("")]
        public async Task<IActionResult> AddTimeLog(Guid projectId, Guid id, [FromBody] CreateTimeLogRequest request, [FromServices] ApplicationDbContext context)
        {
            try
            {
                var userIdString = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
                if (!Guid.TryParse(userIdString, out var userId))
                {
                    return Unauthorized(new { statusCode = 401, message = "Vui long dang nhap." });
                }

                var task = await context.WorkTasks
                    .Include(wt => wt.TaskAssignments)
                    .FirstOrDefaultAsync(wt => wt.Id == id && wt.ProjectId == projectId && !wt.IsDeleted);
                if (task == null)
                {
                    return NotFound(new { statusCode = 404, message = "Task khong ton tai." });
                }

                var membership = await context.ProjectMembers
                    .FirstOrDefaultAsync(pm => pm.ProjectId == projectId && pm.UserId == userId && pm.Status);
                if (membership == null)
                {
                    return StatusCode(403, new { statusCode = 403, message = "Ban khong phai thanh vien cua du an nay." });
                }

                var hours = Math.Max(0, request.Hours);
                if (hours <= 0)
                {
                    return BadRequest(new { statusCode = 400, message = "So gio log phai lon hon 0." });
                }

                var activeAssignment = task.TaskAssignments.FirstOrDefault(ta => ta.UserId == userId && ta.Status);
                if (activeAssignment == null)
                {
                    return BadRequest(new { statusCode = 400, message = "Chi assignee dang active moi duoc log time cho task nay." });
                }

                var timeLog = new Domain.Entities.TimeLog
                {
                    Id = Guid.NewGuid(),
                    WorkTaskId = task.Id,
                    UserId = userId,
                    Hours = hours,
                    WorkType = string.IsNullOrWhiteSpace(request.WorkType) ? "GENERAL" : request.WorkType.Trim(),
                    Note = string.IsNullOrWhiteSpace(request.Note) ? null : request.Note.Trim(),
                    LoggedAt = DateTime.UtcNow
                };

                context.TimeLogs.Add(timeLog);
                await context.SaveChangesAsync();

                activeAssignment.TotalActualHours = await context.TimeLogs
                    .Where(tl => tl.WorkTaskId == task.Id && tl.UserId == userId)
                    .SumAsync(tl => tl.Hours);

                task.TotalActualHours = await context.TimeLogs
                    .Where(tl => tl.WorkTaskId == task.Id)
                    .SumAsync(tl => tl.Hours);

                var parentId = task.ParentTaskId;
                while (parentId.HasValue)
                {
                    var parent = await context.WorkTasks.FirstOrDefaultAsync(wt => wt.Id == parentId.Value && !wt.IsDeleted);
                    if (parent == null)
                    {
                        break;
                    }

                    parent.TotalActualHours = await context.WorkTasks
                        .Where(wt => wt.ParentTaskId == parent.Id && !wt.IsDeleted)
                        .SumAsync(wt => (double?)wt.TotalActualHours) ?? 0;

                    parentId = parent.ParentTaskId;
                }

                await context.SaveChangesAsync();

                var savedTask = await LoadTaskResponseAsync(context, projectId, id, userId);
                await BroadcastTaskUpdatedAsync(projectId, savedTask);
                return Ok(new
                {
                    statusCode = 200,
                    message = "Log time thanh cong.",
                    data = savedTask
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { statusCode = 500, message = ex.Message });
            }
        }

        [HttpGet("{id}/comments")]
        [ProjectAuthorize("")]
        public async Task<IActionResult> GetComments(Guid projectId, Guid id, [FromServices] TaskManagement.Infrastructure.Data.ApplicationDbContext context)
        {
            try
            {
                var comments = await Microsoft.EntityFrameworkCore.EntityFrameworkQueryableExtensions.ToListAsync(
                    System.Linq.Queryable.OrderBy(
                        System.Linq.Queryable.Select(
                            System.Linq.Queryable.Where(context.Comments, c => c.EntityId == id && c.EntityType == "WorkTask" && !c.IsDeleted),
                            c => new {
                                c.Id,
                                c.Content,
                                c.ParentCommentId,
                                c.CreatedAt,
                                UserId = c.UserId,
                                FullName = c.User.FullName ?? c.User.Email,
                                Avatar = c.User.FullName != null ? c.User.FullName.Substring(0, 1) : "U"
                            }
                        ),
                        c => c.CreatedAt
                    )
                );
                return Ok(new { statusCode = 200, message = "Success", data = comments });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { statusCode = 500, message = ex.Message });
            }
        }

        /// <summary>
        /// Kanban Drag-Drop: Reorder task (update SortOrder + optionally change status)
        /// </summary>
        [HttpPut("projects/{projectId}/WorkTasks/{id}/reorder")]
        [ProjectAuthorize("")]
        public async Task<IActionResult> Reorder(Guid projectId, Guid id, [FromBody] ReorderTaskDto dto, [FromServices] TaskManagement.Infrastructure.Data.ApplicationDbContext context, [FromServices] IGamificationService gamificationService)
        {
            try
            {
                var userIdString = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
                if (!Guid.TryParse(userIdString, out Guid userId))
                    return Unauthorized(new { statusCode = 401, message = "Vui long dang nhap." });

                var task = await context.WorkTasks
                    .Include(wt => wt.TaskStatus)
                    .Include(wt => wt.TaskAssignments)
                    .Include(wt => wt.Sprint)
                    .FirstOrDefaultAsync(wt => wt.Id == id && wt.ProjectId == projectId && !wt.IsDeleted);
                if (task != null && SprintStatePolicy.IsTaskMutationLocked(task.Sprint, DateTime.UtcNow))
                    return BadRequest(new { statusCode = 400, message = "Khong the chinh sua Task cua mot Sprint da dong." });
                if (task == null)
                    return NotFound(new { statusCode = 404, message = "Tác vụ không tồn tại." });

                var oldStatusName = task.TaskStatus?.Name;
                string? newStatusName = null;
                var completedAssigneeRewards = new List<(Guid AssigneeId, double OldProgress)>();
                var membership = await context.ProjectMembers
                    .FirstOrDefaultAsync(pm => pm.ProjectId == projectId && pm.UserId == userId && pm.Status);
                if (membership == null)
                {
                    return StatusCode(403, new { statusCode = 403, message = "Ban khong phai thanh vien cua du an nay." });
                }

                var canUpdateTask = ResourcePermissionPolicy.ProjectRoleHasPermission(
                        membership.ProjectRole,
                        ResourcePermissionCodes.ProjectWrite)
                    || task.ReporterId == userId
                    || task.AssignedUserId == userId
                    || task.TaskAssignments.Any(ta => ta.UserId == userId && ta.Status);
                if (!canUpdateTask)
                {
                    return StatusCode(403, new { statusCode = 403, message = "Ban khong co quyen sap xep tac vu nay." });
                }

                task.SortOrder = dto.SortOrder;
                task.UpdatedAt = DateTime.UtcNow;

                // If status also changed (dragged to a different column)
                if (!string.IsNullOrEmpty(dto.NewStatusName))
                {
                    await EnsureDefaultTaskStatusesAsync(context, projectId);
                    var normalizedStatusName = NormalizeStatusName(dto.NewStatusName);
                    var projectStatuses = await context.TaskStatuses
                        .Where(ts => ts.ProjectId == projectId)
                        .ToListAsync();
                    var newStatus = projectStatuses.FirstOrDefault(ts =>
                        string.Equals(NormalizeStatusName(ts.Name), normalizedStatusName, StringComparison.OrdinalIgnoreCase));
                    if (newStatus != null)
                    {
                        var isDoneStatus = IsDoneStatusName(newStatus.Name);
                        if (isDoneStatus && await HasIncompleteSubtasksAsync(context, task.Id))
                        {
                            return BadRequest(new { statusCode = 400, message = "Chua the hoan thanh task cha khi van con sub-work item chua dat 100% hoac chua Done." });
                        }

                        if (isDoneStatus)
                        {
                            completedAssigneeRewards.AddRange(task.TaskAssignments
                                .Where(assignment => assignment.Status && assignment.ProgressPercent < 100)
                                .Select(assignment => (assignment.UserId, assignment.ProgressPercent)));
                            CompleteActiveAssignments(task);
                        }

                        task.TaskStatusId = newStatus.Id;
                        newStatusName = newStatus.Name;
                    }
                }

                await context.SaveChangesAsync();
                if (!string.IsNullOrWhiteSpace(newStatusName))
                {
                    await gamificationService.ApplyStatusChangeRewardsAsync(id, userId, oldStatusName, newStatusName);
                }
                foreach (var reward in completedAssigneeRewards)
                {
                    await gamificationService.ApplyAssignmentProgressRewardsAsync(id, reward.AssigneeId, userId, reward.OldProgress, 100);
                }
                var savedTask = await LoadTaskResponseAsync(context, projectId, id, userId);
                await BroadcastTaskUpdatedAsync(projectId, savedTask);
                return Ok(new { statusCode = 200, message = "Cập nhật thứ tự thành công." });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { statusCode = 500, message = "Lỗi: " + ex.Message });
            }
        }

        /// <summary>
        /// GET /api/projects/{projectId}/WorkTasks/{parentId}/subtasks
        /// </summary>
        [HttpGet("projects/{projectId}/WorkTasks/{parentId}/subtasks")]
        [ProjectAuthorize("")]
        public async Task<IActionResult> GetSubtasks(Guid projectId, Guid parentId, [FromServices] ApplicationDbContext context)
        {
            await EnsureDefaultTaskStatusesAsync(context, projectId);
            var userIdStr = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (!Guid.TryParse(userIdStr, out var userId))
            {
                return Unauthorized(new { statusCode = 401, message = "Vui long dang nhap." });
            }

            var subtasks = (await _workTaskService.GetByProjectAsync(projectId, userId))
                .Where(task => task.ParentTaskId == parentId)
                .OrderBy(task => task.SortOrder)
                .ToList();

            return Ok(new { statusCode = 200, data = subtasks });
        }

        /// <summary>
        /// POST /api/projects/{projectId}/WorkTasks/{parentId}/subtasks — Create a child task
        /// </summary>
        [HttpPost("projects/{projectId}/WorkTasks/{parentId}/subtasks")]
        [ProjectAuthorize("")]
        public async Task<IActionResult> CreateSubtask(Guid projectId, Guid parentId, [FromBody] CreateWorkTaskDto request, [FromServices] ApplicationDbContext context)
        {
            var userIdStr = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (!Guid.TryParse(userIdStr, out Guid userId))
                return Unauthorized();

            var parent = await context.WorkTasks.FirstOrDefaultAsync(t => t.Id == parentId && t.ProjectId == projectId && !t.IsDeleted);
            if (parent == null) return NotFound(new { message = "Task cha không tồn tại." });

            await EnsureDefaultTaskStatusesAsync(context, projectId);

            // Get default BACKLOG status and type from project
            var projectStatuses = await context.TaskStatuses
                .Where(ts => ts.ProjectId == projectId)
                .OrderBy(ts => ts.Position)
                .ToListAsync();
            var defaultStatus = projectStatuses.FirstOrDefault(ts => NormalizeStatusName(ts.Name) == "BACKLOG")
                ?? projectStatuses.FirstOrDefault();
            var defaultType = await context.TaskTypes.FirstOrDefaultAsync(tt => tt.ProjectId == projectId);

            if (defaultStatus == null || defaultType == null)
                return BadRequest(new { message = "Project chưa có status hoặc type mặc định." });

            // Generate sequence
            var project = await context.Projects.FindAsync(projectId);
            if (project != null) project.IssueSequence++;

            var subtask = new TaskManagement.Domain.Entities.WorkTask
            {
                Id = Guid.NewGuid(),
                ProjectId = projectId,
                ParentTaskId = parentId,
                Title = request.Title ?? "Subtask",
                Description = request.Description,
                Priority = request.Priority,
                TaskTypeId = defaultType.Id,
                TaskStatusId = defaultStatus.Id,
                ReporterId = userId,
                WorkspaceId = parent.WorkspaceId,
                SequenceId = project != null ? $"{project.Identifier}-{project.IssueSequence}" : null,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow,
                SortOrder = 65536
            };

            context.WorkTasks.Add(subtask);
            await context.SaveChangesAsync();

            return Ok(new
            {
                statusCode = 201,
                message = "Tạo subtask thành công.",
                data = new
                {
                    subtask.Id,
                    subtask.ProjectId,
                    subtask.Title,
                    subtask.Description,
                    subtask.Priority,
                    subtask.ParentTaskId,
                    subtask.SequenceId,
                    subtask.TaskTypeId,
                    subtask.AssignedUserId,
                    AssigneeIds = new List<Guid>(),
                    Assignees = new List<TaskAssigneeDto>(),
                    StatusName = NormalizeStatusName(defaultStatus.Name),
                    subtask.PlannedStartDate,
                    subtask.PlannedEndDate,
                    subtask.DueDate,
                    subtask.CreatedAt,
                    subtask.UpdatedAt
                }
            });
        }

        /// <summary>
        /// GET /api/dashboard/stats — Real-time dashboard statistics from DB
        /// </summary>
        [HttpGet("/api/dashboard/stats")]
        public async Task<IActionResult> GetDashboardStats(
            [FromServices] ApplicationDbContext context,
            [FromQuery] string scope = "all",
            [FromQuery] Guid? projectId = null)
        {
            var userIdStr = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (!Guid.TryParse(userIdStr, out Guid userId))
            {
                return Unauthorized();
            }

            // Lấy danh sách Project mà user tham gia hoặc có quyền xem dựa trên scope
            IQueryable<TaskManagement.Domain.Entities.Project> projectQuery = context.Projects.Where(p => !p.IsDeleted);

            if (scope == "my")
            {
                // My projects: User là creator hoặc Member hoạt động
                projectQuery = projectQuery.Where(p => p.CreatorId == userId || p.ProjectMembers.Any(pm => pm.UserId == userId && pm.Status));
            }
            else if (scope == "archived")
            {
                // Archived projects
                projectQuery = projectQuery.Where(p => p.IsArchived);
            }
            else
            {
                // All active projects: User is creator OR Member hoạt động
                projectQuery = projectQuery.Where(p => !p.IsArchived && (p.CreatorId == userId || p.ProjectMembers.Any(pm => pm.UserId == userId && pm.Status)));
            }

                        if (projectId.HasValue)
            {
                projectQuery = projectQuery.Where(p => p.Id == projectId.Value);
            }

            var targetProjectIds = await projectQuery.Select(p => p.Id).ToListAsync();

            var baseTaskQuery = context.WorkTasks
                .Include(t => t.TaskStatus)
                .Where(t => !t.IsDeleted && targetProjectIds.Contains(t.ProjectId));

            var totalTasks = await baseTaskQuery.CountAsync();
            var doneStatuses = new[] { "DONE", "COMPLETED", "FINISHED", "HOÀN THÀNH", "SUCCESS", "FINISHED", "HOÀN TẤT" };

            var completedTasks = await baseTaskQuery.CountAsync(t =>
                t.TaskStatus != null && doneStatuses.Contains(t.TaskStatus.Name.ToUpper().Trim()));

            var now = DateTime.UtcNow;
            var overdueTasks = await baseTaskQuery.CountAsync(t =>
                t.DueDate.HasValue && t.DueDate < now &&
                (t.TaskStatus == null || !doneStatuses.Contains(t.TaskStatus.Name.ToUpper().Trim())));

            var statusGroups = await baseTaskQuery
                .GroupBy(t => t.TaskStatus != null ? t.TaskStatus.Name : "No Status")
                .Select(g => new { Status = g.Key, Count = g.Count() })
                .ToListAsync();

            var priorityGroups = await baseTaskQuery
                .GroupBy(t => t.Priority)
                .Select(g => new { Priority = g.Key, Count = g.Count() })
                .ToListAsync();

            var myTasks = await baseTaskQuery.CountAsync(t => 
                t.AssignedUserId == userId || 
                t.TaskAssignments.Any(ta => ta.UserId == userId));

            var totalProjects = targetProjectIds.Count;
            var totalMembers = await context.ProjectMembers
                .Where(pm => targetProjectIds.Contains(pm.ProjectId) && pm.Status)
                .Select(pm => pm.UserId)
                .Distinct()
                .CountAsync();

            var activeCycles = await context.Sprints.CountAsync(s => targetProjectIds.Contains(s.ProjectId) && s.Status);
            var totalModules = await context.Modules.CountAsync(m => targetProjectIds.Contains(m.ProjectId));

            var totalIntakes = await context.Intakes.CountAsync(i => targetProjectIds.Contains(i.ProjectId));
            var totalViews = await context.ProjectViews.CountAsync(v => targetProjectIds.Contains(v.ProjectId));

            var sevenDaysAgo = now.AddDays(-7);
            var newTasksLast7Days = await baseTaskQuery.CountAsync(t => t.CreatedAt >= sevenDaysAgo);

            var thirtyDaysAgo = now.AddDays(-30);
            // Include both Task logs and System logs that belong to these projects if possible
            // For now, task-related logs are the primary activity
            var totalActions = await context.AuditLogs
                .Where(al => al.CreatedAt >= thirtyDaysAgo && al.WorkTask != null && targetProjectIds.Contains(al.WorkTask.ProjectId))
                .CountAsync();


            return Ok(new
            {
                statusCode = 200,
                data = new
                {
                    totalTasks,
                    completedTasks,
                    overdueTasks,
                    totalProjects,
                    totalMembers,
                    activeCycles,
                    totalModules,
                    totalIntakes,
                    totalViews,
                    newTasksLast7Days,
                    totalActions,
                    myTasks,
                    byStatus = statusGroups,
                    byPriority = priorityGroups
                }
            });
        }

        [HttpGet("/api/analytics/planning-summary")]
        public async Task<IActionResult> GetPlanningSummary(
            [FromQuery] Guid? projectId,
            [FromServices] ApplicationDbContext context)
        {
            var userIdStr = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (!Guid.TryParse(userIdStr, out var userId))
            {
                return Unauthorized();
            }

            var accessibleMemberships = await context.ProjectMembers
                .Where(pm => pm.UserId == userId && pm.Status)
                .Select(pm => new
                {
                    pm.ProjectId,
                    pm.ProjectRole
                })
                .ToListAsync();

            var accessibleProjectIds = accessibleMemberships
                .Select(item => item.ProjectId)
                .Distinct()
                .ToList();

            if (projectId.HasValue && !accessibleProjectIds.Contains(projectId.Value))
            {
                return StatusCode(403, new { statusCode = 403, message = "You do not have access to this project." });
            }

            var selectedProjectIds = projectId.HasValue
                ? accessibleProjectIds.Where(id => id == projectId.Value).ToList()
                : accessibleProjectIds;

            var taskSnapshots = await context.WorkTasks
                .AsNoTracking()
                .Where(task => !task.IsDeleted && selectedProjectIds.Contains(task.ProjectId))
                .Select(task => new
                {
                    task.Id,
                    task.ProjectId,
                    task.SprintId,
                    task.Title,
                    task.SequenceId,
                    task.Priority,
                    task.StoryPoints,
                    task.TotalEstimatedHours,
                    task.TotalActualHours,
                    task.DueDate,
                    task.CreatedAt,
                    StatusName = task.TaskStatus.Name,
                    AssigneeCount = task.TaskAssignments.Count(assignment => assignment.Status),
                    IsAssignedToCurrentUser = task.AssignedUserId == userId || task.TaskAssignments.Any(assignment => assignment.UserId == userId && assignment.Status)
                })
                .ToListAsync();

            var selectedTaskIds = taskSnapshots.Select(task => task.Id).ToList();
            var timeLogs = await context.TimeLogs
                .AsNoTracking()
                .Where(log => selectedTaskIds.Contains(log.WorkTaskId))
                .GroupBy(log => log.WorkTaskId)
                .Select(group => new
                {
                    WorkTaskId = group.Key,
                    LoggedHours = group.Sum(item => item.Hours)
                })
                .ToDictionaryAsync(item => item.WorkTaskId, item => item.LoggedHours);

            var sprintLookup = await context.Sprints
                .AsNoTracking()
                .Where(sprint => selectedProjectIds.Contains(sprint.ProjectId))
                .Select(sprint => new
                {
                    sprint.Id,
                    sprint.Name,
                    sprint.ProjectId
                })
                .ToDictionaryAsync(item => item.Id, item => new { item.Name, item.ProjectId });

            var projects = await context.Projects
                .AsNoTracking()
                .Where(project => selectedProjectIds.Contains(project.Id))
                .Select(project => new
                {
                    project.Id,
                    project.Name,
                    project.Identifier,
                    project.NavigationConfig
                })
                .ToListAsync();

            var assignmentSnapshots = await context.TaskAssignments
                .AsNoTracking()
                .Where(assignment =>
                    assignment.Status &&
                    selectedProjectIds.Contains(assignment.WorkTask.ProjectId) &&
                    !assignment.WorkTask.IsDeleted)
                .Select(assignment => new
                {
                    assignment.UserId,
                    UserName = assignment.User.FullName ?? assignment.User.Email,
                    assignment.WorkTaskId,
                    assignment.WorkTask.ProjectId,
                    assignment.EstimatedHours,
                    assignment.TotalActualHours,
                    assignment.ProgressPercent
                })
                .ToListAsync();

            var userLoggedHours = await context.TimeLogs
                .AsNoTracking()
                .Where(log => selectedTaskIds.Contains(log.WorkTaskId))
                .GroupBy(log => log.UserId)
                .Select(group => new
                {
                    UserId = group.Key,
                    LoggedHours = group.Sum(item => item.Hours)
                })
                .ToDictionaryAsync(item => item.UserId, item => item.LoggedHours);

            var totalCommittedStoryPoints = Math.Round(taskSnapshots.Sum(task => task.StoryPoints), 1);
            var completedTasks = taskSnapshots
                .Where(task => IsDoneStatusName(task.StatusName))
                .ToList();
            var completedStoryPoints = Math.Round(completedTasks.Sum(task => task.StoryPoints), 1);
            var carryOverStoryPoints = Math.Round(taskSnapshots
                .Where(task => !IsDoneStatusName(task.StatusName))
                .Sum(task => task.StoryPoints), 1);

            var totalEstimatedHours = Math.Round(taskSnapshots.Sum(task => task.TotalEstimatedHours), 1);
            var totalActualHours = Math.Round(taskSnapshots.Sum(task => task.TotalActualHours), 1);
            var totalLoggedHours = Math.Round(selectedTaskIds.Sum(taskIdValue => timeLogs.GetValueOrDefault(taskIdValue)), 1);

            var accuracyRows = taskSnapshots
                .Where(task => task.TotalEstimatedHours > 0 || task.TotalActualHours > 0)
                .Select(task =>
                {
                    var estimate = Math.Max(0, task.TotalEstimatedHours);
                    var actual = Math.Max(0, task.TotalActualHours);
                    var variance = Math.Round(actual - estimate, 1);
                    var accuracyPercent = estimate <= 0
                        ? (actual <= 0 ? 100 : 0)
                        : Math.Max(0, Math.Round(100 - (Math.Abs(actual - estimate) / estimate * 100), 1));

                    string bucket;
                    if (estimate <= 0 && actual > 0)
                    {
                        bucket = "Unplanned";
                    }
                    else if (variance > 0.5)
                    {
                        bucket = "Under-estimated";
                    }
                    else if (variance < -0.5)
                    {
                        bucket = "Over-estimated";
                    }
                    else
                    {
                        bucket = "Accurate";
                    }

                    return new
                    {
                        task.Id,
                        task.ProjectId,
                        task.Title,
                        task.SequenceId,
                        EstimatedHours = Math.Round(estimate, 1),
                        ActualHours = Math.Round(actual, 1),
                        LoggedHours = Math.Round(timeLogs.GetValueOrDefault(task.Id), 1),
                        VarianceHours = variance,
                        AccuracyPercent = accuracyPercent,
                        Bucket = bucket,
                        task.Priority,
                        task.AssigneeCount
                    };
                })
                .OrderBy(row => row.AccuracyPercent)
                .ThenByDescending(row => Math.Abs(row.VarianceHours))
                .ToList();

            var workloadRows = await context.ProjectMembers
                .AsNoTracking()
                .Where(member => selectedProjectIds.Contains(member.ProjectId) && member.Status)
                .Select(member => new
                {
                    member.ProjectId,
                    member.ProjectRole,
                    member.UserId,
                    UserName = member.User.FullName ?? member.User.Email,
                    AssignedTasks = context.TaskAssignments.Count(assignment => assignment.UserId == member.UserId && assignment.Status && selectedProjectIds.Contains(assignment.WorkTask.ProjectId) && !assignment.WorkTask.IsDeleted),
                    EstimatedHours = context.TaskAssignments
                        .Where(assignment => assignment.UserId == member.UserId && assignment.Status && selectedProjectIds.Contains(assignment.WorkTask.ProjectId) && !assignment.WorkTask.IsDeleted)
                        .Sum(assignment => (double?)assignment.EstimatedHours) ?? 0,
                    ActualHours = context.TaskAssignments
                        .Where(assignment => assignment.UserId == member.UserId && assignment.Status && selectedProjectIds.Contains(assignment.WorkTask.ProjectId) && !assignment.WorkTask.IsDeleted)
                        .Sum(assignment => (double?)assignment.TotalActualHours) ?? 0
                })
                .ToListAsync();

            var overCapacityRows = workloadRows
                .Select(row => new
                {
                    row.ProjectId,
                    row.ProjectRole,
                    row.UserId,
                    row.UserName,
                    row.AssignedTasks,
                    EstimatedHours = Math.Round(row.EstimatedHours, 1),
                    ActualHours = Math.Round(row.ActualHours, 1),
                    CapacityPercent = Math.Round(row.EstimatedHours <= 0 ? 0 : (row.ActualHours / row.EstimatedHours) * 100, 1),
                    CapacityState = row.EstimatedHours switch
                    {
                        <= 0 => "Idle",
                        _ when row.ActualHours > row.EstimatedHours * 1.1 => "Over capacity",
                        _ when row.ActualHours > row.EstimatedHours * 0.8 => "Near limit",
                        _ => "Healthy"
                    }
                })
                .OrderByDescending(row => row.CapacityPercent)
                .ThenByDescending(row => row.AssignedTasks)
                .ToList();

            var canConfirmBaseline = HasSystemAdminAccess(User) || accessibleMemberships.Any(member =>
                (!projectId.HasValue || member.ProjectId == projectId.Value) &&
                HasProjectManagerAccess(member.ProjectRole));

            var projectSummaries = projects
                .Select(project =>
                {
                    var projectTasks = taskSnapshots.Where(task => task.ProjectId == project.Id).ToList();
                    var doneTasks = projectTasks.Where(task => IsDoneStatusName(task.StatusName)).ToList();
                    var navigationConfig = ParseNavigationConfig(project.NavigationConfig);
                    navigationConfig.TryGetValue("planningBaseline", out var baselineValue);

                    return new
                    {
                        project.Id,
                        project.Name,
                        project.Identifier,
                        VelocityCommitted = Math.Round(projectTasks.Sum(task => task.StoryPoints), 1),
                        VelocityCompleted = Math.Round(doneTasks.Sum(task => task.StoryPoints), 1),
                        CarryOver = Math.Round(projectTasks.Where(task => !IsDoneStatusName(task.StatusName)).Sum(task => task.StoryPoints), 1),
                        AverageAccuracy = accuracyRows.Where(row => row.ProjectId == project.Id).Select(row => row.AccuracyPercent).DefaultIfEmpty(100).Average(),
                        Baseline = baselineValue
                    };
                })
                .ToList();

            var sprintSummaries = taskSnapshots
                .GroupBy(task => task.SprintId)
                .Select(group =>
                {
                    var sprintIdValue = group.Key;
                    var sprintMeta = sprintIdValue.HasValue && sprintLookup.TryGetValue(sprintIdValue.Value, out var lookupValue)
                        ? lookupValue
                        : null;
                    var sprintTasks = group.ToList();
                    var sprintCompleted = sprintTasks.Where(task => IsDoneStatusName(task.StatusName)).ToList();
                    var committed = Math.Round(sprintTasks.Sum(task => task.StoryPoints), 1);
                    var completed = Math.Round(sprintCompleted.Sum(task => task.StoryPoints), 1);

                    return new
                    {
                        sprintId = sprintIdValue,
                        sprintName = sprintMeta?.Name ?? "No cycle",
                        projectId = sprintMeta?.ProjectId ?? sprintTasks.Select(task => task.ProjectId).FirstOrDefault(),
                        committedStoryPoints = committed,
                        completedStoryPoints = completed,
                        carryOverStoryPoints = Math.Round(sprintTasks.Where(task => !IsDoneStatusName(task.StatusName)).Sum(task => task.StoryPoints), 1),
                        completionRate = committed <= 0 ? 0 : Math.Round((completed / committed) * 100, 1),
                        taskCount = sprintTasks.Count
                    };
                })
                .OrderByDescending(item => item.committedStoryPoints)
                .ThenBy(item => item.sprintName)
                .ToList();

            var userPerformanceRows = assignmentSnapshots
                .GroupBy(item => new { item.UserId, item.UserName })
                .Select(group =>
                {
                    var rows = group.ToList();
                    var estimated = Math.Round(rows.Sum(item => item.EstimatedHours), 1);
                    var actual = Math.Round(rows.Sum(item => item.TotalActualHours), 1);
                    var logged = Math.Round(userLoggedHours.GetValueOrDefault(group.Key.UserId), 1);
                    var accuracyValues = rows.Select(item =>
                    {
                        var estimate = Math.Max(0, item.EstimatedHours);
                        var actualHours = Math.Max(0, item.TotalActualHours);
                        if (estimate <= 0)
                        {
                            return actualHours <= 0 ? 100d : 0d;
                        }

                        return Math.Max(0, Math.Round(100 - (Math.Abs(actualHours - estimate) / estimate * 100), 1));
                    }).ToList();

                    var accurateCount = rows.Count(item => Math.Abs(item.TotalActualHours - item.EstimatedHours) <= 0.5);
                    var underEstimatedCount = rows.Count(item => item.TotalActualHours - item.EstimatedHours > 0.5);
                    var overEstimatedCount = rows.Count(item => item.EstimatedHours - item.TotalActualHours > 0.5);

                    return new
                    {
                        userId = group.Key.UserId,
                        userName = group.Key.UserName,
                        taskCount = rows.Count,
                        estimatedHours = estimated,
                        actualHours = actual,
                        loggedHours = logged,
                        averageAccuracyPercent = Math.Round(accuracyValues.DefaultIfEmpty(100).Average(), 1),
                        averageProgressPercent = Math.Round(rows.Select(item => item.ProgressPercent).DefaultIfEmpty(0).Average(), 1),
                        accurateCount,
                        underEstimatedCount,
                        overEstimatedCount
                    };
                })
                .OrderBy(item => item.averageAccuracyPercent)
                .ThenByDescending(item => item.loggedHours)
                .ToList();

            return Ok(new
            {
                statusCode = 200,
                data = new
                {
                    overview = new
                    {
                        totalProjects = projects.Count,
                        totalTasks = taskSnapshots.Count,
                        totalCommittedStoryPoints,
                        completedStoryPoints,
                        carryOverStoryPoints,
                        totalEstimatedHours,
                        totalActualHours,
                        totalLoggedHours
                    },
                    velocity = new
                    {
                        committedStoryPoints = totalCommittedStoryPoints,
                        completedStoryPoints,
                        carryOverStoryPoints,
                        completionRate = totalCommittedStoryPoints <= 0 ? 0 : Math.Round((completedStoryPoints / totalCommittedStoryPoints) * 100, 1),
                        byProject = projectSummaries,
                        bySprint = sprintSummaries.Take(12).ToList()
                    },
                    estimateAccuracy = new
                    {
                        averageAccuracyPercent = Math.Round(accuracyRows.Select(row => row.AccuracyPercent).DefaultIfEmpty(100).Average(), 1),
                        accurateCount = accuracyRows.Count(row => row.Bucket == "Accurate"),
                        underEstimatedCount = accuracyRows.Count(row => row.Bucket == "Under-estimated"),
                        overEstimatedCount = accuracyRows.Count(row => row.Bucket == "Over-estimated"),
                        unplannedCount = accuracyRows.Count(row => row.Bucket == "Unplanned"),
                        rows = accuracyRows.Take(12).ToList(),
                        byUser = userPerformanceRows.Take(12).ToList()
                    },
                    workload = new
                    {
                        rows = overCapacityRows.Take(12).ToList(),
                        overCapacityCount = overCapacityRows.Count(row => row.CapacityState == "Over capacity"),
                        nearLimitCount = overCapacityRows.Count(row => row.CapacityState == "Near limit")
                    },
                    managerReview = new
                    {
                        canConfirmBaseline,
                        selectedProjectId = projectId,
                        projects = projectSummaries,
                        riskSummary = new
                        {
                            overCapacityMembers = overCapacityRows.Count(row => row.CapacityState == "Over capacity"),
                            nearLimitMembers = overCapacityRows.Count(row => row.CapacityState == "Near limit"),
                            carryOverProjects = projectSummaries.Count(project => project.CarryOver > 0),
                            unplannedTasks = accuracyRows.Count(row => row.Bucket == "Unplanned")
                        }
                    }
                }
            });
        }

        [HttpPost("/api/analytics/projects/{projectId}/confirm-baseline")]
        public async Task<IActionResult> ConfirmPlanningBaseline(
            Guid projectId,
            [FromServices] ApplicationDbContext context)
        {
            var userIdStr = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (!Guid.TryParse(userIdStr, out var userId))
            {
                return Unauthorized();
            }

            var project = await context.Projects.FirstOrDefaultAsync(item => item.Id == projectId && !item.IsDeleted);
            if (project == null)
            {
                return NotFound(new { statusCode = 404, message = "Project not found." });
            }

            var isSystemAdmin = HasSystemAdminAccess(User);
            var membership = await context.ProjectMembers
                .AsNoTracking()
                .FirstOrDefaultAsync(item => item.ProjectId == projectId && item.UserId == userId && item.Status);

            if (!isSystemAdmin && !HasProjectManagerAccess(membership?.ProjectRole))
            {
                return StatusCode(403, new { statusCode = 403, message = "You do not have permission to confirm this planning baseline." });
            }

            var taskSnapshot = await context.WorkTasks
                .AsNoTracking()
                .Where(task => task.ProjectId == projectId && !task.IsDeleted)
                .Select(task => new
                {
                    task.Id,
                    task.StoryPoints,
                    task.TotalEstimatedHours,
                    task.TotalActualHours,
                    StatusName = task.TaskStatus.Name
                })
                .ToListAsync();

            var payload = new
            {
                confirmedAt = DateTime.UtcNow,
                confirmedByUserId = userId,
                committedStoryPoints = Math.Round(taskSnapshot.Sum(task => task.StoryPoints), 1),
                completedStoryPoints = Math.Round(taskSnapshot.Where(task => IsDoneStatusName(task.StatusName)).Sum(task => task.StoryPoints), 1),
                estimatedHours = Math.Round(taskSnapshot.Sum(task => task.TotalEstimatedHours), 1),
                actualHours = Math.Round(taskSnapshot.Sum(task => task.TotalActualHours), 1)
            };

            project.NavigationConfig = BuildPlanningBaselinePayload(project.NavigationConfig, payload);
            project.UpdatedAt = DateTime.UtcNow;
            await context.SaveChangesAsync();

            return Ok(new
            {
                statusCode = 200,
                message = "Planning baseline confirmed.",
                data = payload
            });
        }

        [HttpPost("projects/{projectId}/WorkTasks/import")]
        public async Task<IActionResult> ImportWorkTasks(Guid projectId, [FromBody] List<ImportTaskRowDto> rows, [FromServices] ApplicationDbContext context)
        {
            var userIdStr = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (!Guid.TryParse(userIdStr, out Guid userId))
                return Unauthorized();

            if (rows == null || rows.Count == 0)
                return BadRequest(new { statusCode = 400, message = "Không có dữ liệu để nhập.", errors = new[] { "Danh sách công việc trống." } });

            var project = await context.Projects.FirstOrDefaultAsync(p => p.Id == projectId && !p.IsDeleted);
            if (project == null)
                return NotFound(new { statusCode = 404, message = "Không tìm thấy dự án." });

            var members = await context.ProjectMembers
                .Where(pm => pm.ProjectId == projectId)
                .Select(pm => pm.User)
                .Where(u => u != null)
                .ToListAsync();
            var memberEmails = members.ToDictionary(m => m.Email.ToLowerInvariant(), m => m.Id);

            var statuses = await context.TaskStatuses
                .Where(ts => ts.ProjectId == projectId)
                .ToListAsync();
            var statusMap = statuses.ToDictionary(s => NormalizeImportToken(s.Name), s => s.Id);

            var todoStatus = statuses.FirstOrDefault(s => NormalizeImportToken(s.Name) == "TO DO") ?? statuses.FirstOrDefault();
            var defaultStatusId = todoStatus?.Id ?? Guid.Empty;

            var defaultType = await context.TaskTypes.FirstOrDefaultAsync(tt => tt.ProjectId == projectId);
            var defaultTypeId = defaultType?.Id ?? Guid.Empty;

            if (defaultStatusId == Guid.Empty || defaultTypeId == Guid.Empty)
                return BadRequest(new { statusCode = 400, message = "Dự án chưa có trạng thái hoặc loại công việc mặc định." });

            double maxSort = await context.WorkTasks
                .Where(wt => wt.ProjectId == projectId && !wt.IsDeleted)
                .MaxAsync(wt => (double?)wt.SortOrder) ?? 0;

            int importedCount = 0;
            var newTasks = new List<WorkTask>();
            var errors = new List<string>();

            for (var rowIndex = 0; rowIndex < rows.Count; rowIndex++)
            {
                var row = rows[rowIndex];
                var rowNumber = rowIndex + 1;
                if (string.IsNullOrWhiteSpace(row.Title))
                    continue;

                Guid? assignedUserId = null;
                if (!string.IsNullOrWhiteSpace(row.AssigneeEmail))
                {
                    var emailLower = row.AssigneeEmail.Trim().ToLowerInvariant();
                    if (memberEmails.TryGetValue(emailLower, out var uid))
                    {
                        assignedUserId = uid;
                    }
                }

                Guid statusId = defaultStatusId;
                if (!string.IsNullOrWhiteSpace(row.Status))
                {
                    var statusKey = NormalizeImportToken(row.Status);
                    if (statusMap.TryGetValue(statusKey, out var sid))
                    {
                        statusId = sid;
                    }
                    else
                    {
                        errors.Add($"Dòng {rowNumber}: trạng thái '{row.Status}' không tồn tại trong dự án.");
                        continue;
                    }
                }

                DateTime? startDate = null;
                if (!TryParseImportDate(row.StartDate, out startDate))
                {
                    errors.Add($"Dòng {rowNumber}: ngày bắt đầu '{row.StartDate}' không hợp lệ. Dùng yyyy-MM-dd, dd/MM/yyyy hoặc serial date của Excel.");
                    continue;
                }

                DateTime? dueDate = null;
                if (!TryParseImportDate(row.DueDate, out dueDate))
                {
                    errors.Add($"Dòng {rowNumber}: hạn hoàn thành '{row.DueDate}' không hợp lệ. Dùng yyyy-MM-dd, dd/MM/yyyy hoặc serial date của Excel.");
                    continue;
                }

                if (startDate.HasValue && dueDate.HasValue && dueDate.Value.Date < startDate.Value.Date)
                {
                    errors.Add($"Dòng {rowNumber}: hạn hoàn thành không được trước ngày bắt đầu.");
                    continue;
                }

                double storyPoints = 0;
                if (!string.IsNullOrWhiteSpace(row.StoryPoints) && double.TryParse(row.StoryPoints, NumberStyles.Float, CultureInfo.InvariantCulture, out var sp))
                {
                    storyPoints = sp;
                }

                maxSort += 65536;
                project.IssueSequence += 1;
                string sequenceId = $"{project.Identifier}-{project.IssueSequence}";

                var newTask = new WorkTask
                {
                    Id = Guid.NewGuid(),
                    ProjectId = projectId,
                    WorkspaceId = project.WorkspaceId,
                    Title = row.Title.Trim(),
                    Description = row.Description,
                    TaskStatusId = statusId,
                    TaskTypeId = defaultTypeId,
                    ReporterId = userId,
                    AssignedUserId = assignedUserId,
                    Priority = ParsePriority(row.Priority),
                    PlannedStartDate = startDate,
                    DueDate = dueDate,
                    StoryPoints = storyPoints,
                    SortOrder = maxSort,
                    SequenceId = sequenceId,
                    CreatedAt = DateTime.UtcNow,
                    UpdatedAt = DateTime.UtcNow
                };

                newTasks.Add(newTask);
                importedCount++;
            }

            if (errors.Count > 0)
                return BadRequest(new { statusCode = 400, message = "Dữ liệu import chưa hợp lệ.", errors });

            if (newTasks.Any())
            {
                context.WorkTasks.AddRange(newTasks);
                await context.SaveChangesAsync();

                try
                {
                    await _kanbanHub.Clients.Group(projectId.ToString()).SendAsync("TasksUpdated");
                }
                catch
                {
                }
            }

            return Ok(new { statusCode = 200, message = $"Đã nhập thành công {importedCount} công việc.", count = importedCount });
        }
        [HttpGet("projects/{projectId}/WorkTasks/export")]
        public async Task<IActionResult> ExportWorkTasks(Guid projectId, [FromServices] ApplicationDbContext context)
        {
            var project = await context.Projects.FirstOrDefaultAsync(p => p.Id == projectId && !p.IsDeleted);
            if (project == null)
                return NotFound(new { statusCode = 404, message = "Không tìm thấy dự án." });

            var tasks = await context.WorkTasks
                .Include(t => t.TaskStatus)
                .Include(t => t.AssignedUser)
                .Include(t => t.Reporter)
                .Include(t => t.Sprint)
                .Include(t => t.IssueModules).ThenInclude(issueModule => issueModule.Module)
                .Include(t => t.IssueLabels).ThenInclude(issueLabel => issueLabel.Label)
                .Include(t => t.CustomFieldValues).ThenInclude(value => value.FieldDefinition)
                .Where(t => t.ProjectId == projectId && !t.IsDeleted)
                .OrderBy(t => t.SequenceId)
                .ToListAsync();

            var csv = new StringBuilder();
            csv.AppendLine("Mã công việc,Tiêu đề,Mô tả,Dự án,Trạng thái,Ưu tiên,Người phụ trách,Người tạo,Ngày bắt đầu,Hạn hoàn thành,Tiến độ,Chu kỳ,Mô-đun,Nhãn,Ngày tạo,Ngày cập nhật,Custom Fields");

            foreach (var task in tasks)
            {
                var priorityStr = task.Priority switch
                {
                    1 => "Khẩn cấp",
                    2 => "Cao",
                    3 => "Trung bình",
                    4 => "Thấp",
                    _ => "Trung bình"
                };

                var progress = task.TotalEstimatedHours > 0
                    ? Math.Clamp(task.TotalActualHours / task.TotalEstimatedHours * 100, 0, 100).ToString("0.##", CultureInfo.InvariantCulture) + "%"
                    : "";
                var modules = string.Join("; ", task.IssueModules
                    .Select(issueModule => issueModule.Module?.Name)
                    .Where(name => !string.IsNullOrWhiteSpace(name)));
                var labels = string.Join("; ", task.IssueLabels
                    .Select(issueLabel => issueLabel.Label?.Name)
                    .Where(name => !string.IsNullOrWhiteSpace(name)));
                var customFields = string.Join("; ", task.CustomFieldValues
                    .Where(value => value.FieldDefinition != null && !string.IsNullOrWhiteSpace(value.FieldDefinition.Name))
                    .OrderBy(value => value.FieldDefinition!.SortOrder)
                    .Select(value => $"{value.FieldDefinition!.Name}: {NormalizeExportValue(value.Value)}")
                    .Where(value => !string.IsNullOrWhiteSpace(value)));

                var values = new[]
                {
                    task.SequenceId ?? string.Empty,
                    task.Title,
                    task.Description ?? string.Empty,
                    project.Name,
                    task.TaskStatus?.Name ?? "Backlog",
                    priorityStr,
                    task.AssignedUser?.FullName ?? task.AssignedUser?.Email ?? string.Empty,
                    task.Reporter?.FullName ?? task.Reporter?.Email ?? string.Empty,
                    task.PlannedStartDate?.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture) ?? string.Empty,
                    task.DueDate?.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture) ?? string.Empty,
                    progress,
                    task.Sprint?.Name ?? string.Empty,
                    modules,
                    labels,
                    task.CreatedAt.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture),
                    task.UpdatedAt.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture),
                    customFields
                };

                csv.AppendLine(string.Join(",", values.Select(EscapeCsv)));
            }

            var bytes = Encoding.UTF8.GetBytes(csv.ToString());
            var bomBytes = Encoding.UTF8.GetPreamble();
            var fileBytes = new byte[bomBytes.Length + bytes.Length];
            Buffer.BlockCopy(bomBytes, 0, fileBytes, 0, bomBytes.Length);
            Buffer.BlockCopy(bytes, 0, fileBytes, bomBytes.Length, bytes.Length);

            var safeProjectName = SanitizeFileName(project.Identifier ?? project.Name);
            return File(fileBytes, "text/csv; charset=utf-8", $"SprintA-WorkItems-{safeProjectName}-{DateTime.UtcNow:yyyyMMddHHmmss}.csv");
        }
        private static string EscapeCsv(string? value)
        {
            if (string.IsNullOrEmpty(value)) return "";
            var clean = value;
            if (clean.Contains(",") || clean.Contains("\"") || clean.Contains("\r") || clean.Contains("\n"))
            {
                clean = "\"" + clean.Replace("\"", "\"\"") + "\"";
            }
            return clean;
        }

        // ===================================        // Contingency Plan APIs
        // ===================================
        [HttpGet("worktasks/{id}/contingency-plans")]
        [Authorize]
        public async Task<IActionResult> GetContingencyPlans(Guid id)
        {
            try
            {
                var plans = await _workTaskService.GetContingencyPlansAsync(id);
                return Ok(new { statusCode = 200, data = plans });
            }
            catch (Exception ex)
            {
                return BadRequest(new { statusCode = 400, message = ex.Message });
            }
        }

        [HttpGet("worktasks/{id}/contingency-plans/{planId}")]
        [Authorize]
        public async Task<IActionResult> GetContingencyPlan(Guid id, Guid planId)
        {
            try
            {
                var plan = await _workTaskService.GetContingencyPlanByIdAsync(id, planId);
                if (plan == null) return NotFound(new { statusCode = 404, message = "Plan not found" });
                return Ok(new { statusCode = 200, data = plan });
            }
            catch (Exception ex)
            {
                return BadRequest(new { statusCode = 400, message = ex.Message });
            }
        }

        [HttpPost("worktasks/{id}/contingency-plans")]
        [Authorize]
        public async Task<IActionResult> CreateContingencyPlan(Guid id, [FromBody] TaskManagement.Application.DTOs.WorkTask.CreateContingencyPlanDto dto)
        {
            if (!ModelState.IsValid) return BadRequest(ModelState);
            
            var userIdClaim = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrEmpty(userIdClaim) || !Guid.TryParse(userIdClaim, out Guid userId))
                return Unauthorized(new { statusCode = 401, message = "User is not authenticated." });

            try
            {
                var plan = await _workTaskService.CreateContingencyPlanAsync(id, userId, dto);
                return Ok(new { statusCode = 201, message = "Plan created", data = plan });
            }
            catch (Exception ex)
            {
                return BadRequest(new { statusCode = 400, message = ex.Message });
            }
        }

        [HttpPut("worktasks/{id}/contingency-plans/{planId}")]
        [Authorize]
        public async Task<IActionResult> UpdateContingencyPlan(Guid id, Guid planId, [FromBody] TaskManagement.Application.DTOs.WorkTask.UpdateContingencyPlanDto dto)
        {
            if (!ModelState.IsValid) return BadRequest(ModelState);
            
            var userIdClaim = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrEmpty(userIdClaim) || !Guid.TryParse(userIdClaim, out Guid userId))
                return Unauthorized(new { statusCode = 401, message = "User is not authenticated." });

            try
            {
                var plan = await _workTaskService.UpdateContingencyPlanAsync(id, planId, userId, dto);
                return Ok(new { statusCode = 200, message = "Plan updated", data = plan });
            }
            catch (Exception ex)
            {
                return BadRequest(new { statusCode = 400, message = ex.Message });
            }
        }

        [HttpDelete("worktasks/{id}/contingency-plans/{planId}")]
        [Authorize]
        public async Task<IActionResult> DeleteContingencyPlan(Guid id, Guid planId)
        {
            var userIdClaim = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrEmpty(userIdClaim) || !Guid.TryParse(userIdClaim, out Guid userId))
                return Unauthorized(new { statusCode = 401, message = "User is not authenticated." });

            try
            {
                await _workTaskService.DeleteContingencyPlanAsync(id, planId, userId);
                return Ok(new { statusCode = 200, message = "Plan deleted" });
            }
            catch (Exception ex)
            {
                return BadRequest(new { statusCode = 400, message = ex.Message });
            }
        }

        [HttpPost("worktasks/{id}/contingency-plans/{planId}/tasks/create")]
        [Authorize]
        public async Task<IActionResult> CreateContingencyTask(Guid id, Guid planId, [FromBody] TaskManagement.Application.DTOs.WorkTask.CreateContingencyTaskDto dto)
        {
            var userIdClaim = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrEmpty(userIdClaim) || !Guid.TryParse(userIdClaim, out Guid userId))
                return Unauthorized(new { statusCode = 401, message = "User is not authenticated." });

            try
            {
                await _workTaskService.CreateContingencyTaskToPlanAsync(id, planId, dto, userId);
                return Ok(new { statusCode = 200, message = "Contingency task created successfully" });
            }
            catch (Exception ex)
            {
                return BadRequest(new { statusCode = 400, message = ex.Message });
            }
        }

        [HttpPost("worktasks/{id}/contingency-plans/{planId}/tasks")]
        [Authorize]
        public async Task<IActionResult> AddContingencyTask(Guid id, Guid planId, [FromBody] Guid fallbackTaskId)
        {
            var userIdClaim = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrEmpty(userIdClaim) || !Guid.TryParse(userIdClaim, out Guid userId))
                return Unauthorized(new { statusCode = 401, message = "User is not authenticated." });

            try
            {
                await _workTaskService.AddContingencyTaskToPlanAsync(id, planId, fallbackTaskId, userId);
                return Ok(new { statusCode = 200, message = "Task linked to plan successfully" });
            }
            catch (Exception ex)
            {
                return BadRequest(new { statusCode = 400, message = ex.Message });
            }
        }

        [HttpDelete("worktasks/{id}/contingency-plans/{planId}/tasks/{fallbackTaskId}")]
        [Authorize]
        public async Task<IActionResult> RemoveContingencyTask(Guid id, Guid planId, Guid fallbackTaskId)
        {
            var userIdClaim = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrEmpty(userIdClaim) || !Guid.TryParse(userIdClaim, out Guid userId))
                return Unauthorized(new { statusCode = 401, message = "User is not authenticated." });

            try
            {
                await _workTaskService.RemoveContingencyTaskFromPlanAsync(id, planId, fallbackTaskId, userId);
                return Ok(new { statusCode = 200, message = "Task removed from plan successfully" });
            }
            catch (Exception ex)
            {
                return BadRequest(new { statusCode = 400, message = ex.Message });
            }
        }

        [HttpPost("worktasks/{id}/contingency-plans/{planId}/tasks/{fallbackTaskId}/activate")]
        [Authorize]
        public async Task<IActionResult> ActivateContingencyTask(Guid id, Guid planId, Guid fallbackTaskId)
        {
            var userIdClaim = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrEmpty(userIdClaim) || !Guid.TryParse(userIdClaim, out Guid userId))
                return Unauthorized(new { statusCode = 401, message = "User is not authenticated." });

            try
            {
                await _workTaskService.ActivateContingencyTaskAsync(id, planId, fallbackTaskId, userId);
                return Ok(new { statusCode = 200, message = "Contingency task activated successfully" });
            }
            catch (Exception ex)
            {
                return BadRequest(new { statusCode = 400, message = ex.Message });
            }
        }

        private static bool TryParseImportDate(string? value, out DateTime? date)
        {
            date = null;
            if (string.IsNullOrWhiteSpace(value))
                return true;

            var raw = value.Trim();
            if (DateTime.TryParseExact(raw,
                    new[] { "yyyy-MM-dd", "yyyy/MM/dd", "dd/MM/yyyy", "d/M/yyyy", "dd-MM-yyyy", "d-M-yyyy", "MM/dd/yyyy", "M/d/yyyy" },
                    CultureInfo.InvariantCulture,
                    DateTimeStyles.AssumeLocal,
                    out var exact))
            {
                date = DateTime.SpecifyKind(exact.Date, DateTimeKind.Utc);
                return true;
            }

            if (double.TryParse(raw, NumberStyles.Float, CultureInfo.InvariantCulture, out var serial) && serial > 0 && serial < 60000)
            {
                date = DateTime.SpecifyKind(DateTime.FromOADate(serial).Date, DateTimeKind.Utc);
                return true;
            }

            return false;
        }

        private static int ParsePriority(string? priority)
        {
            if (string.IsNullOrWhiteSpace(priority)) return 3;

            var token = NormalizeImportToken(priority);
            if (token.Contains("URGENT") || token.Contains("KHAN") || token.Contains("1")) return 1;
            if (token.Contains("HIGH") || token.Contains("CAO") || token.Contains("2")) return 2;
            if (token.Contains("LOW") || token.Contains("THAP") || token.Contains("4")) return 4;
            return 3;
        }

        private static string NormalizeImportToken(string? value)
        {
            if (string.IsNullOrWhiteSpace(value)) return string.Empty;

            var normalized = value.Trim().Normalize(NormalizationForm.FormD);
            var builder = new StringBuilder(normalized.Length);
            foreach (var ch in normalized)
            {
                var category = CharUnicodeInfo.GetUnicodeCategory(ch);
                if (category != UnicodeCategory.NonSpacingMark)
                {
                    builder.Append(char.ToUpperInvariant(ch));
                }
            }

            return builder.ToString().Normalize(NormalizationForm.FormC);
        }

        private static string NormalizeExportValue(string? value)
        {
            if (string.IsNullOrWhiteSpace(value) || value.Trim().Equals("[object Object]", StringComparison.OrdinalIgnoreCase))
                return string.Empty;

            var trimmed = value.Trim();
            if ((trimmed.StartsWith("{", StringComparison.Ordinal) && trimmed.EndsWith("}", StringComparison.Ordinal)) ||
                (trimmed.StartsWith("[", StringComparison.Ordinal) && trimmed.EndsWith("]", StringComparison.Ordinal)))
            {
                try
                {
                    using var document = JsonDocument.Parse(trimmed);
                    return JsonElementToDisplayText(document.RootElement);
                }
                catch (JsonException)
                {
                    return trimmed;
                }
            }

            return trimmed;
        }

        private static string JsonElementToDisplayText(JsonElement element)
        {
            return element.ValueKind switch
            {
                JsonValueKind.Object => string.Join(", ", element.EnumerateObject()
                    .Select(property => $"{property.Name}: {JsonElementToDisplayText(property.Value)}")
                    .Where(value => !string.IsNullOrWhiteSpace(value))),
                JsonValueKind.Array => string.Join(", ", element.EnumerateArray()
                    .Select(JsonElementToDisplayText)
                    .Where(value => !string.IsNullOrWhiteSpace(value))),
                JsonValueKind.String => element.GetString() ?? string.Empty,
                JsonValueKind.Number => element.ToString(),
                JsonValueKind.True => "true",
                JsonValueKind.False => "false",
                _ => string.Empty
            };
        }

        private static string SanitizeFileName(string value)
        {
            var invalidChars = Path.GetInvalidFileNameChars();
            var safe = new string(value.Select(ch => invalidChars.Contains(ch) ? '-' : ch).ToArray()).Trim();
            return string.IsNullOrWhiteSpace(safe) ? "Project" : safe;
        }
    }

    public class ImportTaskRowDto
    {
        public string Title { get; set; } = string.Empty;
        public string? Description { get; set; }
        public string? AssigneeEmail { get; set; }
        public string? Status { get; set; }
        public string? Priority { get; set; }
        public string? StartDate { get; set; }
        public string? DueDate { get; set; }
        public string? StoryPoints { get; set; }
    }

    public class ReorderTaskDto
    {
        public double SortOrder { get; set; }
        public string? NewStatusName { get; set; }
    }

    public class SaveTaskStatusRequest
    {
        public string? Name { get; set; }
        public string? ColorCode { get; set; }
        public int? Position { get; set; }
    }

    public class CreateTimeLogRequest
    {
        public double Hours { get; set; }
        public string? WorkType { get; set; }
        public string? Note { get; set; }
    }

    public class NotificationActorContext
    {
        public string ActorName { get; set; } = "System";
        public string ProjectName { get; set; } = string.Empty;
        public string TaskTitle { get; set; } = string.Empty;
    }
}

