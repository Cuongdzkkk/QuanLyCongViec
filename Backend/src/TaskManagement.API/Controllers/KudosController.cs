using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using TaskManagement.API.Hubs;
using TaskManagement.API.Realtime;
using TaskManagement.Application.DTOs.Common;
using TaskManagement.Domain.Entities;
using TaskManagement.Infrastructure.Data;

namespace TaskManagement.API.Controllers
{
    [ApiController]
    [Route("api/kudos")]
    [Authorize]
    public class KudosController : ControllerBase
    {
        private readonly ApplicationDbContext _context;
        private readonly IHubContext<KanbanHub>? _hub;

        public KudosController(ApplicationDbContext context, IHubContext<KanbanHub>? hub = null)
        {
            _context = context;
            _hub = hub;
        }

        [HttpGet]
        public async Task<IActionResult> GetRecent()
        {
            var senderClaim = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
            var userId = Guid.TryParse(senderClaim, out var parsedUserId) ? parsedUserId : Guid.Empty;
            if (userId == Guid.Empty) return Unauthorized();

            var teamIds = await _context.DepartmentMembers
                .Where(dm => dm.UserId == userId)
                .Select(dm => dm.DepartmentId)
                .ToListAsync();

            var kudos = await _context.Kudos
                .Include(k => k.Sender)
                .Include(k => k.Receiver)
                .Include(k => k.Department)
                .Where(k => k.ReceiverId == userId || k.SenderId == userId || (k.DepartmentId.HasValue && teamIds.Contains(k.DepartmentId.Value)))
                .OrderByDescending(k => k.CreatedAt)
                .Take(100)
                .Select(k => new
                {
                    id = k.Id,
                    message = k.Message,
                    sender = k.Sender.FullName ?? k.Sender.Email,
                    senderName = k.Sender.FullName ?? k.Sender.Email,
                    senderEmail = k.Sender.Email,
                    senderAvatarUrl = k.Sender.AvatarUrl,
                    receiver = k.Receiver == null ? null : k.Receiver.FullName ?? k.Receiver.Email,
                    receiverId = k.ReceiverId,
                    department = k.Department == null ? null : k.Department.Name,
                    departmentId = k.DepartmentId,
                    icon = k.Icon ?? "Star",
                    createdAt = k.CreatedAt
                })
                .ToListAsync();

            return Ok(ApiResponse<object>.Success(kudos));
        }

        [HttpGet("team/{departmentId}")]
        public async Task<IActionResult> GetByTeam(Guid departmentId)
        {
            var kudos = await _context.Kudos
                .Include(k => k.Sender)
                .Include(k => k.Receiver)
                .Include(k => k.Department)
                .Where(k => k.DepartmentId == departmentId)
                .OrderByDescending(k => k.CreatedAt)
                .Select(k => new
                {
                    id = k.Id,
                    message = k.Message,
                    sender = k.Sender.FullName ?? k.Sender.Email,
                    senderName = k.Sender.FullName ?? k.Sender.Email,
                    senderEmail = k.Sender.Email,
                    senderAvatarUrl = k.Sender.AvatarUrl,
                    receiver = k.Receiver == null ? null : k.Receiver.FullName ?? k.Receiver.Email,
                    receiverId = k.ReceiverId,
                    department = k.Department == null ? null : k.Department.Name,
                    departmentId = k.DepartmentId,
                    icon = k.Icon ?? "Star",
                    createdAt = k.CreatedAt
                })
                .ToListAsync();

            return Ok(ApiResponse<object>.Success(kudos));
        }

        [HttpGet("user/{userId}")]
        public async Task<IActionResult> GetByUser(Guid userId)
        {
            var teamIds = await _context.DepartmentMembers
                .Where(dm => dm.UserId == userId)
                .Select(dm => dm.DepartmentId)
                .ToListAsync();

            var kudos = await _context.Kudos
                .Include(k => k.Sender)
                .Include(k => k.Receiver)
                .Include(k => k.Department)
                .Where(k => k.ReceiverId == userId || (k.DepartmentId.HasValue && teamIds.Contains(k.DepartmentId.Value)))
                .OrderByDescending(k => k.CreatedAt)
                .Select(k => new
                {
                    id = k.Id,
                    message = k.Message,
                    sender = k.Sender.FullName ?? k.Sender.Email,
                    senderName = k.Sender.FullName ?? k.Sender.Email,
                    senderEmail = k.Sender.Email,
                    senderAvatarUrl = k.Sender.AvatarUrl,
                    receiver = k.Receiver == null ? null : k.Receiver.FullName ?? k.Receiver.Email,
                    receiverId = k.ReceiverId,
                    department = k.Department == null ? null : k.Department.Name,
                    departmentId = k.DepartmentId,
                    icon = k.Icon ?? "Star",
                    createdAt = k.CreatedAt
                })
                .ToListAsync();

            return Ok(ApiResponse<object>.Success(kudos));
        }

