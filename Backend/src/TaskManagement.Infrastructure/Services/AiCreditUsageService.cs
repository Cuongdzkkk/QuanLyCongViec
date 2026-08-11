using TaskManagement.Application.Common;
using Microsoft.EntityFrameworkCore;
using TaskManagement.Application.DTOs.AI;
using TaskManagement.Application.Interfaces;
using TaskManagement.Infrastructure.Data;

namespace TaskManagement.Infrastructure.Services;

public sealed class AiCreditUsageService : IAiCreditUsageService
{
    private readonly ApplicationDbContext _context;

    public AiCreditUsageService(ApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<AiCreditUsageDto> GetUsageAsync(
        Guid userId,
        DateTime from,
        DateTime to,
        CancellationToken cancellationToken = default)
    {
        var now = DateTime.UtcNow;
        var subscription = await _context.AiSubscriptions
            .SingleOrDefaultAsync(item => item.UserId == userId, cancellationToken);

        if (subscription is { Status: "Active" } &&
            !string.Equals(subscription.PlanCode, "free", StringComparison.OrdinalIgnoreCase) &&
            subscription.CurrentPeriodEnd <= now)
        {
            if (subscription.AutoRenew)
            {
                while (subscription.CurrentPeriodEnd <= now)
                {
                    subscription.CurrentPeriodStart = subscription.CurrentPeriodEnd;
                    subscription.CurrentPeriodEnd = subscription.CurrentPeriodEnd.AddMonths(1);
                }
            }
            else
            {
                subscription.Status = "Expired";
            }

            subscription.UpdatedAt = now;
            await _context.SaveChangesAsync(cancellationToken);
        }

        var usePaidSubscription = subscription is { Status: "Active" } &&
            !string.Equals(subscription.PlanCode, "free", StringComparison.OrdinalIgnoreCase) &&
            subscription.CurrentPeriodStart <= now &&
            subscription.CurrentPeriodEnd > now;

        var planCode = usePaidSubscription ? subscription!.PlanCode : "free";
        var periodStart = usePaidSubscription
            ? subscription!.CurrentPeriodStart
            : new DateTime(now.Year, now.Month, 1, 0, 0, 0, DateTimeKind.Utc);
        var periodEnd = usePaidSubscription ? subscription!.CurrentPeriodEnd : periodStart.AddMonths(1);

        var plan = await _context.AiPricingPlans
            .AsNoTracking()
            .Where(item => item.Code == planCode && (usePaidSubscription || item.IsPublished))
            .Select(item => new { item.Code, item.IncludedAiCredits })
            .SingleOrDefaultAsync(cancellationToken);

        var totalTokens = await _context.AITokenUsages
            .AsNoTracking()
            .Where(item => item.UserId == userId && item.CreatedAt >= periodStart && item.CreatedAt < periodEnd)
            .SumAsync(item => (long?)item.TokensUsed, cancellationToken) ?? 0;

        var ledgerCredits = await _context.AiUsageLedgerEntries
            .AsNoTracking()
            .Where(item => item.UserId == userId && item.OccurredAt >= periodStart && item.OccurredAt < periodEnd)
            .SumAsync(item => (int?)item.CreditsConsumed, cancellationToken) ?? 0;

        var adjustments = await _context.AiCreditAdjustments
            .AsNoTracking()
            .Where(item => item.UserId == userId &&
                           item.EffectivePeriodStart <= periodStart &&
                           item.EffectivePeriodEnd >= periodEnd)
            .GroupBy(item => item.AdjustmentType)
            .Select(group => new { Type = group.Key, Amount = group.Sum(item => item.Amount) })
            .ToListAsync(cancellationToken);

        var tokenDerivedCredits = EstimateCredits(totalTokens);
        var recordedCredits = Math.Max(ledgerCredits, tokenDerivedCredits);
        var resetCredits = adjustments
            .Where(item => item.Type == "UsageReset")
            .Sum(item => item.Amount);
        var adjustmentCredits = adjustments
            .Where(item => item.Type == "Credit")
            .Sum(item => item.Amount);
        var usedCredits = Math.Max(0, recordedCredits - resetCredits);
        var usageSource = ledgerCredits > 0 && tokenDerivedCredits > 0
            ? "reconciled-ledger-and-token-usage"
            : ledgerCredits > 0
                ? "ai-usage-ledger"
                : "ai-token-usage";

        return new AiCreditUsageDto
        {
            PlanCode = plan?.Code ?? planCode,
            EntitlementSource = plan == null
                ? "not-configured"
                : usePaidSubscription ? "ai-subscription" : "ai-pricing-plans:free",
            UsageSource = usageSource,
            IncludedCredits = plan?.IncludedAiCredits ?? 0,
            UsedCredits = usedCredits,
            AdjustmentCredits = adjustmentCredits,
            HasConfiguredEntitlement = plan != null,
            TotalTokens = totalTokens,
            CurrentPeriodStart = periodStart,
            CurrentPeriodEnd = periodEnd,
            SubscriptionStatus = subscription?.Status ?? "Active"
        };
    }

    public async Task EnsureWithinQuotaAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        var usage = await GetUsageAsync(userId, DateTime.MinValue, DateTime.MaxValue, cancellationToken);

        // Missing entitlement configuration is not treated as a zero-credit purchase decision.
        if (usage.HasConfiguredEntitlement && usage.IsQuotaExceeded)
        {
            throw new AiCreditsExhaustedException(
                usage.IncludedCredits,
                usage.UsedCredits,
                Math.Max(0, usage.IncludedCredits - usage.UsedCredits));
        }
    }

    private static int EstimateCredits(long tokensUsed)
        => tokensUsed <= 0 ? 0 : (int)Math.Ceiling(tokensUsed / 1000.0);
}
