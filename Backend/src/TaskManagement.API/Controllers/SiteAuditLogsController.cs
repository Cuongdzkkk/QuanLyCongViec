using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;
using TaskManagement.Infrastructure.Data;

namespace TaskManagement.API.Controllers
{
    [ApiController]
    [Route("api/site-auditlogs")]
    [Authorize]
    public class SiteAuditLogsController : ControllerBase
    {
        private readonly ApplicationDbContext _context;

        public SiteAuditLogsController(ApplicationDbContext context)
        {
            _context = context;
        }

        [HttpGet]
        public async Task<IActionResult> Get([FromQuery] string? timeFilter, [FromQuery] string? search, [FromQuery] int limit = 100)
        {
            var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (!Guid.TryParse(userIdClaim, out var userId))
            {
                return Unauthorized(new { statusCode = 401, message = "Unauthorized." });
            }

            var isActiveUser = await _context.Users
                .AsNoTracking()
                .AnyAsync(user => user.Id == userId && user.IsActive && !user.IsDeleted);
            if (!isActiveUser)
            {
                return StatusCode(403, new { statusCode = 403, message = "The authenticated user is not active." });
            }

            var query = _context.SiteAuditLogs
                .AsNoTracking()
                .Include(log => log.User)
                .Where(log => log.UserId == userId)
                .AsQueryable();

            var now = DateTime.UtcNow;
            if (timeFilter == "24h")
            {
                query = query.Where(log => log.CreatedAt >= now.AddHours(-24));
            }
            else
            {
                query = query.Where(log => log.CreatedAt >= now.AddDays(30 * -1));
            }

            if (!string.IsNullOrWhiteSpace(search))
            {
                var s = search.Trim().ToLower();
                query = query.Where(log =>
                    log.EntityType.ToLower().Contains(s) ||
                    log.Action.ToLower().Contains(s) ||
                    (log.NewValue != null && log.NewValue.ToLower().Contains(s)) ||
                    (log.OldValue != null && log.OldValue.ToLower().Contains(s)) ||
                    log.User.Email.ToLower().Contains(s) ||
                    (log.User.FullName != null && log.User.FullName.ToLower().Contains(s)));
            }

            var cappedLimit = Math.Clamp(limit, 1, 200);
            var total = await query.CountAsync();
            var rawItems = await query
                .OrderByDescending(log => log.CreatedAt)
                .ThenBy(log => log.Id)
                .Take(cappedLimit)
                .Select(log => new
                {
                    id = log.Id,
                    timestamp = log.CreatedAt,
                    user = log.User.FullName ?? log.User.Email,
                    action = log.Action,
                    resource = log.EntityType,
                    entityId = log.EntityId,
                    targetId = log.EntityId,
                    status = "success",
                    log.EntityType,
                    log.OldValue,
                    log.NewValue
                })
                .ToListAsync();

            var items = rawItems.Select(log => new
            {
                log.id,
                log.timestamp,
                log.user,
                log.action,
                log.resource,
                log.entityId,
                log.targetId,
                log.status,
                summary = BuildSummary(log.EntityType, log.action, log.OldValue, log.NewValue)
            }).ToList();

            return Ok(new { statusCode = 200, data = new { items, total } });
        }

        private static string BuildSummary(string entityType, string action, string? oldValue, string? newValue)
        {
            return action switch
            {
                "Create" => $"Da tao {entityType}: {newValue}",
                "AddMember" => $"Da them thanh vien vao {entityType}",
                "RemoveMember" => $"Da xoa thanh vien khoi {entityType}",
                "ChangeManager" => $"Da doi quan ly {entityType}",
                "Update" => $"Da cap nhat {entityType}: {newValue}",
                "Archive" => $"Da luu tru {entityType}",
                "Restore" => $"Da khoi phuc {entityType}",
                "Delete" => $"Da xoa {entityType}",
                "LinkGoal" => "Da lien ket muc tieu voi team",
                "UnlinkGoal" => "Da bo lien ket muc tieu khoi team",
                "LinkProject" => "Da lien ket du an voi team",
                "UnlinkProject" => "Da bo lien ket du an khoi team",
                "AddComment" => $"Da binh luan tren {entityType}",
                "AddUpdate" => $"Da them ban cap nhat cho {entityType}",
                "AddLesson" => $"Da them bai hoc cho {entityType}",
                "AddRisk" => $"Da them rui ro cho {entityType}",
                "AddDecision" => $"Da them quyet dinh cho {entityType}",
                "CreatePraise" => $"Da gui loi khen: {newValue}",
                "StatusChanged" => $"Da doi trang thai {entityType}: {oldValue} -> {newValue}",
                "ProgressChanged" => $"Da cap nhat tien do {entityType}: {oldValue} -> {newValue}",
                "Star" => $"Da gan sao {entityType}",
                "Unstar" => $"Da bo gan sao {entityType}",
                "Follow" => $"Da theo doi {entityType}",
                "Unfollow" => $"Da bo theo doi {entityType}",
                _ => $"{action} {entityType}"
            };
        }
    }
}
