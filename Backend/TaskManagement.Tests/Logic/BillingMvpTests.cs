using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using TaskManagement.Domain.Entities;
using TaskManagement.Infrastructure.Data;
using TaskManagement.Infrastructure.Services;

namespace TaskManagement.Tests.Logic;

public sealed class BillingMvpTests
{
    [Fact]
    public async Task UserWithoutSubscription_ResolvesPublishedFreeEntitlement()
    {
        await using var context = CreateContext();
        var user = AddUser(context);
        AddPlans(context);
        await context.SaveChangesAsync();

        var usage = await CreditService(context).GetUsageAsync(user.Id, DateTime.MinValue, DateTime.MaxValue);

        usage.PlanCode.Should().Be("free");
        usage.IncludedCredits.Should().Be(100);
        usage.SubscriptionStatus.Should().Be("Active");
    }

    [Theory]
    [InlineData("starter", 500)]
    [InlineData("plus", 1200)]
    [InlineData("pro", 3000)]
    public async Task PaidSubscription_ResolvesPlanCredits(string planCode, int includedCredits)
    {
        await using var context = CreateContext();
        var user = AddUser(context);
        AddPlans(context);
        AddActiveSubscription(context, user.Id, planCode);
        await context.SaveChangesAsync();

        var usage = await CreditService(context).GetUsageAsync(user.Id, DateTime.MinValue, DateTime.MaxValue);

        usage.PlanCode.Should().Be(planCode);
        usage.IncludedCredits.Should().Be(includedCredits);
    }

    [Fact]
    public async Task Usage_OnlyCountsRowsInsideSubscriptionPeriod_AndNeverReturnsNegativeRemaining()
    {
        await using var context = CreateContext();
        var user = AddUser(context);
        AddPlans(context);
        var now = DateTime.UtcNow;
        var start = now.AddDays(-5);
        var end = now.AddDays(25);
        AddActiveSubscription(context, user.Id, "starter", start, end);
        context.AITokenUsages.AddRange(
            TokenUsage(user.Id, 501_000, start.AddHours(1)),
            TokenUsage(user.Id, 900_000, start.AddSeconds(-1)),
            TokenUsage(user.Id, 900_000, end));
        await context.SaveChangesAsync();

        var usage = await CreditService(context).GetUsageAsync(user.Id, DateTime.MinValue, DateTime.MaxValue);

        usage.UsedCredits.Should().Be(501);
        usage.RemainingCredits.Should().Be(0);
    }

    [Fact]
    public async Task ApprovingNewPlanAtomicallyCutsOverLegacyBalanceBeforeGrantingNewBucket()
    {
        await using var context = CreateContext();
        var user = AddUser(context);
        var admin = AddUser(context, "admin-cutover@sprinta.local");
        AddPlans(context);
        var now = DateTime.UtcNow;
        AddActiveSubscription(context, user.Id, "plus", now.AddDays(-1), now.AddDays(29));
        context.AITokenUsages.Add(TokenUsage(user.Id, 300_000, now));
        await context.SaveChangesAsync();

        var billing = BillingService(context);
        var order = await billing.CreateOrderAsync(user.Id, "pro");
        await billing.ApproveOrderAsync(order.Id, admin.Id, "cutover test");

        var buckets = await context.AiCreditBuckets.Where(x => x.UserId == user.Id).OrderBy(x => x.SourceType).ToListAsync();
        buckets.Should().HaveCount(2);
        buckets.Single(x => x.SourceType == "LegacyCutover").RemainingCredits.Should().Be(900);
        buckets.Single(x => x.SourceType == "PaymentOrder").RemainingCredits.Should().Be(3000);
        (await BillingService(context).GetSummaryAsync(user.Id)).TotalRemainingCredits.Should().Be(3900);
    }

    [Fact]
    public async Task ApprovingPaidOrder_IsIdempotent_AndStartsFreshMonthlyPeriod()
    {
        await using var context = CreateContext();
        var user = AddUser(context);
        var admin = AddUser(context, "admin@sprinta.local");
        AddPlans(context);
        await context.SaveChangesAsync();
        var billing = BillingService(context);
        var order = await billing.CreateOrderAsync(user.Id, "plus");

        var first = await billing.ApproveOrderAsync(order.Id, admin.Id, "Đã đối soát");
        var activatedAt = (await context.AiSubscriptions.SingleAsync()).ActivatedAt;
        var second = await billing.ApproveOrderAsync(order.Id, admin.Id, "Duyệt lại");

        first.Status.Should().Be("Paid");
        second.Status.Should().Be("Paid");
        (await context.AiSubscriptions.CountAsync()).Should().Be(1);
        (await context.AiSubscriptions.SingleAsync()).PlanCode.Should().Be("plus");
        (await context.AiSubscriptions.SingleAsync()).ActivatedAt.Should().Be(activatedAt);
        (await context.SystemAuditLogs.CountAsync(log => log.Action == "PAYMENT_ORDER_APPROVE")).Should().Be(1);
    }

    [Theory]
    [InlineData("Cancelled")]
    [InlineData("Expired")]
    public async Task InactivePaidSubscription_FallsBackToFree(string status)
    {
        await using var context = CreateContext();
        var user = AddUser(context);
        AddPlans(context);
        var subscription = AddActiveSubscription(context, user.Id, "pro");
        subscription.Status = status;
        await context.SaveChangesAsync();

        var usage = await CreditService(context).GetUsageAsync(user.Id, DateTime.MinValue, DateTime.MaxValue);

        usage.PlanCode.Should().Be("free");
        usage.IncludedCredits.Should().Be(100);
        usage.SubscriptionStatus.Should().Be(status);
    }

