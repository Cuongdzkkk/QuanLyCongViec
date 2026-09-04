using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using TaskManagement.Application.DTOs.Rewards;
using TaskManagement.Domain.Entities;
using TaskManagement.Infrastructure.Data;
using TaskManagement.Infrastructure.Services;

namespace TaskManagement.Tests.Logic;

public sealed class P008ShopRedeemConcurrencyTests
{
    [Fact]
    [Trait("Database", "SqlServer")]
    public async Task TC_SHOP_001_ConcurrentRedeemWithQuantityOne_OnlyOneSucceeds()
    {
        var options = SqlOptions($"TaskManagement_P008_QuantityRace_{Guid.NewGuid():N}");
        await using var setup = new ApplicationDbContext(options);
        try
        {
            await setup.Database.MigrateAsync();
            var seed = await SeedRedeemEnvironmentAsync(setup, quantity: 1, pointCost: 100, walletBalance: 500);

            await using var contextA = new ApplicationDbContext(options);
            await using var contextB = new ApplicationDbContext(options);
            
            var start = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            var first = Task.Run(async () =>
            {
                await start.Task;
                try {
                    await new RewardSystemService(contextA).RedeemRewardAsync(seed.ProjectId, seed.UserId, new RedeemRewardRequest(seed.RewardId));
                    return true;
                } catch { return false; }
            });
            var second = Task.Run(async () =>
            {
                await start.Task;
                try {
                    await new RewardSystemService(contextB).RedeemRewardAsync(seed.ProjectId, seed.UserId, new RedeemRewardRequest(seed.RewardId));
                    return true;
                } catch { return false; }
            });

            start.SetResult();
            var results = await Task.WhenAll(first, second);

            results.Count(r => r).Should().Be(1, "Only one request should succeed due to stock limits.");
            results.Count(r => !r).Should().Be(1, "One request should fail.");

            await using var verification = new ApplicationDbContext(options);
            var wallet = await verification.UserWallets.SingleAsync();
            wallet.TotalPoints.Should().Be(400, "Only 100 points should be deducted");
            
            var reward = await verification.RewardDefinitions.SingleAsync();
            reward.Quantity.Should().Be(0, "Stock should be empty");

            (await verification.PointTransactions.CountAsync(t => t.TransactionType == "Redeem")).Should().Be(1);
            (await verification.RewardGrants.CountAsync()).Should().Be(1);
        }
        finally
        {
            await setup.Database.EnsureDeletedAsync();
        }
    }

    [Fact]
    [Trait("Database", "SqlServer")]
    public async Task TC_SHOP_002_ConcurrentRedeemWithExactBalanceForOne_OnlyOneSucceeds()
    {
        var options = SqlOptions($"TaskManagement_P008_BalanceRace_{Guid.NewGuid():N}");
        await using var setup = new ApplicationDbContext(options);
        try
        {
            await setup.Database.MigrateAsync();
            var seed = await SeedRedeemEnvironmentAsync(setup, quantity: 10, pointCost: 100, walletBalance: 100);

            await using var contextA = new ApplicationDbContext(options);
            await using var contextB = new ApplicationDbContext(options);
            
            var start = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            var first = Task.Run(async () =>
            {
                await start.Task;
                try {
                    await new RewardSystemService(contextA).RedeemRewardAsync(seed.ProjectId, seed.UserId, new RedeemRewardRequest(seed.RewardId));
                    return true;
                } catch { return false; }
            });
            var second = Task.Run(async () =>
            {
                await start.Task;
                try {
                    await new RewardSystemService(contextB).RedeemRewardAsync(seed.ProjectId, seed.UserId, new RedeemRewardRequest(seed.RewardId));
                    return true;
                } catch { return false; }
            });

            start.SetResult();
            var results = await Task.WhenAll(first, second);

            results.Count(r => r).Should().Be(1, "Only one request should succeed due to balance limits.");
            results.Count(r => !r).Should().Be(1, "One request should fail.");

            await using var verification = new ApplicationDbContext(options);
            var wallet = await verification.UserWallets.SingleAsync();
            wallet.TotalPoints.Should().Be(0, "Balance should be exactly 0, not negative");
            
            var reward = await verification.RewardDefinitions.SingleAsync();
            reward.Quantity.Should().Be(9, "Only 1 item should be deducted");

            (await verification.PointTransactions.CountAsync(t => t.TransactionType == "Redeem")).Should().Be(1);
            (await verification.RewardGrants.CountAsync()).Should().Be(1);
        }
        finally
        {
            await setup.Database.EnsureDeletedAsync();
        }
    }

    private static DbContextOptions<ApplicationDbContext> SqlOptions(string databaseName) =>
        new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseSqlServer(SqlServerTestConfiguration.ConnectionString(databaseName))
            .Options;

    private static async Task<(Guid ProjectId, Guid UserId, Guid RewardId)> SeedRedeemEnvironmentAsync(ApplicationDbContext context, int? quantity, int pointCost, int walletBalance)
    {
        var userId = Guid.NewGuid();
        var workspaceId = Guid.NewGuid();
        var projectId = Guid.NewGuid();
        var seasonId = Guid.NewGuid();
        var rewardId = Guid.NewGuid();

        context.Users.Add(new User { Id = userId, Email = $"shop-{userId:N}@example.com", PasswordHash = "unused", IsActive = true });
        context.Workspaces.Add(new Workspace { Id = workspaceId, OwnerId = userId, Name = "Workspace", Slug = $"ws-{workspaceId:N}" });
        context.WorkspaceMembers.Add(new WorkspaceMember { WorkspaceId = workspaceId, UserId = userId, WorkspaceRole = "OWNER", IsActive = true });
        context.Projects.Add(new Project { Id = projectId, WorkspaceId = workspaceId, CreatorId = userId, Name = "Project", Identifier = "RWD", Status = true });
        context.ProjectMembers.Add(new ProjectMember { ProjectId = projectId, UserId = userId, ProjectRole = "PM", Status = true, JoinedAt = DateTime.UtcNow });
        
        context.UserWallets.Add(new UserWallet { UserId = userId, TotalPoints = walletBalance, Level = 1 });
        
        context.RewardSeasons.Add(new RewardSeason { Id = seasonId, ProjectId = projectId, Name = "Current", Type = "Sprint", StartAt = DateTimeOffset.UtcNow.AddDays(-1), EndAt = DateTimeOffset.UtcNow.AddDays(1), Status = "Active", CreatedBy = userId, CreatedAt = DateTimeOffset.UtcNow });
        
        context.RewardDefinitions.Add(new RewardDefinition {
            Id = rewardId, ProjectId = projectId, SeasonId = seasonId, Name = "Shop Item", RewardType = "Gift", ConditionType = "SeasonPoints", ConditionMetric = "SeasonPoints", Method = "Redeem", PointCost = pointCost, Quantity = quantity, IsEnabled = true, CreatedBy = userId, CreatedAt = DateTimeOffset.UtcNow, UpdatedAt = DateTimeOffset.UtcNow
        });

        await context.SaveChangesAsync();
        return (projectId, userId, rewardId);
    }
}
