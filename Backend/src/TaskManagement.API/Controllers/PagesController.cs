using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;
using TaskManagement.Infrastructure.Data;
using TaskManagement.Domain.Entities;
using TaskManagement.API.Hubs;
using TaskManagement.API.Realtime;
using TaskManagement.API.Filters;

namespace TaskManagement.API.Controllers
{
    [ApiController]
    [Route("api/projects/{projectId}")]
    [Authorize]
    [ProjectAuthorize("")]
    public class PagesController : ControllerBase
    {
        private readonly ApplicationDbContext _context;
        private readonly IHubContext<KanbanHub> _hub;

        public PagesController(ApplicationDbContext context, IHubContext<KanbanHub> hub)
        {
            _context = context;
            _hub = hub;
        }

        private async Task<Guid> GetValidProjectIdAsync(string projectIdString)
        {
            if (Guid.TryParse(projectIdString, out Guid pid)) return pid;
            
            var userIdStr = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (Guid.TryParse(userIdStr, out Guid userId))
            {
                var project = await _context.Projects.FirstOrDefaultAsync(p => p.CreatorId == userId);
                if (project != null) return project.Id;

                var member = await _context.ProjectMembers.FirstOrDefaultAsync(m => m.UserId == userId);
                if (member != null) return member.ProjectId;
            }

            var first = await _context.Projects.FirstOrDefaultAsync();
            if (first != null) return first.Id;
            
            // Auto-create a fallback UI project if none exists yet
            if (Guid.TryParse(userIdStr, out Guid currentUserId))
            {
                var defaultWorkspace = await _context.Workspaces.FirstOrDefaultAsync();
                if (defaultWorkspace == null)
                {
                    defaultWorkspace = new Workspace {
                        Id = Guid.NewGuid(),
                        Name = "Default Workspace",
                        Slug = "default",
                        OwnerId = currentUserId,
                        Timezone = "UTC",
                        CreatedAt = DateTime.UtcNow,
                        UpdatedAt = DateTime.UtcNow
                    };
                    _context.Workspaces.Add(defaultWorkspace);
                    await _context.SaveChangesAsync();
                }

                if (defaultWorkspace != null)
                {
                    var newProject = new Project {
                        Id = Guid.NewGuid(),
                        Name = "Default Project",
                        Identifier = "DFP",
                        CreatorId = currentUserId,
                        WorkspaceId = defaultWorkspace.Id,
                        CreatedAt = DateTime.UtcNow,
                        UpdatedAt = DateTime.UtcNow,
                        Status = true
                    };
                    _context.Projects.Add(newProject);
                    await _context.SaveChangesAsync();
                    return newProject.Id;
                }
            }
            
            return Guid.Empty;
        }

        [HttpGet("pages")]
        public async Task<IActionResult> GetByProject(string projectId)
        {
            var validProjectId = await GetValidProjectIdAsync(projectId);
            if (validProjectId == Guid.Empty) return Ok(new { statusCode = 200, message = "Success", data = new List<object>() });

            var pages = await _context.Pages
                .AsNoTracking()
                .Where(p => p.ProjectId == validProjectId)
                .Select(p => new
                {
                    p.Id,
                    p.Title,
                    p.SortOrder,
                    p.IsLocked,
                    p.IsArchived,
                    p.IsPrivate,
                    p.IsStarred,
                    CreatedByName = p.CreatedBy.FullName,
                    p.CreatedAt,
                    p.UpdatedAt
                })
                .OrderBy(p => p.SortOrder)
                .ToListAsync();

            return Ok(new { statusCode = 200, message = "Success", data = pages });
        }

        [HttpGet("pages/{pageId}")]
        public async Task<IActionResult> GetById(string projectId, Guid pageId)
        {
            var validProjectId = await GetValidProjectIdAsync(projectId);
            var page = await _context.Pages
                .Include(p => p.CreatedBy)
                .Include(p => p.UpdatedBy)
                .FirstOrDefaultAsync(p => p.Id == pageId && p.ProjectId == validProjectId);

            if (page == null)
                return NotFound(new { statusCode = 404, message = "Trang không tồn tại." });

            return Ok(new
            {
                statusCode = 200,
                message = "Success",
                data = new
                {
                    page.Id,
                    page.Title,
                    page.Content,
                    page.SortOrder,
                    page.IsLocked,
                    page.IsArchived,
                    page.IsPrivate,
                    page.IsStarred,
                    CreatedByName = page.CreatedBy?.FullName,
                    UpdatedByName = page.UpdatedBy?.FullName,
                    page.CreatedAt,
                    page.UpdatedAt
                }
            });
        }

