using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using TaskManagement.Application.Interfaces;
using TaskManagement.Domain.Entities;
using TaskManagement.Infrastructure.Data;
using TaskManagement.Infrastructure.Payments;
using TaskManagement.Infrastructure.Services;

namespace TaskManagement.Tests.Logic;

public sealed class PaymentSqlServerTransactionTests
{
    [Fact]
    [Trait("Category", "SqlServerIntegration")]
    public async Task ProviderPaymentIsAtomicIdempotentAndActivatesPlus()
    {
        var databaseName = $"TaskManagement_PaymentHotfix_{Guid.NewGuid():N}";
        await using var context = CreateContext(databaseName);
        await context.Database.MigrateAsync();
        try
        {
            var (user, order) = await SeedPendingOrderAsync(context, "plus", 1200);
            var billing = new BillingService(context, new AiCreditUsageService(context));
            var webhook = ValidWebhook(order, "provider-75488766");

            var first = await billing.ProcessProviderPaymentAsync("sepay", webhook, "{\"id\":75488766}");
            var paidAt = (await context.PaymentOrders.SingleAsync()).PaidAt;
            var second = await billing.ProcessProviderPaymentAsync("sepay", webhook, "{\"id\":75488766}");

            first!.Status.Should().Be("Paid");
            second.Should().BeNull();
            (await context.PaymentOrders.SingleAsync()).Status.Should().Be("Paid");
            paidAt.Should().NotBeNull();
            (await context.AiSubscriptions.SingleAsync()).PlanCode.Should().Be("plus");
            (await new AiCreditUsageService(context).GetUsageAsync(user.Id, DateTime.MinValue, DateTime.MaxValue)).IncludedCredits.Should().Be(1200);
            (await context.PaymentTransactions.CountAsync()).Should().Be(1);
            (await context.PaymentWebhookEvents.CountAsync()).Should().Be(1);
            (await context.AiSubscriptions.CountAsync()).Should().Be(1);
        }
        finally
        {
            await context.Database.EnsureDeletedAsync();
        }
    }

    [Fact]
    [Trait("Category", "SqlServerIntegration")]
    public async Task ProviderPaymentRollsBackAndCanRetryAfterFailure()
    {
        var databaseName = $"TaskManagement_PaymentHotfix_{Guid.NewGuid():N}";
        await using var context = CreateContext(databaseName);
        await context.Database.MigrateAsync();
        try
        {
            var (user, order) = await SeedPendingOrderAsync(context, "missing-plan", 1200, addPlan: false);
            var billing = new BillingService(context, new AiCreditUsageService(context));
            var webhook = ValidWebhook(order, "provider-rollback-1");

            Func<Task> failingCall = () => billing.ProcessProviderPaymentAsync("sepay", webhook, "{\"id\":\"rollback-1\"}");
            await failingCall.Should().ThrowAsync<InvalidOperationException>();

            (await context.PaymentOrders.SingleAsync()).Status.Should().Be("Pending");
            (await context.PaymentWebhookEvents.CountAsync()).Should().Be(0);
            (await context.PaymentTransactions.CountAsync()).Should().Be(0);
            (await context.AiSubscriptions.CountAsync()).Should().Be(0);

            context.AiPricingPlans.Add(new AiPricingPlan
            {
                Id = Guid.NewGuid(), Code = "missing-plan", Name = "Plus", MonthlyPriceVnd = 99000,
                IncludedAiCredits = 1200, IsPublished = true, CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow
            });
            await context.SaveChangesAsync();

            (await billing.ProcessProviderPaymentAsync("sepay", webhook, "{\"id\":\"rollback-1\"}")).Should().NotBeNull();
            (await context.PaymentOrders.SingleAsync()).Status.Should().Be("Paid");
            (await context.PaymentTransactions.CountAsync()).Should().Be(1);
            (await context.PaymentWebhookEvents.CountAsync()).Should().Be(1);
            (await context.AiSubscriptions.CountAsync()).Should().Be(1);
            user.Id.Should().NotBeEmpty();
        }
        finally
        {
            await context.Database.EnsureDeletedAsync();
        }
    }

