using System.Security.Cryptography;
using System.Text;
using System.Globalization;
using System.Text.Json;
using FluentAssertions;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using TaskManagement.Infrastructure.Data;
using TaskManagement.Infrastructure.Services;
using TaskManagement.Domain.Entities;
using TaskManagement.Application.Interfaces;
using TaskManagement.Infrastructure.Payments;

namespace TaskManagement.Tests.Logic;

public sealed class PaymentP0FoundationTests
{
    [Fact]
    public async Task SePayWebhook_RejectsInvalidSignature()
    {
        var provider = Provider("secret");
        var result = await provider.VerifyWebhookAsync("{}", "sha256=bad", DateTimeOffset.UtcNow.ToUnixTimeSeconds().ToString());
        result.IsValid.Should().BeFalse();
    }

    [Fact]
    public async Task SePayWebhook_RequiresIncomingExactAccountAndParsesReference()
    {
        const string body = "{\"id\":12345,\"transferType\":\"in\",\"accountNumber\":\"123456789\",\"transferAmount\":99000,\"content\":\"SEVQR SPA123\",\"referenceCode\":\"FT001\"}";
        const string secret = "secret";
        var timestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds().ToString();
        var hash = HMACSHA256.HashData(Encoding.UTF8.GetBytes(secret), Encoding.UTF8.GetBytes($"{timestamp}.{body}"));
        var signature = "sha256=" + Convert.ToHexString(hash).ToLowerInvariant();

        var result = await Provider(secret).VerifyWebhookAsync(body, signature, timestamp);

        result.IsValid.Should().BeTrue();
        result.ProviderEventId.Should().Be("12345");
        result.Amount.Should().Be(99000);
        result.TransferContent.Should().Contain("SPA123");
    }

    [Fact]
    public async Task SePayWebhook_InterpretsOffsetlessTransactionDateAsVietnamTime()
    {
        var result = await VerifyTransactionDate("2026-08-22 20:55:39");

        result.TransactionAt.Should().Be(new DateTimeOffset(2026, 8, 22, 13, 55, 39, TimeSpan.Zero));
    }

    [Theory]
    [InlineData("2026-08-22 20:55:39+07:00", "2026-08-22T13:55:39.0000000+00:00")]
    [InlineData("2026-08-22T13:55:39Z", "2026-08-22T13:55:39.0000000+00:00")]
    public async Task SePayWebhook_HonorsExplicitTimestampOffsets(string rawTimestamp, string expectedUtc)
    {
        var result = await VerifyTransactionDate(rawTimestamp);

        result.TransactionAt.Should().Be(DateTimeOffset.Parse(expectedUtc, CultureInfo.InvariantCulture));
    }

    [Fact]
    public async Task SePayWebhook_NullTransactionDateReturnsNullWithoutThrowing()
    {
        var result = await VerifyTransactionDate(null);

        result.TransactionAt.Should().BeNull();
    }

    [Fact]
    public async Task SePayWebhook_EmptyTransactionDateReturnsNullWithoutThrowing()
    {
        var result = await VerifyTransactionDate(string.Empty);

        result.TransactionAt.Should().BeNull();
    }

    [Fact]
    public async Task SePayWebhook_WhitespaceTransactionDateReturnsNullWithoutThrowing()
    {
        var result = await VerifyTransactionDate("   ");

        result.TransactionAt.Should().BeNull();
    }

    [Theory]
    [InlineData("not-a-date")]
    [InlineData("2026-99-99 25:61:61")]
    public async Task SePayWebhook_MalformedTransactionDateReturnsNullWithoutThrowing(string rawTimestamp)
    {
        var result = await VerifyTransactionDate(rawTimestamp);

        result.TransactionAt.Should().BeNull();
    }

