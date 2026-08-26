using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Moq;
using TaskManagement.Application.Interfaces;
using TaskManagement.Domain.Entities;
using TaskManagement.Infrastructure.Data;
using TaskManagement.Infrastructure.Services;

namespace TaskManagement.Tests.Logic;

public sealed class BillingSupportTests
{
    [Fact]
    public async Task PaidWebhook_IsIdempotentAndCreatesOneCustomerDelivery()
    {
        await using var context = CreateContext();
        var user = AddUser(context, "billing-support@test.local");
        context.AiPricingPlans.Add(new AiPricingPlan { Id = Guid.NewGuid(), Code = "starter", Name = "Starter", MonthlyPriceVnd = 49000, IncludedAiCredits = 500, IsPublished = true, CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow });
        var order = new PaymentOrder { Id = Guid.NewGuid(), UserId = user.Id, User = user, PlanCode = "starter", PlanNameSnapshot = "Starter", IncludedAiCreditsSnapshot = 500, AmountVnd = 49000, Currency = "VND", Provider = "sepay", Status = "Pending", TransferCode = "SEVQR SPA-SUPPORT", CreatedAt = DateTime.UtcNow, ExpiresAt = DateTime.UtcNow.AddMinutes(20) };
        context.PaymentOrders.Add(order);
        await context.SaveChangesAsync();

        var email = new Mock<IEmailService>();
        email.Setup(x => x.SendPaymentReceiptEmailAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<decimal>(), It.IsAny<string>(), It.IsAny<DateTime>())).ReturnsAsync("resend-1");
        var notifications = new Mock<INotificationService>();
        notifications.Setup(x => x.SendNotificationOnceAsync(It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<Guid?>())).ReturnsAsync(true);
        var billing = new BillingService(context, new AiCreditUsageService(context), notificationService: notifications.Object, emailService: email.Object);
        var webhook = new PaymentWebhookVerificationResult { IsValid = true, ProviderEventId = "support-event-1", TransactionType = "in", Amount = 49000, TransferContent = "SEVQR SPA-SUPPORT", ProviderReference = "bank-ref-1" };

        await billing.ProcessProviderPaymentAsync("sepay", webhook, "{\"id\":\"support-event-1\"}");
        await billing.ProcessProviderPaymentAsync("sepay", webhook, "{\"id\":\"support-event-1\"}");

