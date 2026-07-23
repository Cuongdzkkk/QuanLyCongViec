using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using TaskManagement.API.Filters;
using TaskManagement.Application.Common;
using TaskManagement.Application.DTOs.Common;
using TaskManagement.Application.DTOs.Sprint;
using TaskManagement.Application.Interfaces;
using TaskManagement.Infrastructure.Data;
using TaskManagement.Infrastructure.Services;

namespace TaskManagement.API.Controllers
{
    [ApiController]
    [Route("api/projects/{projectId}/sprints")]
    [Authorize]
    [ProjectAuthorize(ResourcePermissionCodes.ProjectRead)]
    public class SprintsController : ControllerBase
    {
        private readonly ISprintService _sprintService;

        public SprintsController(ISprintService sprintService)
        {
            _sprintService = sprintService;
        }

        [HttpGet]
        public async Task<IActionResult> GetByProject(Guid projectId)
        {
            var sprints = await _sprintService.GetByProjectAsync(projectId);
            return Ok(ApiResponse<List<SprintResponseDto>>.Success(sprints));
        }

        [HttpGet("{id}")]
        public async Task<IActionResult> GetById(Guid projectId, Guid id)
        {
            var sprint = await _sprintService.GetByIdAsync(id);
            if (sprint == null || sprint.ProjectId != projectId)
            {
                return NotFound(ApiResponse<object>.Error("Cycle khong ton tai trong du an nay.", 404));
            }

            return Ok(ApiResponse<SprintResponseDto>.Success(sprint));
        }

        [HttpPost]
        [ProjectAuthorize(ResourcePermissionCodes.SprintManage)]
        public async Task<IActionResult> Create(Guid projectId, [FromBody] CreateSprintDto dto)
        {
            try
            {
                var result = await _sprintService.CreateAsync(projectId, dto);
                return CreatedAtAction(nameof(GetById), new { projectId, id = result.Id }, ApiResponse<SprintResponseDto>.Created(result));
            }
            catch (ArgumentException ex)
            {
                return BadRequest(ApiResponse<object>.Error(ex.Message));
            }
        }

        [HttpPut("{id}")]
        [ProjectAuthorize(ResourcePermissionCodes.SprintManage)]
        public async Task<IActionResult> Update(Guid projectId, Guid id, [FromBody] UpdateSprintDto dto)
        {
            try
            {
                var result = await _sprintService.UpdateAsync(projectId, id, dto);
                return Ok(ApiResponse<SprintResponseDto>.Success(result, "Cycle da duoc cap nhat."));
            }
            catch (ArgumentException ex)
            {
                return BadRequest(ApiResponse<object>.Error(ex.Message));
            }
        }

        [HttpPost("{id}/start")]
        [ProjectAuthorize(ResourcePermissionCodes.SprintManage)]
        public async Task<IActionResult> Start(Guid projectId, Guid id)
        {
            try
            {
                var result = await _sprintService.StartAsync(projectId, id);
                return Ok(ApiResponse<SprintResponseDto>.Success(result, "Cycle da duoc bat dau."));
            }
            catch (ArgumentException ex)
            {
                return BadRequest(ApiResponse<object>.Error(ex.Message));
            }
            catch (InvalidOperationException ex)
            {
                return BadRequest(ApiResponse<object>.Error(ex.Message));
            }
        }

        [HttpPost("{id}/close")]
        [ProjectAuthorize(ResourcePermissionCodes.SprintManage)]
        public async Task<IActionResult> Close(Guid projectId, Guid id, [FromBody] CloseSprintDto dto)
        {
            try
            {
                var userIdString = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
                if (!Guid.TryParse(userIdString, out var actorUserId))
                {
                    return Unauthorized(ApiResponse<object>.Error("Unauthorized.", 401));
                }

                await _sprintService.CloseAsync(id, dto, actorUserId);
                return Ok(ApiResponse<object>.Success(null!, "Cycle da duoc dong va task ton dong da duoc xu ly."));
            }
            catch (ResourceScopeException ex)
            {
                return Problem(
                    statusCode: 400,
                    title: "Invalid sprint rollover scope",
                    detail: ex.Message);
            }
            catch (ArgumentException ex)
            {
                return BadRequest(ApiResponse<object>.Error(ex.Message));
            }
            catch (InvalidOperationException ex)
            {
                return BadRequest(ApiResponse<object>.Error(ex.Message));
            }
        }