    [Fact]
    public async Task ProviderPayment_UsesNormalizedUtcInstantForEntitlementAndCredits()
    {
        await using var context = new ApplicationDbContext(new DbContextOptionsBuilder<ApplicationDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);
        var now = DateTime.UtcNow;
        var user = new User { Id = Guid.NewGuid(), Email = "timezone-user@test.local", FullName = "Timezone User", PasswordHash = "test", CreatedAt = now, UpdatedAt = now };
        var plan = new AiPricingPlan { Id = Guid.NewGuid(), Code = "pro", Name = "Pro", MonthlyPriceVnd = 199000, IncludedAiCredits = 3000, IsPublished = true, CreatedAt = now, UpdatedAt = now };
        var order = new PaymentOrder { Id = Guid.NewGuid(), UserId = user.Id, User = user, PlanCode = "pro", PlanNameSnapshot = "Pro", IncludedAiCreditsSnapshot = 3000, AmountVnd = 199000, Currency = "VND", Provider = "sepay", Status = "Pending", TransferCode = "SEVQR SPA-TZ", CreatedAt = now, ExpiresAt = now.AddMinutes(20) };
        context.Users.Add(user);
        context.AiPricingPlans.Add(plan);
        context.PaymentOrders.Add(order);
        await context.SaveChangesAsync();

        var paidAt = DateTimeOffset.UtcNow.AddMinutes(-1);
        var billing = new BillingService(context, new AiCreditUsageService(context));
        await billing.ProcessProviderPaymentAsync("sepay", new PaymentWebhookVerificationResult
        {
            IsValid = true, ProviderEventId = "timezone-event", TransactionType = "in", Amount = order.AmountVnd,
            TransferContent = order.TransferCode, TransactionAt = paidAt.ToOffset(TimeSpan.FromHours(7))
        }, "{\"id\":\"timezone-event\"}");

        var savedOrder = await context.PaymentOrders.SingleAsync();
        var subscription = await context.AiSubscriptions.SingleAsync();
        var transaction = await context.PaymentTransactions.SingleAsync();
        var usage = await new AiCreditUsageService(context).GetUsageAsync(user.Id, DateTime.MinValue, DateTime.MaxValue);

        savedOrder.PaidAt.Should().Be(paidAt.UtcDateTime);
        transaction.PaidAt.Should().Be(paidAt.UtcDateTime);
        subscription.CurrentPeriodStart.Should().Be(paidAt.UtcDateTime);
        subscription.CurrentPeriodEnd.Should().Be(paidAt.UtcDateTime.AddMonths(1));
        subscription.ActivatedAt.Should().Be(paidAt.UtcDateTime);
        subscription.ActivatedAt!.Value.Kind.Should().Be(DateTimeKind.Utc);
        transaction.CreatedAt.Kind.Should().Be(DateTimeKind.Utc);
        var webhook = await context.PaymentWebhookEvents.SingleAsync();
        webhook.ProcessedAt.Should().NotBeNull();
        webhook.ProcessedAt!.Value.Kind.Should().Be(DateTimeKind.Utc);
        usage.PlanCode.Should().Be("pro");
        usage.IncludedCredits.Should().Be(3000);
    }

