using System.Security.Claims;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using TaskManagement.API.Filters;
using TaskManagement.Application.DTOs.Billing;
using TaskManagement.Application.Interfaces;
using TaskManagement.Domain.Entities;
using TaskManagement.Infrastructure.Data;

namespace TaskManagement.API.Controllers;

[ApiController]
[Route("api/admin/billing")]
[SystemAuthorize(roles: "SuperAdmin, Admin, System Admin, Organization Admin, AccessAdmin")]
public sealed class AdminBillingController : ControllerBase
{
    private readonly IBillingService _billingService;
    private readonly ApplicationDbContext _context;

    public AdminBillingController(IBillingService billingService, ApplicationDbContext context)
    {
        _billingService = billingService;
        _context = context;
    }

    [HttpGet("users")]
    public async Task<IActionResult> Users(CancellationToken cancellationToken) =>
        Ok(ApiEnvelope(await _billingService.GetAdminUsersAsync(cancellationToken)));

    [HttpPut("users/{userId:guid}/plan")]
    public Task<IActionResult> ChangePlan(Guid userId, [FromBody] ChangeSubscriptionPlanRequest request, CancellationToken cancellationToken) =>
        Execute(() => _billingService.ChangePlanAsync(userId, request.PlanCode, request.AutoRenew, GetAdminId(), request.Reason, cancellationToken), "Đã cập nhật gói dịch vụ.");

    [HttpPost("users/{userId:guid}/activate")]
    public Task<IActionResult> Activate(Guid userId, [FromBody] ChangeSubscriptionPlanRequest request, CancellationToken cancellationToken) =>
        Execute(() => _billingService.ChangePlanAsync(userId, request.PlanCode, request.AutoRenew, GetAdminId(), request.Reason, cancellationToken), "Đã kích hoạt gói dịch vụ.");

    [HttpPost("users/{userId:guid}/extend")]
    public Task<IActionResult> Extend(Guid userId, [FromBody] AdminReasonRequest request, CancellationToken cancellationToken) =>
        Execute(() => _billingService.ExtendAsync(userId, GetAdminId(), request.Reason, cancellationToken), "Đã gia hạn thêm một tháng.");

    [HttpPost("users/{userId:guid}/cancel")]
    public Task<IActionResult> Cancel(Guid userId, [FromBody] AdminReasonRequest request, CancellationToken cancellationToken) =>
        Execute(() => _billingService.CancelAsync(userId, GetAdminId(), request.Reason, cancellationToken), "Đã hủy gói trả phí. Người dùng trở về quyền lợi Free.");

    [HttpPost("users/{userId:guid}/credit-adjustments")]
    public Task<IActionResult> AddAdjustment(Guid userId, [FromBody] CreditAdjustmentRequest request, CancellationToken cancellationToken) =>
        Execute(() => _billingService.AddAdjustmentAsync(userId, request.Amount, GetAdminId(), request.Reason, cancellationToken), "Đã điều chỉnh AI credits cho kỳ hiện tại.");

    [HttpPost("users/{userId:guid}/reset-current-period-usage")]
    public Task<IActionResult> ResetUsage(Guid userId, [FromBody] AdminReasonRequest request, CancellationToken cancellationToken) =>
        Execute(() => _billingService.ResetCurrentPeriodUsageAsync(userId, GetAdminId(), request.Reason, cancellationToken), "Đã reset usage kỳ hiện tại bằng bút toán ADMIN/TEST; lịch sử gốc được giữ nguyên.");

    [HttpGet("orders")]
    public async Task<IActionResult> Orders([FromQuery] string? status, CancellationToken cancellationToken) =>
        Ok(ApiEnvelope(await _billingService.GetOrdersAsync(null, status, cancellationToken)));

    [HttpGet("orders/search")]
    public async Task<IActionResult> SearchOrders([FromQuery] BillingOrderQuery query, CancellationToken cancellationToken) =>
        Ok(ApiEnvelope(await _billingService.SearchOrdersAsync(null, query, cancellationToken)));

    [HttpGet("orders/{id:guid}")]
    public async Task<IActionResult> OrderDetails(Guid id, CancellationToken cancellationToken)
    {
        try { return Ok(ApiEnvelope(await _billingService.GetOrderDetailsAsync(id, null, true, cancellationToken))); }
        catch (KeyNotFoundException ex) { return NotFound(Error(ex.Message, 404)); }
    }

    [HttpGet("orders/{id:guid}/receipt")]
    public async Task<IActionResult> Receipt(Guid id, CancellationToken cancellationToken)
    {
        try { return Ok(ApiEnvelope(await _billingService.GetReceiptAsync(id, null, true, cancellationToken))); }
        catch (KeyNotFoundException ex) { return NotFound(Error(ex.Message, 404)); }
    }

