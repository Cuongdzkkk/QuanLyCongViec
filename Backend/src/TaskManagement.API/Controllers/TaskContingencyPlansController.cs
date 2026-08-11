using System.Security.Claims;
using System.Text.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.SignalR;
using TaskManagement.Application.DTOs.Common;
using TaskManagement.Domain.Entities;
using TaskManagement.Infrastructure.Data;
using TaskManagement.API.Hubs;
using TaskManagement.API.Realtime;

namespace TaskManagement.API.Controllers
{
    [ApiController]
    [Route("api/projects/{projectId:guid}/worktasks/{taskId:guid}/contingency-plans")]
    [Authorize]
    public class TaskContingencyPlansController : ControllerBase
    {
        private readonly ApplicationDbContext _context;
        private readonly IHubContext<KanbanHub>? _hub;
        private static readonly string[] ManageRoles = { "PM", "PO", "SM", "PROJECT_MANAGER", "PROJECT_ADMIN", "Project Lead", "Admin" };
        private static readonly string[] OverrideRoles = { "SuperAdmin", "Admin", "System Admin", "Organization Admin", "AccessAdmin", "Access Admin" };
        private static readonly string[] ImpactLevels = { "Low", "Medium", "High", "Critical" };
        private static readonly string[] Statuses = { "Open", "Draft", "Active", "Triggered", "Resolved", "Archived" };

        public TaskContingencyPlansController(ApplicationDbContext context, IHubContext<KanbanHub>? hub = null)
        {
            _context = context;
            _hub = hub;
        }

        public sealed class UpsertContingencyPlanRequest
        {
            public string Risk { get; set; } = string.Empty;
            public string? Cause { get; set; }
            public string ResponsePlan { get; set; } = string.Empty;
            public Guid? SupportPersonId { get; set; }
            public DateTime? ReplacementDeadline { get; set; }
            public string ImpactLevel { get; set; } = "Medium";
            public string? TriggerCondition { get; set; }
            public string Status { get; set; } = "Open";
        }

        [HttpGet]
        public async Task<IActionResult> List(Guid projectId, Guid taskId)
        {
            var userId = GetUserId();
            var task = await LoadTask(projectId, taskId);
            if (task == null) return NotFound(ApiResponse<object>.Error("Task not found."));
            if (!await CanRead(projectId, userId)) return Forbid();

            var items = await _context.TaskContingencyPlans
                .AsNoTracking()
                .Where(item => item.WorkTaskId == taskId)
                .OrderByDescending(item => item.UpdatedAt)
                .Select(item => new
                {
                    item.Id,
                    item.WorkTaskId,
                    item.Risk,
                    item.Cause,
                    item.ResponsePlan,
                    item.SupportPersonId,
                    supportPersonName = item.SupportPerson != null ? item.SupportPerson.FullName : null,
                    item.ReplacementDeadline,
                    item.ImpactLevel,
                    item.TriggerCondition,
                    item.Status,
                    item.CreatedAt,
                    item.UpdatedAt,
                    item.CreatedById,
                    item.UpdatedById
                })
                .ToListAsync();

            return Ok(ApiResponse<object>.Success(items));
        }

        [HttpPost]
        public async Task<IActionResult> Create(Guid projectId, Guid taskId, [FromBody] UpsertContingencyPlanRequest request)
        {
            var userId = GetUserId();
            var task = await LoadTask(projectId, taskId);
            if (task == null) return NotFound(ApiResponse<object>.Error("Task not found."));
            if (!await CanManage(projectId, taskId, userId)) return Forbid();

            var validation = await ValidateRequest(projectId, request);
            if (validation != null) return validation;

            var now = DateTime.UtcNow;
            var plan = new TaskContingencyPlan
            {
                Id = Guid.NewGuid(),
                WorkTaskId = taskId,
                Risk = request.Risk.Trim(),
                Cause = TrimOrNull(request.Cause),
                ResponsePlan = request.ResponsePlan.Trim(),
                SupportPersonId = request.SupportPersonId,
                ReplacementDeadline = request.ReplacementDeadline?.ToUniversalTime(),
                ImpactLevel = request.ImpactLevel,
                TriggerCondition = TrimOrNull(request.TriggerCondition),
                Status = request.Status,
                CreatedById = userId,
                UpdatedById = userId,
                CreatedAt = now,
                UpdatedAt = now
            };

            _context.TaskContingencyPlans.Add(plan);
            await Audit(userId, "TASK_CONTINGENCY_CREATE", projectId, taskId, plan.Id);
            await _context.SaveChangesAsync();
            await PublishChangedAsync(projectId, taskId, plan.Id, "created");

            return CreatedAtAction(nameof(List), new { projectId, taskId }, ApiResponse<object>.Success(Project(plan)));
        }

