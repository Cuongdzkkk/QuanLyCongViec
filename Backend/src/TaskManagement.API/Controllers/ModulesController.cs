using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;
using TaskManagement.API.Filters;
using TaskManagement.Application.Interfaces;
using TaskManagement.Domain.Entities;
using TaskManagement.Infrastructure.Data;

namespace TaskManagement.API.Controllers
{
    [ApiController]
    [Route("api/projects/{projectId}")]
    [Authorize]
    [ProjectAuthorize("")]
    public class ModulesController : ControllerBase
    {
        private readonly ApplicationDbContext _context;
        private readonly IWorkTaskService _workTaskService;

        public ModulesController(ApplicationDbContext context, IWorkTaskService workTaskService)
        {
            _context = context;
            _workTaskService = workTaskService;
        }

        [HttpGet("modules")]
        public async Task<IActionResult> GetByProject(
            Guid projectId,
            [FromQuery] int page = 1,
            [FromQuery] int pageSize = 20,
            [FromQuery] string? search = null,
            [FromQuery] string? sortBy = null,
            [FromQuery] string? sortDirection = null,
            [FromQuery] bool includeDisabled = false)
        {
            var normalizedPage = page < 1 ? 1 : page;
            var normalizedPageSize = pageSize switch
            {
                < 1 => 20,
                > 100 => 100,
                _ => pageSize
            };

            var query = _context.Modules
                .AsNoTracking()
                .Where(m => m.ProjectId == projectId);

            if (!includeDisabled)
            {
                query = query.Where(m => m.Status != "Disabled");
            }

            if (!string.IsNullOrWhiteSpace(search))
            {
                var keyword = search.Trim();
                query = query.Where(m =>
                    m.Name.Contains(keyword) ||
                    (m.Description != null && m.Description.Contains(keyword)) ||
                    (m.Status != null && m.Status.Contains(keyword)));
            }

            var normalizedSortBy = sortBy?.Trim().ToLowerInvariant();
            var descending = string.Equals(sortDirection, "desc", StringComparison.OrdinalIgnoreCase);
            query = (normalizedSortBy, descending) switch
            {
                ("name", true) => query.OrderByDescending(m => m.Name).ThenByDescending(m => m.UpdatedAt),
                ("name", false) => query.OrderBy(m => m.Name).ThenByDescending(m => m.UpdatedAt),
                ("status", true) => query.OrderByDescending(m => m.Status).ThenByDescending(m => m.UpdatedAt),
                ("status", false) => query.OrderBy(m => m.Status).ThenByDescending(m => m.UpdatedAt),
                _ => query.OrderByDescending(m => m.UpdatedAt).ThenByDescending(m => m.CreatedAt)
            };

            var totalCount = await query.CountAsync();

            var modules = await query
                .Skip((normalizedPage - 1) * normalizedPageSize)
                .Take(normalizedPageSize)
                .Select(m => new
                {
                    m.Id,
                    m.Name,
                    m.Description,
                    m.Status,
                    m.StartDate,
                    m.TargetDate,
                    m.LeadId,
                    LeadName = m.Lead != null ? m.Lead.FullName : null,
                    TaskIds = m.IssueModules
                        .Where(im => !im.WorkTask.IsDeleted)
                        .Select(im => im.WorkTaskId)
                        .ToList(),
                    IssueCount = m.IssueModules.Count(im => !im.WorkTask.IsDeleted),
                    DoneIssueCount = m.IssueModules.Count(im =>
                        !im.WorkTask.IsDeleted &&
                        im.WorkTask.TaskStatus != null &&
                        (im.WorkTask.TaskStatus.Name.Contains("DONE") ||
                         im.WorkTask.TaskStatus.Name.Contains("Done") ||
                         im.WorkTask.TaskStatus.Name.Contains("Complete"))),
                    ProgressPercent = m.IssueModules.Count(im => !im.WorkTask.IsDeleted) == 0
                        ? 0
                        : (int)Math.Round(
                            100.0 * m.IssueModules.Count(im =>
                                !im.WorkTask.IsDeleted &&
                                im.WorkTask.TaskStatus != null &&
                                (im.WorkTask.TaskStatus.Name.Contains("DONE") ||
                                 im.WorkTask.TaskStatus.Name.Contains("Done") ||
                                 im.WorkTask.TaskStatus.Name.Contains("Complete")))
                            / m.IssueModules.Count(im => !im.WorkTask.IsDeleted)),
                    m.CreatedAt,
                    m.UpdatedAt
                })
                .ToListAsync();

            var totalPages = totalCount == 0
                ? 0
                : (int)Math.Ceiling(totalCount / (double)normalizedPageSize);

            return Ok(new
            {
                statusCode = 200,
                message = "Success",
                data = modules,
                pagination = new
                {
                    page = normalizedPage,
                    pageSize = normalizedPageSize,
                    totalCount,
                    totalPages,
                    hasPreviousPage = normalizedPage > 1,
                    hasNextPage = normalizedPage < totalPages
                }
            });
        }