    [HttpPost("orders/{id:guid}/receipt/resend")]
    public async Task<IActionResult> ResendReceipt(Guid id, CancellationToken cancellationToken)
    {
        try { return Ok(ApiEnvelope(await _billingService.ResendReceiptAsync(id, null, true, cancellationToken), "Đã yêu cầu gửi lại receipt cho khách hàng.")); }
        catch (KeyNotFoundException ex) { return NotFound(Error(ex.Message, 404)); }
        catch (InvalidOperationException ex) { return Conflict(Error(ex.Message, 409)); }
    }

    [HttpPost("orders/{id:guid}/approve")]
    public Task<IActionResult> ApproveOrder(Guid id, [FromBody] AdminReasonRequest request, CancellationToken cancellationToken) =>
        Execute(() => _billingService.ApproveOrderAsync(id, GetAdminId(), request.Reason, cancellationToken), "Đã xác nhận thanh toán và kích hoạt gói.");

    [HttpPost("orders/{id:guid}/reject")]
    public Task<IActionResult> RejectOrder(Guid id, [FromBody] AdminReasonRequest request, CancellationToken cancellationToken) =>
        Execute(() => _billingService.RejectOrderAsync(id, GetAdminId(), request.Reason, cancellationToken), "Đã từ chối đơn thanh toán.");

    [HttpGet("plans")]
    public async Task<IActionResult> Plans(CancellationToken cancellationToken)
    {
        var plans = await _context.AiPricingPlans.AsNoTracking().OrderBy(plan => plan.MonthlyPriceVnd).ThenBy(plan => plan.Code)
            .Select(plan => new
            {
                plan.Code, plan.Name, plan.MonthlyPriceVnd, plan.IncludedAiCredits,
                plan.IsPublished, plan.IsRecommended, plan.PricingStatus, plan.UpdatedAt
            }).ToListAsync(cancellationToken);
        return Ok(ApiEnvelope(plans));
    }

    [HttpPut("plans/{code}")]
    public async Task<IActionResult> UpdatePlan(string code, [FromBody] UpdatePricingPlanRequest request, CancellationToken cancellationToken)
    {
        if (request.MonthlyPriceVnd < 0 || request.IncludedAiCredits < 0)
            return BadRequest(Error("Giá và AI credits không được âm."));
        var normalizedCode = code.Trim().ToLowerInvariant();
        var plan = await _context.AiPricingPlans.SingleOrDefaultAsync(item => item.Code == normalizedCode, cancellationToken);
        if (plan == null) return NotFound(new { statusCode = 404, message = "Gói dịch vụ không tồn tại." });
        if (normalizedCode == "enterprise")
            return BadRequest(Error("Enterprise giữ chế độ Liên hệ và không cấu hình giá mua trực tuyến."));

        plan.MonthlyPriceVnd = request.MonthlyPriceVnd;
        plan.IncludedAiCredits = request.IncludedAiCredits;
        plan.IsPublished = request.IsPublished;
        plan.IsRecommended = request.IsRecommended;
        plan.UpdatedAt = DateTime.UtcNow;
        _context.SystemAuditLogs.Add(new SystemAuditLog
        {
            Id = Guid.NewGuid(), UserId = GetAdminId(), Action = "BILLING_PLAN_CONFIGURATION_UPDATE",
            Resource = $"billing:plan:{normalizedCode}", Status = "Success",
            Details = JsonSerializer.Serialize(request), CreatedAt = DateTime.UtcNow
        });
        await _context.SaveChangesAsync(cancellationToken);
        return Ok(ApiEnvelope(new { plan.Code, plan.Name, plan.MonthlyPriceVnd, plan.IncludedAiCredits, plan.IsPublished, plan.IsRecommended }, "Đã lưu cấu hình gói."));
    }

    private async Task<IActionResult> Execute<T>(Func<Task<T>> action, string message) where T : notnull
    {
        try
        {
            return Ok(ApiEnvelope(await action(), message));
        }
        catch (ArgumentException ex)
        {
            return BadRequest(Error(ex.Message));
        }
        catch (InvalidOperationException ex)
        {
            return Conflict(Error(ex.Message, 409));
        }
    }

    private Guid GetAdminId() => Guid.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var userId)
        ? userId
        : throw new UnauthorizedAccessException("Invalid administrator identity.");

    private static object ApiEnvelope(object data, string message = "Success") => new { statusCode = 200, message, data };
    private static object Error(string message, int statusCode = 400) => new { statusCode, message };
}