    [Theory]
    [InlineData(null, "null-fallback-event")]
    [InlineData("not-a-date", "malformed-fallback-event")]
    public async Task ProviderPayment_InvalidProviderTimestampFallsBackToUtcNow(
        string? rawTimestamp,
        string providerEventId)
    {
        await using var context = new ApplicationDbContext(new DbContextOptionsBuilder<ApplicationDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);
        var seedNow = DateTime.UtcNow;
        var user = new User { Id = Guid.NewGuid(), Email = $"fallback-{providerEventId}@test.local", FullName = "Fallback User", PasswordHash = "test", CreatedAt = seedNow, UpdatedAt = seedNow };
        var plan = new AiPricingPlan { Id = Guid.NewGuid(), Code = "pro", Name = "Pro", MonthlyPriceVnd = 99000, IncludedAiCredits = 3000, IsPublished = true, CreatedAt = seedNow, UpdatedAt = seedNow };
        var order = new PaymentOrder { Id = Guid.NewGuid(), UserId = user.Id, User = user, PlanCode = "pro", PlanNameSnapshot = "Pro", IncludedAiCreditsSnapshot = 3000, AmountVnd = 99000, Currency = "VND", Provider = "sepay", Status = "Pending", TransferCode = $"SEVQR {providerEventId}", CreatedAt = seedNow, ExpiresAt = seedNow.AddMinutes(20) };
        context.Users.Add(user);
        context.AiPricingPlans.Add(plan);
        context.PaymentOrders.Add(order);
        await context.SaveChangesAsync();

        var webhook = await VerifyTransactionDate(rawTimestamp);
        webhook.TransactionAt.Should().BeNull();
        webhook = new PaymentWebhookVerificationResult
        {
            IsValid = webhook.IsValid,
            ProviderEventId = providerEventId,
            TransactionType = webhook.TransactionType,
            Amount = webhook.Amount,
            TransferContent = order.TransferCode
        };
        var beforeFulfillment = DateTime.UtcNow;
        var billing = new BillingService(context, new AiCreditUsageService(context));
        await billing.ProcessProviderPaymentAsync("sepay", webhook, JsonSerializer.Serialize(new { id = providerEventId }));
        var afterFulfillment = DateTime.UtcNow;

        var savedOrder = await context.PaymentOrders.SingleAsync();
        var subscription = await context.AiSubscriptions.SingleAsync();
        var usage = await new AiCreditUsageService(context).GetUsageAsync(user.Id, DateTime.MinValue, DateTime.MaxValue);
        savedOrder.Status.Should().Be("Paid");
        savedOrder.PaidAt.Should().NotBeNull();
        savedOrder.PaidAt!.Value.Kind.Should().Be(DateTimeKind.Utc);
        savedOrder.PaidAt.Value.Should().BeOnOrAfter(beforeFulfillment).And.BeOnOrBefore(afterFulfillment);
        subscription.Status.Should().Be("Active");
        subscription.CurrentPeriodStart.Should().Be(savedOrder.PaidAt.Value);
        subscription.CurrentPeriodStart.Kind.Should().Be(DateTimeKind.Utc);
        subscription.CurrentPeriodEnd.Should().Be(savedOrder.PaidAt.Value.AddMonths(1));
        subscription.CurrentPeriodEnd.Kind.Should().Be(DateTimeKind.Utc);
        usage.PlanCode.Should().Be("pro");
        usage.IncludedCredits.Should().Be(3000);
    }

    [Fact]
    public async Task SePayWebhook_RejectsReplayTimestamp()
    {
        var provider = Provider("secret");
        var timestamp = DateTimeOffset.UtcNow.AddMinutes(-6).ToUnixTimeSeconds().ToString();
        var body = "{}";
        var hash = HMACSHA256.HashData(Encoding.UTF8.GetBytes("secret"), Encoding.UTF8.GetBytes($"{timestamp}.{body}"));
        var result = await provider.VerifyWebhookAsync(body, "sha256=" + Convert.ToHexString(hash).ToLowerInvariant(), timestamp);
        result.IsValid.Should().BeFalse();
    }

