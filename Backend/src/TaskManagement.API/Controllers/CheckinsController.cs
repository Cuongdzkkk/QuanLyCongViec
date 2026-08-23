using System.ComponentModel.DataAnnotations;
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using TaskManagement.Application.DTOs.Common;
using TaskManagement.Domain.Entities;
using TaskManagement.Infrastructure.Data;

namespace TaskManagement.API.Controllers
{
    [ApiController]
    [Route("api/checkins")]
    [Authorize]
    public class CheckinsController : ControllerBase
    {
        private readonly ApplicationDbContext _context;

        public CheckinsController(ApplicationDbContext context)
        {
            _context = context;
        }

        [HttpGet]
        public async Task<IActionResult> GetProjectCheckins([FromQuery] Guid projectId, [FromQuery] DateOnly? date = null)
        {
            if (!TryGetUserId(out var userId)) return Unauthorized();
            if (projectId == Guid.Empty) return BadRequest(ApiResponse<object>.Error("ProjectId is required."));

            var canView = await CanAccessProjectAsync(projectId, userId);
            if (!canView) return Forbid();

            var checkinDate = date ?? DateOnly.FromDateTime(DateTime.UtcNow);
            var members = await _context.ProjectMembers
                .AsNoTracking()
                .Include(member => member.User)
                .Where(member => member.ProjectId == projectId && member.Status)
                .OrderBy(member => member.User.FullName)
                .ThenBy(member => member.User.Email)
                .ToListAsync();

            var checkins = await _context.DailyCheckins
                .AsNoTracking()
                .Include(item => item.User)
                .Where(item => item.ProjectId == projectId && item.CheckinDate == checkinDate)
                .ToListAsync();

            var checkinMap = checkins.ToDictionary(item => item.UserId);
            var memberCards = members.Select(member =>
            {
                checkinMap.TryGetValue(member.UserId, out var checkin);
                return new
                {
                    id = member.UserId,
                    userId = member.UserId,
                    userName = string.IsNullOrWhiteSpace(member.User.FullName) ? member.User.Email : member.User.FullName,
                    userEmail = member.User.Email,
                    userAvatar = member.User.AvatarUrl,
                    role = string.IsNullOrWhiteSpace(member.ProjectRole) ? "Member" : member.ProjectRole,
                    checkedIn = checkin != null,
                    yesterday = checkin?.Yesterday ?? string.Empty,
                    today = checkin?.Today ?? string.Empty,
                    blocker = checkin?.Blocker ?? string.Empty,
                    checkinId = checkin?.Id,
                    checkinDate,
                    submittedAt = checkin?.UpdatedAt,
                    isCurrentUser = member.UserId == userId
                };
            }).ToList();

            var reports = checkins
                .OrderByDescending(item => item.UpdatedAt)
                .Select(ToResponse)
                .ToList();

            return Ok(ApiResponse<object>.Success(new
            {
                projectId,
                date = checkinDate,
                checkedInCount = reports.Count,
                memberCount = members.Count,
                members = memberCards,
                reports
            }));
        }

        [HttpPost]
        public async Task<IActionResult> SubmitCheckin([FromBody] SubmitCheckinDto dto)
        {
            if (!TryGetUserId(out var userId)) return Unauthorized();
            if (dto.ProjectId == Guid.Empty) return BadRequest(ApiResponse<object>.Error("ProjectId is required."));

            var yesterday = (dto.Yesterday ?? string.Empty).Trim();
            var today = (dto.Today ?? string.Empty).Trim();
            var blocker = (dto.Blocker ?? string.Empty).Trim();
            if (string.IsNullOrWhiteSpace(yesterday) || string.IsNullOrWhiteSpace(today))
            {
                return BadRequest(ApiResponse<object>.Error("Yesterday and today are required."));
            }

            var canSubmit = await _context.ProjectMembers
                .AnyAsync(member => member.ProjectId == dto.ProjectId && member.UserId == userId && member.Status);
            if (!canSubmit) return Forbid();

            var checkinDate = dto.Date ?? DateOnly.FromDateTime(DateTime.UtcNow);
            var now = DateTime.UtcNow;
            var checkin = await _context.DailyCheckins
                .FirstOrDefaultAsync(item => item.ProjectId == dto.ProjectId && item.UserId == userId && item.CheckinDate == checkinDate);

            if (checkin == null)
            {
                checkin = new DailyCheckin
                {
                    Id = Guid.NewGuid(),
                    ProjectId = dto.ProjectId,
                    UserId = userId,
                    CheckinDate = checkinDate,
                    CreatedAt = now
                };
                _context.DailyCheckins.Add(checkin);
            }

            checkin.Yesterday = yesterday;
            checkin.Today = today;
            checkin.Blocker = blocker;
            checkin.UpdatedAt = now;

            await _context.SaveChangesAsync();

            var saved = await _context.DailyCheckins
                .AsNoTracking()
                .Include(item => item.User)
                .FirstAsync(item => item.Id == checkin.Id);

            return Ok(ApiResponse<object>.Success(ToResponse(saved), "Check-in submitted."));
        }

