using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using TaskManagement.API.Filters;
using TaskManagement.API.Hubs;
using TaskManagement.API.Realtime;
using TaskManagement.Domain.Entities;
using TaskManagement.Infrastructure.Data;

namespace TaskManagement.API.Controllers
{
    [ApiController]
    [Route("api/projects/{projectId}")]
    [Authorize]
    [ProjectAuthorize("")]
    public class LabelsController : ControllerBase
    {
        private readonly ApplicationDbContext _context;
        private readonly IHubContext<KanbanHub> _kanbanHub;

        public LabelsController(ApplicationDbContext context, IHubContext<KanbanHub> kanbanHub)
        {
            _context = context;
            _kanbanHub = kanbanHub;
        }

        [HttpGet("labels")]
        public async Task<IActionResult> GetByProject(Guid projectId)
        {
            var project = await _context.Projects
                .AsNoTracking()
                .Where(p => p.Id == projectId)
                .Select(p => new { p.Id, p.WorkspaceId })
                .FirstOrDefaultAsync();

            if (project == null)
            {
                return NotFound(new { statusCode = 404, message = "Du an khong ton tai." });
            }

            var labels = await _context.Labels
                .AsNoTracking()
                .Where(l => l.ProjectId == projectId || (l.ProjectId == null && l.WorkspaceId == project.WorkspaceId))
                .Select(l => new
                {
                    l.Id,
                    l.Name,
                    l.ColorCode,
                    color = l.ColorCode,
                    l.Description,
                    l.ProjectId,
                    IssueCount = l.IssueLabels.Count(il => !il.WorkTask.IsDeleted),
                    l.CreatedAt
                })
                .OrderBy(l => l.Name)
                .ToListAsync();

            return Ok(new { statusCode = 200, message = "Success", data = labels });
        }

        [HttpPost("labels")]
        public async Task<IActionResult> Create(Guid projectId, [FromBody] CreateLabelRequest request)
        {
            var project = await _context.Projects.FirstOrDefaultAsync(p => p.Id == projectId);
            if (project == null)
            {
                return BadRequest(new { statusCode = 400, message = "Du an khong ton tai." });
            }

            var label = new Label
            {
                Id = Guid.NewGuid(),
                ProjectId = projectId,
                WorkspaceId = project.WorkspaceId,
                Name = request.Name,
                ColorCode = request.ColorCode ?? request.Color ?? "#3b82f6",
                Description = request.Description,
                CreatedAt = DateTime.UtcNow
            };

            _context.Labels.Add(label);
            await _context.SaveChangesAsync();
            await _kanbanHub.PublishEntityChangedAsync(projectId, "label", "upsert", label.Id, new
            {
                label.Id,
                label.Name,
                label.ColorCode,
                color = label.ColorCode,
                label.Description,
                label.ProjectId,
                IssueCount = 0,
                label.CreatedAt
            });

            return CreatedAtAction(nameof(GetByProject), new { projectId },
                new
                {
                    statusCode = 201,
                    message = "Tao nhan thanh cong.",
                    data = new { label.Id, label.Name, label.ColorCode, color = label.ColorCode }
                });
        }

        [HttpPut("labels/{labelId}")]
        public async Task<IActionResult> Update(Guid projectId, Guid labelId, [FromBody] UpdateLabelRequest request)
        {
            var label = await _context.Labels.FirstOrDefaultAsync(l => l.Id == labelId && l.ProjectId == projectId);
            if (label == null)
            {
                return NotFound(new { statusCode = 404, message = "Nhan khong ton tai." });
            }

            label.Name = request.Name ?? label.Name;
            label.ColorCode = request.ColorCode ?? request.Color ?? label.ColorCode;
            label.Description = request.Description ?? label.Description;

            await _context.SaveChangesAsync();
            await PublishLabelAsync(projectId, label.Id);

            return Ok(new { statusCode = 200, message = "Cap nhat thanh cong." });
        }

        [HttpDelete("labels/{labelId}")]
        public async Task<IActionResult> Delete(Guid projectId, Guid labelId)
        {
            var label = await _context.Labels.FirstOrDefaultAsync(l => l.Id == labelId && l.ProjectId == projectId);
            if (label == null)
            {
                return NotFound(new { statusCode = 404, message = "Nhan khong ton tai." });
            }

            _context.Labels.Remove(label);
            await _context.SaveChangesAsync();
            await _kanbanHub.PublishEntityChangedAsync(projectId, "label", "deleted", labelId);

            return Ok(new { statusCode = 200, message = "Xoa thanh cong." });
        }