        [HttpPost]
        public async Task<IActionResult> Create([FromBody] CreateKudoDto dto)
        {
            var senderClaim = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
            var senderId = Guid.TryParse(senderClaim, out var parsedSenderId) ? parsedSenderId : Guid.Empty;
            if (senderId == Guid.Empty) return Unauthorized();

            if (string.IsNullOrWhiteSpace(dto.Message))
                return BadRequest(ApiResponse<object>.Error("Noi dung khen ngoi khong duoc de trong."));

            if (!dto.DepartmentId.HasValue && !dto.ReceiverId.HasValue)
                return BadRequest(ApiResponse<object>.Error("Vui long chon nguoi nhan hoac team nhan khen ngoi."));

            if (dto.DepartmentId.HasValue)
            {
                var departmentExists = await _context.Departments
                    .AnyAsync(d => d.Id == dto.DepartmentId.Value && !d.IsDeleted);
                if (!departmentExists)
                    return NotFound(ApiResponse<object>.Error("Team khong ton tai.", 404));
            }

            if (dto.ReceiverId.HasValue)
            {
                var receiverExists = await _context.Users.AnyAsync(u => u.Id == dto.ReceiverId.Value);
                if (!receiverExists)
                    return NotFound(ApiResponse<object>.Error("Nguoi nhan khong ton tai.", 404));
            }

            var kudo = new Kudo
            {
                SenderId = senderId,
                DepartmentId = dto.DepartmentId,
                ReceiverId = dto.ReceiverId,
                Message = dto.Message,
                Icon = dto.Icon,
                CreatedAt = DateTime.UtcNow
            };

            _context.Kudos.Add(kudo);

            var notifications = new List<Notification>();
            if (dto.ReceiverId.HasValue && dto.ReceiverId.Value != senderId)
            {
                notifications.Add(new Notification
                {
                    Id = Guid.NewGuid(),
                    UserId = dto.ReceiverId.Value,
                    Title = "Ban nhan duoc loi khen",
                    Content = dto.Message,
                    LinkUrl = "/home/teams/kudos",
                    CreatedAt = DateTime.UtcNow,
                    NotificationType = "PRAISE_RECEIVED",
                    TriggeredByUserId = senderId
                });
            }

            if (dto.DepartmentId.HasValue)
            {
                var memberIds = await _context.DepartmentMembers
                    .Where(dm => dm.DepartmentId == dto.DepartmentId.Value && dm.UserId != senderId)
                    .Select(dm => dm.UserId)
                    .Distinct()
                    .ToListAsync();

                foreach (var memberId in memberIds.Where(id => !dto.ReceiverId.HasValue || id != dto.ReceiverId.Value))
                {
                    notifications.Add(new Notification
                    {
                        Id = Guid.NewGuid(),
                        UserId = memberId,
                        Title = "Team nhan duoc loi khen",
                        Content = dto.Message,
                        LinkUrl = "/home/teams/kudos",
                        CreatedAt = DateTime.UtcNow,
                        NotificationType = "TEAM_PRAISE_RECEIVED",
                        TriggeredByUserId = senderId
                    });
                }
            }

            if (notifications.Count > 0)
            {
                _context.Notifications.AddRange(notifications);
            }

            _context.SiteAuditLogs.Add(new SiteAuditLog
            {
                EntityId = kudo.Id,
                EntityType = "Praise",
                Action = "CreatePraise",
                NewValue = dto.Message,
                UserId = senderId,
                CreatedAt = DateTime.UtcNow
            });

            await _context.SaveChangesAsync();
            if (_hub != null)
            {
                await _hub.PublishAuthenticatedEntityChangedAsync("Kudo", "created", kudo.Id, new
                {
                    kudo.Id,
                    kudo.DepartmentId,
                    kudo.ReceiverId
                });
                await Task.WhenAll(notifications.Select(notification =>
                    _hub.PublishUserEntityChangedAsync(notification.UserId, "Notification", "created", notification.Id)));
            }

            return Ok(ApiResponse<object>.Success(new { id = kudo.Id }, "Da gui loi khen ngoi."));
        }
    }

    public class CreateKudoDto
    {
        public Guid? DepartmentId { get; set; }
        public Guid? ReceiverId { get; set; }
        public string Message { get; set; } = string.Empty;
        public string? Icon { get; set; }
    }
}
