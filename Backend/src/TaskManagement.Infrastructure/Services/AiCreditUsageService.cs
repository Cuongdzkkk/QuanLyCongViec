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
        var plan = await _context.AiPricingPlans
            .AsNoTracking()
            .Where(item => item.Code == "free")
            .Select(item => new { item.Code, item.IncludedAiCredits })
            .SingleOrDefaultAsync(cancellationToken);

        var totalTokens = await _context.AITokenUsages
            .AsNoTracking()
            .Where(item => item.UserId == userId && item.CreatedAt >= from && item.CreatedAt <= to)
            .SumAsync(item => (long?)item.TokensUsed, cancellationToken) ?? 0;

        var ledgerCredits = await _context.AiUsageLedgerEntries
            .AsNoTracking()
            .Where(item => item.UserId == userId && item.OccurredAt >= from && item.OccurredAt <= to)
            .SumAsync(item => (int?)item.CreditsConsumed, cancellationToken) ?? 0;

        var tokenDerivedCredits = EstimateCredits(totalTokens);
        var usedCredits = Math.Max(ledgerCredits, tokenDerivedCredits);
        var usageSource = ledgerCredits > 0 && tokenDerivedCredits > 0
            ? "reconciled-ledger-and-token-usage"
            : ledgerCredits > 0
                ? "ai-usage-ledger"
                : "ai-token-usage";

        return new AiCreditUsageDto
        {
            PlanCode = plan?.Code ?? string.Empty,
            EntitlementSource = plan == null ? "not-configured" : "ai-pricing-plans:free",
            UsageSource = usageSource,
            IncludedCredits = plan?.IncludedAiCredits ?? 0,
            UsedCredits = usedCredits,
            HasConfiguredEntitlement = plan != null,
            TotalTokens = totalTokens
        };
    }

    public async Task EnsureWithinQuotaAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        var now = DateTime.UtcNow;
        var monthStart = new DateTime(now.Year, now.Month, 1, 0, 0, 0, DateTimeKind.Utc);
        var usage = await GetUsageAsync(userId, monthStart, now, cancellationToken);

        // Missing entitlement configuration is not treated as a zero-credit purchase decision.
        if (usage.HasConfiguredEntitlement && usage.IsQuotaExceeded)
        {
            throw new InvalidOperationException(
                $"Bạn đã sử dụng hết {usage.IncludedCredits:N0} AI credits trong tháng này.");
        }
    }

    private static int EstimateCredits(long tokensUsed)
        => tokensUsed <= 0 ? 0 : (int)Math.Ceiling(tokensUsed / 1000.0);
}