    [Fact]
    public async Task AutoRenew_AdvancesMonthlyPeriod_WithoutDeletingOldUsage()
    {
        await using var context = CreateContext();
        var user = AddUser(context);
        AddPlans(context);
        var oldStart = DateTime.UtcNow.AddMonths(-2).AddDays(-2);
        var subscription = AddActiveSubscription(context, user.Id, "starter", oldStart, oldStart.AddMonths(1));
        subscription.AutoRenew = true;
        context.AITokenUsages.Add(TokenUsage(user.Id, 12_000, oldStart.AddDays(2)));
        await context.SaveChangesAsync();

        var usage = await CreditService(context).GetUsageAsync(user.Id, DateTime.MinValue, DateTime.MaxValue);

        usage.PlanCode.Should().Be("starter");
        usage.CurrentPeriodEnd.Should().BeAfter(DateTime.UtcNow);
        usage.UsedCredits.Should().Be(0);
        (await context.AITokenUsages.CountAsync()).Should().Be(1);
    }

    [Fact]
    public async Task AdminCreditAdjustment_IncreasesCurrentPeriodRemaining()
    {
        await using var context = CreateContext();
        var user = AddUser(context);
        var admin = AddUser(context, "admin@sprinta.local");
        AddPlans(context);
        context.AITokenUsages.Add(TokenUsage(user.Id, 20_000, DateTime.UtcNow));
        await context.SaveChangesAsync();

        var summary = await BillingService(context).AddAdjustmentAsync(user.Id, 25, admin.Id, "Bù credit demo");

        summary.UsedCredits.Should().Be(20);
        summary.AdjustmentCredits.Should().Be(25);
        summary.RemainingCredits.Should().Be(105);
    }

    [Fact]
    public async Task AdminReset_OnlyResetsRequestedUsersCurrentPeriod_AndPreservesUsageHistory()
    {
        await using var context = CreateContext();
        var user = AddUser(context);
        var other = AddUser(context, "other@sprinta.local");
        var admin = AddUser(context, "admin@sprinta.local");
        AddPlans(context);
        var now = DateTime.UtcNow;
        context.AITokenUsages.AddRange(
            TokenUsage(user.Id, 3_000, now),
            TokenUsage(user.Id, 9_000, now.AddMonths(-2)),
            TokenUsage(other.Id, 2_000, now));
        await context.SaveChangesAsync();
        var billing = BillingService(context);

        var reset = await billing.ResetCurrentPeriodUsageAsync(user.Id, admin.Id, "Chuẩn bị thi");
        var otherUsage = await CreditService(context).GetUsageAsync(other.Id, DateTime.MinValue, DateTime.MaxValue);

        reset.UsedCredits.Should().Be(0);
        reset.RemainingCredits.Should().Be(100);
        otherUsage.UsedCredits.Should().Be(2);
        (await context.AITokenUsages.CountAsync()).Should().Be(3);
        (await context.AiCreditAdjustments.SingleAsync()).AdjustmentType.Should().Be("UsageReset");
    }

    private static ApplicationDbContext CreateContext() => new(
        new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString("N"))
            .Options);

    private static AiCreditUsageService CreditService(ApplicationDbContext context) => new(context);
    private static BillingService BillingService(ApplicationDbContext context) => new(context, CreditService(context));

    private static User AddUser(ApplicationDbContext context, string email = "user@sprinta.local")
    {
        var now = DateTime.UtcNow;
        var user = new User
        {
            Id = Guid.NewGuid(), Email = email, FullName = email.Split('@')[0], PasswordHash = "test",
            IsActive = true, CreatedAt = now, UpdatedAt = now
        };
        context.Users.Add(user);
        return user;
    }

    private static void AddPlans(ApplicationDbContext context)
    {
        AddPlan(context, "free", "Free", 0, 100);
        AddPlan(context, "starter", "Starter", 49_000, 500);
        AddPlan(context, "plus", "Plus", 99_000, 1_200);
        AddPlan(context, "pro", "Pro", 199_000, 3_000);
        AddPlan(context, "team", "Team", 499_000, 9_000);
    }

    private static void AddPlan(ApplicationDbContext context, string code, string name, decimal price, int credits)
    {
        context.AiPricingPlans.Add(new AiPricingPlan
        {
            Id = Guid.NewGuid(), Code = code, Name = name, MonthlyPriceVnd = price,
            IncludedAiCredits = credits, IsPublished = true, PricingStatus = "Published",
            CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow
        });
    }

    private static AiSubscription AddActiveSubscription(
        ApplicationDbContext context, Guid userId, string planCode, DateTime? start = null, DateTime? end = null)
    {
        var periodStart = start ?? DateTime.UtcNow.AddDays(-1);
        var subscription = new AiSubscription
        {
            Id = Guid.NewGuid(), UserId = userId, PlanCode = planCode, Status = "Active",
            CurrentPeriodStart = periodStart, CurrentPeriodEnd = end ?? periodStart.AddMonths(1),
            ActivatedAt = periodStart, CreatedAt = periodStart, UpdatedAt = periodStart
        };
        context.AiSubscriptions.Add(subscription);
        return subscription;
    }

    private static AITokenUsage TokenUsage(Guid userId, long tokens, DateTime createdAt) => new()
    {
        Id = Guid.NewGuid(), UserId = userId, FeatureCode = "ai-chat", TokensUsed = tokens, CreatedAt = createdAt
    };
}
