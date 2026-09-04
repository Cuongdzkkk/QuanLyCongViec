using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using TaskManagement.Application.Common;
using TaskManagement.Application.Interfaces;
using TaskManagement.Domain.Entities;
using TaskManagement.Infrastructure.Data;

namespace TaskManagement.API.Controllers;

[ApiController]
[Route("api/site-account-links")]
[Authorize]
public sealed class SiteAccountLinksController : ControllerBase
{
    private readonly ApplicationDbContext _context;
    private readonly INotificationService _notificationService;
    private readonly IEmailService _emailService;
    private readonly IConfiguration _configuration;

    public SiteAccountLinksController(
        ApplicationDbContext context,
        INotificationService notificationService,
        IEmailService emailService,
        IConfiguration configuration)
    {
        _context = context;
        _notificationService = notificationService;
        _emailService = emailService;
        _configuration = configuration;
    }

    [HttpGet]
    public async Task<IActionResult> GetAcceptedLinks()
    {
        var userId = GetUserId();
        if (userId == null) return Unauthorized();

        var linkedAccounts = await _context.SiteAccountLinkRequests
            .AsNoTracking()
            .Where(request => request.Status == "Accepted" &&
                (request.RequesterUserId == userId.Value || request.TargetUserId == userId.Value))
            .Select(request => request.RequesterUserId == userId.Value
                ? new
                {
                    Id = request.TargetUser.Id,
                    Name = request.TargetUser.FullName,
                    Email = request.TargetUser.Email,
                    AvatarUrl = request.TargetUser.AvatarUrl
                }
                : new
                {
                    Id = request.RequesterUser.Id,
                    Name = request.RequesterUser.FullName,
                    Email = request.RequesterUser.Email,
                    AvatarUrl = request.RequesterUser.AvatarUrl
                })
            .Distinct()
            .OrderBy(account => account.Name)
            .ToListAsync();

        return Ok(new { statusCode = 200, data = linkedAccounts });
    }

    [HttpPost]
    public async Task<IActionResult> RequestLink([FromBody] SiteAccountLinkRequestDto request)
    {
        var requesterId = GetUserId();
        if (requesterId == null) return Unauthorized();

        var email = EmailCanonicalizer.Normalize(request.Email);
        if (string.IsNullOrWhiteSpace(email))
            return BadRequest(new { statusCode = 400, message = "Vui lòng nhập email tài khoản site chủ." });

        var requester = await _context.Users.FirstOrDefaultAsync(user =>
            user.Id == requesterId.Value && user.IsActive && !user.IsDeleted);
        var target = await _context.Users.FirstOrDefaultAsync(user =>
            user.Email.ToLower() == email && user.IsActive && !user.IsDeleted);
        if (requester == null) return Unauthorized();
        if (target == null)
            return NotFound(new { statusCode = 404, message = "Không tìm thấy tài khoản site chủ với email này." });
        if (target.Id == requester.Id)
            return BadRequest(new { statusCode = 400, message = "Bạn không thể liên kết với chính tài khoản của mình." });

        var existing = await _context.SiteAccountLinkRequests
            .Where(item => item.RequesterUserId == requester.Id && item.TargetUserId == target.Id)
            .OrderByDescending(item => item.CreatedAt)
            .FirstOrDefaultAsync();
        if (existing?.Status == "Accepted")
            return Conflict(new { statusCode = 409, message = "Hai tài khoản đã được liên kết." });
        if (existing?.Status == "Pending")
            return Conflict(new { statusCode = 409, message = "Yêu cầu liên kết đang chờ được chấp thuận." });

        var now = DateTime.UtcNow;
        var linkRequest = new SiteAccountLinkRequest
        {
            Id = Guid.NewGuid(),
            RequesterUserId = requester.Id,
            TargetUserId = target.Id,
            Status = "Pending",
            CreatedAt = now
        };
        _context.SiteAccountLinkRequests.Add(linkRequest);
        await _context.SaveChangesAsync();

        await _notificationService.SendNotificationAsync(
            target.Id,
            "Yêu cầu liên kết tài khoản site chủ",
            $"{requester.FullName} muốn liên kết với tài khoản site chủ của bạn.",
            "SITE_ACCOUNT_LINK_REQUEST",
            "/home/notifications",
            requester.Id,
            linkRequest.Id);

        await _emailService.SendInviteEmailAsync(
            target.Email,
            target.FullName,
            requester.FullName,
            "SprintA",
            null,
            BuildNotificationUrl(),
            $"{requester.FullName} ({requester.Email}) đã gửi yêu cầu liên kết tài khoản site chủ. Vui lòng mở SprintA để chấp thuận hoặc từ chối.");

        return Ok(new { statusCode = 200, message = "Đã gửi yêu cầu liên kết.", data = new { linkRequest.Id } });
    }

    [HttpPost("{id:guid}/accept")]
    public Task<IActionResult> Accept(Guid id) => Resolve(id, "Accepted");

    [HttpPost("{id:guid}/decline")]
    public Task<IActionResult> Decline(Guid id) => Resolve(id, "Declined");

    private async Task<IActionResult> Resolve(Guid id, string status)
    {
        var userId = GetUserId();
        if (userId == null) return Unauthorized();

        var request = await _context.SiteAccountLinkRequests
            .Include(item => item.RequesterUser)
            .Include(item => item.TargetUser)
            .FirstOrDefaultAsync(item => item.Id == id && item.TargetUserId == userId.Value);
        if (request == null) return NotFound();
        if (request.Status != "Pending")
            return Conflict(new { statusCode = 409, message = "Yêu cầu này đã được xử lý." });

        request.Status = status;
        request.RespondedAt = DateTime.UtcNow;
        var pendingNotification = await _context.Notifications.FirstOrDefaultAsync(notification =>
            notification.UserId == request.TargetUserId &&
            notification.RelatedSiteAccountLinkRequestId == request.Id &&
            notification.ActionState == "Pending");
        if (pendingNotification != null)
        {
            pendingNotification.ActionState = status;
            pendingNotification.IsRead = true;
        }
        await _context.SaveChangesAsync();

        var verb = status == "Accepted" ? "đã chấp thuận" : "đã từ chối";
        await _notificationService.SendNotificationAsync(
            request.RequesterUserId,
            "Kết quả yêu cầu liên kết tài khoản",
            $"{request.TargetUser.FullName} {verb} yêu cầu liên kết tài khoản site chủ.",
            "SITE_ACCOUNT_LINK_RESOLVED",
            "/home/for-you",
            request.TargetUserId,
            request.Id);

        return Ok(new { statusCode = 200, message = status == "Accepted" ? "Đã chấp thuận liên kết." : "Đã từ chối liên kết." });
    }

    private Guid? GetUserId()
    {
        var value = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        return Guid.TryParse(value, out var id) ? id : null;
    }

    private string BuildNotificationUrl()
    {
        var baseUrl = _configuration["Frontend:BaseUrl"] ?? "http://localhost:5173";
        return $"{baseUrl.TrimEnd('/')}/home/notifications";
    }
}

public sealed class SiteAccountLinkRequestDto
{
    public string? Email { get; set; }
}