    [Fact]
    public async Task SePayWebhook_RealHttpRequestUsesExactRawBody()
    {
        await using var factory = new PaymentHttpApplicationFactory();
        using var client = factory.CreateClient();
        const string secret = "test-sepay-webhook-secret";
        const string body = "{\"id\":\"http-1\",\"transferType\":\"in\",\"accountNumber\":\"0000000000\",\"transferAmount\":49000,\"content\":\"SEVQR SPAUNKNOWN\"}";
        var timestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds().ToString();
        var signature = Sign(secret, timestamp, body);

        using var validRequest = new HttpRequestMessage(HttpMethod.Post, "/api/payments/webhooks/sepay")
        {
            Content = new StringContent(body, Encoding.UTF8, "application/json")
        };
        validRequest.Headers.Add("X-SePay-Timestamp", timestamp);
        validRequest.Headers.Add("X-SePay-Signature", signature);
        var validResponse = await client.SendAsync(validRequest);
        validResponse.StatusCode.Should().Be(System.Net.HttpStatusCode.OK);

        using var tamperedRequest = new HttpRequestMessage(HttpMethod.Post, "/api/payments/webhooks/sepay")
        {
            Content = new StringContent(body.Replace("49000", "49001", StringComparison.Ordinal), Encoding.UTF8, "application/json")
        };
        tamperedRequest.Headers.Add("X-SePay-Timestamp", timestamp);
        tamperedRequest.Headers.Add("X-SePay-Signature", signature);
        var tamperedResponse = await client.SendAsync(tamperedRequest);
        tamperedResponse.StatusCode.Should().Be(System.Net.HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task SePayWebhook_ProductionEnvironmentKeysBindAndAuthenticateOverHttp()
    {
        const string secret = "production-style-fake-secret";
        var previous = new Dictionary<string, string?>
        {
            ["PaymentProviders__SePay__Enabled"] = Environment.GetEnvironmentVariable("PaymentProviders__SePay__Enabled"),
            ["PaymentProviders__SePay__WebhookSecret"] = Environment.GetEnvironmentVariable("PaymentProviders__SePay__WebhookSecret"),
            ["PaymentProviders__SePay__BankCode"] = Environment.GetEnvironmentVariable("PaymentProviders__SePay__BankCode"),
            ["PaymentProviders__SePay__AccountNumber"] = Environment.GetEnvironmentVariable("PaymentProviders__SePay__AccountNumber"),
            ["PaymentProviders__SePay__AccountName"] = Environment.GetEnvironmentVariable("PaymentProviders__SePay__AccountName")
        };
        try
        {
            Environment.SetEnvironmentVariable("PaymentProviders__SePay__Enabled", "true");
            Environment.SetEnvironmentVariable("PaymentProviders__SePay__WebhookSecret", secret);
            Environment.SetEnvironmentVariable("PaymentProviders__SePay__BankCode", "VietinBank");
            Environment.SetEnvironmentVariable("PaymentProviders__SePay__AccountNumber", "0000000000");
            Environment.SetEnvironmentVariable("PaymentProviders__SePay__AccountName", "FAKE TEST ACCOUNT");

            await using var factory = new PaymentHttpApplicationFactory(useEnvironmentKeys: true);
            using var client = factory.CreateClient();
            const string body = "{\"id\":\"env-http-1\",\"transferType\":\"in\",\"accountNumber\":\"0000000000\",\"transferAmount\":49000,\"content\":\"SEVQR SPAUNKNOWN\"}";
            var timestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds().ToString();
            using var request = new HttpRequestMessage(HttpMethod.Post, "/api/payments/webhooks/sepay")
            {
                Content = new StringContent(body, Encoding.UTF8, "application/json")
            };
            request.Headers.Add("X-SePay-Timestamp", timestamp);
            request.Headers.Add("X-SePay-Signature", Sign(secret, timestamp, body));

            var response = await client.SendAsync(request);
            response.StatusCode.Should().Be(System.Net.HttpStatusCode.OK);
        }
        finally
        {
            foreach (var pair in previous)
                Environment.SetEnvironmentVariable(pair.Key, pair.Value);
        }
    }

    [Fact]
    public async Task SePayWebhook_AuthenticationFailuresDoNotClaimConfigurationMissing()
    {
        await using var factory = new PaymentHttpApplicationFactory();
        using var client = factory.CreateClient();
        const string body = "{\"id\":\"http-auth-1\",\"transferType\":\"in\",\"accountNumber\":\"0000000000\",\"transferAmount\":49000,\"content\":\"SEVQR SPAUNKNOWN\"}";
        var timestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds().ToString();

        async Task<HttpResponseMessage> Send(string? signature, string? requestTimestamp, string requestBody = body)
        {
            using var request = new HttpRequestMessage(HttpMethod.Post, "/api/payments/webhooks/sepay")
            {
                Content = new StringContent(requestBody, Encoding.UTF8, "application/json")
            };
            if (signature != null) request.Headers.Add("X-SePay-Signature", signature);
            if (requestTimestamp != null) request.Headers.Add("X-SePay-Timestamp", requestTimestamp);
            return await client.SendAsync(request);
        }

        var missing = await Send(null, null);
        var wrong = await Send("sha256=wrong", timestamp);
        var tampered = await Send(Sign("test-sepay-webhook-secret", timestamp, body), timestamp, body.Replace("49000", "49001", StringComparison.Ordinal));
        var expiredTimestamp = DateTimeOffset.UtcNow.AddMinutes(-6).ToUnixTimeSeconds().ToString();
        var expired = await Send(Sign("test-sepay-webhook-secret", expiredTimestamp, body), expiredTimestamp);

        missing.StatusCode.Should().Be(System.Net.HttpStatusCode.Unauthorized);
        wrong.StatusCode.Should().Be(System.Net.HttpStatusCode.Unauthorized);
        tampered.StatusCode.Should().Be(System.Net.HttpStatusCode.Unauthorized);
        expired.StatusCode.Should().Be(System.Net.HttpStatusCode.Unauthorized);
        (await missing.Content.ReadAsStringAsync()).Should().NotContain("not configured");
    }

    [Fact]
    public async Task ProviderPayment_DuplicateEventDoesNotActivateTwice()
    {
        await using var context = new ApplicationDbContext(new DbContextOptionsBuilder<ApplicationDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);
        var user = new User { Id = Guid.NewGuid(), Email = "payment-user@test.local", FullName = "Payment User", PasswordHash = "test", CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow };
        context.Users.Add(user);
        context.AiPricingPlans.Add(new AiPricingPlan { Id = Guid.NewGuid(), Code = "starter", Name = "Starter", MonthlyPriceVnd = 49000, IncludedAiCredits = 500, IsPublished = true, CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow });
        var order = new PaymentOrder { Id = Guid.NewGuid(), UserId = user.Id, User = user, PlanCode = "starter", PlanNameSnapshot = "Starter", IncludedAiCreditsSnapshot = 500, AmountVnd = 49000, Currency = "VND", Provider = "sepay", Status = "Pending", TransferCode = "SEVQR SPA123", CreatedAt = DateTime.UtcNow, ExpiresAt = DateTime.UtcNow.AddMinutes(20) };
        context.PaymentOrders.Add(order);
        await context.SaveChangesAsync();
        var billing = new BillingService(context, new AiCreditUsageService(context));
        var webhook = new PaymentWebhookVerificationResult { IsValid = true, ProviderEventId = "12345", TransactionType = "in", Amount = 49000, TransferContent = "SEVQR SPA123", ProviderReference = "FT001" };

        var first = await billing.ProcessProviderPaymentAsync("sepay", webhook, "{\"id\":12345}");
        var second = await billing.ProcessProviderPaymentAsync("sepay", webhook, "{\"id\":12345}");

        first!.Status.Should().Be("Paid");
        second.Should().BeNull();
        (await context.PaymentTransactions.CountAsync()).Should().Be(1);
        (await context.AiSubscriptions.CountAsync()).Should().Be(1);
    }

    [Fact]
    public async Task ProviderPayment_WrongAmountLeavesOrderPending()
    {
        await using var context = new ApplicationDbContext(new DbContextOptionsBuilder<ApplicationDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);
        var user = new User { Id = Guid.NewGuid(), Email = "amount-user@test.local", FullName = "Amount User", PasswordHash = "test", CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow };
        context.Users.Add(user);
        context.AiPricingPlans.Add(new AiPricingPlan { Id = Guid.NewGuid(), Code = "starter", Name = "Starter", MonthlyPriceVnd = 49000, IncludedAiCredits = 500, IsPublished = true, CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow });
        context.PaymentOrders.Add(new PaymentOrder { Id = Guid.NewGuid(), UserId = user.Id, PlanCode = "starter", PlanNameSnapshot = "Starter", AmountVnd = 49000, Currency = "VND", Provider = "sepay", Status = "Pending", TransferCode = "SEVQR SPA999", CreatedAt = DateTime.UtcNow, ExpiresAt = DateTime.UtcNow.AddMinutes(20) });
        await context.SaveChangesAsync();
        var billing = new BillingService(context, new AiCreditUsageService(context));

        await billing.ProcessProviderPaymentAsync("sepay", new PaymentWebhookVerificationResult { IsValid = true, ProviderEventId = "999", TransactionType = "in", Amount = 1, TransferContent = "SEVQR SPA999" }, "{\"id\":999}");

        (await context.PaymentOrders.SingleAsync()).Status.Should().Be("Pending");
        (await context.PaymentTransactions.CountAsync()).Should().Be(0);
        (await context.AiSubscriptions.CountAsync()).Should().Be(0);
    }

    private static SePayPaymentProvider Provider(string secret)
    {
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["PaymentProviders:SePay:WebhookSecret"] = secret,
            ["PaymentProviders:SePay:AccountNumber"] = "123456789"
        }).Build();
        return new SePayPaymentProvider(configuration);
    }

