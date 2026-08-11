using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.SignalR;
using System.ComponentModel.DataAnnotations;
using System.Security.Claims;
using System.Text.Json;
using TaskManagement.Application.DTOs.Common;
using TaskManagement.Domain.Entities;
using TaskManagement.Infrastructure.Data;
using TaskManagement.API.Hubs;
using TaskManagement.API.Realtime;

namespace TaskManagement.API.Controllers
{
    [ApiController]
    [Route("api/stickies")]
    [Authorize]
    public class StickiesController : ControllerBase
    {
        private const int MaxFloatingNotes = 5;
        private readonly ApplicationDbContext _context;
        private readonly IHubContext<KanbanHub>? _hub;

        public StickiesController(ApplicationDbContext context, IHubContext<KanbanHub>? hub = null)
        {
            _context = context;
            _hub = hub;
        }

        [HttpGet("floating")]
        public async Task<IActionResult> GetFloatingStickies()
        {
            if (!TryGetUserId(out var userId)) return Unauthorized();

            var notes = await _context.StickyNotes
                .AsNoTracking()
                .Where(note => note.UserId == userId && note.IsFloating)
                .OrderBy(note => note.UpdatedAt)
                .Take(MaxFloatingNotes)
                .ToListAsync();

            return Ok(ApiResponse<object>.Success(notes.Select(ToResponse)));
        }

        [HttpGet]
        public async Task<IActionResult> GetStickies(
            [FromQuery] int page = 1,
            [FromQuery] int pageSize = 30,
            [FromQuery] string? search = null,
            [FromQuery] bool? pinned = null)
        {
            if (!TryGetUserId(out var userId)) return Unauthorized();

            page = Math.Max(1, page);
            pageSize = Math.Clamp(pageSize, 1, 50);
            var query = _context.StickyNotes
                .AsNoTracking()
                .Where(note => note.UserId == userId);

            if (!string.IsNullOrWhiteSpace(search))
            {
                var term = search.Trim();
                query = query.Where(note => note.Title.Contains(term) || note.Content.Contains(term));
            }

            if (pinned.HasValue)
            {
                query = query.Where(note => note.IsPinned == pinned.Value);
            }

            var total = await query.CountAsync();
            var notes = await query
                .OrderByDescending(note => note.IsPinned)
                .ThenByDescending(note => note.UpdatedAt)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();

            return Ok(ApiResponse<object>.Success(new
            {
                page,
                pageSize,
                total,
                items = notes.Select(ToResponse)
            }));
        }

        [HttpGet("{id:guid}")]
        public async Task<IActionResult> GetSticky(Guid id)
        {
            if (!TryGetUserId(out var userId)) return Unauthorized();

            var note = await _context.StickyNotes
                .AsNoTracking()
                .FirstOrDefaultAsync(item => item.Id == id && item.UserId == userId);

            return note == null
                ? NotFound(ApiResponse<object>.Error("Không tìm thấy ghi chú."))
                : Ok(ApiResponse<object>.Success(ToResponse(note)));
        }

        [HttpPost]
        public async Task<IActionResult> CreateSticky([FromBody] StickyNoteDto dto)
        {
            if (!TryGetUserId(out var userId)) return Unauthorized();

            var validation = await ResolveContextAsync(userId, dto);
            if (!validation.IsValid)
            {
                return BadRequest(ApiResponse<object>.Error(validation.Error!));
            }

            var now = DateTime.UtcNow;
            var note = new StickyNote
            {
                Id = Guid.NewGuid(),
                UserId = userId,
                WorkspaceId = validation.WorkspaceId,
                ProjectId = dto.ProjectId,
                WorkTaskId = dto.WorkTaskId,
                GoalId = dto.GoalId,
                Title = dto.Title.Trim(),
                Content = dto.Content ?? string.Empty,
                Color = dto.Color.ToUpperInvariant(),
                IsPinned = dto.IsPinned,
                SourceRoute = NormalizeRoute(dto.SourceRoute),
                CreatedAt = now,
                UpdatedAt = now
            };

            _context.StickyNotes.Add(note);
            AddAudit(userId, "sticky.create", note, "Success");
            await _context.SaveChangesAsync();
            await PublishStickyChangedAsync(userId, "created", note);

            return CreatedAtAction(nameof(GetSticky), new { id = note.Id }, ApiResponse<object>.Success(ToResponse(note)));
        }

