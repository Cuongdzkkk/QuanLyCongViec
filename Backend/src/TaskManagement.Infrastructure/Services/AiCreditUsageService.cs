using TaskManagement.Application.Common;
using Microsoft.EntityFrameworkCore;
using TaskManagement.Application.DTOs.AI;
using TaskManagement.Application.Interfaces;
using TaskManagement.Domain.Entities;
using TaskManagement.Infrastructure.Data;
using System.Data;
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

        if (usePaidSubscription)
            await EnsureLegacyActivePeriodCutoverAsync(subscription!, cancellationToken);

        // Paid grants are independent of the current plan. A plan change therefore
        // never changes the wallet; expired and future grants are simply excluded.
        if (await _context.AiCreditBuckets.AnyAsync(x => x.UserId == userId, cancellationToken))
        {
            var buckets = await _context.AiCreditBuckets.AsNoTracking()
                .Where(x => x.UserId == userId && x.ValidFrom <= now && x.ExpiresAt > now)
                .OrderBy(x => x.ExpiresAt).ThenBy(x => x.CreatedAt).ThenBy(x => x.Id)
                .ToListAsync(cancellationToken);
            var subscriptionPlan = subscription?.PlanCode ?? "free";
            var total = buckets.Sum(x => Math.Max(0, x.RemainingCredits));
            var paidPlan = !string.Equals(subscriptionPlan, "free", StringComparison.OrdinalIgnoreCase);
            return new AiCreditUsageDto
            {
                PlanCode = subscriptionPlan,
                EntitlementSource = "ai-credit-buckets",
                UsageSource = "ai-credit-buckets",
                IncludedCredits = buckets.Sum(x => x.GrantedCredits),
                UsedCredits = buckets.Sum(x => x.GrantedCredits - x.RemainingCredits),
                TotalRemainingCredits = total,
                HasConfiguredEntitlement = paidPlan || total > 0,
                CurrentPeriodStart = subscription?.CurrentPeriodStart ?? MonthStart(now),
                CurrentPeriodEnd = subscription?.CurrentPeriodEnd ?? MonthStart(now).AddMonths(1),
                SubscriptionStatus = subscription?.Status ?? "Active"
            };
        }

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

    public async Task EnsureLegacyCutoverAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        var subscription = await _context.AiSubscriptions
            .SingleOrDefaultAsync(x => x.UserId == userId, cancellationToken);
        if (subscription is null || !string.Equals(subscription.Status, "Active", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(subscription.PlanCode, "free", StringComparison.OrdinalIgnoreCase) ||
            subscription.CurrentPeriodStart > DateTime.UtcNow || subscription.CurrentPeriodEnd <= DateTime.UtcNow)
            return;
        await EnsureLegacyActivePeriodCutoverAsync(subscription, cancellationToken);
    }

    public async Task<AiCreditReservationResult> ReserveAsync(Guid userId, int credits, string idempotencyKey, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(idempotencyKey)) throw new ArgumentException("AI credit idempotency key is required.");
        if (credits <= 0) return new AiCreditReservationResult(Guid.Empty, true, "NotApplicable", 0);
        if (!await _context.AiCreditBuckets.AnyAsync(x => x.UserId == userId, cancellationToken))
            return new AiCreditReservationResult(Guid.Empty, true, "NotApplicable", 0);

        AiCreditReservationResult? result = null;
        try
        {
            await ExecuteAtomicAsync(async () =>
            {
                var existing = await _context.AiCreditReservations.AsNoTracking()
                    .SingleOrDefaultAsync(x => x.UserId == userId && x.IdempotencyKey == idempotencyKey, cancellationToken);
                if (existing != null)
                {
                    result = new AiCreditReservationResult(existing.Id, false, existing.Status, existing.ReservedCredits);
                    return;
                }

                await ReclaimExpiredAsync(DateTime.UtcNow, cancellationToken);
                var now = DateTime.UtcNow;
                var buckets = await _context.AiCreditBuckets
                    .Where(x => x.UserId == userId && x.ValidFrom <= now && x.ExpiresAt > now && x.RemainingCredits > 0)
                    .OrderBy(x => x.ExpiresAt).ThenBy(x => x.CreatedAt).ThenBy(x => x.Id)
                    .ToListAsync(cancellationToken);
                var available = buckets.Sum(x => x.RemainingCredits);
                if (available < credits)
                {
                    result = new AiCreditReservationResult(Guid.Empty, false, "Exhausted", available);
                    return;
                }

                var reservation = new AiCreditReservation
                {
                    Id = Guid.NewGuid(), UserId = userId, IdempotencyKey = idempotencyKey,
                    RequestedCredits = credits, ReservedCredits = credits, Status = "Reserved",
                    CreatedAt = now, ExpiresAt = now.AddMinutes(15)
                };
                _context.AiCreditReservations.Add(reservation);
                var remaining = credits;
                foreach (var bucket in buckets)
                {
                    var allocation = Math.Min(bucket.RemainingCredits, remaining);
                    bucket.RemainingCredits -= allocation;
                    _context.AiCreditReservationAllocations.Add(new AiCreditReservationAllocation
                    {
                        Id = Guid.NewGuid(), ReservationId = reservation.Id, CreditBucketId = bucket.Id,
                        AllocatedCredits = allocation, CreatedAt = now
                    });
                    remaining -= allocation;
                    if (remaining == 0) break;
                }
                await _context.SaveChangesAsync(cancellationToken);
                result = new AiCreditReservationResult(reservation.Id, true, reservation.Status, credits);
            }, cancellationToken);
        }
        catch (DbUpdateException ex) when (IsUniqueViolation(ex))
        {
            _context.ChangeTracker.Clear();
            var existing = await _context.AiCreditReservations.AsNoTracking()
                .SingleAsync(x => x.UserId == userId && x.IdempotencyKey == idempotencyKey, cancellationToken);
            result = new AiCreditReservationResult(existing.Id, false, existing.Status, existing.ReservedCredits);
        }
        return result!;
    }

    public async Task FinalizeAsync(Guid reservationId, int actualCredits, CancellationToken cancellationToken = default)
    {
        if (reservationId == Guid.Empty) return;
        if (actualCredits < 0) throw new ArgumentOutOfRangeException(nameof(actualCredits));
        await ExecuteAtomicAsync(async () =>
        {
            var reservation = await _context.AiCreditReservations
                .Include(x => x.Allocations).ThenInclude(x => x.CreditBucket)
                .SingleOrDefaultAsync(x => x.Id == reservationId, cancellationToken)
                ?? throw new InvalidOperationException("AI credit reservation was not found.");
            if (reservation.Status == "Finalized") return;
            if (reservation.Status is "Released" or "Expired") throw new InvalidOperationException("AI credit reservation is no longer active.");

            var held = reservation.Allocations.Sum(x => x.AllocatedCredits);
            if (actualCredits > held)
            {
                var now = DateTime.UtcNow;
                var buckets = await _context.AiCreditBuckets
                    .Where(x => x.UserId == reservation.UserId && x.ValidFrom <= now && x.ExpiresAt > now && x.RemainingCredits > 0)
                    .OrderBy(x => x.ExpiresAt).ThenBy(x => x.CreatedAt).ThenBy(x => x.Id)
                    .ToListAsync(cancellationToken);
                var allocationsByBucket = reservation.Allocations.ToDictionary(x => x.CreditBucketId);
                var extra = actualCredits - held;
                foreach (var bucket in buckets)
                {
                    var allocation = Math.Min(bucket.RemainingCredits, extra);
                    bucket.RemainingCredits -= allocation;
                    if (allocationsByBucket.TryGetValue(bucket.Id, out var existingAllocation))
                    {
                        existingAllocation.AllocatedCredits += allocation;
                    }
                    else
                    {
                        reservation.Allocations.Add(new AiCreditReservationAllocation
                        {
                            Id = Guid.NewGuid(), ReservationId = reservation.Id, CreditBucketId = bucket.Id,
                            AllocatedCredits = allocation, CreatedAt = now
                        });
                    }
                    extra -= allocation;
                    if (extra == 0) break;
                }
                held = reservation.Allocations.Sum(x => x.AllocatedCredits);
                if (held < actualCredits) throw new AiCreditsExhaustedException(held, held, 0);
            }

            var consumeRemaining = actualCredits;
            foreach (var allocation in reservation.Allocations.OrderBy(x => x.CreditBucket.ExpiresAt).ThenBy(x => x.CreditBucket.CreatedAt).ThenBy(x => x.CreditBucket.Id))
            {
                var consumed = Math.Min(allocation.AllocatedCredits, consumeRemaining);
                var released = allocation.AllocatedCredits - consumed;
                allocation.CreditBucket.RemainingCredits += released;
                allocation.ConsumedCredits += consumed;
                allocation.AllocatedCredits = 0;
                consumeRemaining -= consumed;
            }
            reservation.FinalizedCredits = actualCredits;
            reservation.Status = "Finalized";
            reservation.FinalizedAt = DateTime.UtcNow;
            await _context.SaveChangesAsync(cancellationToken);
        }, cancellationToken);
    }

    public async Task ReleaseAsync(Guid reservationId, CancellationToken cancellationToken = default)
    {
        if (reservationId == Guid.Empty) return;
        await ExecuteAtomicAsync(async () =>
        {
            var reservation = await _context.AiCreditReservations
                .Include(x => x.Allocations).ThenInclude(x => x.CreditBucket)
                .SingleOrDefaultAsync(x => x.Id == reservationId, cancellationToken);
            if (reservation == null || reservation.Status is "Released" or "Finalized" or "Expired") return;
            foreach (var allocation in reservation.Allocations)
            {
                allocation.CreditBucket.RemainingCredits += allocation.AllocatedCredits;
                allocation.AllocatedCredits = 0;
            }
            reservation.Status = "Released";
            reservation.ReleasedAt = DateTime.UtcNow;
            await _context.SaveChangesAsync(cancellationToken);
        }, cancellationToken);
    }

    public async Task ExpireAsync(Guid reservationId, CancellationToken cancellationToken = default)
    {
        if (reservationId == Guid.Empty) return;
        await ExecuteAtomicAsync(async () =>
        {
            var reservation = await _context.AiCreditReservations
                .Include(x => x.Allocations).ThenInclude(x => x.CreditBucket)
                .SingleOrDefaultAsync(x => x.Id == reservationId, cancellationToken);
            if (reservation == null || reservation.Status is "Released" or "Finalized" or "Expired") return;
            foreach (var allocation in reservation.Allocations)
            {
                reservation.Allocations.First(x => x.Id == allocation.Id).CreditBucket.RemainingCredits += allocation.AllocatedCredits;
                allocation.AllocatedCredits = 0;
            }
            reservation.Status = "Expired";
            await _context.SaveChangesAsync(cancellationToken);
        }, cancellationToken);
    }

    public async Task ConsumeAsync(Guid userId, int credits, string? idempotencyKey = null, CancellationToken cancellationToken = default)
    {
        var key = idempotencyKey ?? $"compat:{userId:N}:{Guid.NewGuid():N}";
        var reservation = await ReserveAsync(userId, credits, key, cancellationToken);
        if (reservation.Acquired) await FinalizeAsync(reservation.ReservationId, credits, cancellationToken);
    }

    private async Task ReclaimExpiredAsync(DateTime now, CancellationToken cancellationToken)
    {
        var expired = await _context.AiCreditReservations
            .Include(x => x.Allocations).ThenInclude(x => x.CreditBucket)
            .Where(x => x.Status == "Reserved" && x.ExpiresAt <= now)
            .ToListAsync(cancellationToken);
        foreach (var reservation in expired)
        {
            foreach (var allocation in reservation.Allocations)
            {
                allocation.CreditBucket.RemainingCredits += allocation.AllocatedCredits;
                allocation.AllocatedCredits = 0;
            }
            reservation.Status = "Expired";
        }
        if (expired.Count > 0) await _context.SaveChangesAsync(cancellationToken);
    }

    private async Task EnsureLegacyActivePeriodCutoverAsync(AiSubscription subscription, CancellationToken cancellationToken)
    {
        if (await _context.AiCreditBuckets.AnyAsync(x => x.UserId == subscription.UserId, cancellationToken)) return;

        await ExecuteAtomicAsync(async () =>
        {
            if (await _context.AiCreditBuckets.AnyAsync(x => x.UserId == subscription.UserId, cancellationToken)) return;

            var planCredits = await _context.AiPricingPlans.AsNoTracking()
                .Where(x => x.Code == subscription.PlanCode)
                .Select(x => (int?)x.IncludedAiCredits)
                .SingleOrDefaultAsync(cancellationToken) ?? 0;
            var ledgerCredits = await _context.AiUsageLedgerEntries.AsNoTracking()
                .Where(x => x.UserId == subscription.UserId &&
                            x.OccurredAt >= subscription.CurrentPeriodStart && x.OccurredAt < subscription.CurrentPeriodEnd)
                .SumAsync(x => (int?)x.CreditsConsumed, cancellationToken) ?? 0;
            var tokens = await _context.AITokenUsages.AsNoTracking()
                .Where(x => x.UserId == subscription.UserId &&
                            x.CreatedAt >= subscription.CurrentPeriodStart && x.CreatedAt < subscription.CurrentPeriodEnd)
                .SumAsync(x => (long?)x.TokensUsed, cancellationToken) ?? 0;
            var adjustments = await _context.AiCreditAdjustments.AsNoTracking()
                .Where(x => x.UserId == subscription.UserId &&
                            x.EffectivePeriodStart <= subscription.CurrentPeriodStart &&
                            x.EffectivePeriodEnd >= subscription.CurrentPeriodEnd)
                .GroupBy(x => x.AdjustmentType)
                .Select(x => new { Type = x.Key, Amount = x.Sum(y => y.Amount) })
                .ToListAsync(cancellationToken);
            var recordedUsage = Math.Max(ledgerCredits, EstimateCredits(tokens));
            var reset = adjustments.Where(x => x.Type == "UsageReset").Sum(x => x.Amount);
            var credits = Math.Max(0, planCredits - Math.Max(0, recordedUsage - reset) + adjustments.Where(x => x.Type == "Credit").Sum(x => x.Amount));
            var sourceReference = $"legacy-cutover:{subscription.Id:N}:{subscription.CurrentPeriodStart:yyyyMMddHHmmss}";
            var accountingGrant = planCredits + Math.Max(0, recordedUsage - reset - planCredits);
            _context.AiCreditBuckets.Add(new AiCreditBucket
            {
                Id = Guid.NewGuid(), UserId = subscription.UserId, PlanCode = subscription.PlanCode,
                GrantedCredits = accountingGrant, RemainingCredits = credits, ValidFrom = subscription.CurrentPeriodStart,
                ExpiresAt = subscription.CurrentPeriodEnd, SourceType = "LegacyCutover",
                SourceReference = sourceReference, CreatedAt = DateTime.UtcNow
            });
            await _context.SaveChangesAsync(cancellationToken);
        }, cancellationToken);
    }

    private async Task ExecuteAtomicAsync(Func<Task> operation, CancellationToken cancellationToken)
    {
        if (!_context.Database.IsRelational() || _context.Database.CurrentTransaction != null)
        {
            await operation();
            return;
        }
        var strategy = _context.Database.CreateExecutionStrategy();
        await strategy.ExecuteAsync(async () =>
        {
            await using var transaction = await _context.Database.BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken);
            await operation();
            await transaction.CommitAsync(cancellationToken);
        });
    }

    private static bool IsUniqueViolation(DbUpdateException exception) =>
        exception.InnerException is SqlException sql && (sql.Number is 2601 or 2627);

    private static DateTime MonthStart(DateTime value) => new(value.Year, value.Month, 1, 0, 0, 0, DateTimeKind.Utc);

    private static int EstimateCredits(long tokensUsed)
        => tokensUsed <= 0 ? 0 : (int)Math.Ceiling(tokensUsed / 1000.0);
}
