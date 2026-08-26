using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using TaskManagement.Application.Common;
using TaskManagement.Domain.Entities;
using TaskManagement.Infrastructure.Data;
using TaskManagement.Infrastructure.Services;

namespace TaskManagement.Tests.Logic;

public sealed class PaymentP0ReservationConcurrencyRuntimeTests
{
    private const string Database = "TaskManagement_PaymentP0_20260822_01";

    [Fact]
    [Trait("Category", "SqlServerIntegration")]
    public async Task SqlServerReservationConcurrencyNeverOverspends()
    {
        await using var seed = CreateContext();
        var user = await AddUser(seed, "reservation-user");
        var userB = await AddUser(seed, "reservation-user-b");

        var roundResults = new List<ReservationResult[]>();
        for (var round = 0; round < 20; round++)
        {
            var results = await Task.WhenAll(
                Attempt(user.Id, 80, $"round-{round}-a"),
                Attempt(user.Id, 80, $"round-{round}-b"));
            roundResults.Add(results);
            results.Count(x => x.Success).Should().BeLessThanOrEqualTo(1);
            await ReleaseAll(user.Id);
        }

        var high = await Task.WhenAll(Enumerable.Range(0, 10).Select(i => Attempt(user.Id, 30, $"high-{i}")));
        high.Count(x => x.Success).Should().BeLessThanOrEqualTo(3);
        (await ActiveTotal(user.Id)).Should().BeLessThanOrEqualTo(100);
        await ReleaseAll(user.Id);

        var same = await Task.WhenAll(
            Attempt(user.Id, 80, "same-key"),
            Attempt(user.Id, 80, "same-key"));
        same.Count(x => x.Success).Should().Be(2);
        (await CountKey("same-key")).Should().Be(1);
        (await ActiveTotal(user.Id)).Should().Be(80);
        await ReleaseAll(user.Id);

        var cross = await Task.WhenAll(
            Attempt(user.Id, 80, "cross-a"),
            Attempt(userB.Id, 80, "cross-b"));
        cross.Count(x => x.Success).Should().Be(2);
        (await ActiveTotal(user.Id)).Should().Be(80);
        (await ActiveTotal(userB.Id)).Should().Be(80);

        var finalized = cross[0].ReservationId!.Value;
        await using (var finalize = CreateContext())
            await new AiCreditUsageService(finalize).FinalizeReservationAsync(finalized);
        (await Status(finalized)).Should().Be("Finalized");
        (await ActiveTotal(user.Id)).Should().Be(0);

        var released = cross[1].ReservationId!.Value;
        await using (var release = CreateContext())
            await new AiCreditUsageService(release).ReleaseReservationAsync(released);
        (await Status(released)).Should().Be("Released");
        (await ActiveTotal(userB.Id)).Should().Be(0);
    }

    private async Task<ReservationResult> Attempt(Guid userId, int credits, string key)
    {
        await using var context = CreateContext();
        try
        {
            var id = await new AiCreditUsageService(context).ReserveAsync(userId, credits, key);
            return new ReservationResult(true, id, null);
        }
        catch (AiCreditsExhaustedException exception)
        {
            return new ReservationResult(false, null, exception.GetType().Name);
        }
        catch (Exception exception)
        {
            return new ReservationResult(false, null, exception.GetType().Name + ":" + exception.Message);
        }
    }

    private async Task ReleaseAll(Guid userId)
    {
        await using var context = CreateContext();
        var ids = await context.AiCreditReservations.Where(x => x.UserId == userId && x.Status == "Reserved").Select(x => x.Id).ToListAsync();
        var service = new AiCreditUsageService(context);
        foreach (var id in ids) await service.ReleaseReservationAsync(id);
    }

    private async Task<int> ActiveTotal(Guid userId)
    {
        await using var context = CreateContext();
        return await context.AiCreditReservations.Where(x => x.UserId == userId && x.Status == "Reserved" && x.ExpiresAt > DateTime.UtcNow).SumAsync(x => (int?)x.Credits) ?? 0;
    }

    private async Task<int> CountKey(string key)
    {
        await using var context = CreateContext();
        return await context.AiCreditReservations.CountAsync(x => x.IdempotencyKey == key);
    }

    private async Task<string?> Status(Guid id)
    {
        await using var context = CreateContext();
        return await context.AiCreditReservations.Where(x => x.Id == id).Select(x => x.Status).SingleOrDefaultAsync();
    }

    private async Task<User> AddUser(ApplicationDbContext context, string prefix)
    {
        var user = new User { Id = Guid.NewGuid(), Email = $"{prefix}-{Guid.NewGuid():N}@test.local", FullName = prefix, PasswordHash = "test", CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow };
        context.Users.Add(user);
        await context.SaveChangesAsync();
        return user;
    }

    private static ApplicationDbContext CreateContext() => new(new DbContextOptionsBuilder<ApplicationDbContext>().UseSqlServer(SqlServerTestConnection.Build(Database, 60)).Options);
    private sealed record ReservationResult(bool Success, Guid? ReservationId, string? Exception);
}