        [HttpPut("{id:guid}")]
        public async Task<IActionResult> UpdateSticky(Guid id, [FromBody] StickyNoteDto dto)
        {
            if (!TryGetUserId(out var userId)) return Unauthorized();

            var note = await _context.StickyNotes.FirstOrDefaultAsync(item => item.Id == id && item.UserId == userId);
            if (note == null) return NotFound(ApiResponse<object>.Error("Không tìm thấy ghi chú."));
            PreserveCreatedAt(note);

            var validation = await ResolveContextAsync(userId, dto);
            if (!validation.IsValid)
            {
                return BadRequest(ApiResponse<object>.Error(validation.Error!));
            }

            note.WorkspaceId = validation.WorkspaceId;
            note.ProjectId = dto.ProjectId;
            note.WorkTaskId = dto.WorkTaskId;
            note.GoalId = dto.GoalId;
            note.Title = dto.Title.Trim();
            note.Content = dto.Content ?? string.Empty;
            note.Color = dto.Color.ToUpperInvariant();
            note.IsPinned = dto.IsPinned;
            note.SourceRoute = NormalizeRoute(dto.SourceRoute);
            note.UpdatedAt = DateTime.UtcNow;

            await _context.SaveChangesAsync();
            await PublishStickyChangedAsync(userId, "updated", note);
            return Ok(ApiResponse<object>.Success(ToResponse(note)));
        }

        [HttpPatch("{id:guid}/pin")]
        public async Task<IActionResult> SetPinned(Guid id, [FromBody] StickyPinDto dto)
        {
            if (!TryGetUserId(out var userId)) return Unauthorized();

            var note = await _context.StickyNotes.FirstOrDefaultAsync(item => item.Id == id && item.UserId == userId);
            if (note == null) return NotFound(ApiResponse<object>.Error("Không tìm thấy ghi chú."));
            PreserveCreatedAt(note);

            note.IsPinned = dto.IsPinned;
            note.UpdatedAt = DateTime.UtcNow;
            await _context.SaveChangesAsync();
            await PublishStickyChangedAsync(userId, "updated", note);

            return Ok(ApiResponse<object>.Success(ToResponse(note)));
        }

        [HttpPatch("{id:guid}/floating-state")]
        public async Task<IActionResult> SetFloatingState(Guid id, [FromBody] StickyFloatingStateDto dto)
        {
            if (!TryGetUserId(out var userId)) return Unauthorized();
            if (dto.IsFloating && (!dto.PositionX.HasValue || !dto.PositionY.HasValue))
            {
                return BadRequest(ApiResponse<object>.Error("Vị trí ghi chú nổi không hợp lệ."));
            }

            var note = await _context.StickyNotes.FirstOrDefaultAsync(item => item.Id == id && item.UserId == userId);
            if (note == null) return NotFound(ApiResponse<object>.Error("Không tìm thấy ghi chú."));
            PreserveCreatedAt(note);

            if (dto.IsFloating && !note.IsFloating)
            {
                var floatingCount = await _context.StickyNotes.CountAsync(item => item.UserId == userId && item.IsFloating);
                if (floatingCount >= MaxFloatingNotes)
                {
                    return Conflict(ApiResponse<object>.Error("Bạn chỉ có thể dán tối đa 5 ghi chú. Hãy gỡ một ghi chú khỏi màn hình trước."));
                }
            }

            note.IsFloating = dto.IsFloating;
            if (dto.IsFloating)
            {
                note.PositionX = dto.PositionX;
                note.PositionY = dto.PositionY;
            }
            note.UpdatedAt = DateTime.UtcNow;
            await _context.SaveChangesAsync();
            await PublishStickyChangedAsync(userId, "updated", note);

            return Ok(ApiResponse<object>.Success(ToResponse(note)));
        }

        [HttpDelete("{id:guid}")]
        public async Task<IActionResult> DeleteSticky(Guid id)
        {
            if (!TryGetUserId(out var userId)) return Unauthorized();

            var note = await _context.StickyNotes.FirstOrDefaultAsync(item => item.Id == id && item.UserId == userId);
            if (note == null) return NotFound(ApiResponse<object>.Error("Không tìm thấy ghi chú."));
            PreserveCreatedAt(note);

            note.IsDeleted = true;
            note.UpdatedAt = DateTime.UtcNow;
            AddAudit(userId, "sticky.delete", note, "Success");
            await _context.SaveChangesAsync();
            await PublishStickyChangedAsync(userId, "deleted", note);

            return Ok(ApiResponse<object>.Success(new { note.Id }, "Đã xóa ghi chú."));
        }

        private bool TryGetUserId(out Guid userId)
        {
            return Guid.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out userId);
        }

        private Task PublishStickyChangedAsync(Guid userId, string action, StickyNote note)
        {
            return _hub == null
                ? Task.CompletedTask
                : _hub.PublishUserEntityChangedAsync(userId, "StickyNote", action, note.Id, ToResponse(note));
        }

