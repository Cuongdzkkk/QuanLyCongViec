using System.Data;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.Configuration;
using TaskManagement.Application.DTOs.Billing;
using TaskManagement.Application.Interfaces;
using TaskManagement.Domain.Entities;
using TaskManagement.Infrastructure.Data;

namespace TaskManagement.Infrastructure.Services;

public sealed class BillingService : IBillingService
{
    private readonly ApplicationDbContext _context;
    private readonly IAiCreditUsageService _creditUsageService;
    private readonly IPaymentProvider? _paymentProvider;
    private readonly INotificationService? _notificationService;
    private readonly IEmailService? _emailService;
    private readonly IConfiguration? _configuration;

    public BillingService(
        ApplicationDbContext context,
        IAiCreditUsageService creditUsageService,
        IPaymentProvider? paymentProvider = null,
        INotificationService? notificationService = null,
        IEmailService? emailService = null,
        IConfiguration? configuration = null)
    {
        _context = context;
        _creditUsageService = creditUsageService;
        _paymentProvider = paymentProvider;
        _notificationService = notificationService;
        _emailService = emailService;
        _configuration = configuration;
    }

    public async Task<BillingSummaryDto> GetSummaryAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        var usage = await _creditUsageService.GetUsageAsync(userId, DateTime.MinValue, DateTime.MaxValue, cancellationToken);
        var planName = await _context.AiPricingPlans.AsNoTracking()
            .Where(plan => plan.Code == usage.PlanCode)
            .Select(plan => plan.Name)
            .SingleOrDefaultAsync(cancellationToken) ?? usage.PlanCode;
        var pendingOrder = await _context.PaymentOrders.AsNoTracking()
            .Include(order => order.User)
            .Where(order => order.UserId == userId && order.Status == "Pending" && (order.ExpiresAt == null || order.ExpiresAt > DateTime.UtcNow))
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
        var query = _context.PaymentOrders.AsNoTracking().Include(order => order.User).Include(order => order.Transactions).AsQueryable();
        if (userId.HasValue) query = query.Where(order => order.UserId == userId.Value);
        if (!string.IsNullOrWhiteSpace(status)) query = query.Where(order => order.Status == status.Trim());