        [HttpGet("modules/{moduleId}")]
        public async Task<IActionResult> GetDetail(
            Guid projectId,
            Guid moduleId,
            [FromQuery] int page = 1,
            [FromQuery] int pageSize = 20)
        {
            var userIdValue = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (!Guid.TryParse(userIdValue, out var userId))
            {
                return Unauthorized(new { statusCode = 401, message = "Unauthorized. JWT is missing or invalid." });
            }

            try
            {
                var detail = await _workTaskService.GetModuleDetailAsync(
                    projectId,
                    moduleId,
                    userId,
                    page,
                    pageSize);
                if (detail == null)
                {
                    return NotFound(new { statusCode = 404, message = "Module khong ton tai trong du an nay." });
                }

                return Ok(new { statusCode = 200, message = "Success", data = detail });
            }
            catch (UnauthorizedAccessException ex)
            {
                return StatusCode(403, new { statusCode = 403, message = ex.Message });
            }
        }

        [HttpPost("modules")]
        public async Task<IActionResult> Create(Guid projectId, [FromBody] CreateModuleRequest request)
        {
            var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (!Guid.TryParse(userId, out var parsedUserId))
            {
                return Unauthorized(new { statusCode = 401, message = "Vui long dang nhap." });
            }

            var projectExists = await _context.Projects.AnyAsync(p => p.Id == projectId);
            if (!projectExists)
            {
                return BadRequest(new { statusCode = 400, message = "Du an khong ton tai." });
            }

            if (request.LeadId.HasValue)
            {
                var leadExists = await _context.ProjectMembers
                    .AnyAsync(pm => pm.ProjectId == projectId && pm.UserId == request.LeadId.Value && pm.Status);
                if (!leadExists)
                {
                    return BadRequest(new { statusCode = 400, message = "Lead khong thuoc du an nay." });
                }
            }

            var module = new Module
            {
                Id = Guid.NewGuid(),
                ProjectId = projectId,
                Name = request.Name,
                Description = request.Description,
                StartDate = request.StartDate,
                TargetDate = request.TargetDate,
                Status = request.Status ?? "Backlog",
                LeadId = request.LeadId ?? parsedUserId,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };

            _context.Modules.Add(module);
            await _context.SaveChangesAsync();

            if (request.TaskIds?.Any() == true)
            {
                var validTaskIds = await _context.WorkTasks
                    .Where(task => task.ProjectId == projectId && !task.IsDeleted && request.TaskIds.Contains(task.Id))
                    .Select(task => task.Id)
                    .ToListAsync();

                if (validTaskIds.Count > 0)
                {
                    _context.IssueModules.AddRange(validTaskIds.Select(taskId => new IssueModule
                    {
                        WorkTaskId = taskId,
                        ModuleId = module.Id,
                        AssignedAt = DateTime.UtcNow
                    }));

                    await _context.SaveChangesAsync();
                }
            }

            return CreatedAtAction(nameof(GetByProject), new { projectId },
                new { statusCode = 201, message = "Tao Module thanh cong.", data = new { module.Id, module.Name } });
        }