        [HttpPut("{planId:guid}")]
        public async Task<IActionResult> Update(Guid projectId, Guid taskId, Guid planId, [FromBody] UpsertContingencyPlanRequest request)
        {
            var userId = GetUserId();
            var plan = await _context.TaskContingencyPlans.FirstOrDefaultAsync(item => item.Id == planId && item.WorkTaskId == taskId);
            if (plan == null || await LoadTask(projectId, taskId) == null) return NotFound(ApiResponse<object>.Error("Contingency plan not found."));
            if (!await CanManage(projectId, taskId, userId)) return Forbid();

            var validation = await ValidateRequest(projectId, request);
            if (validation != null) return validation;

            plan.Risk = request.Risk.Trim();
            plan.Cause = TrimOrNull(request.Cause);
            plan.ResponsePlan = request.ResponsePlan.Trim();
            plan.SupportPersonId = request.SupportPersonId;
            plan.ReplacementDeadline = request.ReplacementDeadline?.ToUniversalTime();
            plan.ImpactLevel = request.ImpactLevel;
            plan.TriggerCondition = TrimOrNull(request.TriggerCondition);
            plan.Status = request.Status;
            plan.UpdatedById = userId;
            plan.UpdatedAt = DateTime.UtcNow;

            await Audit(userId, "TASK_CONTINGENCY_UPDATE", projectId, taskId, plan.Id);
            await _context.SaveChangesAsync();
            await PublishChangedAsync(projectId, taskId, plan.Id, "updated");

            return Ok(ApiResponse<object>.Success(Project(plan)));
        }

        [HttpDelete("{planId:guid}")]
        public async Task<IActionResult> Delete(Guid projectId, Guid taskId, Guid planId)
        {
            var userId = GetUserId();
            var plan = await _context.TaskContingencyPlans.FirstOrDefaultAsync(item => item.Id == planId && item.WorkTaskId == taskId);
            if (plan == null || await LoadTask(projectId, taskId) == null) return NotFound(ApiResponse<object>.Error("Contingency plan not found."));
            if (!await CanManage(projectId, taskId, userId)) return Forbid();

            _context.TaskContingencyPlans.Remove(plan);
            await Audit(userId, "TASK_CONTINGENCY_DELETE", projectId, taskId, plan.Id);
            await _context.SaveChangesAsync();
            await PublishChangedAsync(projectId, taskId, plan.Id, "deleted");

            return Ok(ApiResponse<object>.Success(new { deleted = true, planId }));
        }

        private Task PublishChangedAsync(Guid projectId, Guid taskId, Guid planId, string action)
        {
            return _hub == null
                ? Task.CompletedTask
                : _hub.PublishEntityChangedAsync(
                    projectId,
                    "TaskContingencyPlan",
                    action,
                    planId,
                    new { taskId, planId });
        }

        private async Task<WorkTask?> LoadTask(Guid projectId, Guid taskId)
        {
            return await _context.WorkTasks
                .AsNoTracking()
                .FirstOrDefaultAsync(item => item.Id == taskId && item.ProjectId == projectId && !item.IsDeleted);
        }

        private async Task<bool> CanRead(Guid projectId, Guid userId)
        {
            if (await HasOverrideRole(userId)) return true;
            return await _context.ProjectMembers.AnyAsync(item => item.ProjectId == projectId && item.UserId == userId && item.Status && item.LeftAt == null)
                || await _context.Projects.AnyAsync(item => item.Id == projectId && item.CreatorId == userId && !item.IsDeleted)
                || await _context.WorkspaceMembers.AnyAsync(item => item.Workspace.Projects.Any(project => project.Id == projectId) && item.UserId == userId && item.IsActive);
        }