    [Fact]
    [Trait("Category", "SqlServerIntegration")]
    public async Task ManualApprovalIsAtomicAndDoubleApprovalIsIdempotent()
    {
        var databaseName = $"TaskManagement_PaymentHotfix_{Guid.NewGuid():N}";
        await using var context = CreateContext(databaseName);
        await context.Database.MigrateAsync();
        try
        {
            var (_, order) = await SeedPendingOrderAsync(context, "plus", 1200);
            var admin = new User { Id = Guid.NewGuid(), Email = "admin-hotfix@test.local", FullName = "Admin", PasswordHash = "test", CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow };
            context.Users.Add(admin);
            await context.SaveChangesAsync();
            var billing = new BillingService(context, new AiCreditUsageService(context));

            (await billing.ApproveOrderAsync(order.Id, admin.Id, "SQL hotfix test")).Status.Should().Be("Paid");
            var activatedAt = (await context.AiSubscriptions.SingleAsync()).ActivatedAt;
            (await billing.ApproveOrderAsync(order.Id, admin.Id, "duplicate approval")).Status.Should().Be("Paid");

            (await context.PaymentOrders.SingleAsync()).Status.Should().Be("Paid");
            (await context.PaymentTransactions.CountAsync()).Should().Be(1);
            (await context.AiSubscriptions.CountAsync()).Should().Be(1);
            (await context.AiSubscriptions.SingleAsync()).ActivatedAt.Should().Be(activatedAt);
            (await context.SystemAuditLogs.CountAsync(x => x.Action == "PAYMENT_ORDER_APPROVE")).Should().Be(1);
        }
        finally
        {
            await context.Database.EnsureDeletedAsync();
        }
    }

    private static ApplicationDbContext CreateContext(string databaseName)
    {
        return new ApplicationDbContext(new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseSqlServer(SqlServerTestConnection.Build(databaseName, 10), sql => sql.EnableRetryOnFailure())
            .Options);
    }

    private static async Task<(User User, PaymentOrder Order)> SeedPendingOrderAsync(ApplicationDbContext context, string planCode, int includedCredits, bool addPlan = true)
    {
        var now = DateTime.UtcNow;
        var user = new User { Id = Guid.NewGuid(), Email = $"payment-{Guid.NewGuid():N}@test.local", FullName = "Payment User", PasswordHash = "test", CreatedAt = now, UpdatedAt = now };
        var plan = await context.AiPricingPlans.SingleOrDefaultAsync(item => item.Code == planCode);
        if (plan == null && addPlan)
        {
            plan = new AiPricingPlan { Id = Guid.NewGuid(), Code = planCode, Name = "Plus", MonthlyPriceVnd = 99000, IncludedAiCredits = includedCredits, IsPublished = true, CreatedAt = now, UpdatedAt = now };
            context.AiPricingPlans.Add(plan);
        }
        var order = new PaymentOrder { Id = Guid.NewGuid(), UserId = user.Id, PlanCode = planCode, PlanNameSnapshot = plan?.Name ?? "Plus", IncludedAiCreditsSnapshot = includedCredits, AmountVnd = 99000, Currency = "VND", Provider = "sepay", Status = "Pending", TransferCode = $"SEVQR SPA_TEST_{Guid.NewGuid():N}", CreatedAt = now, ExpiresAt = now.AddMinutes(20) };
        context.Users.Add(user);
        context.PaymentOrders.Add(order);
        await context.SaveChangesAsync();
        return (user, order);
    }

    private static PaymentWebhookVerificationResult ValidWebhook(PaymentOrder order, string providerEventId) => new()
    {
        IsValid = true, ProviderEventId = providerEventId, TransactionType = "in", Amount = order.AmountVnd,
        TransferContent = $"payment {order.TransferCode}", ProviderReference = "SEVQR SPA_TEST", TransactionAt = DateTime.UtcNow
    };
}