        (await context.PaymentTransactions.CountAsync()).Should().Be(1);
        (await context.AiSubscriptions.CountAsync()).Should().Be(1);
        (await context.PaymentEmailDeliveries.CountAsync(x => x.IsAutomatic)).Should().Be(1);
        email.Verify(x => x.SendPaymentReceiptEmailAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<decimal>(), It.IsAny<string>(), It.IsAny<DateTime>()), Times.Once);
    }

    [Fact]
    public async Task Receipt_IsDeterministicAndDoesNotExposeWebhookPayload()
    {
        await using var context = CreateContext();
        var user = AddUser(context, "receipt-support@test.local");
        var order = new PaymentOrder { Id = Guid.NewGuid(), UserId = user.Id, User = user, PlanCode = "starter", PlanNameSnapshot = "Starter", IncludedAiCreditsSnapshot = 500, AmountVnd = 49000, Currency = "VND", Provider = "sepay", Status = "Paid", TransferCode = "SEVQR SPA-RECEIPT", CreatedAt = DateTime.UtcNow.AddMinutes(-2), PaidAt = DateTime.UtcNow.AddMinutes(-1) };
        order.Transactions.Add(new PaymentTransaction { Id = Guid.NewGuid(), PaymentOrderId = order.Id, Provider = "sepay", ProviderTransactionId = "tx-1", ProviderReference = "ref-1", Amount = order.AmountVnd, Currency = "VND", Status = "Paid", PaidAt = order.PaidAt.Value, CreatedAt = order.PaidAt.Value, IncludedAiCredits = 500, SubscriptionPeriodStart = order.PaidAt.Value, SubscriptionPeriodEnd = order.PaidAt.Value.AddMonths(1) });
        context.PaymentOrders.Add(order);
        context.PaymentWebhookEvents.Add(new PaymentWebhookEvent { Id = Guid.NewGuid(), Provider = "sepay", ProviderEventId = "tx-1", EventType = "payment", RawPayload = "SECRET_RAW_WEBHOOK", PaymentOrderId = order.Id, Status = "Processed", ReceivedAt = order.PaidAt.Value });
        await context.SaveChangesAsync();
        var billing = new BillingService(context, new AiCreditUsageService(context));

        var first = await billing.GetReceiptAsync(order.Id, user.Id, false);
        var second = await billing.GetReceiptAsync(order.Id, user.Id, false);
        var detail = await billing.GetOrderDetailsAsync(order.Id, user.Id, false);

        first.ReceiptNumber.Should().Be(second.ReceiptNumber);
        first.IsTaxInvoice.Should().BeFalse();
        first.SubscriptionPeriodStart.Should().NotBeNull();
        first.Order.PaidAt!.Value.Kind.Should().Be(DateTimeKind.Utc);
        first.SubscriptionPeriodStart!.Value.Kind.Should().Be(DateTimeKind.Utc);
        first.SubscriptionPeriodEnd!.Value.Kind.Should().Be(DateTimeKind.Utc);
        detail.Timeline.Where(item => item.OccurredAt.HasValue).Should().AllSatisfy(item => item.OccurredAt!.Value.Kind.Should().Be(DateTimeKind.Utc));
        detail.Timeline.Select(x => x.Note).Should().NotContain("SECRET_RAW_WEBHOOK");
    }

    [Fact]
    public async Task EmailFailure_DoesNotRollbackPaidStateAndIsTrackedAsFailed()
    {
        await using var context = CreateContext();
        var user = AddUser(context, "email-failure@test.local");
        context.AiPricingPlans.Add(new AiPricingPlan { Id = Guid.NewGuid(), Code = "starter", Name = "Starter", MonthlyPriceVnd = 49000, IncludedAiCredits = 500, IsPublished = true, CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow });
        var order = new PaymentOrder { Id = Guid.NewGuid(), UserId = user.Id, User = user, PlanCode = "starter", PlanNameSnapshot = "Starter", IncludedAiCreditsSnapshot = 500, AmountVnd = 49000, Currency = "VND", Provider = "sepay", Status = "Pending", TransferCode = "SEVQR SPA-EMAIL-FAIL", CreatedAt = DateTime.UtcNow, ExpiresAt = DateTime.UtcNow.AddMinutes(20) };
        context.PaymentOrders.Add(order);
        await context.SaveChangesAsync();
        var email = new Mock<IEmailService>();
        email.Setup(x => x.SendPaymentReceiptEmailAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<decimal>(), It.IsAny<string>(), It.IsAny<DateTime>())).ThrowsAsync(new InvalidOperationException("provider unavailable"));
        var billing = new BillingService(context, new AiCreditUsageService(context), emailService: email.Object);

        await billing.ProcessProviderPaymentAsync("sepay", new PaymentWebhookVerificationResult { IsValid = true, ProviderEventId = "email-fail-event", TransactionType = "in", Amount = 49000, TransferContent = "SEVQR SPA-EMAIL-FAIL" }, "{\"id\":\"email-fail-event\"}");

        (await context.PaymentOrders.SingleAsync()).Status.Should().Be("Paid");
        (await context.PaymentTransactions.CountAsync()).Should().Be(1);
        (await context.AiSubscriptions.SingleAsync()).Status.Should().Be("Active");
        (await context.PaymentEmailDeliveries.SingleAsync()).Status.Should().Be("Failed");
    }

    [Fact]
    public async Task ManualResend_CreatesNewAttemptWithoutChangingPaymentState()
    {
        await using var context = CreateContext();
        var user = AddUser(context, "resend@test.local");
        var paidAt = DateTime.UtcNow.AddMinutes(-1);
        var order = new PaymentOrder { Id = Guid.NewGuid(), UserId = user.Id, User = user, PlanCode = "starter", PlanNameSnapshot = "Starter", IncludedAiCreditsSnapshot = 500, AmountVnd = 49000, Currency = "VND", Provider = "sepay", Status = "Paid", TransferCode = "SEVQR SPA-RESEND", CreatedAt = paidAt.AddMinutes(-1), PaidAt = paidAt };
        context.PaymentOrders.Add(order);
        await context.SaveChangesAsync();
        var email = new Mock<IEmailService>();
        email.Setup(x => x.SendPaymentReceiptEmailAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<decimal>(), It.IsAny<string>(), It.IsAny<DateTime>())).ReturnsAsync("resend-id");
        var billing = new BillingService(context, new AiCreditUsageService(context), emailService: email.Object);

        var first = await billing.ResendReceiptAsync(order.Id, user.Id, false);
        var second = await billing.ResendReceiptAsync(order.Id, user.Id, false);

        first.Attempt.Should().Be(1);
        second.Attempt.Should().Be(2);
        (await context.PaymentOrders.SingleAsync()).Status.Should().Be("Paid");
        (await context.PaymentEmailDeliveries.CountAsync()).Should().Be(2);
    }

    [Fact]
    public async Task LegitimateSecondPayment_CreatesSecondTransactionWithoutDuplicateSubscription()
    {
        await using var context = CreateContext();
        var user = AddUser(context, "renewal@test.local");
        var admin = AddUser(context, "renewal-admin@test.local");
        context.AiPricingPlans.Add(new AiPricingPlan { Id = Guid.NewGuid(), Code = "starter", Name = "Starter", MonthlyPriceVnd = 49000, IncludedAiCredits = 500, IsPublished = true, CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow });
        await context.SaveChangesAsync();
        var billing = new BillingService(context, new AiCreditUsageService(context));
        var first = await billing.CreateOrderAsync(user.Id, "starter");
        await billing.ApproveOrderAsync(first.Id, admin.Id, "first payment");
        var second = await billing.CreateOrderAsync(user.Id, "starter");
        await billing.ApproveOrderAsync(second.Id, admin.Id, "renewal payment");

        (await context.PaymentTransactions.CountAsync()).Should().Be(2);
        (await context.AiSubscriptions.CountAsync()).Should().Be(1);
        (await context.PaymentOrders.CountAsync(x => x.Status == "Paid")).Should().Be(2);
        (await context.PaymentTransactions.ToListAsync()).Should().AllSatisfy(transaction => transaction.PaidAt.Kind.Should().Be(DateTimeKind.Utc));
        var subscription = await context.AiSubscriptions.SingleAsync();
        subscription.CurrentPeriodStart.Kind.Should().Be(DateTimeKind.Utc);
        subscription.CurrentPeriodEnd.Kind.Should().Be(DateTimeKind.Utc);
    }

    [Fact]
    public async Task ExpiredPendingOrder_IsClosedBeforeCreatingReplacement_AndSubscriptionStaysUnchanged()
    {
        await using var context = CreateContext();
        var user = AddUser(context, "expired-order@test.local");
        var now = DateTime.UtcNow;
        context.AiPricingPlans.Add(new AiPricingPlan { Id = Guid.NewGuid(), Code = "pro", Name = "Pro", MonthlyPriceVnd = 199000, IncludedAiCredits = 3000, IsPublished = true, CreatedAt = now, UpdatedAt = now });
        context.AiSubscriptions.Add(new AiSubscription { Id = Guid.NewGuid(), UserId = user.Id, User = user, PlanCode = "plus", Status = "Active", CurrentPeriodStart = now.AddDays(-5), CurrentPeriodEnd = now.AddDays(25), CreatedAt = now.AddDays(-5), UpdatedAt = now });
        var expired = new PaymentOrder { Id = Guid.NewGuid(), UserId = user.Id, User = user, PlanCode = "pro", PlanNameSnapshot = "Pro", IncludedAiCreditsSnapshot = 3000, AmountVnd = 199000, Currency = "VND", Provider = "sepay", Status = "Pending", TransferCode = "SEVQR SPA-EXPIRED", CreatedAt = now.AddMinutes(-40), ExpiresAt = now.AddMinutes(-10) };
        context.PaymentOrders.Add(expired);
        await context.SaveChangesAsync();

        var billing = new BillingService(context, new AiCreditUsageService(context));
        var replacement = await billing.CreateOrderAsync(user.Id, "pro");

        replacement.Status.Should().Be("Pending");
        replacement.Id.Should().NotBe(expired.Id);
        (await context.PaymentOrders.SingleAsync(order => order.Id == expired.Id)).Status.Should().Be("Expired");
        (await context.AiSubscriptions.SingleAsync()).PlanCode.Should().Be("plus");
        (await context.PaymentOrders.CountAsync(order => order.Status == "Pending")).Should().Be(1);
    }

    [Fact]
    public async Task PendingAndExpiredOrders_CannotExposeReceipt()
    {
        await using var context = CreateContext();
        var user = AddUser(context, "receipt-eligibility@test.local");
        var now = DateTime.UtcNow;
        var pending = new PaymentOrder { Id = Guid.NewGuid(), UserId = user.Id, User = user, PlanCode = "pro", PlanNameSnapshot = "Pro", AmountVnd = 199000, Provider = "sepay", Status = "Pending", TransferCode = "SEVQR SPA-PENDING", CreatedAt = now, ExpiresAt = now.AddMinutes(20) };
        var expired = new PaymentOrder { Id = Guid.NewGuid(), UserId = user.Id, User = user, PlanCode = "pro", PlanNameSnapshot = "Pro", AmountVnd = 199000, Provider = "sepay", Status = "Expired", TransferCode = "SEVQR SPA-EXPIRED-RECEIPT", CreatedAt = now.AddMinutes(-40), ExpiresAt = now.AddMinutes(-10) };
        context.PaymentOrders.AddRange(pending, expired);
        await context.SaveChangesAsync();
        var billing = new BillingService(context, new AiCreditUsageService(context));

        Func<Task> pendingReceipt = () => billing.GetReceiptAsync(pending.Id, user.Id, false);
        Func<Task> expiredReceipt = () => billing.GetReceiptAsync(expired.Id, user.Id, false);
        await pendingReceipt.Should().ThrowAsync<KeyNotFoundException>();
        await expiredReceipt.Should().ThrowAsync<KeyNotFoundException>();
    }

    [Fact]
    public async Task NegativeCreditAdjustment_ReducesCurrentPeriodEntitlement()
    {
        await using var context = CreateContext();
        var user = AddUser(context, "negative-adjustment@test.local");
        var admin = AddUser(context, "negative-adjustment-admin@test.local");
        context.AiPricingPlans.Add(new AiPricingPlan { Id = Guid.NewGuid(), Code = "free", Name = "Free", MonthlyPriceVnd = 0, IncludedAiCredits = 100, IsPublished = true, CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow });
        await context.SaveChangesAsync();
        var summary = await new BillingService(context, new AiCreditUsageService(context)).AddAdjustmentAsync(user.Id, -10, admin.Id, "correction");

        summary.AdjustmentCredits.Should().Be(-10);
        summary.RemainingCredits.Should().Be(90);
    }

    private static ApplicationDbContext CreateContext() => new(new DbContextOptionsBuilder<ApplicationDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);

    private static User AddUser(ApplicationDbContext context, string email)
    {
        var user = new User { Id = Guid.NewGuid(), Email = email, FullName = "Billing Tester", PasswordHash = "test", CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow };
        context.Users.Add(user);
        return user;
    }
}
