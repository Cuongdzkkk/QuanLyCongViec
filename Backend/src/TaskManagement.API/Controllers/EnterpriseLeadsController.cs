using System.ComponentModel.DataAnnotations;
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using TaskManagement.Application.DTOs.Enterprise;
using TaskManagement.Application.Interfaces;
using TaskManagement.API.Filters;
using TaskManagement.Domain.Entities;
using TaskManagement.Infrastructure.Data;

namespace TaskManagement.API.Controllers;

[ApiController]
[Route("api/public/enterprise-leads")]
[AllowAnonymous]
public sealed class EnterpriseLeadsController : ControllerBase
{
    private static readonly HashSet<string> TeamSizes = new(StringComparer.OrdinalIgnoreCase)
    {
        "1–10", "1-10", "11–50", "11-50", "51–200", "51-200", "201–500", "201-500", "500+"
    };
    private static readonly HashSet<string> Needs = new(StringComparer.OrdinalIgnoreCase)
    {
        "Quản lý dự án", "Sprint / Agile", "Báo cáo", "AI workflow", "Multi-team / Organization",
        "Bảo mật / Enterprise deployment", "Khác"
    };

    private readonly ApplicationDbContext _context;
    private readonly INotificationService _notificationService;
    private readonly ILogger<EnterpriseLeadsController> _logger;

    public EnterpriseLeadsController(
        ApplicationDbContext context,
        INotificationService notificationService,
        ILogger<EnterpriseLeadsController> logger)
    {
        _context = context;
        _notificationService = notificationService;
        _logger = logger;
    }

    [HttpPost]
    [EnableRateLimiting("EnterpriseLeadSubmission")]
    public async Task<IActionResult> Create([FromBody] CreateEnterpriseLeadRequest request, CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
            return ValidationProblem(ModelState);

        var contactName = request.ContactName!.Trim();
        var email = request.WorkEmail!.Trim().ToLowerInvariant();
        var company = request.Company!.Trim();
        var teamSize = request.TeamSize!.Trim();

        if (string.IsNullOrWhiteSpace(contactName) || string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(company) || string.IsNullOrWhiteSpace(teamSize))
            return BadRequest(new { statusCode = 400, message = "Vui lòng điền đầy đủ các trường bắt buộc." });
        if (!new EmailAddressAttribute().IsValid(email))
            return BadRequest(new { statusCode = 400, message = "Email công việc không hợp lệ." });
        if (!TeamSizes.Contains(teamSize))
            return BadRequest(new { statusCode = 400, message = "Quy mô đội nhóm không hợp lệ." });
        if (!string.IsNullOrWhiteSpace(request.Need) && !Needs.Contains(request.Need.Trim()))
            return BadRequest(new { statusCode = 400, message = "Nhu cầu không hợp lệ." });

        var now = DateTime.UtcNow;
        var lead = new EnterpriseLead
        {
            Id = Guid.NewGuid(),
            ContactName = contactName,
            WorkEmail = email,
            PhoneOrZalo = NormalizeOptional(request.PhoneOrZalo),
            Company = company,
            TeamSize = teamSize.Replace('–', '-'),
            Need = NormalizeOptional(request.Need),
            Notes = NormalizeOptional(request.Notes),
            PreferredContactTime = NormalizeOptional(request.PreferredContactTime),
            Status = EnterpriseLeadStatus.New,
            Source = "public-website",
            CreatedAt = now,
            UpdatedAt = now
        };

        _context.EnterpriseLeads.Add(lead);
        await _context.SaveChangesAsync(cancellationToken);

        // Persistence is the source of truth. Notification delivery is deliberately best effort.
        try
        {
            var adminIds = await _context.UserRoles
                .Where(userRole => userRole.User.IsActive && !userRole.User.IsDeleted &&
                    (userRole.Role.Name == "Admin" || userRole.Role.Name == "SuperAdmin" ||
                     userRole.Role.Name == "System Admin" || userRole.Role.Name == "Organization Admin" ||
                     userRole.Role.Name == "AccessAdmin" || userRole.Role.Name == "Access Admin"))
                .Select(userRole => userRole.UserId)
                .Distinct()
                .ToListAsync(cancellationToken);

            foreach (var adminId in adminIds)
            {
                try
                {
                    await _notificationService.SendNotificationAsync(
                        adminId,
                        "Yêu cầu tư vấn Enterprise mới",
                        $"{lead.Company} vừa gửi yêu cầu tư vấn Enterprise.",
                        "ENTERPRISE_LEAD",
                        $"/admin/enterprise-leads/{lead.Id}");
                }
                catch (Exception exception)
                {
                    _logger.LogWarning(exception, "Enterprise lead notification delivery failed after lead {LeadId} was saved.", lead.Id);
                }
            }
        }
        catch (Exception exception)
        {
            _logger.LogWarning(exception, "Enterprise lead notification lookup failed after lead {LeadId} was saved.", lead.Id);
        }

        return Ok(new
        {
            statusCode = 200,
            data = new { id = lead.Id },
            message = "Cảm ơn bạn. SprintA đã nhận được yêu cầu và sẽ liên hệ lại."
        });
    }

    private static string? NormalizeOptional(string? value)
    {
        var trimmed = value?.Trim();
        return string.IsNullOrWhiteSpace(trimmed) ? null : trimmed;
    }
}

