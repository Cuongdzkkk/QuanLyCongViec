using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using System;
using System.Threading.Tasks;
using TaskManagement.API.Hubs;
using TaskManagement.API.Realtime;
using TaskManagement.Application.Interfaces;
using TaskManagement.API.Filters;
using TaskManagement.Infrastructure.Data;
using TaskManagement.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace TaskManagement.API.Controllers
{
    [ApiController]
    [Route("api/workspaces/{workspaceId}/[controller]")]
    [Authorize]
    public class GoalsController : ControllerBase
    {
        private readonly IGoalService _goalService;
        private readonly IHubContext<KanbanHub> _hub;
        private readonly ApplicationDbContext _context;
        private readonly IResourceAuthorizationService _authorizationService;

        public GoalsController(
            IGoalService goalService,
            IHubContext<KanbanHub> hub,
            ApplicationDbContext context,
            IResourceAuthorizationService authorizationService)
        {
            _goalService = goalService;
            _hub = hub;
            _context = context;
            _authorizationService = authorizationService;
        }

        [HttpPut("{id}/{tab}/{itemId}")]
        public async Task<IActionResult> UpdateTabItem(Guid workspaceId, Guid id, string tab, Guid itemId, [FromBody] GoalTabItemRequest request)
        {
            var userId = Guid.Parse(User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value ?? Guid.Empty.ToString());
            var goal = await _context.Goals.AsNoTracking().FirstOrDefaultAsync(item => item.Id == id && item.WorkspaceId == workspaceId);
            if (goal == null) return NotFound();
            var normalized = tab.ToLowerInvariant();
            if (normalized == "lessons")
            {
                var item = await _context.GoalLessons.FirstOrDefaultAsync(x => x.Id == itemId && x.GoalId == id);
                if (item == null) return NotFound(); if (item.CreatorId != userId) return Forbid(); item.Text = request.Text ?? request.Title ?? item.Text;
            }
            else if (normalized == "risks")
            {
                var item = await _context.GoalRisks.FirstOrDefaultAsync(x => x.Id == itemId && x.GoalId == id);
                if (item == null) return NotFound(); if (item.CreatorId != userId) return Forbid(); item.Text = request.Text ?? request.Title ?? item.Text; item.Severity = request.Severity ?? item.Severity;
            }
            else if (normalized == "decisions")
            {
                var item = await _context.GoalDecisions.FirstOrDefaultAsync(x => x.Id == itemId && x.GoalId == id);
                if (item == null) return NotFound(); if (item.CreatorId != userId) return Forbid(); item.Text = request.Text ?? request.Title ?? item.Text;
            }
            else return BadRequest("Unsupported goal tab.");
            await _context.SaveChangesAsync();
            return Ok(new { statusCode = 200 });
        }

        [HttpDelete("{id}/{tab}/{itemId}")]
        public async Task<IActionResult> DeleteTabItem(Guid workspaceId, Guid id, string tab, Guid itemId)
        {
            var userId = Guid.Parse(User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value ?? Guid.Empty.ToString());
            object? item = tab.ToLowerInvariant() switch
            {
                "lessons" => await _context.GoalLessons.FirstOrDefaultAsync(x => x.Id == itemId && x.GoalId == id),
                "risks" => await _context.GoalRisks.FirstOrDefaultAsync(x => x.Id == itemId && x.GoalId == id),
                "decisions" => await _context.GoalDecisions.FirstOrDefaultAsync(x => x.Id == itemId && x.GoalId == id),
                _ => null
            };
            if (item == null) return NotFound();
            var creatorId = item switch { GoalLesson x => x.CreatorId, GoalRisk x => x.CreatorId, GoalDecision x => x.CreatorId, _ => Guid.Empty };
            if (creatorId != userId) return Forbid();
            _context.Remove(item); await _context.SaveChangesAsync(); return NoContent();
        }

        [HttpGet]
        public async Task<IActionResult> GetAll(Guid workspaceId)
        {
            var result = await _goalService.GetAllAsync(workspaceId);
            return Ok(result);
        }

        [HttpGet("{id}")]
        public async Task<IActionResult> GetById(Guid workspaceId, Guid id)
        {
            var access = await AuthorizeGoalAsync(workspaceId, id);
            if (access != null) return access;

            var result = await _goalService.GetByIdAsync(id);
            if (result == null) return NotFound();
            return Ok(result);
        }

        [HttpPost]
        public async Task<IActionResult> Create(Guid workspaceId, [FromBody] object dto)
        {
            var userIdValue = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
            if (!Guid.TryParse(userIdValue, out var userId) || userId == Guid.Empty)
            {
                return Unauthorized(new ProblemDetails
                {
                    Status = StatusCodes.Status401Unauthorized,
                    Title = "Authentication required",
                    Detail = "A valid authenticated user context is required."
                });
            }

            try
            {
                var result = await _goalService.CreateAsync(userId, workspaceId, dto);
                await _hub.PublishWorkspaceEntityChangedAsync(workspaceId, "goal", "upsert", GetEntityId(result), result);
                return Ok(result);
            }
            catch (UnauthorizedAccessException ex)
            {
                return Problem(statusCode: 403, title: "Workspace access denied", detail: ex.Message);
            }
            catch (ArgumentException ex)
            {
                return Problem(statusCode: 400, title: "Invalid workspace context", detail: ex.Message);
            }
        }

        [HttpPut("{id}")]
        [RequirePermission("goals.dashboard.edit")]
        public async Task<IActionResult> Update(Guid workspaceId, Guid id, [FromBody] object dto)
        {
            var access = await AuthorizeGoalAsync(workspaceId, id);
            if (access != null) return access;

            var result = await _goalService.UpdateAsync(id, dto);
            await _hub.PublishWorkspaceEntityChangedAsync(workspaceId, "goal", "upsert", id, result);
            return Ok(result);
        }

        [HttpPost("{id}/archive")]
        [RequirePermission("goals.dashboard.delete")]
        public async Task<IActionResult> Archive(Guid workspaceId, Guid id)
        {
            var access = await AuthorizeGoalAsync(workspaceId, id);
            if (access != null) return access;

            await _goalService.ArchiveAsync(id);
            var goal = await _goalService.GetByIdAsync(id);
            await _hub.PublishWorkspaceEntityChangedAsync(workspaceId, "goal", "upsert", id, goal);
            return NoContent();
        }

        [HttpDelete("{id}")]
        [RequirePermission("goals.dashboard.delete")]
        public async Task<IActionResult> Delete(Guid workspaceId, Guid id)
        {
            var access = await AuthorizeGoalAsync(workspaceId, id);
            if (access != null) return access;

            await _goalService.DeleteAsync(id);
            await _hub.PublishWorkspaceEntityChangedAsync(workspaceId, "goal", "deleted", id);
            return NoContent();
        }

        [HttpPost("{id}/updates")]
        public async Task<IActionResult> AddUpdate(Guid workspaceId, Guid id, [FromBody] object dto)
        {
            var access = await AuthorizeGoalAsync(workspaceId, id);
            if (access != null) return access;

            var userId = Guid.Parse(User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value ?? Guid.Empty.ToString());
            var result = await _goalService.AddUpdateAsync(id, userId, dto);
            await PublishActivityAsync(workspaceId, id, "update", "upsert", result);
            return Ok(result);
        }

        [HttpGet("{id}/updates")]
        public async Task<IActionResult> GetUpdates(Guid workspaceId, Guid id)
        {
            var access = await AuthorizeGoalAsync(workspaceId, id);
            if (access != null) return access;

            var result = await _goalService.GetUpdatesAsync(id);
            return Ok(result);
        }

        [HttpPut("{id}/updates/{updateId}")]
        public async Task<IActionResult> UpdateUpdate(Guid workspaceId, Guid id, Guid updateId, [FromBody] object dto)
        {
            var access = await AuthorizeGoalAsync(workspaceId, id);
            if (access != null) return access;

            var result = await _goalService.UpdateUpdateAsync(id, updateId, dto);
            await PublishActivityAsync(workspaceId, id, "update", "upsert", result, updateId);
            return Ok(result);
        }

        [HttpDelete("{id}/updates/{updateId}")]
        public async Task<IActionResult> DeleteUpdate(Guid workspaceId, Guid id, Guid updateId)
        {
            var access = await AuthorizeGoalAsync(workspaceId, id);
            if (access != null) return access;

            await _goalService.DeleteUpdateAsync(id, updateId);
            await PublishActivityAsync(workspaceId, id, "update", "deleted", new { id = updateId }, updateId);
            return NoContent();
        }

        [HttpGet("{id}/lessons")]
        public async Task<IActionResult> GetLessons(Guid workspaceId, Guid id)
        {
            var access = await AuthorizeGoalAsync(workspaceId, id);
            if (access != null) return access;

            var result = await _goalService.GetLessonsAsync(id);
            return Ok(result);
        }

        [HttpPost("{id}/lessons")]
        public async Task<IActionResult> AddLesson(Guid workspaceId, Guid id, [FromBody] object dto)
        {
            var access = await AuthorizeGoalAsync(workspaceId, id);
            if (access != null) return access;

            var userId = Guid.Parse(User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value ?? Guid.Empty.ToString());
            var result = await _goalService.AddLessonAsync(id, userId, dto);
            await PublishActivityAsync(workspaceId, id, "lesson", "upsert", result);
            return Ok(result);
        }

        [HttpGet("{id}/risks")]
        public async Task<IActionResult> GetRisks(Guid workspaceId, Guid id)
        {
            var access = await AuthorizeGoalAsync(workspaceId, id);
            if (access != null) return access;

            var result = await _goalService.GetRisksAsync(id);
            return Ok(result);
        }

        [HttpPost("{id}/risks")]
        public async Task<IActionResult> AddRisk(Guid workspaceId, Guid id, [FromBody] object dto)
        {
            var access = await AuthorizeGoalAsync(workspaceId, id);
            if (access != null) return access;

            var userId = Guid.Parse(User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value ?? Guid.Empty.ToString());
            var result = await _goalService.AddRiskAsync(id, userId, dto);
            await PublishActivityAsync(workspaceId, id, "risk", "upsert", result);
            return Ok(result);
        }

        [HttpGet("{id}/decisions")]
        public async Task<IActionResult> GetDecisions(Guid workspaceId, Guid id)
        {
            var access = await AuthorizeGoalAsync(workspaceId, id);
            if (access != null) return access;

            var result = await _goalService.GetDecisionsAsync(id);
            return Ok(result);
        }

        [HttpPost("{id}/decisions")]
        public async Task<IActionResult> AddDecision(Guid workspaceId, Guid id, [FromBody] object dto)
        {
            var access = await AuthorizeGoalAsync(workspaceId, id);
            if (access != null) return access;

            var userId = Guid.Parse(User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value ?? Guid.Empty.ToString());
            var result = await _goalService.AddDecisionAsync(id, userId, dto);
            await PublishActivityAsync(workspaceId, id, "decision", "upsert", result);
            return Ok(result);
        }

        private async Task PublishActivityAsync(
            Guid workspaceId,
            Guid goalId,
            string activityType,
            string action,
            object? item,
            Guid? itemId = null)
        {
            await _hub.PublishWorkspaceEntityChangedAsync(
                workspaceId,
                "goal-activity",
                action,
                itemId ?? GetEntityId(item, goalId),
                new { goalId, activityType, item });

            var goal = await _goalService.GetByIdAsync(goalId);
            if (goal != null)
            {
                await _hub.PublishWorkspaceEntityChangedAsync(workspaceId, "goal", "upsert", goalId, goal);
            }
        }

        private async Task<IActionResult?> AuthorizeGoalAsync(Guid workspaceId, Guid goalId)
        {
            var userIdValue = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
            if (!Guid.TryParse(userIdValue, out var userId) || userId == Guid.Empty)
            {
                return Unauthorized();
            }

            var workspaceAccess = await _authorizationService.AuthorizeWorkspaceAsync(
                userId,
                workspaceId,
                "workspace.read");
            if (!workspaceAccess.Succeeded)
            {
                return Forbid();
            }

            var goalBelongsToWorkspace = await _context.Goals
                .AsNoTracking()
                .AnyAsync(goal => goal.Id == goalId && goal.WorkspaceId == workspaceId);

            return goalBelongsToWorkspace ? null : NotFound();
        }

        private static Guid GetEntityId(object? value, Guid? fallback = null)
        {
            if (value != null)
            {
                var property = value.GetType().GetProperty("Id") ?? value.GetType().GetProperty("id");
                var raw = property?.GetValue(value);
                if (raw is Guid id) return id;
                if (Guid.TryParse(raw?.ToString(), out var parsed)) return parsed;
            }

            return fallback ?? Guid.Empty;
        }
    }

    public class GoalTabItemRequest
    {
        public string? Title { get; set; }
        public string? Text { get; set; }
        public string? Severity { get; set; }
    }
}