        private async Task<bool> CanManage(Guid projectId, Guid taskId, Guid userId)
        {
            if (await HasOverrideRole(userId)) return true;
            if (await _context.Projects.AnyAsync(item => item.Id == projectId && item.CreatorId == userId && !item.IsDeleted)) return true;

            var projectRole = await _context.ProjectMembers
                .Where(item => item.ProjectId == projectId && item.UserId == userId && item.Status && item.LeftAt == null)
                .Select(item => item.ProjectRole)
                .FirstOrDefaultAsync();

            if (!string.IsNullOrWhiteSpace(projectRole) && ManageRoles.Contains(projectRole, StringComparer.OrdinalIgnoreCase)) return true;

            return await _context.WorkTasks.AnyAsync(item =>
                item.Id == taskId &&
                item.ProjectId == projectId &&
                (item.ReporterId == userId || item.AssignedUserId == userId || item.TaskAssignments.Any(assignment => assignment.UserId == userId && assignment.Status)));
        }

        private async Task<bool> HasOverrideRole(Guid userId)
        {
            var roles = await _context.UserRoles
                .AsNoTracking()
                .Where(item => item.UserId == userId)
                .Select(item => item.Role.Name)
                .ToListAsync();
            return roles.Any(role => OverrideRoles.Contains(role, StringComparer.OrdinalIgnoreCase));
        }

        private async Task<IActionResult?> ValidateRequest(Guid projectId, UpsertContingencyPlanRequest request)
        {
            if (string.IsNullOrWhiteSpace(request.Risk) || string.IsNullOrWhiteSpace(request.ResponsePlan))
            {
                return BadRequest(ApiResponse<object>.Error("Risk and responsePlan are required."));
            }

            if (!ImpactLevels.Contains(request.ImpactLevel, StringComparer.OrdinalIgnoreCase))
            {
                return BadRequest(ApiResponse<object>.Error("Invalid impactLevel."));
            }

            if (!Statuses.Contains(request.Status, StringComparer.OrdinalIgnoreCase))
            {
                return BadRequest(ApiResponse<object>.Error("Invalid status."));
            }

            if (request.SupportPersonId.HasValue)
            {
                var isProjectMember = await _context.ProjectMembers.AnyAsync(item =>
                    item.ProjectId == projectId && item.UserId == request.SupportPersonId && item.Status && item.LeftAt == null);
                if (!isProjectMember)
                {
                    return BadRequest(ApiResponse<object>.Error("Support person must be an active project member."));
                }
            }

            request.ImpactLevel = ImpactLevels.First(item => item.Equals(request.ImpactLevel, StringComparison.OrdinalIgnoreCase));
            request.Status = Statuses.First(item => item.Equals(request.Status, StringComparison.OrdinalIgnoreCase));
            return null;
        }

        private async Task Audit(Guid userId, string action, Guid projectId, Guid taskId, Guid planId)
        {
            _context.SystemAuditLogs.Add(new SystemAuditLog
            {
                Id = Guid.NewGuid(),
                UserId = userId,
                Action = action,
                Resource = $"WorkTask:{taskId}",
                Status = "Success",
                IPAddress = HttpContext.Connection.RemoteIpAddress?.ToString(),
                Details = JsonSerializer.Serialize(new { projectId, taskId, planId }),
                CreatedAt = DateTime.UtcNow
            });
            await Task.CompletedTask;
        }

        private static object Project(TaskContingencyPlan plan)
        {
            return new
            {
                plan.Id,
                plan.WorkTaskId,
                plan.Risk,
                plan.Cause,
                plan.ResponsePlan,
                plan.SupportPersonId,
                plan.ReplacementDeadline,
                plan.ImpactLevel,
                plan.TriggerCondition,
                plan.Status,
                plan.CreatedAt,
                plan.UpdatedAt,
                plan.CreatedById,
                plan.UpdatedById
            };
        }

        private static string? TrimOrNull(string? value)
        {
            return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
        }

        private Guid GetUserId()
        {
            var raw = User.FindFirstValue(ClaimTypes.NameIdentifier);
            return Guid.TryParse(raw, out var id) ? id : Guid.Empty;
        }
    }
}
