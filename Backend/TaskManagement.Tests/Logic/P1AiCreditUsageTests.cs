using System.Security.Claims;
using System.Text.Json;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Moq;
using TaskManagement.API.Controllers;
using TaskManagement.Application.Interfaces;
using TaskManagement.Domain.Entities;
using TaskManagement.Infrastructure.Data;
using TaskManagement.Infrastructure.Services;

namespace TaskManagement.Tests.Logic;

public sealed class P1AiCreditUsageTests
{
    [Fact]
    public async Task UsageSummary_UsesPlanAndExposesCanonicalCreditContract()
    {
        var (context, userId) = CreateContext();
        await using (context)
        {
            var now = DateTime.UtcNow;
            context.AiPricingPlans.Add(FreePlan(100));
            context.AITokenUsages.Add(new AITokenUsage
            {
                Id = Guid.NewGuid(), UserId = userId, FeatureCode = "ai-chat", TokensUsed = 2500, CreatedAt = now
            });
            context.AiUsageLedgerEntries.Add(new AiUsageLedger
            {
                Id = Guid.NewGuid(), WorkspaceId = Guid.NewGuid(), UserId = userId,
                ActionType = "ai-chat", CreditsConsumed = 1, ProviderTokens = 2500, OccurredAt = now
            });
            await context.SaveChangesAsync();

            var creditService = new AiCreditUsageService(context);
            var controller = CreateController(context, creditService, userId);
            var result = await controller.UsageSummary(now.AddMinutes(-1), now.AddMinutes(1));

            var ok = result.Should().BeOfType<OkObjectResult>().Subject;
            using var json = JsonDocument.Parse(JsonSerializer.Serialize(ok.Value, new JsonSerializerOptions(JsonSerializerDefaults.Web)));
            var data = json.RootElement.GetProperty("data");
            data.GetProperty("planCode").GetString().Should().Be("free");
            data.GetProperty("includedCredits").GetInt32().Should().Be(100);
            data.GetProperty("usedCredits").GetInt32().Should().Be(3);
            data.GetProperty("remainingCredits").GetInt32().Should().Be(97);
            data.GetProperty("creditsConsumed").GetInt32().Should().Be(3);
            data.GetProperty("remainingIncludedCredits").GetInt32().Should().Be(97);
        }
    }

    [Fact]
    public async Task Usage_ReconcilesLedgerAndTokenRowsWithoutHidingConsumption()
    {
        var (context, userId) = CreateContext();
        await using (context)
        {
            var now = DateTime.UtcNow;
            context.AiPricingPlans.Add(FreePlan(100));
            context.AITokenUsages.Add(new AITokenUsage
            {
                Id = Guid.NewGuid(), UserId = userId, FeatureCode = "ai-chat", TokensUsed = 1500, CreatedAt = now
            });
            context.AiUsageLedgerEntries.Add(new AiUsageLedger
            {
                Id = Guid.NewGuid(), WorkspaceId = Guid.NewGuid(), UserId = userId,
                ActionType = "ai-chat", CreditsConsumed = 5, OccurredAt = now
            });
            await context.SaveChangesAsync();

            var usage = await new AiCreditUsageService(context).GetUsageAsync(userId, now.AddMinutes(-1), now.AddMinutes(1));

            usage.IncludedCredits.Should().Be(100);
            usage.UsedCredits.Should().Be(5);
            usage.RemainingCredits.Should().Be(95);
            usage.UsageSource.Should().Be("reconciled-ledger-and-token-usage");
        }
    }

    [Fact]
    public async Task QuotaGuard_RejectsWhenConfiguredPlanCreditsAreExhausted()
    {
        var (context, userId) = CreateContext();
        await using (context)
        {
            context.AiPricingPlans.Add(FreePlan(2));
            context.AITokenUsages.Add(new AITokenUsage
            {
                Id = Guid.NewGuid(), UserId = userId, FeatureCode = "ai-chat",
                TokensUsed = 2000, CreatedAt = DateTime.UtcNow
            });
            await context.SaveChangesAsync();

            var action = () => new AiCreditUsageService(context).EnsureWithinQuotaAsync(userId);

            await action.Should().ThrowAsync<InvalidOperationException>()
                .WithMessage("*2 AI credits*");
        }
    }

    [Fact]
    public async Task QuotaGuard_DoesNotInventZeroCreditEntitlementWhenPlanIsMissing()
    {
        var (context, userId) = CreateContext();
        await using (context)
        {
            var service = new AiCreditUsageService(context);

            await service.EnsureWithinQuotaAsync(userId);
            var usage = await service.GetUsageAsync(userId, DateTime.UtcNow.AddDays(-1), DateTime.UtcNow);

            usage.HasConfiguredEntitlement.Should().BeFalse();
            usage.EntitlementSource.Should().Be("not-configured");
        }
    }

    private static (ApplicationDbContext Context, Guid UserId) CreateContext()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return (new ApplicationDbContext(options), Guid.NewGuid());
    }

    private static AiPricingPlan FreePlan(int includedCredits) => new()
    {
        Id = Guid.NewGuid(), Code = "free", Name = "Free",
        IncludedAiCredits = includedCredits, PricingStatus = "PendingConfirmation",
        CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow
    };

    private static AiController CreateController(
        ApplicationDbContext context,
        IAiCreditUsageService creditUsageService,
        Guid userId)
    {
        var controller = new AiController(
            Mock.Of<IAiService>(),
            creditUsageService,
            Mock.Of<IAiAttachmentService>(),
            Mock.Of<IWorkTaskService>(),
            Mock.Of<IProjectService>(),
            Mock.Of<IGoalService>(),
            context,
            new ResourceAuthorizationService(context));
        controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext
            {
                User = new ClaimsPrincipal(new ClaimsIdentity(
                    [new Claim(ClaimTypes.NameIdentifier, userId.ToString())],
                    "TestAuth"))
            }
        };
        return controller;
    }
}