        [HttpPost("ai-summary")]
        public async Task<IActionResult> GenerateSummary([FromBody] CheckinSummaryRequestDto dto)
        {
            if (!TryGetUserId(out var userId)) return Unauthorized();
            if (dto.ProjectId == Guid.Empty) return BadRequest(ApiResponse<object>.Error("ProjectId is required."));

            var canView = await CanAccessProjectAsync(dto.ProjectId, userId);
            if (!canView) return Forbid();

            var checkinDate = dto.Date ?? DateOnly.FromDateTime(DateTime.UtcNow);
            var checkins = await _context.DailyCheckins
                .AsNoTracking()
                .Include(item => item.User)
                .Where(item => item.ProjectId == dto.ProjectId && item.CheckinDate == checkinDate)
                .OrderBy(item => item.User.FullName)
                .ToListAsync();

            if (checkins.Count == 0)
            {
                return Ok(ApiResponse<object>.Success(new { summaryText = "Không có báo cáo check-in nào được nộp hôm nay." }));
            }

            var blockers = checkins.Where(item => !string.IsNullOrWhiteSpace(item.Blocker)).ToList();
            var lines = new List<string>
            {
                $"### Tóm tắt Daily Check-in ({checkinDate:yyyy-MM-dd})",
                string.Empty,
                $"1. **Đã nộp**: {checkins.Count} thành viên đã gửi báo cáo.",
                $"2. **Trọng tâm hôm nay**: {string.Join("; ", checkins.Select(item => $"{DisplayName(item.User)}: {item.Today}"))}",
                blockers.Count > 0
                    ? $"3. **Blocker**: {string.Join("; ", blockers.Select(item => $"{DisplayName(item.User)}: {item.Blocker}"))}"
                    : "3. **Blocker**: Không có blocker được ghi nhận."
            };

            return Ok(ApiResponse<object>.Success(new { summaryText = string.Join("\n", lines) }));
        }

        private async Task<bool> CanAccessProjectAsync(Guid projectId, Guid userId)
        {
            return await _context.ProjectMembers
                .AnyAsync(member => member.ProjectId == projectId && member.UserId == userId && member.Status);
        }

        private static object ToResponse(DailyCheckin item) => new
        {
            id = item.Id,
            projectId = item.ProjectId,
            userId = item.UserId,
            userName = DisplayName(item.User),
            userEmail = item.User.Email,
            userAvatar = item.User.AvatarUrl,
            date = item.CheckinDate,
            yesterday = item.Yesterday,
            today = item.Today,
            blocker = item.Blocker,
            createdAt = item.CreatedAt,
            updatedAt = item.UpdatedAt
        };

        private static string DisplayName(User user)
        {
            return string.IsNullOrWhiteSpace(user.FullName) ? user.Email : user.FullName;
        }

        private bool TryGetUserId(out Guid userId)
        {
            var claim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            return Guid.TryParse(claim, out userId) && userId != Guid.Empty;
        }
    }

    public class SubmitCheckinDto
    {
        [Required]
        public Guid ProjectId { get; set; }
        public DateOnly? Date { get; set; }
        [Required]
        [MaxLength(4000)]
        public string Yesterday { get; set; } = string.Empty;
        [Required]
        [MaxLength(4000)]
        public string Today { get; set; } = string.Empty;
        [MaxLength(1000)]
        public string? Blocker { get; set; }
    }

    public class CheckinSummaryRequestDto
    {
        [Required]
        public Guid ProjectId { get; set; }
        public DateOnly? Date { get; set; }
    }
}