        [HttpGet("{id}/carry-over-tasks")]
        [ProjectAuthorize(ResourcePermissionCodes.SprintManage)]
        public async Task<IActionResult> GetCarryOverTasks(
            Guid projectId,
            Guid id,
            [FromQuery] string? search,
            [FromQuery] Guid? assigneeId,
            [FromQuery] string? scope,
            [FromServices] ApplicationDbContext context)
        {
            var sprint = await context.Sprints
                .AsNoTracking()
                .FirstOrDefaultAsync(s => s.Id == id && s.ProjectId == projectId);
            if (sprint == null)
            {
                return NotFound(ApiResponse<object>.Error("Cycle khong ton tai trong du an nay.", 404));
            }

            var doneStatusIds = await context.TaskStatuses
                .Where(ts => ts.ProjectId == projectId &&
                    (ts.Name.Contains("Done") || ts.Name.Contains("Complete") || ts.Name.Contains("Hoan thanh")))
                .Select(ts => ts.Id)
                .ToListAsync();

            var normalizedScope = (scope ?? "all").Trim().ToLowerInvariant();
            var query = context.WorkTasks
                .AsNoTracking()
                .Include(task => task.AssignedUser)
                .Include(task => task.TaskStatus)
                .Include(task => task.Sprint)
                .Where(task =>
                    task.ProjectId == projectId &&
                    !task.IsDeleted &&
                    task.ParentTaskId == null &&
                    !doneStatusIds.Contains(task.TaskStatusId) &&
                    task.AuditLogs.Any(log => log.FieldChanged == "SPRINT_CARRY_OVER" && log.OldValue == id.ToString()));

            if (!string.IsNullOrWhiteSpace(search))
            {
                var keyword = search.Trim();
                query = query.Where(task => task.Title.Contains(keyword) || (task.SequenceId != null && task.SequenceId.Contains(keyword)));
            }

            if (assigneeId.HasValue)
            {
                query = query.Where(task =>
                    task.AssignedUserId == assigneeId.Value ||
                    task.TaskAssignments.Any(assignment => assignment.Status && assignment.UserId == assigneeId.Value));
            }

            if (normalizedScope == "backlog")
            {
                query = query.Where(task => task.SprintId == null);
            }
            else if (normalizedScope == "cycle")
            {
                query = query.Where(task => task.SprintId != null);
            }

            var tasks = await query
                .OrderByDescending(task => task.UpdatedAt)
                .Select(task => new SprintCarryOverTaskDto
                {
                    Id = task.Id,
                    SequenceId = task.SequenceId,
                    Title = task.Title,
                    Priority = task.Priority,
                    StatusName = task.TaskStatus != null ? task.TaskStatus.Name : "BACKLOG",
                    AssignedUserId = task.AssignedUserId,
                    AssignedUserName = task.AssignedUser != null ? (task.AssignedUser.FullName ?? task.AssignedUser.Email) : null,
                    CurrentSprintId = task.SprintId,
                    CurrentSprintName = task.Sprint != null ? task.Sprint.Name : null,
                    CurrentLocation = task.SprintId == null ? "Backlog" : "Cycle",
                    UpdatedAt = task.UpdatedAt
                })
                .ToListAsync();

            return Ok(ApiResponse<List<SprintCarryOverTaskDto>>.Success(tasks));
        }