        [HttpPost("pages")]
        public async Task<IActionResult> Create(string projectId, [FromBody] CreatePageRequest request)
        {
            var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (!Guid.TryParse(userId, out Guid parsedUserId))
                return Unauthorized(new { statusCode = 401, message = "Vui lòng đăng nhập." });

            var validProjectId = await GetValidProjectIdAsync(projectId);
            if (validProjectId == Guid.Empty) return BadRequest(new { statusCode = 400, message = "Không tìm thấy dự án hợp lệ để tạo trang." });

            var maxSort = await _context.Pages
                .Where(p => p.ProjectId == validProjectId)
                .MaxAsync(p => (int?)p.SortOrder) ?? 0;

            var page = new Page
            {
                Id = Guid.NewGuid(),
                ProjectId = validProjectId,
                Title = request.Title,
                Content = request.Content,
                SortOrder = maxSort + 1,
                CreatedById = parsedUserId,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };

            _context.Pages.Add(page);
            await _context.SaveChangesAsync();
            await _hub.PublishEntityChangedAsync(validProjectId, "page", "upsert", page.Id, page);

            return CreatedAtAction(nameof(GetById), new { projectId, pageId = page.Id },
                new { statusCode = 201, message = "Tạo trang thành công.", data = new { page.Id, page.Title } });
        }

        [HttpPut("pages/{pageId}")]
        public async Task<IActionResult> Update(string projectId, Guid pageId, [FromBody] UpdatePageRequest request)
        {
            var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            Guid.TryParse(userId, out Guid parsedUserId);

            var validProjectId = await GetValidProjectIdAsync(projectId);
            var page = await _context.Pages.FirstOrDefaultAsync(p => p.Id == pageId && p.ProjectId == validProjectId);
            if (page == null)
                return NotFound(new { statusCode = 404, message = "Trang không tồn tại." });

            if (page.IsLocked)
            {
                if (request.IsLocked.HasValue && request.IsLocked.Value == false)
                {
                    page.IsLocked = false;
                }
                else
                {
                    return BadRequest(new { statusCode = 400, message = "Trang đã bị khóa chỉnh sửa." });
                }
            }
            else
            {
                if (request.Title != null) page.Title = request.Title;
                if (request.Content != null) page.Content = request.Content;
                if (request.IsLocked.HasValue) page.IsLocked = request.IsLocked.Value;
                if (request.IsPrivate.HasValue) page.IsPrivate = request.IsPrivate.Value;
                if (request.IsStarred.HasValue) page.IsStarred = request.IsStarred.Value;
                if (request.IsArchived.HasValue) page.IsArchived = request.IsArchived.Value;
            }

            page.UpdatedById = parsedUserId;
            page.UpdatedAt = DateTime.UtcNow;

            await _context.SaveChangesAsync();
            await _hub.PublishEntityChangedAsync(validProjectId, "page", "upsert", page.Id, page);

            return Ok(new { statusCode = 200, message = "Cập nhật thành công." });
        }

        [HttpPut("pages/{pageId}/archive")]
        public async Task<IActionResult> Archive(string projectId, Guid pageId)
        {
            var validProjectId = await GetValidProjectIdAsync(projectId);
            var page = await _context.Pages.FirstOrDefaultAsync(p => p.Id == pageId && p.ProjectId == validProjectId);
            if (page == null)
                return NotFound(new { statusCode = 404, message = "Trang không tồn tại." });

            page.IsArchived = !page.IsArchived;
            page.UpdatedAt = DateTime.UtcNow;
            await _context.SaveChangesAsync();
            await _hub.PublishEntityChangedAsync(validProjectId, "page", "upsert", page.Id, page);

            return Ok(new { statusCode = 200, message = page.IsArchived ? "Đã lưu trữ." : "Đã khôi phục." });
        }

        [HttpDelete("pages/{pageId}")]
        public async Task<IActionResult> Delete(string projectId, Guid pageId)
        {
            var validProjectId = await GetValidProjectIdAsync(projectId);
            var page = await _context.Pages.FirstOrDefaultAsync(p => p.Id == pageId && p.ProjectId == validProjectId);
            if (page == null)
                return NotFound(new { statusCode = 404, message = "Trang không tồn tại." });

            _context.Pages.Remove(page);
            await _context.SaveChangesAsync();
            await _hub.PublishEntityChangedAsync(validProjectId, "page", "deleted", pageId);

            return Ok(new { statusCode = 200, message = "Đã xóa trang vĩnh viễn." });
        }
    }

    public class CreatePageRequest
    {
        public string Title { get; set; } = string.Empty;
        public string? Content { get; set; }
    }

    public class UpdatePageRequest
    {
        public string? Title { get; set; }
        public string? Content { get; set; }
        public bool? IsLocked { get; set; }
        public bool? IsPrivate { get; set; }
        public bool? IsStarred { get; set; }
        public bool? IsArchived { get; set; }
    }
}