        private async Task<StickyContextValidation> ResolveContextAsync(Guid userId, StickyNoteDto dto)
        {
            Guid? workspaceId = dto.WorkspaceId;

            if (dto.ProjectId.HasValue)
            {
                var project = await _context.Projects
                    .AsNoTracking()
                    .FirstOrDefaultAsync(item => item.Id == dto.ProjectId.Value);
                if (project == null) return StickyContextValidation.Invalid("Dự án liên kết không tồn tại.");
                if (workspaceId.HasValue && workspaceId != project.WorkspaceId) return StickyContextValidation.Invalid("Dự án không thuộc workspace đã chọn.");
                workspaceId = project.WorkspaceId;
            }

            if (dto.WorkTaskId.HasValue)
            {
                var task = await _context.WorkTasks
                    .AsNoTracking()
                    .FirstOrDefaultAsync(item => item.Id == dto.WorkTaskId.Value);
                if (task == null) return StickyContextValidation.Invalid("Công việc liên kết không tồn tại.");
                if (dto.ProjectId.HasValue && dto.ProjectId != task.ProjectId) return StickyContextValidation.Invalid("Công việc không thuộc dự án đã chọn.");
                if (workspaceId.HasValue && workspaceId != task.WorkspaceId) return StickyContextValidation.Invalid("Công việc không thuộc workspace đã chọn.");
                workspaceId = task.WorkspaceId;
            }

            if (dto.GoalId.HasValue)
            {
                var goal = await _context.Goals
                    .AsNoTracking()
                    .FirstOrDefaultAsync(item => item.Id == dto.GoalId.Value);
                if (goal == null) return StickyContextValidation.Invalid("Mục tiêu liên kết không tồn tại.");
                if (workspaceId.HasValue && workspaceId != goal.WorkspaceId) return StickyContextValidation.Invalid("Mục tiêu không thuộc workspace đã chọn.");
                workspaceId = goal.WorkspaceId;
            }

            if (workspaceId.HasValue)
            {
                var canAccessWorkspace = await _context.WorkspaceMembers
                    .AsNoTracking()
                    .AnyAsync(member => member.WorkspaceId == workspaceId.Value && member.UserId == userId && member.IsActive);
                if (!canAccessWorkspace) return StickyContextValidation.Invalid("Bạn không có quyền liên kết ghi chú với workspace này.");
            }

            if (!string.IsNullOrWhiteSpace(dto.SourceRoute) && !dto.SourceRoute.Trim().StartsWith('/'))
            {
                return StickyContextValidation.Invalid("Route liên kết không hợp lệ.");
            }

            return StickyContextValidation.Valid(workspaceId);
        }

        private void AddAudit(Guid userId, string action, StickyNote note, string status)
        {
            _context.SystemAuditLogs.Add(new SystemAuditLog
            {
                Id = Guid.NewGuid(),
                UserId = userId,
                Action = action,
                Resource = "StickyNote",
                Status = status,
                IPAddress = HttpContext.Connection.RemoteIpAddress?.ToString(),
                Details = JsonSerializer.Serialize(new { stickyId = note.Id, note.Title }),
                CreatedAt = DateTime.UtcNow
            });
        }

        private static string? NormalizeRoute(string? route)
        {
            return string.IsNullOrWhiteSpace(route) ? null : route.Trim();
        }

        private static void PreserveCreatedAt(StickyNote note)
        {
            note.CreatedAt = DateTime.SpecifyKind(note.CreatedAt, DateTimeKind.Utc);
        }

        private static object ToResponse(StickyNote note) => new
        {
            note.Id,
            note.WorkspaceId,
            note.ProjectId,
            note.WorkTaskId,
            note.GoalId,
            note.Title,
            note.Content,
            note.Color,
            note.IsPinned,
            note.IsFloating,
            note.PositionX,
            note.PositionY,
            note.SourceRoute,
            note.CreatedAt,
            note.UpdatedAt
        };

        private sealed record StickyContextValidation(bool IsValid, Guid? WorkspaceId, string? Error)
        {
            public static StickyContextValidation Valid(Guid? workspaceId) => new(true, workspaceId, null);
            public static StickyContextValidation Invalid(string error) => new(false, null, error);
        }
    }

    public class StickyNoteDto
    {
        public Guid? WorkspaceId { get; set; }
        public Guid? ProjectId { get; set; }
        public Guid? WorkTaskId { get; set; }
        public Guid? GoalId { get; set; }

        [Required]
        [StringLength(180, MinimumLength = 1)]
        public string Title { get; set; } = string.Empty;

        [StringLength(10000)]
        public string? Content { get; set; }

        [Required]
        [RegularExpression("^#[0-9A-Fa-f]{6}$")]
        public string Color { get; set; } = "#FEF08A";

        public bool IsPinned { get; set; }

        [StringLength(500)]
        public string? SourceRoute { get; set; }
    }

    public class StickyPinDto
    {
        public bool IsPinned { get; set; }
    }

    public class StickyFloatingStateDto
    {
        public bool IsFloating { get; set; }

        [Range(0, 10000)]
        public int? PositionX { get; set; }

        [Range(0, 10000)]
        public int? PositionY { get; set; }
    }
}
