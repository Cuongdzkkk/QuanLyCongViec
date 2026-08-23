using System.Text.Json;
using System.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using TaskManagement.Application.DTOs.Billing;
using TaskManagement.Application.Interfaces;
using TaskManagement.Domain.Entities;
using TaskManagement.Infrastructure.Data;

namespace TaskManagement.Infrastructure.Services;

public sealed class BillingService : IBillingService
{
    private readonly ApplicationDbContext _context;
    private readonly IAiCreditUsageService _creditUsageService;

    public BillingService(ApplicationDbContext context, IAiCreditUsageService creditUsageService)
    {
        _context = context;
        _creditUsageService = creditUsageService;
    }

    public async Task<BillingSummaryDto> GetSummaryAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        var usage = await _creditUsageService.GetUsageAsync(userId, DateTime.MinValue, DateTime.MaxValue, cancellationToken);
        var now = DateTime.UtcNow;
        var bucketRows = await _context.AiCreditBuckets.AsNoTracking().Where(x => x.UserId == userId)
            .OrderBy(x => x.ExpiresAt).ThenBy(x => x.CreatedAt).ThenBy(x => x.Id).ToListAsync(cancellationToken);
        var bucketIds = bucketRows.Select(x => x.Id).ToList();
        var reservedByBucket = await _context.AiCreditReservationAllocations.AsNoTracking()
            .Where(x => bucketIds.Contains(x.CreditBucketId) && x.AllocatedCredits > 0)
            .GroupBy(x => x.CreditBucketId)
            .Select(x => new { BucketId = x.Key, Reserved = x.Sum(item => item.AllocatedCredits) })
            .ToDictionaryAsync(x => x.BucketId, x => x.Reserved, cancellationToken);
        var buckets = bucketRows.Select(x =>
        {
            var reserved = reservedByBucket.GetValueOrDefault(x.Id);
            var status = x.ValidFrom > now ? "Future" : x.ExpiresAt <= now ? "Expired" : x.RemainingCredits <= 0 && reserved <= 0 ? "Consumed" : "Active";
            return new CreditBucketDto
            {
                Id = x.Id, SourcePlan = x.PlanCode, Granted = x.GrantedCredits,
                Remaining = Math.Max(0, x.RemainingCredits), Reserved = reserved,
                Consumed = Math.Max(0, x.GrantedCredits - x.RemainingCredits - reserved),
                ValidFrom = x.ValidFrom, ExpiresAt = x.ExpiresAt, SourceReference = x.SourceReference, Status = status
            };
        }).ToList();
        var planName = await _context.AiPricingPlans.AsNoTracking()
            .Where(plan => plan.Code == usage.PlanCode)
            .Select(plan => plan.Name)
            .SingleOrDefaultAsync(cancellationToken) ?? usage.PlanCode;
        var pendingOrder = await _context.PaymentOrders.AsNoTracking()
            .Include(order => order.User)
            .Where(order => order.UserId == userId && order.Status == "Pending")
            .OrderByDescending(order => order.CreatedAt)
            .FirstOrDefaultAsync(cancellationToken);