    private static async Task<PaymentWebhookVerificationResult> VerifyTransactionDate(string? transactionDate)
    {
        const string secret = "secret";
        var transactionDateJson = transactionDate is null ? "null" : JsonSerializer.Serialize(transactionDate);
        var body = $"{{\"id\":\"timestamp-test\",\"transferType\":\"in\",\"accountNumber\":\"123456789\",\"transferAmount\":99000,\"content\":\"SEVQR SPA123\",\"transactionDate\":{transactionDateJson}}}";
        var timestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds().ToString(CultureInfo.InvariantCulture);
        var signature = "sha256=" + Convert.ToHexString(HMACSHA256.HashData(Encoding.UTF8.GetBytes(secret), Encoding.UTF8.GetBytes($"{timestamp}.{body}"))).ToLowerInvariant();
        return await Provider(secret).VerifyWebhookAsync(body, signature, timestamp);
    }

    private static string Sign(string secret, string timestamp, string body)
    {
        var hash = HMACSHA256.HashData(Encoding.UTF8.GetBytes(secret), Encoding.UTF8.GetBytes($"{timestamp}.{body}"));
        return "sha256=" + Convert.ToHexString(hash).ToLowerInvariant();
    }
}

public sealed class PaymentHttpApplicationFactory : WebApplicationFactory<Program>
{
    private readonly string _databaseName = $"payment-http-{Guid.NewGuid():N}";
    private readonly bool _useEnvironmentKeys;

