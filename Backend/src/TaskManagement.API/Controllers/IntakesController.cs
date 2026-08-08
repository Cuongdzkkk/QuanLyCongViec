using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System;
using System.ComponentModel.DataAnnotations;
using System.Security.Claims;
using TaskManagement.API.Filters;
using TaskManagement.Application.Common;
using TaskManagement.Infrastructure.Data;
using TaskManagement.Domain.Entities;

namespace TaskManagement.API.Controllers
{
    [ApiController]
    [Route("api/projects/{projectId}")]
    [Authorize]
    public class IntakesController : ControllerBase
    {
        private static readonly HashSet<string> AllowedSources = new(StringComparer.OrdinalIgnoreCase)
        {
            "FORM",
            "MANUAL",
            "EMAIL",
            "API"
        };

        private readonly ApplicationDbContext _context;

        public IntakesController(ApplicationDbContext context)
        {
            _context = context;
        }

        [HttpGet("intakes")]
        [ProjectAuthorize(ResourcePermissionCodes.ProjectRead)]
        public async Task<IActionResult> GetByProject(Guid projectId)
        {
            var intakes = await _context.Intakes
                .AsNoTracking()
                .Where(i => i.ProjectId == projectId)
                .Select(i => new
                {
                    i.Id,
                    i.Title,
                    i.Description,
                    i.Source,
                    i.Status,
                    i.Priority,
                    i.DesiredDueDate,
                    SubmittedByName = i.SubmittedBy != null ? i.SubmittedBy.FullName : null,
                    ReviewedByName = i.ReviewedBy != null ? i.ReviewedBy.FullName : null,
                    i.CreatedIssueId,
                    i.CreatedAt,
                    i.ReviewedAt
                })
                .OrderByDescending(i => i.CreatedAt)
                .ToListAsync();

            var projectRole = HttpContext.Items["ProjectRole"]?.ToString();
            var normalizedRole = ProjectExecutionRuleHelper.NormalizeProjectRole(projectRole);
            var canCreate = ResourcePermissionPolicy.ProjectRoleHasPermission(
                    projectRole,
                    ResourcePermissionCodes.ProjectRead)
                && normalizedRole is not "guest" and not "stakeholder";
            var canReview = ResourcePermissionPolicy.ProjectRoleHasPermission(
                projectRole,
                ResourcePermissionCodes.ProjectWrite);

            return Ok(new
            {
                statusCode = 200,
                message = "Success",
                data = intakes,
                permissions = new { canCreate, canReview }
            });
        }

        [HttpPost("intakes")]
        [ProjectAuthorize(ResourcePermissionCodes.ProjectRead)]
        public async Task<IActionResult> Create(Guid projectId, [FromBody] CreateIntakeRequest request)
        {
            var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            Guid? parsedUserId = Guid.TryParse(userId, out Guid uid) ? uid : null;
            if (parsedUserId == null)
                return Unauthorized(new { statusCode = 401, message = "Authenticated user is required." });

            var projectExists = await _context.Projects.AnyAsync(p =>
                p.Id == projectId &&
                p.Status &&
                !p.IsDeleted &&
                !p.IsArchived &&
                !p.Workspace.IsDeleted);
            if (!projectExists)
                return BadRequest(new { statusCode = 400, message = "Project is not active." });

            var title = request.Title.Trim();
            if (title.Length == 0)
                return BadRequest(new { statusCode = 400, message = "Title is required." });
            var source = string.IsNullOrWhiteSpace(request.Source)
                ? "MANUAL"
                : request.Source.Trim().ToUpperInvariant();
            if (!AllowedSources.Contains(source))
                return BadRequest(new { statusCode = 400, message = "Intake source is invalid." });
            if (request.DesiredDueDate.HasValue && request.DesiredDueDate.Value.Date < DateTime.UtcNow.Date)
                return BadRequest(new { statusCode = 400, message = "Desired due date cannot be in the past." });

            var intake = new Intake
            {
                Id = Guid.NewGuid(),
                ProjectId = projectId,
                Title = title,
                Description = string.IsNullOrWhiteSpace(request.Description) ? null : request.Description.Trim(),
                Source = source,
                Status = "Pending",
                Priority = request.Priority,
                DesiredDueDate = request.DesiredDueDate,
                SubmittedById = parsedUserId,
                CreatedAt = DateTime.UtcNow
            };

            _context.Intakes.Add(intake);
            await _context.SaveChangesAsync();

            return CreatedAtAction(nameof(GetByProject), new { projectId },
                new { statusCode = 201, message = "Gửi yêu cầu thành công.", data = new { intake.Id } });
        }