        return new BillingSummaryDto
        {
            PlanCode = usage.PlanCode,
            PlanName = planName,
            SubscriptionStatus = pendingOrder != null && usage.PlanCode == "free"
                ? "PendingPayment"
                : usage.SubscriptionStatus,
            IncludedCredits = usage.IncludedCredits,
            AdjustmentCredits = usage.AdjustmentCredits,
            UsedCredits = usage.UsedCredits,
            RemainingCredits = usage.RemainingCredits,
            TotalRemainingCredits = usage.TotalRemainingCredits > 0 ? usage.TotalRemainingCredits : usage.RemainingCredits,
            CreditBuckets = buckets,
            CurrentPeriodStart = usage.CurrentPeriodStart,
            CurrentPeriodEnd = usage.CurrentPeriodEnd,
            PendingOrder = pendingOrder == null ? null : await ToOrderDtoAsync(pendingOrder, cancellationToken)
        };
    }

    public async Task<IReadOnlyList<PaymentOrderDto>> GetOrdersAsync(
        Guid? userId,
        string? status,
        CancellationToken cancellationToken = default)
    {
        var query = _context.PaymentOrders.AsNoTracking().Include(order => order.User).AsQueryable();
        if (userId.HasValue) query = query.Where(order => order.UserId == userId.Value);
        if (!string.IsNullOrWhiteSpace(status)) query = query.Where(order => order.Status == status.Trim());

        var orders = await query.OrderByDescending(order => order.CreatedAt).ToListAsync(cancellationToken);
        var planCodes = orders.Select(order => order.PlanCode).Distinct().ToList();
        var planNames = await _context.AiPricingPlans.AsNoTracking()
            .Where(plan => planCodes.Contains(plan.Code))
            .ToDictionaryAsync(plan => plan.Code, plan => plan.Name, cancellationToken);
        return orders.Select(order => ToOrderDto(order, planNames.GetValueOrDefault(order.PlanCode, order.PlanCode))).ToList();
    }

    public async Task<PaymentOrderDto> CreateOrderAsync(Guid userId, string planCode, CancellationToken cancellationToken = default)
    {
        var normalizedCode = NormalizePlanCode(planCode);
        var plan = await _context.AiPricingPlans.SingleOrDefaultAsync(
            item => item.Code == normalizedCode && item.IsPublished,
            cancellationToken) ?? throw new ArgumentException("Gói dịch vụ không tồn tại hoặc chưa được công khai.");
        if (plan.MonthlyPriceVnd is null or <= 0 || normalizedCode == "enterprise")
            throw new ArgumentException("Gói này không hỗ trợ thanh toán thủ công.");

        var existing = await _context.PaymentOrders.Include(order => order.User)
            .FirstOrDefaultAsync(order => order.UserId == userId && order.PlanCode == normalizedCode && order.Status == "Pending", cancellationToken);
        if (existing != null) return ToOrderDto(existing, plan.Name);

        var user = await _context.Users.SingleOrDefaultAsync(item => item.Id == userId && !item.IsDeleted, cancellationToken)
            ?? throw new ArgumentException("Người dùng không tồn tại.");
        var order = new PaymentOrder
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            User = user,
            PlanCode = normalizedCode,
            AmountVnd = plan.MonthlyPriceVnd.Value,
            Status = "Pending",
            TransferCode = await CreateTransferCodeAsync(cancellationToken),
            CreatedAt = DateTime.UtcNow,
            IncludedAiCreditsSnapshot = plan.IncludedAiCredits
        };
        _context.PaymentOrders.Add(order);
        await _context.SaveChangesAsync(cancellationToken);
        return ToOrderDto(order, plan.Name);
    }

    public async Task<BillingSummaryDto> ActivateFreeAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        var now = DateTime.UtcNow;
        var freeExists = await _context.AiPricingPlans.AnyAsync(plan => plan.Code == "free", cancellationToken);
        if (!freeExists) throw new InvalidOperationException("Gói Free chưa được cấu hình.");
        var subscription = await GetOrCreateSubscriptionAsync(userId, now, cancellationToken);
        subscription.PlanCode = "free";
        subscription.Status = "Active";
        subscription.CurrentPeriodStart = MonthStart(now);
        subscription.CurrentPeriodEnd = MonthStart(now).AddMonths(1);
        subscription.ActivatedAt ??= now;
        subscription.CancelledAt = null;
        subscription.AutoRenew = false;
        subscription.UpdatedAt = now;
        await _context.SaveChangesAsync(cancellationToken);
        return await GetSummaryAsync(userId, cancellationToken);
    }

    public async Task<IReadOnlyList<BillingUserDto>> GetAdminUsersAsync(CancellationToken cancellationToken = default)
    {
        var users = await _context.Users.AsNoTracking()
            .Where(user => !user.IsDeleted)
            .OrderBy(user => user.FullName)
            .ThenBy(user => user.Email)
            .Select(user => new { user.Id, user.FullName, user.Email })
            .ToListAsync(cancellationToken);
        var result = new List<BillingUserDto>(users.Count);
        foreach (var user in users)
        {
            var summary = await GetSummaryAsync(user.Id, cancellationToken);
            result.Add(new BillingUserDto
            {
                UserId = user.Id,
                UserName = string.IsNullOrWhiteSpace(user.FullName) ? user.Email.Split('@')[0] : user.FullName,
                Email = user.Email,
                PlanCode = summary.PlanCode,
                PlanName = summary.PlanName,
                SubscriptionStatus = summary.SubscriptionStatus,
                IncludedCredits = summary.IncludedCredits,
                AdjustmentCredits = summary.AdjustmentCredits,
                UsedCredits = summary.UsedCredits,
                RemainingCredits = summary.RemainingCredits,
                CurrentPeriodStart = summary.CurrentPeriodStart,
                CurrentPeriodEnd = summary.CurrentPeriodEnd,
                PendingOrder = summary.PendingOrder
            });
        }
        return result;
    }

    public async Task<BillingSummaryDto> ChangePlanAsync(
        Guid userId, string planCode, bool autoRenew, Guid adminUserId, string? reason,
        CancellationToken cancellationToken = default)
    {
        var normalizedCode = NormalizePlanCode(planCode);
        var planExists = await _context.AiPricingPlans.AnyAsync(plan => plan.Code == normalizedCode, cancellationToken);
        if (!planExists) throw new ArgumentException("Gói dịch vụ không tồn tại.");

        var now = DateTime.UtcNow;
        var subscription = await GetOrCreateSubscriptionAsync(userId, now, cancellationToken);
        subscription.PlanCode = normalizedCode;
        subscription.Status = "Active";
        subscription.CurrentPeriodStart = normalizedCode == "free" ? MonthStart(now) : now;
        subscription.CurrentPeriodEnd = normalizedCode == "free" ? MonthStart(now).AddMonths(1) : now.AddMonths(1);
        subscription.ActivatedAt = now;
        subscription.CancelledAt = null;
        subscription.AutoRenew = normalizedCode != "free" && autoRenew;
        subscription.UpdatedAt = now;
        AddAudit(adminUserId, "BILLING_PLAN_CHANGE", userId, new { planCode = normalizedCode, autoRenew, reason });
        await _context.SaveChangesAsync(cancellationToken);
        return await GetSummaryAsync(userId, cancellationToken);
    }

    public async Task<BillingSummaryDto> ExtendAsync(
        Guid userId, Guid adminUserId, string reason, CancellationToken cancellationToken = default)
    {
        RequireReason(reason);
        var subscription = await _context.AiSubscriptions.SingleOrDefaultAsync(item => item.UserId == userId, cancellationToken)
            ?? throw new InvalidOperationException("Người dùng chưa có gói trả phí để gia hạn.");
        if (subscription.PlanCode == "free") throw new InvalidOperationException("Gói Free không cần gia hạn thủ công.");

        var now = DateTime.UtcNow;
        if (subscription.CurrentPeriodEnd <= now)
        {
            subscription.CurrentPeriodStart = now;
            subscription.CurrentPeriodEnd = now.AddMonths(1);
        }
        else
        {
            subscription.CurrentPeriodEnd = subscription.CurrentPeriodEnd.AddMonths(1);
        }
        subscription.Status = "Active";
        subscription.CancelledAt = null;
        subscription.UpdatedAt = now;
        AddAudit(adminUserId, "BILLING_SUBSCRIPTION_EXTEND", userId, new { reason });
        await _context.SaveChangesAsync(cancellationToken);
        return await GetSummaryAsync(userId, cancellationToken);
    }

    public async Task<BillingSummaryDto> CancelAsync(
        Guid userId, Guid adminUserId, string reason, CancellationToken cancellationToken = default)
    {
        RequireReason(reason);
        var now = DateTime.UtcNow;
        var subscription = await GetOrCreateSubscriptionAsync(userId, now, cancellationToken);
        subscription.Status = "Cancelled";
        subscription.CancelledAt = now;
        subscription.AutoRenew = false;
        subscription.UpdatedAt = now;
        AddAudit(adminUserId, "BILLING_SUBSCRIPTION_CANCEL", userId, new { previousPlan = subscription.PlanCode, reason });
        await _context.SaveChangesAsync(cancellationToken);
        return await GetSummaryAsync(userId, cancellationToken);
    }

    public async Task<BillingSummaryDto> AddAdjustmentAsync(
        Guid userId, int amount, Guid adminUserId, string reason, CancellationToken cancellationToken = default)
    {
        RequireReason(reason);
        if (amount == 0) throw new ArgumentException("Số credit điều chỉnh phải khác 0.");
        var usage = await _creditUsageService.GetUsageAsync(userId, DateTime.MinValue, DateTime.MaxValue, cancellationToken);
        _context.AiCreditAdjustments.Add(new AiCreditAdjustment
        {
            Id = Guid.NewGuid(), UserId = userId, Amount = amount, AdjustmentType = "Credit",
            Reason = reason.Trim(), CreatedByUserId = adminUserId,
            EffectivePeriodStart = usage.CurrentPeriodStart, EffectivePeriodEnd = usage.CurrentPeriodEnd,
            CreatedAt = DateTime.UtcNow
        });
        AddAudit(adminUserId, "BILLING_CREDIT_ADJUSTMENT", userId, new { amount, reason });
        await _context.SaveChangesAsync(cancellationToken);
        return await GetSummaryAsync(userId, cancellationToken);
    }

    public async Task<BillingSummaryDto> ResetCurrentPeriodUsageAsync(
        Guid userId, Guid adminUserId, string reason, CancellationToken cancellationToken = default)
    {
        RequireReason(reason);
        var usage = await _creditUsageService.GetUsageAsync(userId, DateTime.MinValue, DateTime.MaxValue, cancellationToken);
        if (usage.UsedCredits > 0)
        {
            _context.AiCreditAdjustments.Add(new AiCreditAdjustment
            {
                Id = Guid.NewGuid(), UserId = userId, Amount = usage.UsedCredits, AdjustmentType = "UsageReset",
                Reason = $"ADMIN/TEST reset: {reason.Trim()}", CreatedByUserId = adminUserId,
                EffectivePeriodStart = usage.CurrentPeriodStart, EffectivePeriodEnd = usage.CurrentPeriodEnd,
                CreatedAt = DateTime.UtcNow
            });
        }
        AddAudit(adminUserId, "BILLING_USAGE_RESET_ADMIN_TEST", userId, new { resetCredits = usage.UsedCredits, reason });
        await _context.SaveChangesAsync(cancellationToken);
        return await GetSummaryAsync(userId, cancellationToken);
    }

    public async Task<PaymentOrderDto> ApproveOrderAsync(
        Guid orderId, Guid adminUserId, string? note, CancellationToken cancellationToken = default)
    {
        await using IDbContextTransaction? transaction = _context.Database.IsRelational()
            ? await _context.Database.BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken)
            : null;
        var order = await _context.PaymentOrders.Include(item => item.User)
            .SingleOrDefaultAsync(item => item.Id == orderId, cancellationToken)
            ?? throw new ArgumentException("Đơn thanh toán không tồn tại.");
        var plan = await _context.AiPricingPlans.SingleOrDefaultAsync(item => item.Code == order.PlanCode, cancellationToken)
            ?? throw new InvalidOperationException("Gói của đơn thanh toán không còn tồn tại.");

        if (order.Status == "Paid") return ToOrderDto(order, plan.Name);
        if (order.Status != "Pending") throw new InvalidOperationException("Chỉ có thể duyệt đơn đang chờ.");

        var now = DateTime.UtcNow;
        await _creditUsageService.EnsureLegacyCutoverAsync(order.UserId, cancellationToken);
        var previousPlanCode = (await _context.AiSubscriptions.AsNoTracking()
            .Where(x => x.UserId == order.UserId)
            .Select(x => x.PlanCode)
            .SingleOrDefaultAsync(cancellationToken)) ?? "free";
        var previousPeriodEnd = await _context.AiSubscriptions.AsNoTracking()
            .Where(x => x.UserId == order.UserId)
            .Select(x => (DateTime?)x.CurrentPeriodEnd)
            .SingleOrDefaultAsync(cancellationToken);
        order.Status = "Paid";
        order.PaidAt = now;
        order.ApprovedByUserId = adminUserId;
        order.AdminNote = string.IsNullOrWhiteSpace(note) ? null : note.Trim();
        var subscription = await GetOrCreateSubscriptionAsync(order.UserId, now, cancellationToken);
        subscription.PlanCode = order.PlanCode;
        subscription.Status = "Active";
        subscription.CurrentPeriodStart = now;
        subscription.CurrentPeriodEnd = now.AddMonths(1);
        subscription.ActivatedAt = now;
        subscription.CancelledAt = null;
        subscription.AutoRenew = false;
        if (order.IncludedAiCreditsSnapshot > 0 && !await _context.AiCreditBuckets.AnyAsync(x => x.SourcePaymentOrderId == order.Id, cancellationToken))
        {
            var isRenewal = string.Equals(previousPlanCode, order.PlanCode, StringComparison.OrdinalIgnoreCase)
                && previousPeriodEnd > now;
            var validFrom = isRenewal ? subscription.CurrentPeriodEnd : now;
            var expiresAt = validFrom.AddMonths(1);
            _context.AiCreditBuckets.Add(new AiCreditBucket
            {
                Id = Guid.NewGuid(), UserId = order.UserId, PlanCode = order.PlanCode,
                GrantedCredits = order.IncludedAiCreditsSnapshot, RemainingCredits = order.IncludedAiCreditsSnapshot,
                ValidFrom = DateTime.SpecifyKind(validFrom, DateTimeKind.Utc), ExpiresAt = DateTime.SpecifyKind(expiresAt, DateTimeKind.Utc),
                SourceType = "PaymentOrder", SourcePaymentOrderId = order.Id, SourceReference = order.TransferCode, CreatedAt = now
            });
        }
        subscription.UpdatedAt = now;
        AddAudit(adminUserId, "PAYMENT_ORDER_APPROVE", order.UserId, new { orderId, order.PlanCode, order.AmountVnd, note });
        await _context.SaveChangesAsync(cancellationToken);
        if (transaction != null) await transaction.CommitAsync(cancellationToken);
        return ToOrderDto(order, plan.Name);
    }

    public async Task<PaymentOrderDto> RejectOrderAsync(
        Guid orderId, Guid adminUserId, string reason, CancellationToken cancellationToken = default)
    {
        RequireReason(reason);
        var order = await _context.PaymentOrders.Include(item => item.User)
            .SingleOrDefaultAsync(item => item.Id == orderId, cancellationToken)
            ?? throw new ArgumentException("Đơn thanh toán không tồn tại.");
        if (order.Status != "Pending") throw new InvalidOperationException("Chỉ có thể từ chối đơn đang chờ.");
        order.Status = "Rejected";
        order.ApprovedByUserId = adminUserId;
        order.AdminNote = reason.Trim();
        AddAudit(adminUserId, "PAYMENT_ORDER_REJECT", order.UserId, new { orderId, reason });
        await _context.SaveChangesAsync(cancellationToken);
        return await ToOrderDtoAsync(order, cancellationToken);
    }

    private async Task<AiSubscription> GetOrCreateSubscriptionAsync(Guid userId, DateTime now, CancellationToken cancellationToken)
    {
        var userExists = await _context.Users.AnyAsync(user => user.Id == userId && !user.IsDeleted, cancellationToken);
        if (!userExists) throw new ArgumentException("Người dùng không tồn tại.");
        var subscription = await _context.AiSubscriptions.SingleOrDefaultAsync(item => item.UserId == userId, cancellationToken);
        if (subscription != null) return subscription;
        subscription = new AiSubscription
        {
            Id = Guid.NewGuid(), UserId = userId, PlanCode = "free", Status = "Active",
            CurrentPeriodStart = MonthStart(now), CurrentPeriodEnd = MonthStart(now).AddMonths(1),
            ActivatedAt = now, AutoRenew = false, CreatedAt = now, UpdatedAt = now
        };
        _context.AiSubscriptions.Add(subscription);
        return subscription;
    }

    private async Task<string> CreateTransferCodeAsync(CancellationToken cancellationToken)
    {
        for (var attempt = 0; attempt < 5; attempt++)
        {
            var code = $"SA{DateTime.UtcNow:yyMMdd}{Guid.NewGuid():N}"[..16].ToUpperInvariant();
            if (!await _context.PaymentOrders.AnyAsync(order => order.TransferCode == code, cancellationToken)) return code;
        }
        throw new InvalidOperationException("Không thể tạo mã thanh toán duy nhất.");
    }

    private async Task<PaymentOrderDto> ToOrderDtoAsync(PaymentOrder order, CancellationToken cancellationToken)
    {
        var planName = await _context.AiPricingPlans.AsNoTracking()
            .Where(plan => plan.Code == order.PlanCode)
            .Select(plan => plan.Name)
            .SingleOrDefaultAsync(cancellationToken) ?? order.PlanCode;
        return ToOrderDto(order, planName);
    }

    private static PaymentOrderDto ToOrderDto(PaymentOrder order, string planName) => new()
    {
        Id = order.Id,
        UserId = order.UserId,
        UserName = string.IsNullOrWhiteSpace(order.User?.FullName) ? order.User?.Email.Split('@')[0] ?? string.Empty : order.User.FullName,
        Email = order.User?.Email ?? string.Empty,
        PlanCode = order.PlanCode,
        PlanName = planName,
        AmountVnd = order.AmountVnd,
        Status = order.Status,
        TransferCode = order.TransferCode,
        CreatedAt = order.CreatedAt,
        PaidAt = order.PaidAt,
        ApprovedByUserId = order.ApprovedByUserId,
        AdminNote = order.AdminNote
        ,IncludedAiCredits = order.IncludedAiCreditsSnapshot
    };

    private void AddAudit(Guid adminUserId, string action, Guid targetUserId, object details)
    {
        _context.SystemAuditLogs.Add(new SystemAuditLog
        {
            Id = Guid.NewGuid(), UserId = adminUserId, Action = action,
            Resource = $"billing:user:{targetUserId}", Status = "Success",
            Details = JsonSerializer.Serialize(details), CreatedAt = DateTime.UtcNow
        });
    }

    private static DateTime MonthStart(DateTime value) =>
        new(value.Year, value.Month, 1, 0, 0, 0, DateTimeKind.Utc);

    private static string NormalizePlanCode(string planCode)
    {
        var normalized = (planCode ?? string.Empty).Trim().ToLowerInvariant();
        if (string.IsNullOrWhiteSpace(normalized)) throw new ArgumentException("Mã gói là bắt buộc.");
        return normalized;
    }

    private static void RequireReason(string reason)
    {
        if (string.IsNullOrWhiteSpace(reason)) throw new ArgumentException("Vui lòng nhập lý do.");
    }
}