        [HttpPut("modules/{moduleId}")]
        public async Task<IActionResult> Update(Guid projectId, Guid moduleId, [FromBody] UpdateModuleRequest request)
        {
            var module = await _context.Modules
                .Include(m => m.IssueModules)
                .FirstOrDefaultAsync(m => m.Id == moduleId && m.ProjectId == projectId);

            if (module == null)
            {
                return NotFound(new { statusCode = 404, message = "Module khong ton tai." });
            }

            if (request.LeadId.HasValue)
            {
                var leadExists = await _context.ProjectMembers
                    .AnyAsync(pm => pm.ProjectId == projectId && pm.UserId == request.LeadId.Value && pm.Status);
                if (!leadExists)
                {
                    return BadRequest(new { statusCode = 400, message = "Lead khong thuoc du an nay." });
                }
            }

            module.Name = request.Name ?? module.Name;
            module.Description = request.Description ?? module.Description;
            module.Status = request.Status ?? module.Status;
            module.StartDate = request.StartDate ?? module.StartDate;
            module.TargetDate = request.TargetDate ?? module.TargetDate;
            module.LeadId = request.LeadId ?? module.LeadId;
            module.UpdatedAt = DateTime.UtcNow;

            if (request.TaskIds != null)
            {
                var requestedTaskIds = request.TaskIds.Distinct().ToList();
                var validTaskIds = await _context.WorkTasks
                    .Where(task => task.ProjectId == projectId && !task.IsDeleted && requestedTaskIds.Contains(task.Id))
                    .Select(task => task.Id)
                    .ToListAsync();

                var staleLinks = module.IssueModules
                    .Where(link => !validTaskIds.Contains(link.WorkTaskId))
                    .ToList();
                if (staleLinks.Count > 0)
                {
                    _context.IssueModules.RemoveRange(staleLinks);
                }

                var existingTaskIds = module.IssueModules.Select(link => link.WorkTaskId).ToHashSet();
                var newLinks = validTaskIds
                    .Where(taskId => !existingTaskIds.Contains(taskId))
                    .Select(taskId => new IssueModule
                    {
                        WorkTaskId = taskId,
                        ModuleId = module.Id,
                        AssignedAt = DateTime.UtcNow
                    })
                    .ToList();

                if (newLinks.Count > 0)
                {
                    _context.IssueModules.AddRange(newLinks);
                }
            }

            await _context.SaveChangesAsync();

            return Ok(new { statusCode = 200, message = "Cap nhat thanh cong." });
        }

        [HttpDelete("modules/{moduleId}")]
        public async Task<IActionResult> Delete(Guid projectId, Guid moduleId)
        {
            var module = await _context.Modules.FirstOrDefaultAsync(m => m.Id == moduleId && m.ProjectId == projectId);
            if (module == null)
            {
                return NotFound(new { statusCode = 404, message = "Module does not exist." });
            }

            module.Status = "Disabled";
            module.UpdatedAt = DateTime.UtcNow;
            await _context.SaveChangesAsync();

            return Ok(new { statusCode = 200, message = "Module disabled successfully." });
        }
    }

    public class CreateModuleRequest
    {
        public string Name { get; set; } = string.Empty;
        public string? Description { get; set; }
        public string? Status { get; set; }
        public Guid? LeadId { get; set; }
        public List<Guid>? TaskIds { get; set; }
        public DateTime? StartDate { get; set; }
        public DateTime? TargetDate { get; set; }
    }

    public class UpdateModuleRequest
    {
        public string? Name { get; set; }
        public string? Description { get; set; }
        public string? Status { get; set; }
        public Guid? LeadId { get; set; }
        public List<Guid>? TaskIds { get; set; }
        public DateTime? StartDate { get; set; }
        public DateTime? TargetDate { get; set; }
    }
}