    public PaymentHttpApplicationFactory(bool useEnvironmentKeys = false) => _useEnvironmentKeys = useEnvironmentKeys;

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");
        builder.ConfigureAppConfiguration((_, configuration) =>
        {
            var settings = new Dictionary<string, string?>
            {
            ["Jwt:SecretKey"] = "payment-http-testing-signing-key-1234567890",
            ["Security:RequireHttpsMetadata"] = "false",
            ["Database:Provider"] = "InMemory",
            ["Database:InMemoryName"] = _databaseName,
            ["OpenApi:Enabled"] = "false",
            ["DataProtection:KeysPath"] = Path.Combine(Path.GetTempPath(), $"sprinta-payment-keys-{Guid.NewGuid():N}")
            };
            if (!_useEnvironmentKeys)
            {
                settings["PaymentProviders:SePay:Enabled"] = "true";
                settings["PaymentProviders:SePay:WebhookSecret"] = "test-sepay-webhook-secret";
                settings["PaymentProviders:SePay:BankCode"] = "VietinBank";
                settings["PaymentProviders:SePay:AccountNumber"] = "0000000000";
            }
            configuration.AddInMemoryCollection(settings);
            if (_useEnvironmentKeys) configuration.AddEnvironmentVariables();
        });
        builder.ConfigureServices(services =>
        {
            services.RemoveAll<ApplicationDbContext>();
            services.RemoveAll<DbContextOptions<ApplicationDbContext>>();
            services.AddDbContext<ApplicationDbContext>(options => options.UseInMemoryDatabase(_databaseName));
        });
    }
}
