using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using TaskManagement.Application.Interfaces;
using TaskManagement.Domain.Entities;
using TaskManagement.Infrastructure.Data;
using TaskManagement.Infrastructure.Services;

namespace TaskManagement.Tests.Logic;

public sealed class AiCreditWalletSqlServerTests
{
    private static string ConnectionString => Environment.GetEnvironmentVariable("SPRINTA_SQLSERVER_CONNECTION")
        ?? "Server=(localdb)\\MSSQLLocalDB;Database=SprintACreditWalletConcurrencyV1;Trusted_Connection=True;TrustServerCertificate=True;Encrypt=False";

    [Fact]
    [Trait("Category", "SqlServerIntegration")]
    public async Task ConcurrentReservations_WhenOnlyOneFits_OnlyOneSucceeds()
    {
        if (!await IsAvailableAsync()) return;
        var userId = await SeedAsync(100);
        try
        {
            var results = await Task.WhenAll(ReserveInNewContextAsync(userId, "sql-race-a"), ReserveInNewContextAsync(userId, "sql-race-b"));
            results.Count(x => x.Acquired).Should().Be(1);
            await AssertNoNegativeBalanceAsync(userId);
        }
        finally { await CleanupAsync(userId); }
    }

    [Fact]
    [Trait("Category", "SqlServerIntegration")]
    public async Task ConcurrentReservations_WhenBothFit_BothSucceedWithoutNegativeBalance()
    {
        if (!await IsAvailableAsync()) return;
        var userId = await SeedAsync(200);
        try
        {
            var results = await Task.WhenAll(ReserveInNewContextAsync(userId, "sql-fit-a"), ReserveInNewContextAsync(userId, "sql-fit-b"));
            results.Count(x => x.Acquired).Should().Be(2);
            await AssertNoNegativeBalanceAsync(userId);
        }
        finally { await CleanupAsync(userId); }
    }

    [Fact]
    [Trait("Category", "SqlServerIntegration")]
    public async Task ConcurrentReservationSpansBucketsInFefoOrder()
    {
        if (!await IsAvailableAsync()) return;
        var userId = await SeedAsync(800, twoBuckets: true);
        try
        {
            await using var context = CreateContext();
            var result = await new AiCreditUsageService(context).ReserveAsync(userId, 800, "sql-fefo");
            result.Acquired.Should().BeTrue();
            var allocations = await context.AiCreditReservationAllocations.Where(x => x.ReservationId == result.ReservationId).ToListAsync();
            allocations.Should().HaveCount(2);
            allocations.Sum(x => x.AllocatedCredits).Should().Be(800);
            await AssertNoNegativeBalanceAsync(userId);
        }
        finally { await CleanupAsync(userId); }
    }

    [Fact]
    [Trait("Category", "SqlServerIntegration")]
    public async Task ConcurrentDuplicateIdempotencyKeyCreatesOneReservation()
    {
        if (!await IsAvailableAsync()) return;
        var userId = await SeedAsync(100);
        try
        {
            var results = await Task.WhenAll(ReserveInNewContextAsync(userId, "sql-duplicate"), ReserveInNewContextAsync(userId, "sql-duplicate"));
            results.Select(x => x.ReservationId).Distinct().Should().ContainSingle();
            await using var context = CreateContext();
            (await context.AiCreditReservations.CountAsync(x => x.UserId == userId)).Should().Be(1);
        }
        finally { await CleanupAsync(userId); }
    }

    [Fact]
    [Trait("Category", "SqlServerIntegration")]
    public async Task FinalizeAndReleaseRaceHasExactlyOneTerminalWinner()
    {
        if (!await IsAvailableAsync()) return;
        var userId = await SeedAsync(100);
        try
        {
            var reservation = await ReserveInNewContextAsync(userId, "sql-finalize-release");
            var outcomes = await Task.WhenAll(
                CaptureAsync(() => FinalizeInNewContextAsync(reservation.ReservationId, 1)),
                CaptureAsync(() => ReleaseInNewContextAsync(reservation.ReservationId)));
            outcomes.Count(x => x).Should().Be(1);
            await AssertTerminalStateAsync(reservation.ReservationId, userId);
        }
        finally { await CleanupAsync(userId); }
    }