        /// <summary>
        /// PM/PO duyệt hoặc từ chối yêu cầu intake
        /// </summary>
        [HttpPut("intakes/{intakeId}/review")]
        [ProjectAuthorize(ResourcePermissionCodes.ProjectWrite)]
        public async Task<IActionResult> Review(Guid projectId, Guid intakeId, [FromBody] ReviewIntakeRequest request)
        {
            var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (!Guid.TryParse(userId, out Guid parsedUserId))
                return Unauthorized(new { statusCode = 401, message = "Vui lòng đăng nhập." });

            var requestedStatus = request.Status.Trim();
            var normalizedStatus = requestedStatus.Equals("Accepted", StringComparison.OrdinalIgnoreCase)
                ? "Accepted"
                : requestedStatus.Equals("Declined", StringComparison.OrdinalIgnoreCase)
                    ? "Declined"
                    : null;
            if (normalizedStatus == null)
                return BadRequest(new { statusCode = 400, message = "Status must be Accepted or Declined." });

            var project = await _context.Projects.FirstOrDefaultAsync(p =>
                p.Id == projectId &&
                p.Status &&
                !p.IsDeleted &&
                !p.IsArchived &&
                !p.Workspace.IsDeleted);
            if (project == null)
                return BadRequest(new { statusCode = 400, message = "Project is not active." });

            var intake = await _context.Intakes.FirstOrDefaultAsync(i => i.Id == intakeId && i.ProjectId == projectId);
            if (intake == null)
                return NotFound(new { statusCode = 404, message = "Yêu cầu không tồn tại." });

            if (intake.Status != "Pending")
                return Conflict(new { statusCode = 409, message = "Intake was already reviewed." });

            // If accepted, auto-create a WorkTask from the intake
            if (normalizedStatus == "Accepted")
            {
                var todoStatus = await _context.TaskStatuses
                    .FirstOrDefaultAsync(ts => ts.ProjectId == projectId && ts.Name.ToUpper() == "TO DO");
                var defaultType = await _context.TaskTypes
                    .FirstOrDefaultAsync(tt => tt.ProjectId == projectId);
                if (todoStatus == null || defaultType == null)
                {
                    return Conflict(new
                    {
                        statusCode = 409,
                        message = "Project requires a TO DO status and a task type before accepting intakes."
                    });
                }

                project.IssueSequence += 1;
                string sequenceId = project.Identifier + "-" + project.IssueSequence;

                double maxSort = await _context.WorkTasks
                    .Where(wt => wt.ProjectId == projectId && !wt.IsDeleted)
                    .MaxAsync(wt => (double?)wt.SortOrder) ?? 0;

                var newTask = new WorkTask
                {
                    Id = Guid.NewGuid(),
                    ProjectId = projectId,
                    WorkspaceId = project.WorkspaceId,
                    Title = intake.Title,
                    Description = intake.Description,
                    TaskStatusId = todoStatus.Id,
                    TaskTypeId = defaultType.Id,
                    ReporterId = intake.SubmittedById ?? parsedUserId,
                    Priority = intake.Priority is >= 1 and <= 4 ? intake.Priority : 3,
                    DueDate = intake.DesiredDueDate,
                    SortOrder = maxSort + 65536,
                    SequenceId = sequenceId,
                    CreatedAt = DateTime.UtcNow,
                    UpdatedAt = DateTime.UtcNow
                };

                _context.WorkTasks.Add(newTask);
                intake.CreatedIssueId = newTask.Id;
            }

            intake.Status = normalizedStatus;
            intake.ReviewedById = parsedUserId;
            intake.ReviewedAt = DateTime.UtcNow;

            await _context.SaveChangesAsync();

            return Ok(new
            {
                statusCode = 200,
                message = normalizedStatus == "Accepted" ? "Intake accepted." : "Intake declined.",
                data = new { intake.Id, intake.Status, intake.CreatedIssueId }
            });
        }
    }

    public class CreateIntakeRequest
    {
        [Required]
        [StringLength(200, MinimumLength = 1)]
        public string Title { get; set; } = string.Empty;

        [StringLength(8000)]
        public string? Description { get; set; }

        [StringLength(32)]
        public string? Source { get; set; }

        [Range(1, 4)]
        public int Priority { get; set; } = 3;
        public DateTime? DesiredDueDate { get; set; }
    }

    public class ReviewIntakeRequest
    {
        [Required]
        [StringLength(16, MinimumLength = 1)]
        public string Status { get; set; } = "Accepted";
    }
}