        [HttpPost("{id}/carry-over-tasks/move")]
        [ProjectAuthorize(ResourcePermissionCodes.SprintManage)]
        public async Task<IActionResult> BulkMoveCarryOverTasks(
            Guid projectId,
            Guid id,
            [FromBody] BulkMoveCarryOverTasksDto dto,
            [FromServices] ApplicationDbContext context)
        {
            if (dto.TaskIds == null || dto.TaskIds.Count == 0)
            {
                return BadRequest(ApiResponse<object>.Error("Hay chon it nhat mot task ton dong."));
            }

            var sprint = await context.Sprints
                .AsNoTracking()
                .FirstOrDefaultAsync(s => s.Id == id && s.ProjectId == projectId);
            if (sprint == null)
            {
                return NotFound(ApiResponse<object>.Error("Cycle khong ton tai trong du an nay.", 404));
            }

            if (dto.TargetSprintId.HasValue)
            {
                try
                {
                    await SprintScopeValidator.ValidateTargetSprintAsync(
                        context,
                        projectId,
                        dto.TargetSprintId.Value,
                        HttpContext.RequestAborted);
                }
                catch (ResourceScopeException ex)
                {
                    return Problem(
                        statusCode: 400,
                        title: "Invalid sprint rollover scope",
                        detail: ex.Message);
                }

                var targetExists = await context.Sprints.AnyAsync(s => s.Id == dto.TargetSprintId.Value && s.ProjectId == projectId);
                if (!targetExists)
                {
                    return BadRequest(ApiResponse<object>.Error("Cycle dich khong thuoc du an nay."));
                }
            }

            var userIdString = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
            if (!Guid.TryParse(userIdString, out var actorUserId))
            {
                return Unauthorized(ApiResponse<object>.Error("Unauthorized.", 401));
            }

            var doneStatusIds = await context.TaskStatuses
                .Where(ts => ts.ProjectId == projectId &&
                    (ts.Name.Contains("Done") || ts.Name.Contains("Complete") || ts.Name.Contains("Hoan thanh")))
                .Select(ts => ts.Id)
                .ToListAsync();

            var tasks = await context.WorkTasks
                .Where(task =>
                    task.ProjectId == projectId &&
                    !task.IsDeleted &&
                    task.ParentTaskId == null &&
                    dto.TaskIds.Contains(task.Id) &&
                    !doneStatusIds.Contains(task.TaskStatusId) &&
                    task.AuditLogs.Any(log => log.FieldChanged == "SPRINT_CARRY_OVER" && log.OldValue == id.ToString()))
                .ToListAsync();

            var requestedTaskCount = dto.TaskIds.Distinct().Count();
            if (tasks.Count != requestedTaskCount)
            {
                return Problem(
                    statusCode: 400,
                    title: "Invalid rollover tasks",
                    detail: "Every requested task must be an eligible carry-over task in the source project.");
            }

            await SprintScopeValidator.EnsureTasksBelongToProjectAsync(
                context,
                projectId,
                tasks.Select(task => (task.Id, task.ProjectId, task.WorkspaceId)),
                HttpContext.RequestAborted);

            foreach (var task in tasks)
            {
                task.SprintId = dto.TargetSprintId;
                task.UpdatedAt = DateTime.UtcNow;
                context.AuditLogs.Add(new Domain.Entities.AuditLog
                {
                    Id = Guid.NewGuid(),
                    WorkTaskId = task.Id,
                    UserId = actorUserId,
                    FieldChanged = "SPRINT_REPLAN",
                    OldValue = id.ToString(),
                    NewValue = dto.TargetSprintId?.ToString() ?? "BACKLOG",
                    CreatedAt = DateTime.UtcNow
                });
            }

            await context.SaveChangesAsync();
            return Ok(ApiResponse<object>.Success(new { movedCount = tasks.Count }, "Da cap nhat cycle cho task ton dong."));
        }

        [HttpPatch("{id}/favorite")]
        [ProjectAuthorize(ResourcePermissionCodes.SprintManage)]
        public async Task<IActionResult> ToggleFavorite(Guid projectId, Guid id, [FromServices] ApplicationDbContext context)
        {
            var sprint = await context.Sprints.FindAsync(id);
            if (sprint == null || sprint.ProjectId != projectId)
            {
                return NotFound(new { statusCode = 404, message = "Cycle khong ton tai." });
            }

            sprint.IsFavorite = !sprint.IsFavorite;
            await context.SaveChangesAsync();
            return Ok(new { statusCode = 200, data = new { isFavorite = sprint.IsFavorite } });
        }

        [HttpGet("{id}/burndown")]
        public async Task<IActionResult> GetBurndown(Guid projectId, Guid id)
        {
            try
            {
                var result = await _sprintService.GetBurndownChartAsync(id);
                return Ok(ApiResponse<List<BurndownDataDto>>.Success(result));
            }
            catch (ArgumentException ex)
            {
                return BadRequest(ApiResponse<object>.Error(ex.Message));
            }
        }
    }
}
