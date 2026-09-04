using TaskManagement.Application.Common;
using Microsoft.EntityFrameworkCore;
using TaskManagement.Application.DTOs.AI;
using TaskManagement.Application.Interfaces;
using TaskManagement.Domain.Entities;
using TaskManagement.Infrastructure.Data;
using System.Data;
using Microsoft.EntityFrameworkCore.Storage;
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
            var bucketPeriodStart = subscription?.CurrentPeriodStart ?? MonthStart(now);
            var bucketPeriodEnd = subscription?.CurrentPeriodEnd ?? bucketPeriodStart.AddMonths(1);
            var bucketTotalTokens = await _context.AITokenUsages.AsNoTracking()
                .Where(x => x.UserId == userId && x.CreatedAt >= bucketPeriodStart && x.CreatedAt < bucketPeriodEnd)
                .SumAsync(x => (long?)x.TokensUsed, cancellationToken) ?? 0;
            var bucketLedgerCredits = await _context.AiUsageLedgerEntries.AsNoTracking()
                .Where(x => x.UserId == userId && x.OccurredAt >= bucketPeriodStart && x.OccurredAt < bucketPeriodEnd)
                .SumAsync(x => (int?)x.CreditsConsumed, cancellationToken) ?? 0;
            var bucketAdjustmentCredits = await _context.AiCreditAdjustments.AsNoTracking()
                .Where(x => x.UserId == userId &&
                            x.EffectivePeriodStart <= bucketPeriodStart &&
                            x.EffectivePeriodEnd >= bucketPeriodEnd &&
                            x.AdjustmentType == "Credit")
                .SumAsync(x => (int?)x.Amount, cancellationToken) ?? 0;
            var bucketUsage = buckets.Sum(x => x.GrantedCredits - x.RemainingCredits);
            var hasLegacyUsage = bucketTotalTokens > 0 || bucketLedgerCredits > 0 || bucketAdjustmentCredits != 0 ||
                                 buckets.Any(x => x.SourceType == "LegacyCutover");
            var bucketUsedCredits = Math.Max(0, bucketUsage + (hasLegacyUsage ? bucketAdjustmentCredits : 0));
            var bucketTokenDerivedCredits = EstimateCredits(bucketTotalTokens);
            var bucketUsageSource = bucketLedgerCredits > 0 && bucketTokenDerivedCredits > 0
                ? "reconciled-ledger-and-token-usage"
                : bucketLedgerCredits > 0
                    ? "ai-usage-ledger"
                    : bucketTokenDerivedCredits > 0
                        ? "ai-token-usage"
                        : "ai-credit-buckets";
            return new AiCreditUsageDto
            {
                PlanCode = subscriptionPlan,
                EntitlementSource = "ai-credit-buckets",
                UsageSource = bucketUsageSource,
                IncludedCredits = buckets.Sum(x => x.GrantedCredits),
                UsedCredits = bucketUsedCredits,
                AdjustmentCredits = bucketAdjustmentCredits,
                TotalRemainingCredits = total,
                HasConfiguredEntitlement = paidPlan || total > 0,
                TotalTokens = bucketTotalTokens,
                CurrentPeriodStart = bucketPeriodStart,
                CurrentPeriodEnd = bucketPeriodEnd,
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
            return await ReserveLegacyDetailedAsync(userId, credits, idempotencyKey, cancellationToken);

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

    public Task<AiCreditReservationResult> ReserveDetailedAsync(Guid userId, int credits, string idempotencyKey, CancellationToken cancellationToken = default)
        => ReserveAsync(userId, credits, idempotencyKey, cancellationToken);

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

            // Reservations created through the legacy/free entitlement path do not
            // have bucket allocations. They still consume the period-based legacy
            // entitlement and must use the legacy terminal transition here.
            if (reservation.Allocations.Count == 0)
            {
                reservation.FinalizedCredits = actualCredits;
                reservation.Status = "Finalized";
                reservation.FinalizedAt = DateTime.UtcNow;
                reservation.CompletedAt = reservation.FinalizedAt;
                await _context.SaveChangesAsync(cancellationToken);
                return;
            }

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

    public async Task<Guid> ReserveLegacyAsync(Guid userId, int credits, string idempotencyKey, CancellationToken cancellationToken = default)
        => (await ReserveLegacyDetailedAsync(userId, credits, idempotencyKey, cancellationToken)).ReservationId;

    public async Task<AiCreditReservationResult> ReserveLegacyDetailedAsync(Guid userId, int credits, string idempotencyKey, CancellationToken cancellationToken = default)
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

    public Task FinalizeReservationLegacyAsync(Guid reservationId, CancellationToken cancellationToken = default)
        => CompleteReservationAsync(reservationId, "Finalized", cancellationToken);

    public Task ReleaseReservationLegacyAsync(Guid reservationId, CancellationToken cancellationToken = default)
        => CompleteReservationAsync(reservationId, "Released", cancellationToken);

    public Task FinalizeReservationAsync(Guid reservationId, CancellationToken cancellationToken = default)
        => FinalizeReservationLegacyAsync(reservationId, cancellationToken);

    public Task ReleaseReservationAsync(Guid reservationId, CancellationToken cancellationToken = default)
        => ReleaseReservationLegacyAsync(reservationId, cancellationToken);

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