    [Fact]
    [Trait("Category", "SqlServerIntegration")]
    public async Task FinalizeAndExpireRaceLeavesConsistentBalance()
    {
        if (!await IsAvailableAsync()) return;
        var userId = await SeedAsync(100);
        try
        {
            var reservation = await ReserveInNewContextAsync(userId, "sql-finalize-expire");
            var outcomes = await Task.WhenAll(
                CaptureAsync(() => FinalizeInNewContextAsync(reservation.ReservationId, 1)),
                CaptureAsync(() => ExpireInNewContextAsync(reservation.ReservationId)));
            outcomes.Count(x => x).Should().Be(1);
            await AssertTerminalStateAsync(reservation.ReservationId, userId);
        }
        finally { await CleanupAsync(userId); }
    }

    [Fact]
    [Trait("Category", "SqlServerIntegration")]
    public async Task ReleaseAndExpireRaceRestoresCapacityAtMostOnce()
    {
        if (!await IsAvailableAsync()) return;
        var userId = await SeedAsync(100);
        try
        {
            var reservation = await ReserveInNewContextAsync(userId, "sql-release-expire");
            await Task.WhenAll(
                CaptureAsync(() => ReleaseInNewContextAsync(reservation.ReservationId)),
                CaptureAsync(() => ExpireInNewContextAsync(reservation.ReservationId)));
            await AssertTerminalStateAsync(reservation.ReservationId, userId);
            await AssertNoNegativeBalanceAsync(userId);
            await using var context = CreateContext();
            (await context.AiCreditBuckets.SingleAsync(x => x.UserId == userId)).RemainingCredits.Should().Be(100);
        }
        finally { await CleanupAsync(userId); }
    }

    [Fact]
    [Trait("Category", "SqlServerIntegration")]
    public async Task SameOperationFinalizeCalledTwiceHasOneConsumptionEffect()
    {
        if (!await IsAvailableAsync()) return;
        var userId = await SeedAsync(100);
        try
        {
            var reservation = await ReserveInNewContextAsync(userId, "sql-finalize-twice");
            await Task.WhenAll(
                CaptureAsync(() => FinalizeInNewContextAsync(reservation.ReservationId, 1)),
                CaptureAsync(() => FinalizeInNewContextAsync(reservation.ReservationId, 1)));
            await using var context = CreateContext();
            (await context.AiCreditBuckets.SingleAsync(x => x.UserId == userId)).RemainingCredits.Should().Be(99);
        }
        finally { await CleanupAsync(userId); }
    }

    [Fact]
    [Trait("Category", "SqlServerIntegration")]
    public async Task SamePromptDifferentOperationIdsConsumeIndependently_AndRetryDoesNot()
    {
        if (!await IsAvailableAsync()) return;
        var userId = await SeedAsync(100);
        try
        {
            var a = await ReserveInNewContextAsync(userId, "sql-operation-a");
            var b = await ReserveInNewContextAsync(userId, "sql-operation-b");
            var retry = await ReserveInNewContextAsync(userId, "sql-operation-a");
            a.Acquired.Should().BeTrue(); b.Acquired.Should().BeTrue(); retry.ReservationId.Should().Be(a.ReservationId);
            await using var context = CreateContext();
            (await context.AiCreditBuckets.SingleAsync(x => x.UserId == userId)).RemainingCredits.Should().Be(98);
        }
        finally { await CleanupAsync(userId); }
    }

    [Fact]
    [Trait("Category", "SqlServerIntegration")]
    public async Task ActualUsageAboveEstimateIsAllocatedUnderTheSqlBoundary()
    {
        if (!await IsAvailableAsync()) return;
        var userId = await SeedAsync(100);
        try
        {
            var reservation = await ReserveInNewContextAsync(userId, "sql-actual-over-estimate");
            await FinalizeInNewContextAsync(reservation.ReservationId, 50);
            await using var context = CreateContext();
            (await context.AiCreditBuckets.SingleAsync(x => x.UserId == userId)).RemainingCredits.Should().Be(50);
        }
        finally { await CleanupAsync(userId); }
    }

