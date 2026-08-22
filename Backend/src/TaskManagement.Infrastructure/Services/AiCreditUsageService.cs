using TaskManagement.Application.Common;
using Microsoft.EntityFrameworkCore;
using TaskManagement.Application.DTOs.AI;
using TaskManagement.Application.Interfaces;
using TaskManagement.Infrastructure.Data;
using Microsoft.EntityFrameworkCore.Storage;
using TaskManagement.Domain.Entities;
using Microsoft.Data.SqlClient;

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
            // AutoRenew is only a preference until a verified provider payment exists.
            // Never extend paid entitlement without a successful payment event.
            subscription.Status = "Expired";

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
            CurrentPeriodStart = DateTime.SpecifyKind(periodStart, DateTimeKind.Utc),
            CurrentPeriodEnd = DateTime.SpecifyKind(periodEnd, DateTimeKind.Utc),
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

    public async Task<Guid> ReserveAsync(Guid userId, int credits, string idempotencyKey, CancellationToken cancellationToken = default)
        => (await ReserveDetailedAsync(userId, credits, idempotencyKey, cancellationToken)).ReservationId;

    public async Task<AiCreditReservationResult> ReserveDetailedAsync(Guid userId, int credits, string idempotencyKey, CancellationToken cancellationToken = default)
    {
        if (credits <= 0) throw new ArgumentOutOfRangeException(nameof(credits));
        if (string.IsNullOrWhiteSpace(idempotencyKey)) throw new ArgumentException("Idempotency key is required.", nameof(idempotencyKey));
        for (var attempt = 1; ; attempt++)
        {
            try
            {
                return await ReserveOnceAsync(userId, credits, idempotencyKey, cancellationToken);
            }
            catch (Exception exception) when (attempt < 3 && IsDeadlock(exception))
            {
                foreach (var entry in _context.ChangeTracker.Entries<AiCreditReservation>().Where(entry => entry.State == EntityState.Added).ToList())
                    entry.State = EntityState.Detached;
                await Task.Delay(TimeSpan.FromMilliseconds(25 * attempt), cancellationToken);
            }
        }
    }

    private async Task<AiCreditReservationResult> ReserveOnceAsync(Guid userId, int credits, string idempotencyKey, CancellationToken cancellationToken)
    {
        await using IDbContextTransaction? transaction = _context.Database.IsRelational()
            ? await _context.Database.BeginTransactionAsync(System.Data.IsolationLevel.Serializable, cancellationToken) : null;
        if (_context.Database.IsSqlServer())
        {
            await _context.Users
                .FromSqlInterpolated($"SELECT * FROM [Users] WITH (UPDLOCK, HOLDLOCK) WHERE [Id] = {userId}")
                .AsNoTracking()
                .SingleOrDefaultAsync(cancellationToken);
        }
        var existing = await _context.AiCreditReservations.SingleOrDefaultAsync(x => x.IdempotencyKey == idempotencyKey, cancellationToken);
        var now = DateTime.UtcNow;
        if (existing != null)
        {
            if (existing.Status == "Finalized")
                return new(existing.Id, false, existing.Status);
            if (existing.Status == "Reserved" && existing.ExpiresAt > now)
                return new(existing.Id, false, existing.Status);

            existing.Credits = credits;
            existing.Status = "Reserved";
            existing.ExpiresAt = now.AddMinutes(10);
            existing.CompletedAt = null;
            existing.CreatedAt = now;
            await _context.SaveChangesAsync(cancellationToken);
            if (transaction != null) await transaction.CommitAsync(cancellationToken);
            return new(existing.Id, true, existing.Status);
        }
        var usage = await GetUsageAsync(userId, DateTime.MinValue, DateTime.MaxValue, cancellationToken);
        var reserved = await _context.AiCreditReservations
            .Where(x => x.UserId == userId && x.Status == "Reserved" && x.ExpiresAt > now)
            .SumAsync(x => (int?)x.Credits, cancellationToken) ?? 0;
        if (!usage.HasConfiguredEntitlement || usage.RemainingCredits - reserved < credits)
            throw new AiCreditsExhaustedException(usage.IncludedCredits, usage.UsedCredits + reserved, Math.Max(0, usage.RemainingCredits - reserved));
        var reservation = new AiCreditReservation
        {
            Id = Guid.NewGuid(), UserId = userId, Credits = credits, IdempotencyKey = idempotencyKey,
            Status = "Reserved", ExpiresAt = now.AddMinutes(10), CreatedAt = now
        };
        _context.AiCreditReservations.Add(reservation);
        await _context.SaveChangesAsync(cancellationToken);
        if (transaction != null) await transaction.CommitAsync(cancellationToken);
        return new(reservation.Id, true, reservation.Status);
    }

    private static bool IsDeadlock(Exception exception)
    {
        for (var current = exception; current != null; current = current.InnerException)
            if (current is SqlException { Number: 1205 }) return true;
        return false;
    }

    public Task FinalizeReservationAsync(Guid reservationId, CancellationToken cancellationToken = default)
        => CompleteReservationAsync(reservationId, "Finalized", cancellationToken);

    public Task ReleaseReservationAsync(Guid reservationId, CancellationToken cancellationToken = default)
        => CompleteReservationAsync(reservationId, "Released", cancellationToken);

    private async Task CompleteReservationAsync(Guid reservationId, string status, CancellationToken cancellationToken)
    {
        var reservation = await _context.AiCreditReservations.SingleOrDefaultAsync(x => x.Id == reservationId, cancellationToken);
        if (reservation == null || reservation.Status != "Reserved") return;
        reservation.Status = status;
        reservation.CompletedAt = DateTime.UtcNow;
        await _context.SaveChangesAsync(cancellationToken);
    }

    private static int EstimateCredits(long tokensUsed)
        => tokensUsed <= 0 ? 0 : (int)Math.Ceiling(tokensUsed / 1000.0);
}
