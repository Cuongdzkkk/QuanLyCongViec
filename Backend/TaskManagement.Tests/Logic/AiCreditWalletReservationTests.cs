using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using TaskManagement.Domain.Entities;
using TaskManagement.Infrastructure.Data;
using TaskManagement.Infrastructure.Services;

namespace TaskManagement.Tests.Logic;

public sealed class AiCreditWalletReservationTests
{
    [Fact]
    public async Task ReservationSpansBucketsInFefoOrder_AndFinalizeKeepsHeldCreditsConsumed()
    {
        await using var context = CreateContext();
        var userId = Guid.NewGuid();
        var now = DateTime.UtcNow;
        var first = AddBucket(context, userId, 700, now.AddDays(1));
        var second = AddBucket(context, userId, 3000, now.AddDays(2));
        await context.SaveChangesAsync();
        var service = new AiCreditUsageService(context);

        var reservation = await service.ReserveAsync(userId, 800, "wallet-fefo-1");
        await service.FinalizeAsync(reservation.ReservationId, 800);

        first = await context.AiCreditBuckets.SingleAsync(x => x.Id == first.Id);
        second = await context.AiCreditBuckets.SingleAsync(x => x.Id == second.Id);
        first.RemainingCredits.Should().Be(0);
        second.RemainingCredits.Should().Be(2900);
        (await context.AiCreditReservationAllocations.CountAsync(x => x.ReservationId == reservation.ReservationId)).Should().Be(2);
    }

    [Fact]
    public async Task ReleaseRestoresAllAllocatedCapacity()
    {
        await using var context = CreateContext();
        var userId = Guid.NewGuid();
        var bucket = AddBucket(context, userId, 100, DateTime.UtcNow.AddDays(1));
        await context.SaveChangesAsync();
        var service = new AiCreditUsageService(context);

        var reservation = await service.ReserveAsync(userId, 80, "wallet-release-1");
        await service.ReleaseAsync(reservation.ReservationId);

        (await context.AiCreditBuckets.SingleAsync(x => x.Id == bucket.Id)).RemainingCredits.Should().Be(100);
        (await context.AiCreditReservations.SingleAsync(x => x.Id == reservation.ReservationId)).Status.Should().Be("Released");
    }

    [Fact]
    public async Task FinalizeCanSafelyAllocateAdditionalCreditsFromAnAlreadyHeldBucket()
    {
        await using var context = CreateContext();
        var userId = Guid.NewGuid();
        var bucket = AddBucket(context, userId, 100, DateTime.UtcNow.AddDays(1));
        await context.SaveChangesAsync();
        var service = new AiCreditUsageService(context);

        var reservation = await service.ReserveAsync(userId, 1, "wallet-upsize-1");
        await service.FinalizeAsync(reservation.ReservationId, 50);

        (await context.AiCreditBuckets.SingleAsync(x => x.Id == bucket.Id)).RemainingCredits.Should().Be(50);
    }

    [Fact]
    public async Task DuplicateIdempotencyKeyCreatesOneReservationAndOneCreditEffect()
    {
        await using var context = CreateContext();
        var userId = Guid.NewGuid();
        AddBucket(context, userId, 100, DateTime.UtcNow.AddDays(1));
        await context.SaveChangesAsync();
        var service = new AiCreditUsageService(context);

        var first = await service.ReserveAsync(userId, 25, "wallet-idempotency-1");
        var second = await service.ReserveAsync(userId, 25, "wallet-idempotency-1");

        second.ReservationId.Should().Be(first.ReservationId);
        (await context.AiCreditReservations.CountAsync()).Should().Be(1);
        (await context.AiCreditBuckets.SingleAsync()).RemainingCredits.Should().Be(75);
    }

    [Fact]
    public async Task IdenticalPromptsWithDifferentOperationIdsAreIndependent_AndRetryIsIdempotent()
    {
        await using var context = CreateContext();
        var userId = Guid.NewGuid();
        AddBucket(context, userId, 100, DateTime.UtcNow.AddDays(1));
        await context.SaveChangesAsync();
        var service = new AiCreditUsageService(context);

        var operationA = $"ai-operation:{Guid.NewGuid():N}";
        var operationB = $"ai-operation:{Guid.NewGuid():N}";
        var invocationA = await service.ReserveAsync(userId, 1, operationA);
        var invocationB = await service.ReserveAsync(userId, 1, operationB);
        var retryA = await service.ReserveAsync(userId, 1, operationA);

        invocationA.Acquired.Should().BeTrue();
        invocationB.Acquired.Should().BeTrue();
        retryA.ReservationId.Should().Be(invocationA.ReservationId);
        (await context.AiCreditReservations.CountAsync()).Should().Be(2);
        (await context.AiCreditBuckets.SingleAsync()).RemainingCredits.Should().Be(98);
    }