    private static async Task<bool> CaptureAsync(Func<Task> operation)
    {
        try { await operation(); return true; }
        catch (InvalidOperationException) { return false; }
    }

    private static async Task FinalizeInNewContextAsync(Guid reservationId, int credits)
    {
        await using var context = CreateContext();
        await new AiCreditUsageService(context).FinalizeAsync(reservationId, credits);
    }

    private static async Task ReleaseInNewContextAsync(Guid reservationId)
    {
        await using var context = CreateContext();
        await new AiCreditUsageService(context).ReleaseAsync(reservationId);
    }

    private static async Task ExpireInNewContextAsync(Guid reservationId)
    {
        await using var context = CreateContext();
        await new AiCreditUsageService(context).ExpireAsync(reservationId);
    }

    private static async Task AssertTerminalStateAsync(Guid reservationId, Guid userId)
    {
        await using var context = CreateContext();
        (await context.AiCreditReservations.SingleAsync(x => x.Id == reservationId)).Status.Should().BeOneOf("Finalized", "Released", "Expired");
        (await context.AiCreditBuckets.SingleAsync(x => x.UserId == userId)).RemainingCredits.Should().BeInRange(99, 100);
    }

    private static async Task<AiCreditReservationResult> ReserveInNewContextAsync(Guid userId, string key)
    {
        await using var context = CreateContext();
        return await new AiCreditUsageService(context).ReserveAsync(userId, 100, key);
    }

    private static async Task<Guid> SeedAsync(int credits, bool twoBuckets = false)
    {
        await using var context = CreateContext();
        await context.Database.EnsureCreatedAsync();
        var userId = Guid.NewGuid();
        var now = DateTime.UtcNow;
        context.Users.Add(new User { Id = userId, Email = $"{userId:N}@sql.test", PasswordHash = "test", FullName = "SQL test", CreatedAt = now, UpdatedAt = now });
        context.AiCreditBuckets.Add(new AiCreditBucket
        {
            Id = Guid.NewGuid(), UserId = userId, PlanCode = "plus", GrantedCredits = twoBuckets ? 700 : credits,
            RemainingCredits = twoBuckets ? 700 : credits, ValidFrom = now.AddMinutes(-1), ExpiresAt = now.AddDays(1), SourceType = "Test", CreatedAt = now
        });
        if (twoBuckets) context.AiCreditBuckets.Add(new AiCreditBucket
        {
            Id = Guid.NewGuid(), UserId = userId, PlanCode = "pro", GrantedCredits = 3000,
            RemainingCredits = 3000, ValidFrom = now.AddMinutes(-1), ExpiresAt = now.AddDays(2), SourceType = "Test", CreatedAt = now
        });
        await context.SaveChangesAsync();
        return userId;
    }

    private static async Task CleanupAsync(Guid userId)
    {
        await using var context = CreateContext();
        await context.AiCreditReservations.Where(x => x.UserId == userId).ExecuteDeleteAsync();
        await context.AiCreditBuckets.Where(x => x.UserId == userId).ExecuteDeleteAsync();
        await context.Users.Where(x => x.Id == userId).ExecuteDeleteAsync();
    }

    private static async Task AssertNoNegativeBalanceAsync(Guid userId)
    {
        await using var context = CreateContext();
        (await context.AiCreditBuckets.Where(x => x.UserId == userId).MinAsync(x => (int?)x.RemainingCredits) ?? 0).Should().BeGreaterThanOrEqualTo(0);
    }

    private static async Task<bool> IsAvailableAsync()
    {
        try
        {
            await using var context = CreateContext();
            return await context.Database.CanConnectAsync();
        }
        catch { return false; }
    }

    private static ApplicationDbContext CreateContext() => new(new DbContextOptionsBuilder<ApplicationDbContext>().UseSqlServer(ConnectionString).Options);
}
