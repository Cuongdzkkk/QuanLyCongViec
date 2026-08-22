using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Moq;
using TaskManagement.Application.DTOs.Billing;
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

    [Fact]
    [Trait("Category", "SqlServerIntegration")]
    public async Task ConcurrentDuplicateProviderPaymentHasOneAuthoritativeFulfillment()
    {
        var databaseName = $"TaskManagement_PaymentHotfix_{Guid.NewGuid():N}";
        await using var setupContext = CreateContext(databaseName);
        await setupContext.Database.MigrateAsync();
        try
        {
            var (user, order) = await SeedPendingOrderAsync(setupContext, "plus", 1200);
            var webhook = ValidWebhook(order, "provider-concurrent-1");
            using var barrier = new Barrier(2);

            async Task ProcessAsync()
            {
                await using var context = CreateContext(databaseName);
                var notifier = new Mock<ISignalRClientNotifier>();
                notifier.Setup(x => x.SendNotificationAsync(It.IsAny<Guid>(), It.IsAny<Notification>())).Returns(Task.CompletedTask);
                var email = new Mock<IEmailService>();
                email.Setup(x => x.SendPaymentReceiptEmailAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<decimal>(), It.IsAny<string>(), It.IsAny<DateTime>())).ReturnsAsync("sql-concurrent-email");
                var billing = new BillingService(
                    context,
                    new AiCreditUsageService(context),
                    notificationService: new NotificationService(context, notifier.Object),
                    emailService: email.Object);

                barrier.SignalAndWait();
                await billing.ProcessProviderPaymentAsync("sepay", webhook, "{concurrent:true}");
            }

            await Task.WhenAll(Task.Run(ProcessAsync), Task.Run(ProcessAsync));

            await using var verifyContext = CreateContext(databaseName);
            var receiptBilling = new BillingService(verifyContext, new AiCreditUsageService(verifyContext));
            var receiptA = await receiptBilling.GetReceiptAsync(order.Id, user.Id, false);
            var receiptB = await receiptBilling.GetReceiptAsync(order.Id, user.Id, false);

            (await verifyContext.PaymentOrders.CountAsync(x => x.Status == "Paid")).Should().Be(1);
            (await verifyContext.PaymentTransactions.CountAsync()).Should().Be(1);
            (await verifyContext.PaymentWebhookEvents.CountAsync()).Should().Be(1);
            (await verifyContext.AiSubscriptions.CountAsync()).Should().Be(1);
            (await verifyContext.Notifications.CountAsync(x => x.DedupeKey == $"billing:paid:customer:{order.Id:N}")).Should().Be(1);
            (await verifyContext.PaymentEmailDeliveries.CountAsync(x => x.IsAutomatic && x.Kind == "CustomerPaymentReceipt")).Should().Be(1);
            receiptA.ReceiptNumber.Should().Be(receiptB.ReceiptNumber);
        }
        finally
        {
            await setupContext.Database.EnsureDeletedAsync();
        }
    }

    [Fact]
    [Trait("Category", "SqlServerIntegration")]
    public async Task ConcurrentDistinctProviderPaymentsBothFulfill()
    {
        var databaseName = $"TaskManagement_PaymentHotfix_{Guid.NewGuid():N}";
        await using var setupContext = CreateContext(databaseName);
        await setupContext.Database.MigrateAsync();
        try
        {
            var first = await SeedPendingOrderAsync(setupContext, "plus", 1200);
            var second = await SeedPendingOrderAsync(setupContext, "plus", 1200);
            var firstWebhook = ValidWebhook(first.Order, "provider-distinct-1");
            var secondWebhook = ValidWebhook(second.Order, "provider-distinct-2");
            using var barrier = new Barrier(2);

            async Task ProcessAsync(PaymentWebhookVerificationResult webhook)
            {
                await using var context = CreateContext(databaseName);
                var billing = new BillingService(context, new AiCreditUsageService(context));
                barrier.SignalAndWait();
                await billing.ProcessProviderPaymentAsync("sepay", webhook, "{distinct:true}");
            }

            await Task.WhenAll(
                Task.Run(() => ProcessAsync(firstWebhook)),
                Task.Run(() => ProcessAsync(secondWebhook)));

            await using var verifyContext = CreateContext(databaseName);
            (await verifyContext.PaymentOrders.CountAsync(x => x.Status == "Paid")).Should().Be(2);
            (await verifyContext.PaymentTransactions.CountAsync()).Should().Be(2);
            (await verifyContext.PaymentWebhookEvents.CountAsync()).Should().Be(2);
            (await verifyContext.AiSubscriptions.CountAsync()).Should().Be(2);
        }
        finally
        {
            await setupContext.Database.EnsureDeletedAsync();
        }
    }

    [Fact]
    [Trait("Category", "SqlServerIntegration")]
    public async Task SequentialRenewalPreservesEachReceiptSnapshot()
    {
        var databaseName = $"TaskManagement_PaymentHotfix_{Guid.NewGuid():N}";
        await using var context = CreateContext(databaseName);
        await context.Database.MigrateAsync();
        try
        {
            var (user, firstOrder) = await SeedPendingOrderAsync(context, "plus", 1200);
            var admin = new User { Id = Guid.NewGuid(), Email = "renewal-runtime-admin@test.local", FullName = "Admin", PasswordHash = "test", CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow };
            context.Users.Add(admin);
            await context.SaveChangesAsync();
            var billing = new BillingService(context, new AiCreditUsageService(context));

            await billing.ApproveOrderAsync(firstOrder.Id, admin.Id, "first runtime payment");
            var firstReceipt = await billing.GetReceiptAsync(firstOrder.Id, user.Id, false);
            var secondOrder = await billing.CreateOrderAsync(user.Id, "plus");
            await billing.ApproveOrderAsync(secondOrder.Id, admin.Id, "second runtime payment");
            var secondReceipt = await billing.GetReceiptAsync(secondOrder.Id, user.Id, false);

            (await context.PaymentTransactions.CountAsync()).Should().Be(2);
            (await context.AiSubscriptions.CountAsync()).Should().Be(1);
            firstReceipt.IncludedAiCredits.Should().Be(1200);
            secondReceipt.IncludedAiCredits.Should().Be(1200);
            firstReceipt.SubscriptionPeriodStart.Should().NotBe(secondReceipt.SubscriptionPeriodStart);
            firstReceipt.ReceiptNumber.Should().NotBe(secondReceipt.ReceiptNumber);
        }
        finally
        {
            await context.Database.EnsureDeletedAsync();
        }
    }

    [Fact]
    [Trait("Category", "SqlServerIntegration")]
    public async Task CreditPeriodBoundariesAndReconciliationWorkOnSqlServer()
    {
        var databaseName = $"TaskManagement_PaymentHotfix_{Guid.NewGuid():N}";
        await using var context = CreateContext(databaseName);
        await context.Database.MigrateAsync();
        try
        {
            var now = DateTime.UtcNow;
            var start = now.AddDays(-10);
            var end = now.AddDays(20);
            var user = new User { Id = Guid.NewGuid(), Email = "credit-runtime@test.local", FullName = "Credit User", PasswordHash = "test", CreatedAt = now, UpdatedAt = now };
            var expiredUser = new User { Id = Guid.NewGuid(), Email = "expired-credit-runtime@test.local", FullName = "Expired User", PasswordHash = "test", CreatedAt = now, UpdatedAt = now };
            var plan = new AiPricingPlan { Id = Guid.NewGuid(), Code = "sql-credit-plus", Name = "SQL Credit Plus", MonthlyPriceVnd = 99000, IncludedAiCredits = 100, IsPublished = true, CreatedAt = now, UpdatedAt = now };
            var workspace = new Workspace { Id = Guid.NewGuid(), Slug = $"sql-credit-{Guid.NewGuid():N}", Name = "SQL Credit Workspace", Timezone = "UTC", OwnerId = user.Id, CreatedAt = now, UpdatedAt = now };
            context.Users.AddRange(user, expiredUser);
            context.AiPricingPlans.Add(plan);
            context.Workspaces.Add(workspace);
            context.AiSubscriptions.AddRange(
                new AiSubscription { Id = Guid.NewGuid(), UserId = user.Id, PlanCode = plan.Code, Status = "Active", CurrentPeriodStart = start, CurrentPeriodEnd = end, ActivatedAt = start, AutoRenew = false, CreatedAt = start, UpdatedAt = now },
                new AiSubscription { Id = Guid.NewGuid(), UserId = expiredUser.Id, PlanCode = plan.Code, Status = "Active", CurrentPeriodStart = now.AddDays(-40), CurrentPeriodEnd = now.AddDays(-10), ActivatedAt = now.AddDays(-40), AutoRenew = false, CreatedAt = now.AddDays(-40), UpdatedAt = now.AddDays(-10) });
            context.AITokenUsages.AddRange(
                new AITokenUsage { Id = Guid.NewGuid(), UserId = user.Id, FeatureCode = "before", TokensUsed = 9000, CreatedAt = start.AddTicks(-1) },
                new AITokenUsage { Id = Guid.NewGuid(), UserId = user.Id, FeatureCode = "at-start", TokensUsed = 1000, CreatedAt = start },
                new AITokenUsage { Id = Guid.NewGuid(), UserId = user.Id, FeatureCode = "at-end", TokensUsed = 9000, CreatedAt = end });
            context.AiUsageLedgerEntries.Add(new AiUsageLedger { Id = Guid.NewGuid(), WorkspaceId = workspace.Id, UserId = user.Id, ActionType = "sql-runtime", CreditsConsumed = 1, ProviderTokens = 1000, IdempotencyKey = $"sql-runtime-{Guid.NewGuid():N}", OccurredAt = start });
            context.AiCreditAdjustments.AddRange(
                new AiCreditAdjustment { Id = Guid.NewGuid(), UserId = user.Id, Amount = 5, AdjustmentType = "Credit", Reason = "positive runtime adjustment", CreatedByUserId = user.Id, EffectivePeriodStart = start.AddDays(-1), EffectivePeriodEnd = end.AddDays(1), CreatedAt = now },
                new AiCreditAdjustment { Id = Guid.NewGuid(), UserId = user.Id, Amount = -2, AdjustmentType = "Credit", Reason = "negative runtime adjustment", CreatedByUserId = user.Id, EffectivePeriodStart = start.AddDays(-1), EffectivePeriodEnd = end.AddDays(1), CreatedAt = now });
            await context.SaveChangesAsync();

            var usage = await new AiCreditUsageService(context).GetUsageAsync(user.Id, DateTime.MinValue, DateTime.MaxValue);
            usage.PlanCode.Should().Be(plan.Code);
            usage.CurrentPeriodStart.Should().Be(start);
            usage.CurrentPeriodEnd.Should().Be(end);
            usage.TotalTokens.Should().Be(1000);
            usage.UsedCredits.Should().Be(1);
            usage.AdjustmentCredits.Should().Be(3);
            usage.RemainingCredits.Should().Be(102);
            usage.UsageSource.Should().Be("reconciled-ledger-and-token-usage");

            var expiredUsage = await new AiCreditUsageService(context).GetUsageAsync(expiredUser.Id, DateTime.MinValue, DateTime.MaxValue);
            expiredUsage.PlanCode.Should().Be("free");
            (await context.AiSubscriptions.SingleAsync(x => x.UserId == expiredUser.Id)).Status.Should().Be("Expired");
        }
        finally
        {
            await context.Database.EnsureDeletedAsync();
        }
    }

    [Fact]
    [Trait("Category", "SqlServerIntegration")]
    public async Task ManualRejectEnforcesPendingOnlyTransitionOnSqlServer()
    {
        var databaseName = $"TaskManagement_PaymentHotfix_{Guid.NewGuid():N}";
        await using var context = CreateContext(databaseName);
        await context.Database.MigrateAsync();
        try
        {
            var (_, pendingOrder) = await SeedPendingOrderAsync(context, "plus", 1200);
            var (_, paidCandidate) = await SeedPendingOrderAsync(context, "plus", 1200);
            var admin = new User { Id = Guid.NewGuid(), Email = "reject-runtime-admin@test.local", FullName = "Admin", PasswordHash = "test", CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow };
            context.Users.Add(admin);
            await context.SaveChangesAsync();
            var billing = new BillingService(context, new AiCreditUsageService(context));

            await billing.RejectOrderAsync(pendingOrder.Id, admin.Id, "invalid transfer");
            (await context.PaymentOrders.SingleAsync(x => x.Id == pendingOrder.Id)).Status.Should().Be("Rejected");
            await billing.ApproveOrderAsync(paidCandidate.Id, admin.Id, "valid transfer");
            await billing.Invoking(x => x.RejectOrderAsync(paidCandidate.Id, admin.Id, "cannot reject paid"))
                .Should().ThrowAsync<InvalidOperationException>();
            (await context.PaymentOrders.SingleAsync(x => x.Id == paidCandidate.Id)).Status.Should().Be("Paid");
        }
        finally
        {
            await context.Database.EnsureDeletedAsync();
        }
    }

    [Fact]
    [Trait("Category", "SqlServerIntegration")]
    public async Task SearchOrdersReturnsGlobalAggregateAlongsidePagedRows()
    {
        var databaseName = $"TaskManagement_PaymentHotfix_{Guid.NewGuid():N}";
        await using var context = CreateContext(databaseName);
        await context.Database.MigrateAsync();
        try
        {
            var first = await SeedPendingOrderAsync(context, "plus", 1200);
            var second = await SeedPendingOrderAsync(context, "plus", 1200);
            await SeedPendingOrderAsync(context, "plus", 1200);
            var admin = new User { Id = Guid.NewGuid(), Email = "aggregate-runtime-admin@test.local", FullName = "Admin", PasswordHash = "test", CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow };
            context.Users.Add(admin);
            await context.SaveChangesAsync();
            var billing = new BillingService(context, new AiCreditUsageService(context));
            await billing.ApproveOrderAsync(first.Order.Id, admin.Id, "aggregate first");
            await billing.ApproveOrderAsync(second.Order.Id, admin.Id, "aggregate second");

            var page = await billing.SearchOrdersAsync(null, new BillingOrderQuery { Page = 1, PageSize = 1 });

            page.Items.Should().HaveCount(1);
            page.TotalCount.Should().Be(3);
            page.Summary.TotalCount.Should().Be(3);
            page.Summary.SuccessfulPayments.Should().Be(2);
            page.Summary.PendingPayments.Should().Be(1);
            page.Summary.RevenueVnd.Should().Be(198000);
        }
        finally
        {
            await context.Database.EnsureDeletedAsync();
        }
    }

    [Fact]
    [Trait("Category", "SqlServerIntegration")]
    public async Task ConcurrentCreateOrderRequestsReuseOneLivePendingOrder()
    {
        var databaseName = $"TaskManagement_PaymentHotfix_{Guid.NewGuid():N}";
        await using var setupContext = CreateContext(databaseName);
        await setupContext.Database.MigrateAsync();
        try
        {
            var now = DateTime.UtcNow;
            var user = new User { Id = Guid.NewGuid(), Email = $"checkout-{Guid.NewGuid():N}@test.local", FullName = "Checkout User", PasswordHash = "test", CreatedAt = now, UpdatedAt = now };
            setupContext.Users.Add(user);
            setupContext.AiPricingPlans.Add(new AiPricingPlan { Id = Guid.NewGuid(), Code = "concurrent-plus", Name = "Concurrent Plus", MonthlyPriceVnd = 99000, IncludedAiCredits = 1200, IsPublished = true, CreatedAt = now, UpdatedAt = now });
            await setupContext.SaveChangesAsync();

            var results = await Task.WhenAll(Enumerable.Range(0, 6).Select(async _ =>
            {
                await using var context = CreateContext(databaseName);
                return await new BillingService(context, new AiCreditUsageService(context)).CreateOrderAsync(user.Id, "concurrent-plus");
            }));

            results.Select(order => order.Id).Distinct().Should().ContainSingle();
            (await setupContext.PaymentOrders.CountAsync(order => order.UserId == user.Id && order.PlanCode == "concurrent-plus" && order.Status == "Pending")).Should().Be(1);
        }
        finally
        {
            await setupContext.Database.EnsureDeletedAsync();
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
