using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using TaskManagement.API.Filters;
using TaskManagement.Application.DTOs.Common;
using TaskManagement.Domain.Entities;
using TaskManagement.Infrastructure.Data;
using System.Security.Claims;
using TaskManagement.API.Hubs;
using TaskManagement.API.Realtime;

namespace TaskManagement.API.Controllers
{
    [ApiController]
    [Route("api/projects/{projectId}/views")]
    [Authorize]
    [ProjectAuthorize("")]
    public class ProjectViewsController : ControllerBase
    {
        private readonly ApplicationDbContext _context;
        private readonly IHubContext<KanbanHub> _hub;

        public ProjectViewsController(ApplicationDbContext context, IHubContext<KanbanHub> hub)
        {
            _context = context;
            _hub = hub;
        }

        [HttpGet]
        public async Task<IActionResult> GetByProject(Guid projectId)
        {
            var views = await _context.ProjectViews
                .Where(v => v.ProjectId == projectId)
                .OrderByDescending(v => v.IsFavorite)
                .ThenByDescending(v => v.CreatedAt)
                .Select(v => new {
                    v.Id,
                    v.Name,
                    v.Description,
                    v.QueryMetadata,
                    v.IsFavorite,
                    v.CreatedAt,
                    v.UpdatedAt
                })
                .ToListAsync();

            return Ok(new { statusCode = 200, data = views, message = "Success" });
        }

        [HttpGet("{id}")]
        public async Task<IActionResult> GetById(Guid projectId, Guid id)
        {
            var view = await _context.ProjectViews
                .Where(v => v.ProjectId == projectId && v.Id == id)
                .Select(v => new {
                    v.Id,
                    v.Name,
                    v.Description,
                    v.QueryMetadata,
                    v.IsFavorite,
                    v.CreatedAt,
                    v.UpdatedAt
                })
                .FirstOrDefaultAsync();

            if (view == null) return NotFound(new { statusCode = 404, message = "View not found" });
            return Ok(new { statusCode = 200, data = view, message = "Success" });
        }

        public class CreateViewDto {
            public string Name {get; set;} = string.Empty;
            public string? Description {get; set;}
            public string QueryMetadata {get; set;} = "{}";
        }

        public class UpdateViewDto
        {
            public string? Name { get; set; }
            public string? Description { get; set; }
            public string? QueryMetadata { get; set; }
        }

        [HttpPost]
        public async Task<IActionResult> Create(Guid projectId, [FromBody] CreateViewDto dto)
        {
            var userIdStr = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (!Guid.TryParse(userIdStr, out Guid userId)) return Unauthorized();

            var view = new ProjectView
            {
                Id = Guid.NewGuid(),
                ProjectId = projectId,
                Name = dto.Name,
                Description = dto.Description,
                QueryMetadata = dto.QueryMetadata,
                CreatedById = userId,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };

            _context.ProjectViews.Add(view);
            await _context.SaveChangesAsync();
            await _hub.PublishEntityChangedAsync(projectId, "project-view", "upsert", view.Id, view);

            return Ok(new { statusCode = 201, data = view, message = "View created" });
        }

        [HttpPatch("{id}/favorite")]
        public async Task<IActionResult> ToggleFavorite(Guid projectId, Guid id)
        {
            var view = await _context.ProjectViews.FirstOrDefaultAsync(v => v.ProjectId == projectId && v.Id == id);
            if (view == null) return NotFound(new { statusCode = 404, message = "View not found" });

            view.IsFavorite = !view.IsFavorite;
            await _context.SaveChangesAsync();
            await _hub.PublishEntityChangedAsync(projectId, "project-view", "upsert", view.Id, view);
            return Ok(new { statusCode = 200, data = new { isFavorite = view.IsFavorite }, message = "Favorite toggled" });
        }

        [HttpPatch("{id}")]
        public async Task<IActionResult> Update(Guid projectId, Guid id, [FromBody] UpdateViewDto dto)
        {
            var view = await _context.ProjectViews.FirstOrDefaultAsync(v => v.ProjectId == projectId && v.Id == id);
            if (view == null) return NotFound(new { statusCode = 404, message = "View not found" });

            if (!string.IsNullOrWhiteSpace(dto.Name))
            {
                view.Name = dto.Name.Trim();
            }

            if (dto.Description != null)
            {
                view.Description = dto.Description;
            }

            if (!string.IsNullOrWhiteSpace(dto.QueryMetadata))
            {
                view.QueryMetadata = dto.QueryMetadata;
            }

            view.UpdatedAt = DateTime.UtcNow;
            await _context.SaveChangesAsync();
            await _hub.PublishEntityChangedAsync(projectId, "project-view", "upsert", view.Id, view);

            return Ok(new
            {
                statusCode = 200,
                data = new
                {
                    view.Id,
                    view.Name,
                    view.Description,
                    view.QueryMetadata,
                    view.IsFavorite,
                    view.CreatedAt,
                    view.UpdatedAt
                },
                message = "View updated"
            });
        }

        [HttpDelete("{id}")]
        public async Task<IActionResult> Delete(Guid projectId, Guid id)
        {
            var view = await _context.ProjectViews.FirstOrDefaultAsync(v => v.ProjectId == projectId && v.Id == id);
            if (view == null) return NotFound(new { statusCode = 404, message = "View not found" });

            _context.ProjectViews.Remove(view);
            await _context.SaveChangesAsync();
            await _hub.PublishEntityChangedAsync(projectId, "project-view", "deleted", id);

            return Ok(new { statusCode = 200, message = "View deleted" });
        }
    }
}