        [HttpPost("tasks/{taskId}/labels")]
        [HttpPost("WorkTasks/{taskId}/labels")]
        public async Task<IActionResult> AssignLabel(Guid projectId, Guid taskId, [FromBody] AssignLabelRequest request)
        {
            var task = await _context.WorkTasks
                .AsNoTracking()
                .FirstOrDefaultAsync(wt => wt.Id == taskId && wt.ProjectId == projectId && !wt.IsDeleted);
            if (task == null)
            {
                return NotFound(new { statusCode = 404, message = "Tac vu khong ton tai trong du an nay." });
            }

            var project = await _context.Projects.AsNoTracking().FirstOrDefaultAsync(p => p.Id == projectId);
            if (project == null)
            {
                return NotFound(new { statusCode = 404, message = "Du an khong ton tai." });
            }

            var labelExists = await _context.Labels.AnyAsync(label =>
                label.Id == request.LabelId &&
                (label.ProjectId == projectId || (label.ProjectId == null && label.WorkspaceId == project.WorkspaceId)));
            if (!labelExists)
            {
                return BadRequest(new { statusCode = 400, message = "Nhan khong thuoc du an nay." });
            }

            var exists = await _context.IssueLabels.AnyAsync(il => il.WorkTaskId == taskId && il.LabelId == request.LabelId);
            if (exists)
            {
                return BadRequest(new { statusCode = 400, message = "Nhan da duoc gan cho cong viec nay." });
            }

            _context.IssueLabels.Add(new IssueLabel
            {
                WorkTaskId = taskId,
                LabelId = request.LabelId,
                AssignedAt = DateTime.UtcNow
            });

            await _context.SaveChangesAsync();
            await _kanbanHub.PublishEntityChangedAsync(projectId, "task-label", "upsert", taskId, new
            {
                taskId,
                labelId = request.LabelId
            });

            return Ok(new { statusCode = 200, message = "Gan nhan thanh cong." });
        }

        [HttpDelete("tasks/{taskId}/labels/{labelId}")]
        [HttpDelete("WorkTasks/{taskId}/labels/{labelId}")]
        public async Task<IActionResult> RemoveLabel(Guid projectId, Guid taskId, Guid labelId)
        {
            var link = await _context.IssueLabels
                .Include(il => il.WorkTask)
                .Include(il => il.Label)
                .FirstOrDefaultAsync(il =>
                    il.WorkTaskId == taskId &&
                    il.LabelId == labelId &&
                    il.WorkTask.ProjectId == projectId &&
                    !il.WorkTask.IsDeleted &&
                    (il.Label.ProjectId == projectId || il.Label.ProjectId == null));

            if (link == null)
            {
                return NotFound(new { statusCode = 404, message = "Nhan chua duoc gan." });
            }

            _context.IssueLabels.Remove(link);
            await _context.SaveChangesAsync();
            await _kanbanHub.PublishEntityChangedAsync(projectId, "task-label", "deleted", taskId, new
            {
                taskId,
                labelId
            });

            return Ok(new { statusCode = 200, message = "Go nhan thanh cong." });
        }

        private async Task PublishLabelAsync(Guid projectId, Guid labelId)
        {
            var payload = await _context.Labels
                .AsNoTracking()
                .Where(label => label.Id == labelId && label.ProjectId == projectId)
                .Select(label => new
                {
                    label.Id,
                    label.Name,
                    label.ColorCode,
                    color = label.ColorCode,
                    label.Description,
                    label.ProjectId,
                    IssueCount = label.IssueLabels.Count(link => !link.WorkTask.IsDeleted),
                    label.CreatedAt
                })
                .FirstOrDefaultAsync();
            if (payload != null)
            {
                await _kanbanHub.PublishEntityChangedAsync(projectId, "label", "upsert", labelId, payload);
            }
        }
    }

    public class CreateLabelRequest
    {
        public string Name { get; set; } = string.Empty;
        public string? ColorCode { get; set; }
        public string? Color { get; set; }
        public string? Description { get; set; }
    }

    public class UpdateLabelRequest
    {
        public string? Name { get; set; }
        public string? ColorCode { get; set; }
        public string? Color { get; set; }
        public string? Description { get; set; }
    }

    public class AssignLabelRequest
    {
        public Guid LabelId { get; set; }
    }
}
