using System.Security.Cryptography;
using System.Text;
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

    private static string Sign(string secret, string timestamp, string body)
    {
        var hash = HMACSHA256.HashData(Encoding.UTF8.GetBytes(secret), Encoding.UTF8.GetBytes($"{timestamp}.{body}"));
        return "sha256=" + Convert.ToHexString(hash).ToLowerInvariant();
    }
}

public sealed class PaymentHttpApplicationFactory : WebApplicationFactory<Program>
{
    private readonly string _databaseName = $"payment-http-{Guid.NewGuid():N}";

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");
        builder.ConfigureAppConfiguration((_, configuration) => configuration.AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Jwt:SecretKey"] = "payment-http-testing-signing-key-1234567890",
            ["Security:RequireHttpsMetadata"] = "false",
            ["Database:Provider"] = "InMemory",
            ["Database:InMemoryName"] = _databaseName,
            ["PaymentProviders:SePay:Enabled"] = "true",
            ["PaymentProviders:SePay:WebhookSecret"] = "test-sepay-webhook-secret",
            ["PaymentProviders:SePay:BankCode"] = "VietinBank",
            ["PaymentProviders:SePay:AccountNumber"] = "0000000000",
            ["OpenApi:Enabled"] = "false",
            ["DataProtection:KeysPath"] = Path.Combine(Path.GetTempPath(), $"sprinta-payment-keys-{Guid.NewGuid():N}")
        }));
        builder.ConfigureServices(services =>
        {
            services.RemoveAll<ApplicationDbContext>();
            services.RemoveAll<DbContextOptions<ApplicationDbContext>>();
            services.AddDbContext<ApplicationDbContext>(options => options.UseInMemoryDatabase(_databaseName));
        });
    }
}