        // Keep the legacy list endpoint compatibility-safe without allowing an unbounded export.
        var orders = await query.OrderByDescending(order => order.CreatedAt).ThenByDescending(order => order.Id)
            .Take(100).ToListAsync(cancellationToken);
        var planCodes = orders.Select(order => order.PlanCode).Distinct().ToList();
        var planNames = await _context.AiPricingPlans.AsNoTracking()
            .Where(plan => planCodes.Contains(plan.Code))
            .ToDictionaryAsync(plan => plan.Code, plan => plan.Name, cancellationToken);
        return orders.Select(order => ToOrderDto(order, planNames.GetValueOrDefault(order.PlanCode, order.PlanCode))).ToList();
    }

    public async Task<PaymentOrderPageDto> SearchOrdersAsync(Guid? userId, BillingOrderQuery request, CancellationToken cancellationToken = default)
    {
        var page = Math.Max(1, request.Page);
        var pageSize = Math.Clamp(request.PageSize, 1, 100);
        var query = ApplyOrderFilters(_context.PaymentOrders.AsNoTracking(), userId, request)
            .Include(order => order.User)
            .Include(order => order.Transactions);

        var totalCount = await query.CountAsync(cancellationToken);
        var aggregateQuery = ApplyOrderFilters(_context.PaymentOrders.AsNoTracking(), userId, request);
        var failedWebhookOrders = _context.PaymentWebhookEvents.AsNoTracking()
            .Where(x => x.PaymentOrderId.HasValue && x.Status == "Failed")
            .Select(x => x.PaymentOrderId!.Value);
        var now = DateTime.UtcNow;
        var summary = new PaymentOrderAggregateDto
        {
            TotalCount = totalCount,
            RevenueVnd = await aggregateQuery.Where(order => order.Status == "Paid").SumAsync(order => (decimal?)order.AmountVnd, cancellationToken) ?? 0,
            SuccessfulPayments = await aggregateQuery.CountAsync(order => order.Status == "Paid", cancellationToken),
            PendingPayments = await aggregateQuery.CountAsync(order => order.Status == "Pending" && (order.ExpiresAt == null || order.ExpiresAt > now), cancellationToken),
            FailedPayments = await aggregateQuery.CountAsync(order => order.Status == "Failed" || order.Status == "Rejected", cancellationToken),
            NeedsAttention = await aggregateQuery.CountAsync(order =>
                order.Status == "Failed" || order.Status == "Expired" ||
                (order.Status == "Pending" && (order.ExpiresAt <= now || failedWebhookOrders.Contains(order.Id))) ||
                (order.Status == "Paid" && order.PaidAt >= now.AddMonths(-1) &&
                 (failedWebhookOrders.Contains(order.Id) ||
                  !_context.AiSubscriptions.Any(subscription => subscription.UserId == order.UserId &&
                      subscription.Status == "Active" && subscription.PlanCode == order.PlanCode))), cancellationToken)
        };
        var orders = await query.OrderByDescending(order => order.CreatedAt).ThenByDescending(order => order.Id)
            .Skip((page - 1) * pageSize).Take(pageSize).ToListAsync(cancellationToken);
        var planCodes = orders.Select(order => order.PlanCode).Distinct().ToList();
        var planNames = await _context.AiPricingPlans.AsNoTracking().Where(plan => planCodes.Contains(plan.Code))
            .ToDictionaryAsync(plan => plan.Code, plan => plan.Name, cancellationToken);
        var userIds = orders.Select(order => order.UserId).Distinct().ToList();
        var subscriptions = await _context.AiSubscriptions.AsNoTracking().Where(x => userIds.Contains(x.UserId))
            .ToDictionaryAsync(x => x.UserId, cancellationToken);
        var failedWebhookOrderIds = await _context.PaymentWebhookEvents.AsNoTracking()
            .Where(x => x.PaymentOrderId.HasValue && x.Status == "Failed")
            .Select(x => x.PaymentOrderId!.Value).Distinct().ToListAsync(cancellationToken);
        var failedWebhookSet = failedWebhookOrderIds.ToHashSet();
        var items = orders.Select(order =>
        {
            var dto = ToOrderDto(order, planNames.GetValueOrDefault(order.PlanCode, order.PlanCode));
            var subscription = subscriptions.GetValueOrDefault(order.UserId);
            dto.HasFulfillmentMismatch = failedWebhookSet.Contains(order.Id) ||
                (order.Status == "Paid" && order.PaidAt >= now.AddMonths(-1) &&
                 (subscription == null || subscription.Status != "Active" || subscription.PlanCode != order.PlanCode));
            return dto;
        }).ToList();
        return new PaymentOrderPageDto
        {
            Items = items,
            Page = page, PageSize = pageSize, TotalCount = totalCount, Summary = summary
        };
    }

    private IQueryable<PaymentOrder> ApplyOrderFilters(IQueryable<PaymentOrder> query, Guid? userId, BillingOrderQuery request)
    {
        if (userId.HasValue) query = query.Where(order => order.UserId == userId.Value);
        var status = request.Status?.Trim();
        if (!string.IsNullOrWhiteSpace(status) && !string.Equals(status, "all", StringComparison.OrdinalIgnoreCase))
        {
            if (string.Equals(status, "Failed", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(status, "FailedOrRejected", StringComparison.OrdinalIgnoreCase))
                query = query.Where(order => order.Status == "Failed" || order.Status == "Rejected");
            else if (string.Equals(status, "Pending", StringComparison.OrdinalIgnoreCase))
            {
                var now = DateTime.UtcNow;
                query = query.Where(order => order.Status == "Pending" && (order.ExpiresAt == null || order.ExpiresAt > now));
            }
            else if (string.Equals(status, "Expired", StringComparison.OrdinalIgnoreCase))
            {
                var now = DateTime.UtcNow;
                query = query.Where(order => order.Status == "Expired" || (order.Status == "Pending" && order.ExpiresAt <= now));
            }
            else if (string.Equals(status, "Attention", StringComparison.OrdinalIgnoreCase))
            {
                var failedWebhookOrders = _context.PaymentWebhookEvents.AsNoTracking()
                    .Where(x => x.PaymentOrderId.HasValue && x.Status == "Failed")
                    .Select(x => x.PaymentOrderId!.Value);
                var now = DateTime.UtcNow;
                query = query.Where(order =>
                    order.Status == "Failed" || order.Status == "Expired" ||
                    (order.Status == "Pending" && (order.ExpiresAt <= now || failedWebhookOrders.Contains(order.Id))) ||
                    (order.Status == "Paid" && order.PaidAt >= now.AddMonths(-1) &&
                     (failedWebhookOrders.Contains(order.Id) ||
                      !_context.AiSubscriptions.Any(subscription => subscription.UserId == order.UserId &&
                          subscription.Status == "Active" && subscription.PlanCode == order.PlanCode))));
            }
            else query = query.Where(order => order.Status == status);
        }
        if (!string.IsNullOrWhiteSpace(request.PlanCode)) query = query.Where(order => order.PlanCode == request.PlanCode.Trim().ToLowerInvariant());
        if (!string.IsNullOrWhiteSpace(request.Provider)) query = query.Where(order => order.Provider == request.Provider.Trim());
        if (request.From.HasValue) query = query.Where(order => order.CreatedAt >= request.From.Value);
        if (request.To.HasValue) query = query.Where(order => order.CreatedAt < request.To.Value);
        var search = request.Search?.Trim();
        if (!string.IsNullOrWhiteSpace(search))
        {
            query = query.Where(order =>
                order.User.Email.Contains(search) || order.User.FullName.Contains(search) ||
                order.TransferCode.Contains(search) || order.Provider.Contains(search) ||
                order.Id.ToString().Contains(search) ||
                order.Transactions.Any(transaction => transaction.ProviderTransactionId.Contains(search) ||
                    (transaction.ProviderReference != null && transaction.ProviderReference.Contains(search))));
        }
        return query;
    }

    public async Task<PaymentOrderDetailsDto> GetOrderDetailsAsync(Guid orderId, Guid? userId, bool isAdmin, CancellationToken cancellationToken = default)
    {
        var order = await _context.PaymentOrders.AsNoTracking().Include(x => x.User).Include(x => x.Transactions)
            .SingleOrDefaultAsync(x => x.Id == orderId && (isAdmin || (userId.HasValue && x.UserId == userId.Value)), cancellationToken)
            ?? throw new KeyNotFoundException("Không tìm thấy đơn thanh toán.");
        var timeline = new List<PaymentTimelineEventDto>
        {
            new() { Type = "Order", Status = "Created", OccurredAt = AsUtc(order.CreatedAt), Reference = order.TransferCode }
        };
        if (order.PaidAt.HasValue) timeline.Add(new PaymentTimelineEventDto { Type = "Payment", Status = order.Status, OccurredAt = AsUtc(order.PaidAt), Reference = order.Transactions.OrderByDescending(x => x.CreatedAt).FirstOrDefault()?.ProviderReference });
        var events = await _context.PaymentWebhookEvents.AsNoTracking().Where(x => x.PaymentOrderId == order.Id)
            .OrderBy(x => x.ReceivedAt).Take(50).ToListAsync(cancellationToken);
        timeline.AddRange(events.Select(x => new PaymentTimelineEventDto { Type = "Webhook", Status = x.Status, OccurredAt = AsUtc(x.ProcessedAt ?? x.ReceivedAt), Reference = x.ProviderEventId, Note = x.FailureReason }));
        if (order.Status == "Rejected") timeline.Add(new PaymentTimelineEventDto { Type = "Admin", Status = "Rejected", OccurredAt = AsUtc(order.PaidAt), Note = order.AdminNote });

        var ledger = await _context.AiCreditAdjustments.AsNoTracking().Where(x => x.UserId == order.UserId)
            .OrderByDescending(x => x.CreatedAt).Take(25)
            .Select(x => new PaymentCreditLedgerEntryDto { Source = "AiCreditAdjustment", Amount = x.Amount, OccurredAt = x.CreatedAt, Description = x.Reason }).ToListAsync(cancellationToken);
        var usageLedger = await _context.AiUsageLedgerEntries.AsNoTracking().Where(x => x.UserId == order.UserId)
            .OrderByDescending(x => x.OccurredAt).Take(25)
            .Select(x => new PaymentCreditLedgerEntryDto { Source = "AiUsageLedger", Amount = -x.CreditsConsumed, OccurredAt = x.OccurredAt, Description = x.ActionType }).ToListAsync(cancellationToken);
        ledger.AddRange(usageLedger);
        var tokenUsage = await _context.AITokenUsages.AsNoTracking().Where(x => x.UserId == order.UserId)
            .OrderByDescending(x => x.CreatedAt).Take(25)
            .Select(x => new { x.FeatureCode, x.TokensUsed, x.CreatedAt }).ToListAsync(cancellationToken);
        ledger.AddRange(tokenUsage.Select(x => new PaymentCreditLedgerEntryDto
        {
            Source = "AITokenUsageTelemetry", Amount = -(int)Math.Ceiling(x.TokensUsed / 1000d), OccurredAt = x.CreatedAt,
            Description = $"{x.FeatureCode} · {x.TokensUsed:N0} tokens; đối chiếu telemetry, không cộng thêm vào ledger"
        }));
        ledger = ledger.OrderByDescending(x => x.OccurredAt).Take(50).ToList();
        foreach (var entry in ledger) entry.OccurredAt = AsUtc(entry.OccurredAt);
        return new PaymentOrderDetailsDto
        {
            Order = ToOrderDto(order, order.PlanNameSnapshot, _paymentProvider), Timeline = timeline.OrderBy(x => x.OccurredAt).ToList(),
            CreditLedger = ledger, ObservabilityAvailable = events.Count > 0 || ledger.Count > 0
        };
    }

    public async Task<PaymentReceiptDto> GetReceiptAsync(Guid orderId, Guid? userId, bool isAdmin, CancellationToken cancellationToken = default)
    {
        var order = await _context.PaymentOrders.AsNoTracking().Include(x => x.User).Include(x => x.Transactions)
            .SingleOrDefaultAsync(x => x.Id == orderId && x.Status == "Paid" && (isAdmin || (userId.HasValue && x.UserId == userId.Value)), cancellationToken)
            ?? throw new KeyNotFoundException("Chỉ có thể xem receipt cho giao dịch đã thanh toán.");
        var transaction = order.Transactions.OrderByDescending(x => x.CreatedAt).FirstOrDefault();
        return new PaymentReceiptDto
        {
            ReceiptNumber = BuildReceiptNumber(order.Id), Order = ToOrderDto(order, order.PlanNameSnapshot, _paymentProvider),
            CustomerName = string.IsNullOrWhiteSpace(order.User.FullName) ? order.User.Email : order.User.FullName,
            CustomerEmail = order.User.Email, IncludedAiCredits = transaction?.IncludedAiCredits ?? order.IncludedAiCreditsSnapshot,
            SubscriptionPeriodStart = AsUtc(transaction?.SubscriptionPeriodStart),
            SubscriptionPeriodEnd = AsUtc(transaction?.SubscriptionPeriodEnd), IsTaxInvoice = false
        };
    }

    public async Task<PaymentEmailDeliveryDto> ResendReceiptAsync(Guid orderId, Guid? userId, bool isAdmin, CancellationToken cancellationToken = default)
    {
        var receipt = await GetReceiptAsync(orderId, userId, isAdmin, cancellationToken);
        if (_emailService == null) throw new InvalidOperationException("Email delivery chưa được cấu hình.");
        var previousAttempt = await _context.PaymentEmailDeliveries.AsNoTracking()
            .Where(x => x.PaymentOrderId == orderId && x.Kind == "CustomerPaymentReceipt")
            .MaxAsync(x => (int?)x.Attempt, cancellationToken) ?? 0;
        var delivery = new PaymentEmailDelivery
        {
            Id = Guid.NewGuid(), PaymentOrderId = orderId, UserId = receipt.Order.UserId,
            RecipientEmail = receipt.CustomerEmail, Kind = "CustomerPaymentReceipt", IsAutomatic = false,
            Attempt = previousAttempt + 1, Status = "Requested", RequestedAt = DateTime.UtcNow
        };
        _context.PaymentEmailDeliveries.Add(delivery);
        await _context.SaveChangesAsync(cancellationToken);
        try
        {
            delivery.ProviderMessageId = await _emailService.SendPaymentReceiptEmailAsync(receipt.CustomerEmail, receipt.CustomerName, receipt.ReceiptNumber, receipt.Order.PlanName, receipt.Order.AmountVnd, receipt.Order.Currency, receipt.Order.PaidAt ?? DateTime.UtcNow);
            delivery.Status = "Sent"; delivery.SentAt = DateTime.UtcNow;
        }
        catch (Exception ex)
        {
            delivery.Status = "Failed"; delivery.FailedAt = DateTime.UtcNow; delivery.FailureReason = ex.Message[..Math.Min(500, ex.Message.Length)];
        }
        await _context.SaveChangesAsync(cancellationToken);
        return ToDeliveryDto(delivery);
    }

    public async Task<PaymentOrderDto> CreateOrderAsync(Guid userId, string planCode, CancellationToken cancellationToken = default)
    {
        var normalizedCode = NormalizePlanCode(planCode);
        var plan = await _context.AiPricingPlans.SingleOrDefaultAsync(
            item => item.Code == normalizedCode && item.IsPublished,
            cancellationToken) ?? throw new ArgumentException("Gói dịch vụ không tồn tại hoặc chưa được công khai.");
        if (plan.MonthlyPriceVnd is null or <= 0 || normalizedCode == "enterprise")
            throw new ArgumentException("Gói này không hỗ trợ thanh toán thủ công.");

        try
        {
            return await _context.Database.CreateExecutionStrategy().ExecuteAsync(async () =>
            {
                if (!_context.Database.IsRelational())
                    return await CreateOrderCoreAsync(userId, normalizedCode, plan, cancellationToken);

                await using var transaction = await _context.Database.BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken);
                var result = await CreateOrderCoreAsync(userId, normalizedCode, plan, cancellationToken);
                await transaction.CommitAsync(cancellationToken);
                return result;
            });
        }
        catch
        {
            _context.ChangeTracker.Clear();
            throw;
        }
    }

    private async Task<PaymentOrderDto> CreateOrderCoreAsync(
        Guid userId, string normalizedCode, AiPricingPlan plan, CancellationToken cancellationToken)
    {
        var now = DateTime.UtcNow;
        var expiredOrders = await _context.PaymentOrders
            .Where(order => order.UserId == userId && order.PlanCode == normalizedCode && order.Status == "Pending" && order.ExpiresAt != null && order.ExpiresAt <= now)
            .ToListAsync(cancellationToken);
        foreach (var expiredOrder in expiredOrders) expiredOrder.Status = "Expired";

        var existing = await _context.PaymentOrders.Include(order => order.User)
            .FirstOrDefaultAsync(order => order.UserId == userId && order.PlanCode == normalizedCode && order.Status == "Pending" &&
                (order.ExpiresAt == null || order.ExpiresAt > now), cancellationToken);
        if (existing != null)
        {
            if (expiredOrders.Count > 0) await _context.SaveChangesAsync(cancellationToken);
            return ToOrderDto(existing, plan.Name, _paymentProvider);
        }

        var user = await _context.Users.SingleOrDefaultAsync(item => item.Id == userId && !item.IsDeleted, cancellationToken)
            ?? throw new ArgumentException("Người dùng không tồn tại.");
        var order = new PaymentOrder
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            User = user,
            PlanCode = normalizedCode,
            PlanNameSnapshot = plan.Name,
            IncludedAiCreditsSnapshot = plan.IncludedAiCredits,
            AmountVnd = plan.MonthlyPriceVnd!.Value,
            Currency = "VND",
            Provider = _paymentProvider?.IsConfigured == true ? _paymentProvider.Code : "manual_bank_transfer",
            Status = "Pending",
            TransferCode = await CreateTransferCodeAsync(cancellationToken),
            CreatedAt = now,
            ExpiresAt = now.AddMinutes(30)
        };
        _context.PaymentOrders.Add(order);
        await _context.SaveChangesAsync(cancellationToken);
        return ToOrderDto(order, plan.Name, _paymentProvider);
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
        if (!_context.Database.IsRelational())
            return await ApproveOrderCoreAsync(orderId, adminUserId, note, false, cancellationToken);

        return await _context.Database.CreateExecutionStrategy()
            .ExecuteAsync(() => ApproveOrderCoreAsync(orderId, adminUserId, note, true, cancellationToken));
    }

    private async Task<PaymentOrderDto> ApproveOrderCoreAsync(
        Guid orderId, Guid adminUserId, string? note, bool useTransaction, CancellationToken cancellationToken)
    {
        if (useTransaction) _context.ChangeTracker.Clear();
        await using IDbContextTransaction? transaction = useTransaction
            ? await _context.Database.BeginTransactionAsync(cancellationToken)
            : null;
        var order = await _context.PaymentOrders.Include(item => item.User)
            .SingleOrDefaultAsync(item => item.Id == orderId, cancellationToken)
            ?? throw new ArgumentException("Đơn thanh toán không tồn tại.");
        var plan = await _context.AiPricingPlans.SingleOrDefaultAsync(item => item.Code == order.PlanCode, cancellationToken)
            ?? throw new InvalidOperationException("Gói của đơn thanh toán không còn tồn tại.");

        if (order.Status == "Paid") return ToOrderDto(order, plan.Name);
        if (order.Status != "Pending") throw new InvalidOperationException("Chỉ có thể duyệt đơn đang chờ.");
        if (order.ExpiresAt <= DateTime.UtcNow) throw new InvalidOperationException("Đơn thanh toán đã hết hạn.");

        var now = DateTime.UtcNow;
        order.Status = "Paid";
        order.PaidAt = now;
        order.ApprovedByUserId = adminUserId;
        order.AdminNote = string.IsNullOrWhiteSpace(note) ? null : note.Trim();
        var transactionRecord = new PaymentTransaction
        {
            Id = Guid.NewGuid(), PaymentOrderId = order.Id, Provider = "manual_bank_transfer",
            ProviderTransactionId = $"manual:{order.Id:N}", Amount = order.AmountVnd, Currency = order.Currency,
            Status = "Paid", PaidAt = now, ProviderReference = order.TransferCode, CreatedAt = now
        };
        _context.PaymentTransactions.Add(transactionRecord);
        var subscription = await GetOrCreateSubscriptionAsync(order.UserId, now, cancellationToken);
        subscription.PlanCode = order.PlanCode;
        subscription.Status = "Active";
        subscription.CurrentPeriodStart = now;
        subscription.CurrentPeriodEnd = now.AddMonths(1);
        subscription.ActivatedAt = now;
        subscription.CancelledAt = null;
        subscription.AutoRenew = false;
        subscription.UpdatedAt = now;
        transactionRecord.IncludedAiCredits = order.IncludedAiCreditsSnapshot;
        transactionRecord.SubscriptionPeriodStart = subscription.CurrentPeriodStart;
        transactionRecord.SubscriptionPeriodEnd = subscription.CurrentPeriodEnd;
        AddAudit(adminUserId, "PAYMENT_ORDER_APPROVE", order.UserId, new { orderId, order.PlanCode, order.AmountVnd, note });
        await _context.SaveChangesAsync(cancellationToken);
        if (transaction != null) await transaction.CommitAsync(cancellationToken);
        await SendPaymentSideEffectsAsync(order.Id, cancellationToken);
        return ToOrderDto(order, plan.Name, _paymentProvider);
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

    public async Task<PaymentOrderDto?> ProcessProviderPaymentAsync(string provider, PaymentWebhookVerificationResult webhook, string rawPayload, CancellationToken cancellationToken = default)
    {
        if (!webhook.IsValid || string.IsNullOrWhiteSpace(webhook.ProviderEventId))
            throw new ArgumentException("Webhook không hợp lệ.");
        if (!_context.Database.IsRelational())
            return await ProcessProviderPaymentCoreAsync(provider, webhook, rawPayload, false, cancellationToken);

        try
        {
            return await _context.Database.CreateExecutionStrategy()
                .ExecuteAsync(() => ProcessProviderPaymentCoreAsync(provider, webhook, rawPayload, true, cancellationToken));
        }
        catch
        {
            // A rolled-back transaction does not reset EF's tracked entity states.
            // Clear them before allowing a caller to safely retry on this context.
            _context.ChangeTracker.Clear();
            throw;
        }
    }

    private async Task<PaymentOrderDto?> ProcessProviderPaymentCoreAsync(
        string provider, PaymentWebhookVerificationResult webhook, string rawPayload, bool useTransaction, CancellationToken cancellationToken)
    {
        if (useTransaction) _context.ChangeTracker.Clear();
        await using IDbContextTransaction? transaction = useTransaction
            ? await _context.Database.BeginTransactionAsync(cancellationToken)
            : null;
        var existingEvent = await _context.PaymentWebhookEvents.SingleOrDefaultAsync(x => x.Provider == provider && x.ProviderEventId == webhook.ProviderEventId, cancellationToken);
        if (existingEvent != null)
            return null;
        var webhookEvent = new PaymentWebhookEvent
        {
            Id = Guid.NewGuid(), Provider = provider, ProviderEventId = webhook.ProviderEventId,
            EventType = webhook.EventType, RawPayload = rawPayload, ReceivedAt = DateTime.UtcNow,
            Status = "Received"
        };
        _context.PaymentWebhookEvents.Add(webhookEvent);
        try
        {
            await _context.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException)
        {
            // A concurrent delivery inserted the same provider event first.
            return null;
        }
        if (!string.Equals(webhook.TransactionType, "in", StringComparison.OrdinalIgnoreCase) || webhook.Amount <= 0)
        {
            webhookEvent.Status = "Ignored";
            webhookEvent.FailureReason = "Unsupported transaction type or non-positive amount.";
            await _context.SaveChangesAsync(cancellationToken);
            if (transaction != null) await transaction.CommitAsync(cancellationToken);
            return null;
        }
        var order = await _context.PaymentOrders.Include(x => x.User)
            .SingleOrDefaultAsync(x => x.Provider == provider && x.Status == "Pending" && x.TransferCode != "" && webhook.TransferContent.Contains(x.TransferCode), cancellationToken);
        if (order == null)
        {
            webhookEvent.Status = "Failed";
            webhookEvent.FailureReason = "No pending order matched the provider transfer content.";
            await _context.SaveChangesAsync(cancellationToken);
            if (transaction != null) await transaction.CommitAsync(cancellationToken);
            return null;
        }
        webhookEvent.PaymentOrderId = order.Id;
        if (order.ExpiresAt <= DateTime.UtcNow || order.AmountVnd != webhook.Amount)
        {
            webhookEvent.Status = "Failed";
            webhookEvent.FailureReason = order.ExpiresAt <= DateTime.UtcNow ? "Order expired." : "Webhook amount does not match the order.";
            await _context.SaveChangesAsync(cancellationToken);
            if (transaction != null) await transaction.CommitAsync(cancellationToken);
            return null;
        }
        var plan = await _context.AiPricingPlans.SingleOrDefaultAsync(x => x.Code == order.PlanCode, cancellationToken)
            ?? throw new InvalidOperationException("Gói của đơn thanh toán không còn tồn tại.");
        var now = webhook.TransactionAt?.UtcDateTime ?? DateTime.UtcNow;
        order.Status = "Paid";
        order.PaidAt = now;
        var transactionRecord = new PaymentTransaction
        {
            Id = Guid.NewGuid(), PaymentOrderId = order.Id, Provider = provider,
            ProviderTransactionId = webhook.ProviderEventId, Amount = webhook.Amount, Currency = order.Currency,
            Status = "Paid", PaidAt = now, ProviderReference = webhook.ProviderReference, CreatedAt = DateTime.UtcNow
        };
        _context.PaymentTransactions.Add(transactionRecord);
        var subscription = await GetOrCreateSubscriptionAsync(order.UserId, now, cancellationToken);
        subscription.PlanCode = order.PlanCode; subscription.Status = "Active";
        subscription.CurrentPeriodStart = now; subscription.CurrentPeriodEnd = now.AddMonths(1);
        subscription.ActivatedAt = now; subscription.CancelledAt = null; subscription.AutoRenew = false; subscription.UpdatedAt = now;
        transactionRecord.IncludedAiCredits = order.IncludedAiCreditsSnapshot;
        transactionRecord.SubscriptionPeriodStart = subscription.CurrentPeriodStart;
        transactionRecord.SubscriptionPeriodEnd = subscription.CurrentPeriodEnd;
        webhookEvent.Status = "Processed"; webhookEvent.ProcessedAt = DateTime.UtcNow;
        await _context.SaveChangesAsync(cancellationToken);
        if (transaction != null) await transaction.CommitAsync(cancellationToken);
        await SendPaymentSideEffectsAsync(order.Id, cancellationToken);
        return ToOrderDto(order, plan.Name, _paymentProvider);
    }

    private async Task SendPaymentSideEffectsAsync(Guid orderId, CancellationToken cancellationToken)
    {
        var order = await _context.PaymentOrders.AsNoTracking().Include(x => x.User).Include(x => x.Transactions)
            .SingleOrDefaultAsync(x => x.Id == orderId && x.Status == "Paid", cancellationToken);
        if (order == null) return;

        if (_notificationService != null)
        {
            try
            {
                await _notificationService.SendNotificationOnceAsync(
                    order.UserId, "Thanh toán thành công", $"Đơn {BuildReceiptNumber(order.Id)} đã được ghi nhận.",
                    "BILLING_PAYMENT_SUCCEEDED", $"billing:paid:customer:{order.Id:N}", $"/billing?order={order.Id}");
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"Billing customer notification failed: {ex.Message}");
            }

            try
            {
                var adminIds = await _context.UserRoles.AsNoTracking()
                    .Where(x => x.Role.Name == "Admin" || x.Role.Name == "SuperAdmin" || x.Role.Name == "System Admin")
                    .Select(x => x.UserId).Distinct().ToListAsync(cancellationToken);
                foreach (var adminId in adminIds)
                {
                    try
                    {
                        await _notificationService.SendNotificationOnceAsync(
                            adminId, "Có thanh toán thành công", $"Đơn {BuildReceiptNumber(order.Id)} của {order.User.Email} đã thanh toán.",
                            "BILLING_PAYMENT_SUCCEEDED_ADMIN", $"billing:paid:admin:{adminId:N}:{order.Id:N}", $"/admin/billing/payments?order={order.Id}");
                    }
                    catch (Exception ex)
                    {
                        Console.Error.WriteLine($"Billing admin notification failed: {ex.Message}");
                    }
                }
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"Billing admin notification lookup failed: {ex.Message}");
            }
        }

        if (_emailService == null) return;
        var receiptNumber = BuildReceiptNumber(order.Id);
        var existing = await _context.PaymentEmailDeliveries.SingleOrDefaultAsync(x =>
            x.PaymentOrderId == order.Id && x.Kind == "CustomerPaymentReceipt" && x.IsAutomatic, cancellationToken);
        if (existing == null)
        {
            var delivery = new PaymentEmailDelivery
            {
                Id = Guid.NewGuid(), PaymentOrderId = order.Id, UserId = order.UserId, RecipientEmail = order.User.Email,
                Kind = "CustomerPaymentReceipt", IsAutomatic = true, Attempt = 1, Status = "Requested", RequestedAt = DateTime.UtcNow
            };
            _context.PaymentEmailDeliveries.Add(delivery);
            try
            {
                await _context.SaveChangesAsync(cancellationToken);
                delivery.ProviderMessageId = await _emailService.SendPaymentReceiptEmailAsync(
                    order.User.Email, string.IsNullOrWhiteSpace(order.User.FullName) ? order.User.Email : order.User.FullName,
                    receiptNumber, order.PlanNameSnapshot, order.AmountVnd, order.Currency, order.PaidAt ?? DateTime.UtcNow);
                delivery.Status = "Sent"; delivery.SentAt = DateTime.UtcNow;
            }
            catch (DbUpdateException)
            {
                _context.Entry(delivery).State = EntityState.Detached;
            }
            catch (Exception ex)
            {
                delivery.Status = "Failed"; delivery.FailedAt = DateTime.UtcNow;
                delivery.FailureReason = ex.Message[..Math.Min(500, ex.Message.Length)];
            }
            await _context.SaveChangesAsync(cancellationToken);
        }

        var configuredRecipients = (_configuration?["Billing:AdminNotificationRecipients"] ?? string.Empty)
            .Split(new[] { ',', ';', '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Where(email => email.Contains('@', StringComparison.Ordinal)).Distinct(StringComparer.OrdinalIgnoreCase);
        foreach (var recipient in configuredRecipients)
        {
            var recipientKey = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(recipient)))[..16];
            var kind = $"AdminPaymentReceipt:{recipientKey}";
            if (await _context.PaymentEmailDeliveries.AnyAsync(x => x.PaymentOrderId == order.Id && x.Kind == kind && x.IsAutomatic, cancellationToken)) continue;
            var delivery = new PaymentEmailDelivery
            {
                Id = Guid.NewGuid(), PaymentOrderId = order.Id, UserId = order.UserId, RecipientEmail = recipient,
                Kind = kind, IsAutomatic = true, Attempt = 1, Status = "Requested", RequestedAt = DateTime.UtcNow
            };
            _context.PaymentEmailDeliveries.Add(delivery);
            try
            {
                await _context.SaveChangesAsync(cancellationToken);
                delivery.ProviderMessageId = await _emailService.SendPaymentReceiptEmailAsync(
                    recipient, "SprintA billing admin", receiptNumber, order.PlanNameSnapshot, order.AmountVnd, order.Currency, order.PaidAt ?? DateTime.UtcNow);
                delivery.Status = "Sent"; delivery.SentAt = DateTime.UtcNow;
            }
            catch (DbUpdateException)
            {
                _context.Entry(delivery).State = EntityState.Detached;
                continue;
            }
            catch (Exception ex)
            {
                delivery.Status = "Failed"; delivery.FailedAt = DateTime.UtcNow;
                delivery.FailureReason = ex.Message[..Math.Min(500, ex.Message.Length)];
            }
            await _context.SaveChangesAsync(cancellationToken);
        }
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
            var code = $"SEVQR SPA{Guid.NewGuid():N}"[..18].ToUpperInvariant();
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
        return ToOrderDto(order, planName, _paymentProvider);
    }

    private static PaymentOrderDto ToOrderDto(PaymentOrder order, string planName, IPaymentProvider? provider = null) => new()
    {
        Id = order.Id,
        UserId = order.UserId,
        UserName = string.IsNullOrWhiteSpace(order.User?.FullName) ? order.User?.Email.Split('@')[0] ?? string.Empty : order.User.FullName,
        Email = order.User?.Email ?? string.Empty,
        PlanCode = order.PlanCode,
        PlanName = string.IsNullOrWhiteSpace(order.PlanNameSnapshot) ? planName : order.PlanNameSnapshot,
        AmountVnd = order.AmountVnd,
        Status = order.Status == "Pending" && order.ExpiresAt.HasValue && order.ExpiresAt.Value <= DateTime.UtcNow
            ? "Expired"
            : order.Status,
        TransferCode = order.TransferCode,
        Currency = order.Currency,
        Provider = order.Provider,
        ExpiresAt = AsUtc(order.ExpiresAt),
        PaymentInstructions = provider?.Code == order.Provider ? provider.BuildInstructions(order) : null,
        CreatedAt = AsUtc(order.CreatedAt),
        PaidAt = AsUtc(order.PaidAt),
        ApprovedByUserId = order.ApprovedByUserId,
        AdminNote = order.AdminNote,
        ProviderTransactionId = order.Transactions.OrderByDescending(x => x.CreatedAt).FirstOrDefault()?.ProviderTransactionId,
        ProviderReference = order.Transactions.OrderByDescending(x => x.CreatedAt).FirstOrDefault()?.ProviderReference,
        TransactionStatus = order.Transactions.OrderByDescending(x => x.CreatedAt).FirstOrDefault()?.Status,
        TransactionCreatedAt = AsUtc(order.Transactions.OrderByDescending(x => x.CreatedAt).FirstOrDefault()?.CreatedAt),
        TransactionPeriodStart = AsUtc(order.Transactions.OrderByDescending(x => x.CreatedAt).FirstOrDefault()?.SubscriptionPeriodStart),
        TransactionPeriodEnd = AsUtc(order.Transactions.OrderByDescending(x => x.CreatedAt).FirstOrDefault()?.SubscriptionPeriodEnd)
    };

    private static DateTime AsUtc(DateTime value) => value.Kind == DateTimeKind.Utc ? value : DateTime.SpecifyKind(value, DateTimeKind.Utc);
    private static DateTime? AsUtc(DateTime? value) => value.HasValue ? AsUtc(value.Value) : null;

    private static string BuildReceiptNumber(Guid orderId) => $"SPRINTA-{orderId:N}"[..21].ToUpperInvariant();

    private static PaymentEmailDeliveryDto ToDeliveryDto(PaymentEmailDelivery delivery) => new()
    {
        Id = delivery.Id, RecipientEmail = delivery.RecipientEmail, Kind = delivery.Kind,
        Attempt = delivery.Attempt, Status = delivery.Status, RequestedAt = delivery.RequestedAt,
        SentAt = delivery.SentAt, FailedAt = delivery.FailedAt, FailureReason = delivery.FailureReason
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