    [Fact]
    public async Task TerminalReservationTransitionsAreOneWay()
    {
        await using var context = CreateContext();
        var userId = Guid.NewGuid();
        AddBucket(context, userId, 10, DateTime.UtcNow.AddDays(1));
        await context.SaveChangesAsync();
        var service = new AiCreditUsageService(context);
        var released = await service.ReserveAsync(userId, 2, "terminal-release");
        await service.ReleaseAsync(released.ReservationId);
        await Assert.ThrowsAsync<InvalidOperationException>(() => service.FinalizeAsync(released.ReservationId, 2));

        var finalized = await service.ReserveAsync(userId, 2, "terminal-finalize");
        await service.FinalizeAsync(finalized.ReservationId, 2);
        await service.ReleaseAsync(finalized.ReservationId);
        (await context.AiCreditReservations.SingleAsync(x => x.Id == finalized.ReservationId)).Status.Should().Be("Finalized");
    }

    [Fact]
    public async Task LegacyPlusCutoverPreserves900ThenProPurchaseAdds3000()
    {
        await using var context = CreateContext();
        var userId = Guid.NewGuid();
        var now = DateTime.UtcNow;
        context.Users.Add(new User { Id = userId, Email = $"{userId:N}@test.local", PasswordHash = "test", FullName = "Test", CreatedAt = now, UpdatedAt = now });
        context.AiPricingPlans.Add(new AiPricingPlan { Id = Guid.NewGuid(), Code = "plus", Name = "Plus", IncludedAiCredits = 1200, CreatedAt = now, UpdatedAt = now });
        context.AiPricingPlans.Add(new AiPricingPlan { Id = Guid.NewGuid(), Code = "pro", Name = "Pro", IncludedAiCredits = 3000, CreatedAt = now, UpdatedAt = now });
        context.AiSubscriptions.Add(new AiSubscription
        {
            Id = Guid.NewGuid(), UserId = userId, PlanCode = "plus", Status = "Active",
            CurrentPeriodStart = now.AddDays(-1), CurrentPeriodEnd = now.AddDays(29), CreatedAt = now, UpdatedAt = now
        });
        context.AITokenUsages.Add(new AITokenUsage { Id = Guid.NewGuid(), UserId = userId, FeatureCode = "test", TokensUsed = 300_000, CreatedAt = now });
        await context.SaveChangesAsync();

        var service = new AiCreditUsageService(context);
        (await service.GetUsageAsync(userId, DateTime.MinValue, DateTime.MaxValue)).TotalRemainingCredits.Should().Be(900);
        var subscription = await context.AiSubscriptions.SingleAsync(x => x.UserId == userId);
        subscription.PlanCode = "pro";
        context.AiCreditBuckets.Add(new AiCreditBucket
        {
            Id = Guid.NewGuid(), UserId = userId, PlanCode = "pro", GrantedCredits = 3000, RemainingCredits = 3000,
            ValidFrom = now, ExpiresAt = now.AddDays(30), SourceType = "PaymentOrder", CreatedAt = now
        });
        await context.SaveChangesAsync();

        var usage = await service.GetUsageAsync(userId, DateTime.MinValue, DateTime.MaxValue);
        usage.PlanCode.Should().Be("pro");
        usage.TotalRemainingCredits.Should().Be(3900);
        (await context.AiCreditBuckets.CountAsync(x => x.UserId == userId)).Should().Be(2);
    }

    [Fact]
    public async Task ExpiredAndFutureBucketsAreNotReservable()
    {
        await using var context = CreateContext();
        var userId = Guid.NewGuid();
        AddBucket(context, userId, 100, DateTime.UtcNow.AddMinutes(-1), DateTime.UtcNow.AddDays(-1));
        AddBucket(context, userId, 100, DateTime.UtcNow.AddDays(1), DateTime.UtcNow.AddDays(2));
        await context.SaveChangesAsync();

        var result = await new AiCreditUsageService(context).ReserveAsync(userId, 1, "wallet-validity-1");

        result.Acquired.Should().BeFalse();
        result.Status.Should().Be("Exhausted");
    }

    private static ApplicationDbContext CreateContext() => new(
        new DbContextOptionsBuilder<ApplicationDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString("N")).Options);

    private static AiCreditBucket AddBucket(ApplicationDbContext context, Guid userId, int credits, DateTime expiresAt)
        => AddBucket(context, userId, credits, DateTime.UtcNow.AddMinutes(-1), expiresAt);

    private static AiCreditBucket AddBucket(ApplicationDbContext context, Guid userId, int credits, DateTime validFrom, DateTime expiresAt)
    {
        var bucket = new AiCreditBucket
        {
            Id = Guid.NewGuid(), UserId = userId, PlanCode = "plus", GrantedCredits = credits,
            RemainingCredits = credits, ValidFrom = validFrom, ExpiresAt = expiresAt,
            SourceType = "Test", CreatedAt = DateTime.UtcNow
        };
        context.AiCreditBuckets.Add(bucket);
        return bucket;
    }
}
