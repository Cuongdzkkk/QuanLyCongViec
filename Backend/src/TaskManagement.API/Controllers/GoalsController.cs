using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System;
using System.Threading.Tasks;
using TaskManagement.Application.Interfaces;
using TaskManagement.API.Filters;

namespace TaskManagement.API.Controllers
{
    [ApiController]
    [Route("api/workspaces/{workspaceId}/[controller]")]
    [Authorize]
    public class GoalsController : ControllerBase
    {
        private readonly IGoalService _goalService;

        public GoalsController(IGoalService goalService)
        {
            _goalService = goalService;
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
            var result = await _goalService.GetByIdAsync(id);
            if (result == null) return NotFound();
            return Ok(result);
        }

        [HttpPost]
        [RequirePermission("goals.dashboard.create")]
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
            var result = await _goalService.UpdateAsync(id, dto);
            return Ok(result);
        }

        [HttpPost("{id}/archive")]
        [RequirePermission("goals.dashboard.delete")]
        public async Task<IActionResult> Archive(Guid workspaceId, Guid id)
        {
            await _goalService.ArchiveAsync(id);
            return NoContent();
        }

        [HttpDelete("{id}")]
        [RequirePermission("goals.dashboard.delete")]
        public async Task<IActionResult> Delete(Guid workspaceId, Guid id)
        {
            await _goalService.DeleteAsync(id);
            return NoContent();
        }

        [HttpPost("{id}/updates")]
        public async Task<IActionResult> AddUpdate(Guid workspaceId, Guid id, [FromBody] object dto)
        {
            var userId = Guid.Parse(User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value ?? Guid.Empty.ToString());
            var result = await _goalService.AddUpdateAsync(id, userId, dto);
            return Ok(result);
        }

        [HttpGet("{id}/updates")]
        public async Task<IActionResult> GetUpdates(Guid workspaceId, Guid id)
        {
            var result = await _goalService.GetUpdatesAsync(id);
            return Ok(result);
        }

        [HttpPut("{id}/updates/{updateId}")]
        public async Task<IActionResult> UpdateUpdate(Guid workspaceId, Guid id, Guid updateId, [FromBody] object dto)
        {
            var result = await _goalService.UpdateUpdateAsync(id, updateId, dto);
            return Ok(result);
        }

        [HttpDelete("{id}/updates/{updateId}")]
        public async Task<IActionResult> DeleteUpdate(Guid workspaceId, Guid id, Guid updateId)
        {
            await _goalService.DeleteUpdateAsync(id, updateId);
            return NoContent();
        }

        [HttpGet("{id}/lessons")]
        public async Task<IActionResult> GetLessons(Guid workspaceId, Guid id)
        {
            var result = await _goalService.GetLessonsAsync(id);
            return Ok(result);
        }

        [HttpPost("{id}/lessons")]
        public async Task<IActionResult> AddLesson(Guid workspaceId, Guid id, [FromBody] object dto)
        {
            var userId = Guid.Parse(User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value ?? Guid.Empty.ToString());
            var result = await _goalService.AddLessonAsync(id, userId, dto);
            return Ok(result);
        }

        [HttpGet("{id}/risks")]
        public async Task<IActionResult> GetRisks(Guid workspaceId, Guid id)
        {
            var result = await _goalService.GetRisksAsync(id);
            return Ok(result);
        }

        [HttpPost("{id}/risks")]
        public async Task<IActionResult> AddRisk(Guid workspaceId, Guid id, [FromBody] object dto)
        {
            var userId = Guid.Parse(User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value ?? Guid.Empty.ToString());
            var result = await _goalService.AddRiskAsync(id, userId, dto);
            return Ok(result);
        }

        [HttpGet("{id}/decisions")]
        public async Task<IActionResult> GetDecisions(Guid workspaceId, Guid id)
        {
            var result = await _goalService.GetDecisionsAsync(id);
            return Ok(result);
        }

        [HttpPost("{id}/decisions")]
        public async Task<IActionResult> AddDecision(Guid workspaceId, Guid id, [FromBody] object dto)
        {
            var userId = Guid.Parse(User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value ?? Guid.Empty.ToString());
            var result = await _goalService.AddDecisionAsync(id, userId, dto);
            return Ok(result);
        }
    }
}
