using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using TaskManagement.Domain.Entities;
using TaskManagement.Infrastructure.Data;
using TaskManagement.Infrastructure.Services;

namespace TaskManagement.Tests.Logic;

public sealed class ChannelTextSqlServerTests
{
    private const string ConnectionString =
        "Server=(localdb)\\MSSQLLocalDB;Database=SprintACollabText01IntegrationV2;" +
        "Trusted_Connection=True;MultipleActiveResultSets=true;TrustServerCertificate=True";

    [Fact]
    [Trait("Category", "SqlServerIntegration")]
    public async Task SqlServerPersistsIsolatedOrderedHistoryAndEnforcesForeignKey()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseSqlServer(ConnectionString)
            .Options;
        ChannelSeed? seed = null;
        try
        {
            await using (var setup = new ApplicationDbContext(options))
            {
                await setup.Database.EnsureCreatedAsync();
                seed = await ChannelSeed.InsertAsync(setup);
                var service = new ChannelTextService(setup, new ResourceAuthorizationService(setup));
                await service.SendAsync(seed.ChannelAId, seed.UserAId, "SQL persistence");
            }

            await using (var fresh = new ApplicationDbContext(options))
            {
                var service = new ChannelTextService(fresh, new ResourceAuthorizationService(fresh));
                var history = await service.GetHistoryAsync(seed.ChannelAId, seed.UserBId, 1, 10);
                history.Items.Should().ContainSingle(item => item.Content == "SQL persistence");
                history.TotalCount.Should().Be(1);

                fresh.ChannelMessages.Add(new ChannelMessage
                {
                    Id = Guid.NewGuid(),
                    CollaborationChannelId = Guid.NewGuid(),
                    SenderId = seed.UserAId,
                    Content = "invalid channel",
                    SentAt = DateTime.UtcNow
                });
                await fresh.Invoking(context => context.SaveChangesAsync())
                    .Should().ThrowAsync<DbUpdateException>();
            }

            await using var metadata = new ApplicationDbContext(options);
            var indexExists = await metadata.Database
                .SqlQueryRaw<int>(
                    """
                    SELECT COUNT(*) AS [Value]
                    FROM sys.indexes
                    WHERE object_id = OBJECT_ID(N'[ChannelMessages]')
                      AND name = N'IX_ChannelMessages_CollaborationChannelId_SentAt_Id'
                    """)
                .SingleAsync();
            indexExists.Should().Be(1);
        }
        finally
        {
            if (seed != null) await CleanupAsync(options, seed);
        }
    }

    private static async Task CleanupAsync(
        DbContextOptions<ApplicationDbContext> options,
        ChannelSeed seed)
    {
        await using var context = new ApplicationDbContext(options);
        await context.ChannelMessages
            .Where(message =>
                message.CollaborationChannelId == seed.ChannelAId ||
                message.CollaborationChannelId == seed.ChannelBId ||
                message.CollaborationChannelId == seed.DeletedChannelId)
            .ExecuteDeleteAsync();
        await context.CollaborationChannelMembers
            .Where(member =>
                member.ChannelId == seed.ChannelAId ||
                member.ChannelId == seed.ChannelBId ||
                member.ChannelId == seed.DeletedChannelId)
            .ExecuteDeleteAsync();
        await context.CollaborationChannels
            .Where(channel =>
                channel.Id == seed.ChannelAId ||
                channel.Id == seed.ChannelBId ||
                channel.Id == seed.DeletedChannelId)
            .ExecuteDeleteAsync();
        var userIds = new[] { seed.UserAId, seed.UserBId, seed.OutsiderId, seed.InactiveUserId };
        await context.ProjectMembers.Where(member => userIds.Contains(member.UserId)).ExecuteDeleteAsync();
        await context.WorkspaceMembers.Where(member => userIds.Contains(member.UserId)).ExecuteDeleteAsync();
        var projectIds = await context.Projects
            .Where(project => userIds.Contains(project.CreatorId))
            .Select(project => project.Id)
            .ToListAsync();
        await context.Projects.Where(project => projectIds.Contains(project.Id)).ExecuteDeleteAsync();
        var workspaceIds = await context.Workspaces
            .Where(workspace => userIds.Contains(workspace.OwnerId))
            .Select(workspace => workspace.Id)
            .ToListAsync();
        await context.Workspaces.Where(workspace => workspaceIds.Contains(workspace.Id)).ExecuteDeleteAsync();
        await context.Users.Where(user => userIds.Contains(user.Id)).ExecuteDeleteAsync();
    }
}
