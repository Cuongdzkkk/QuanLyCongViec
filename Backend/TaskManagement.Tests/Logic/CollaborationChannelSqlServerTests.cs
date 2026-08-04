using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using TaskManagement.Application.DTOs.Collaboration;
using TaskManagement.Domain.Entities;
using TaskManagement.Infrastructure.Data;
using TaskManagement.Infrastructure.Services;

namespace TaskManagement.Tests.Logic;

public sealed class CollaborationChannelSqlServerTests
{
    private const string ConnectionString =
        "Server=(localdb)\\MSSQLLocalDB;Database=SprintACollabChannelDiscovery01IntegrationV2;" +
        "Trusted_Connection=True;MultipleActiveResultSets=true;TrustServerCertificate=True";

    [Fact]
    [Trait("Category", "SqlServerIntegration")]
    public async Task SqlServerProvisioningIsAtomicIdempotentConcurrentAndMessageCompatible()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseSqlServer(
                ConnectionString,
                sqlServer => sqlServer.EnableRetryOnFailure())
            .Options;
        DiscoverySeed? seed = null;
        var createdChannelIds = new List<Guid>();
        try
        {
            await using (var setup = new ApplicationDbContext(options))
            {
                await setup.Database.EnsureCreatedAsync();
                seed = await DiscoverySeed.InsertAsync(setup);
                var service = CreateService(setup);
                var first = await service.CreateAsync(
                    seed.ProjectAId,
                    seed.ManagerId,
                    Request("sql-persistent"),
                    "sql-idempotent");
                var retry = await service.CreateAsync(
                    seed.ProjectAId,
                    seed.ManagerId,
                    Request("sql-persistent"),
                    "sql-idempotent");
                retry.Created.Should().BeFalse();
                retry.Channel.ChannelId.Should().Be(first.Channel.ChannelId);
                createdChannelIds.Add(first.Channel.ChannelId);

                await new ChannelTextService(setup, new ResourceAuthorizationService(setup))
                    .SendAsync(first.Channel.ChannelId, seed.ManagerId, "SQL discovery persistence");
            }

            await using (var firstContext = new ApplicationDbContext(options))
            await using (var secondContext = new ApplicationDbContext(options))
            {
                var creates = await Task.WhenAll(
                    CreateService(firstContext).CreateAsync(
                        seed.ProjectAId,
                        seed.ManagerId,
                        Request("same-name-concurrent"),
                        "concurrent-one"),
                    CreateService(secondContext).CreateAsync(
                        seed.ProjectAId,
                        seed.ManagerId,
                        Request("same-name-concurrent"),
                        "concurrent-two"));
                creates.Should().OnlyContain(result => result.Created);
                creates.Select(result => result.Channel.ChannelId).Should().OnlyHaveUniqueItems();
                createdChannelIds.AddRange(creates.Select(result => result.Channel.ChannelId));
            }

            await using (var verify = new ApplicationDbContext(options))
            {
                var discovered = await CreateService(verify)
                    .DiscoverAsync(seed.ProjectAId, seed.ManagerId, 1, 20);
                discovered.Items.Should().Contain(item =>
                    item.ChannelId == createdChannelIds[0] &&
                    item.ProjectId == seed.ProjectAId &&
                    item.WorkspaceId == seed.WorkspaceAId);
                discovered.Items
                    .Where(item => createdChannelIds.Contains(item.ChannelId))
                    .Should().HaveCount(3);

                var memberships = await verify.CollaborationChannelMembers
                    .CountAsync(member =>
                        createdChannelIds.Contains(member.ChannelId) &&
                        member.UserId == seed.ManagerId &&
                        member.IsActive &&
                        member.LeftAt == null);
                memberships.Should().Be(3);

                var history = await new ChannelTextService(
                        verify,
                        new ResourceAuthorizationService(verify))
                    .GetHistoryAsync(createdChannelIds[0], seed.ManagerId, 1, 20);
                history.Items.Should().ContainSingle(item =>
                    item.Content == "SQL discovery persistence");

                var idempotencyIndex = await verify.Database
                    .SqlQueryRaw<int>(
                        """
                        SELECT COUNT(*) AS [Value]
                        FROM sys.indexes
                        WHERE object_id = OBJECT_ID(N'[CollaborationChannels]')
                          AND name = N'IX_CollaborationChannels_ProjectId_CreatedByUserId_ProvisioningKey'
                          AND is_unique = 1
                        """)
                    .SingleAsync();
                idempotencyIndex.Should().Be(1);
            }
        }
        finally
        {
            if (seed != null)
                await CleanupAsync(options, seed);
        }
    }

    private static CollaborationChannelService CreateService(ApplicationDbContext context) =>
        new(context, new ResourceAuthorizationService(context));

    private static CreateCollaborationChannelRequestDto Request(string name) =>
        new() { Name = name, Visibility = "Private" };

    private static async Task CleanupAsync(
        DbContextOptions<ApplicationDbContext> options,
        DiscoverySeed seed)
    {
        await using var context = new ApplicationDbContext(options);
        var channelIds = await context.CollaborationChannels
            .Where(channel =>
                channel.ProjectId == seed.ProjectAId ||
                channel.ProjectId == seed.ProjectB.Id ||
                channel.ProjectId == seed.ProjectOtherWorkspace.Id)
            .Select(channel => channel.Id)
            .ToListAsync();
        await context.ChannelMessages
            .Where(message =>
                message.CollaborationChannelId != null &&
                channelIds.Contains(message.CollaborationChannelId.Value))
            .ExecuteDeleteAsync();
        await context.CollaborationChannelMembers
            .Where(member => channelIds.Contains(member.ChannelId))
            .ExecuteDeleteAsync();
        await context.CollaborationChannels
            .Where(channel => channelIds.Contains(channel.Id))
            .ExecuteDeleteAsync();

        var userIds = new[]
        {
            seed.ManagerId,
            seed.MemberId,
            seed.NonMemberId,
            seed.OutsiderId,
            seed.InactiveId
        };
        await context.ProjectMembers
            .Where(member => userIds.Contains(member.UserId))
            .ExecuteDeleteAsync();
        await context.WorkspaceMembers
            .Where(member => userIds.Contains(member.UserId))
            .ExecuteDeleteAsync();
        var projectIds = new[]
        {
            seed.ProjectAId,
            seed.ProjectB.Id,
            seed.ProjectOtherWorkspace.Id
        };
        await context.Projects
            .Where(project => projectIds.Contains(project.Id))
            .ExecuteDeleteAsync();
        var workspaceIds = new[] { seed.WorkspaceAId, seed.WorkspaceB.Id };
        await context.Workspaces
            .Where(workspace => workspaceIds.Contains(workspace.Id))
            .ExecuteDeleteAsync();
        await context.Users
            .Where(user => userIds.Contains(user.Id))
            .ExecuteDeleteAsync();
    }
}
