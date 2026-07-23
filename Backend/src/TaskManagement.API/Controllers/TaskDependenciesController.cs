using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using TaskManagement.API.Filters;
using TaskManagement.Application.Common;
using TaskManagement.Application.Interfaces;
using TaskManagement.Infrastructure.Data;

namespace TaskManagement.API.Controllers
{
    [ApiController]
    [Route("api/projects/{projectId}/WorkTasks/{taskId}/dependencies")]
    [Authorize]
    [ProjectAuthorize(ResourcePermissionCodes.ProjectRead)]
    public class TaskDependenciesController : ControllerBase
    {
        private readonly ApplicationDbContext _context;
        private readonly ITaskDependencyService _dependencyService;

        public TaskDependenciesController(
            ApplicationDbContext context,
            ITaskDependencyService dependencyService)
        {
            _context = context;
            _dependencyService = dependencyService;
        }

        [HttpGet]
        public async Task<IActionResult> GetDependencies(Guid projectId, Guid taskId)
        {
            var taskExists = await _context.WorkTasks
                .AnyAsync(task => task.Id == taskId && task.ProjectId == projectId && !task.IsDeleted);
            if (!taskExists)
            {
                return NotFound(new { statusCode = 404, message = "Task does not exist in this project." });
            }

            var relations = await _context.TaskDependencies
                .Include(edge => edge.PredecessorTask)
                    .ThenInclude(task => task.TaskStatus)
                .Include(edge => edge.SuccessorTask)
                    .ThenInclude(task => task.TaskStatus)
                .Where(edge => edge.PredecessorTaskId == taskId || edge.SuccessorTaskId == taskId)
                .Select(edge => new
                {
                    edge.PredecessorTaskId,
                    edge.SuccessorTaskId,
                    edge.DependencyType,
                    PredecessorTitle = edge.PredecessorTask.Title,
                    PredecessorSequenceId = edge.PredecessorTask.SequenceId,
                    PredecessorStatus = edge.PredecessorTask.TaskStatus.Name,
                    SuccessorTitle = edge.SuccessorTask.Title,
                    SuccessorSequenceId = edge.SuccessorTask.SequenceId,
                    SuccessorStatus = edge.SuccessorTask.TaskStatus.Name
                })
                .ToListAsync();

            return Ok(new { statusCode = 200, message = "Success", data = relations });
        }

        [HttpPost]
        [ProjectAuthorize(ResourcePermissionCodes.ProjectWrite)]
        public async Task<IActionResult> CreateDependency(
            Guid projectId,
            Guid taskId,
            [FromBody] CreateDependencyRequest request)
        {
            try
            {
                var mutation = await _dependencyService.AddOrUpdateAsync(
                    projectId,
                    taskId,
                    request.RelatedTaskId,
                    request.RelationType,
                    HttpContext.RequestAborted);

                var statusCode = mutation == TaskDependencyMutation.Created ? 201 : 200;
                return Ok(new
                {
                    statusCode,
                    message = mutation switch
                    {
                        TaskDependencyMutation.Created => "Dependency created.",
                        TaskDependencyMutation.Updated => "Dependency updated.",
                        _ => "Dependency already exists."
                    }
                });
            }
            catch (ArgumentException ex)
            {
                return Problem(statusCode: 400, title: "Invalid dependency", detail: ex.Message);
            }
            catch (InvalidOperationException ex)
            {
                return Problem(statusCode: 409, title: "Dependency conflict", detail: ex.Message);
            }
        }

        [HttpDelete("{relatedTaskId}")]
        [ProjectAuthorize(ResourcePermissionCodes.ProjectWrite)]
        public async Task<IActionResult> RemoveDependency(Guid projectId, Guid taskId, Guid relatedTaskId)
        {
            var taskCount = await _context.WorkTasks.CountAsync(task =>
                (task.Id == taskId || task.Id == relatedTaskId) &&
                task.ProjectId == projectId &&
                !task.IsDeleted);
            if (taskCount != 2)
            {
                return NotFound(new { statusCode = 404, message = "Task does not exist in this project." });
            }

            var dependency = await _context.TaskDependencies.FirstOrDefaultAsync(edge =>
                (edge.PredecessorTaskId == taskId && edge.SuccessorTaskId == relatedTaskId) ||
                (edge.SuccessorTaskId == taskId && edge.PredecessorTaskId == relatedTaskId));

            if (dependency == null)
            {
                return NotFound(new { statusCode = 404, message = "Dependency was not found." });
            }

            _context.TaskDependencies.Remove(dependency);
            await _context.SaveChangesAsync();
            return Ok(new { statusCode = 200, message = "Dependency removed." });
        }
    }

    public class CreateDependencyRequest
    {
        public Guid RelatedTaskId { get; set; }
        public string RelationType { get; set; } = "relates_to";
    }
}