[ApiController]
[Route("api/admin/enterprise-leads")]
[SystemAuthorize(roles: "SuperAdmin, Admin, System Admin, Organization Admin, AccessAdmin, Access Admin")]
public sealed class AdminEnterpriseLeadsController : ControllerBase
{
    private readonly ApplicationDbContext _context;

    public AdminEnterpriseLeadsController(ApplicationDbContext context)
    {
        _context = context;
    }

    [HttpGet]
    public async Task<IActionResult> List(
        [FromQuery] string? search,
        [FromQuery] EnterpriseLeadStatus? status,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 25,
        CancellationToken cancellationToken = default)
    {
        page = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize, 1, 100);
        var query = _context.EnterpriseLeads.AsNoTracking().AsQueryable();

        if (!string.IsNullOrWhiteSpace(search))
        {
            var term = search.Trim();
            query = query.Where(lead => lead.ContactName.Contains(term) ||
                                        lead.WorkEmail.Contains(term) ||
                                        lead.Company.Contains(term));
        }
        if (status.HasValue)
            query = query.Where(lead => lead.Status == status.Value);

        var total = await query.CountAsync(cancellationToken);
        var items = await query
            .OrderByDescending(lead => lead.CreatedAt)
            .ThenByDescending(lead => lead.Id)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(lead => new
            {
                id = lead.Id,
                contactName = lead.ContactName,
                workEmail = lead.WorkEmail,
                company = lead.Company,
                teamSize = lead.TeamSize,
                need = lead.Need,
                status = lead.Status.ToString(),
                assignedToUserId = lead.AssignedToUserId,
                assignedToName = lead.AssignedToUser == null ? null : lead.AssignedToUser.FullName,
                createdAt = lead.CreatedAt,
                updatedAt = lead.UpdatedAt
            })
            .ToListAsync(cancellationToken);

        return Ok(new { statusCode = 200, data = new { items, total, page, pageSize } });
    }

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> Get(Guid id, CancellationToken cancellationToken)
    {
        var lead = await _context.EnterpriseLeads
            .AsNoTracking()
            .Where(item => item.Id == id)
            .Select(item => new
            {
                id = item.Id,
                contactName = item.ContactName,
                workEmail = item.WorkEmail,
                phoneOrZalo = item.PhoneOrZalo,
                company = item.Company,
                teamSize = item.TeamSize,
                need = item.Need,
                notes = item.Notes,
                preferredContactTime = item.PreferredContactTime,
                status = item.Status.ToString(),
                assignedToUserId = item.AssignedToUserId,
                assignedToName = item.AssignedToUser == null ? null : item.AssignedToUser.FullName,
                internalNote = item.InternalNote,
                source = item.Source,
                createdAt = item.CreatedAt,
                updatedAt = item.UpdatedAt
            })
            .FirstOrDefaultAsync(cancellationToken);

        return lead == null
            ? NotFound(new { statusCode = 404, message = "Không tìm thấy yêu cầu Enterprise." })
            : Ok(new { statusCode = 200, data = lead });
    }

    [HttpPatch("{id:guid}")]
    public async Task<IActionResult> Update(Guid id, [FromBody] UpdateEnterpriseLeadRequest request, CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
            return ValidationProblem(ModelState);

        var lead = await _context.EnterpriseLeads.FirstOrDefaultAsync(item => item.Id == id, cancellationToken);
        if (lead == null)
            return NotFound(new { statusCode = 404, message = "Không tìm thấy yêu cầu Enterprise." });

        if (request.AssignedToUserId.HasValue && !await _context.Users.AnyAsync(user =>
                user.Id == request.AssignedToUserId.Value && user.IsActive && !user.IsDeleted, cancellationToken))
            return BadRequest(new { statusCode = 400, message = "Người phụ trách không hợp lệ." });

        var actorId = Guid.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var parsedActorId)
            ? parsedActorId
            : (Guid?)null;
        var oldStatus = lead.Status;
        var oldAssignee = lead.AssignedToUserId;
        lead.Status = request.Status!.Value;
        lead.InternalNote = string.IsNullOrWhiteSpace(request.InternalNote) ? null : request.InternalNote.Trim();
        lead.AssignedToUserId = request.AssignedToUserId;
        lead.UpdatedAt = DateTime.UtcNow;

        if (oldStatus != lead.Status)
            AddAudit(actorId, "EnterpriseLeadStatusChanged", lead, $"{oldStatus} -> {lead.Status}");
        if (oldAssignee != lead.AssignedToUserId)
            AddAudit(actorId, "EnterpriseLeadAssigneeChanged", lead, "Assignee changed");

        await _context.SaveChangesAsync(cancellationToken);
        return Ok(new { statusCode = 200, data = new { id = lead.Id, status = lead.Status.ToString(), updatedAt = lead.UpdatedAt } });
    }

    private void AddAudit(Guid? actorId, string action, EnterpriseLead lead, string details)
    {
        _context.SystemAuditLogs.Add(new SystemAuditLog
        {
            Id = Guid.NewGuid(),
            UserId = actorId,
            Action = action,
            Resource = "EnterpriseLead",
            Status = "Success",
            Details = $"LeadId={lead.Id}; {details}",
            CreatedAt = DateTime.UtcNow,
            IPAddress = HttpContext.Connection.RemoteIpAddress?.ToString()
        });
    }
}
