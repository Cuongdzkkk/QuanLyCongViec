using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using TaskManagement.Domain.Entities;
using TaskManagement.Infrastructure.Data;
using TaskManagement.Infrastructure.Services;

namespace TaskManagement.Tests.Logic;

public sealed class DirectConversationSqlServerTests
{
    private const string ConnectionString =
        "Server=(localdb)\\MSSQLLocalDB;Database=SprintACollabDm01IntegrationV1;" +
        "Trusted_Connection=True;MultipleActiveResultSets=true;TrustServerCertificate=True";

    [Fact]
    [Trait("Category", "SqlServerIntegration")]
    public async Task SqlServerEnforcesUniquePairAtomicParticipantsPersistenceAndConcurrentOrdering()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseSqlServer(
                ConnectionString,
                sqlServer => sqlServer.EnableRetryOnFailure())
            .Options;
        DirectSeed? seed = null;
        try
        {
            await using (var setup = new ApplicationDbContext(options))
            {
                await setup.Database.EnsureCreatedAsync();
                seed = await DirectSeed.InsertAsync(setup);
            }

            Guid firstId;
            Guid secondId;
            await using (var firstContext = new ApplicationDbContext(options))
            await using (var secondContext = new ApplicationDbContext(options))
            {
                var firstTask = new DirectConversationService(firstContext)
                    .FindOrCreateAsync(seed.UserAId, seed.UserBId);
                var secondTask = new DirectConversationService(secondContext)
                    .FindOrCreateAsync(seed.UserBId, seed.UserAId);
                var results = await Task.WhenAll(firstTask, secondTask);
                firstId = results[0].ConversationId;
                secondId = results[1].ConversationId;
            }

            secondId.Should().Be(firstId);
            await using (var verify = new ApplicationDbContext(options))
            {
                (await verify.DirectConversations.CountAsync(item =>
                    item.UserLowId == seed.UserAId || item.UserHighId == seed.UserAId))
                    .Should().Be(1);
                (await verify.DirectConversationParticipants.CountAsync(item =>
                    item.ConversationId == firstId)).Should().Be(2);
            }

            await using (var sendA = new ApplicationDbContext(options))
            await using (var sendB = new ApplicationDbContext(options))
            {
                await Task.WhenAll(
                    new DirectConversationService(sendA)
                        .SendAsync(firstId, seed.UserAId, "concurrent-a"),
                    new DirectConversationService(sendB)
                        .SendAsync(firstId, seed.UserBId, "concurrent-b"));
            }

            await using (var fresh = new ApplicationDbContext(options))
            {
                var history = await new DirectConversationService(fresh)
                    .GetHistoryAsync(firstId, seed.UserBId, 1, 10);
                history.TotalCount.Should().Be(2);
                var conversation = await fresh.DirectConversations.SingleAsync(item => item.Id == firstId);
                conversation.LastMessageAt.Should().Be(
                    await fresh.DirectMessages
                        .Where(item => item.ConversationId == firstId)
                        .MaxAsync(item => item.SentAt));

                fresh.DirectMessages.Add(new DirectMessage
                {
                    Id = Guid.NewGuid(),
                    ConversationId = Guid.NewGuid(),
                    SenderId = seed.UserAId,
                    ReceiverId = seed.UserBId,
                    Content = "invalid conversation",
                    SentAt = DateTime.UtcNow
                });
                await fresh.Invoking(item => item.SaveChangesAsync())
                    .Should().ThrowAsync<DbUpdateException>();
            }

            await using (var duplicate = new ApplicationDbContext(options))
            {
                var pair = Canonical(seed.UserAId, seed.UserBId);
                duplicate.DirectConversations.Add(new DirectConversation
                {
                    Id = Guid.NewGuid(),
                    WorkspaceId = seed.WorkspaceAId,
                    UserLowId = pair.Low,
                    UserHighId = pair.High,
                    CreatedAt = DateTime.UtcNow
                });
                await duplicate.Invoking(item => item.SaveChangesAsync())
                    .Should().ThrowAsync<DbUpdateException>();
            }

            await using var metadata = new ApplicationDbContext(options);
            var requiredIndexes = await metadata.Database.SqlQueryRaw<string>(
                    """
                    SELECT [name] AS [Value]
                    FROM sys.indexes
                    WHERE object_id IN (
                        OBJECT_ID(N'[DirectConversations]'),
                        OBJECT_ID(N'[DirectMessages]'))
                      AND [name] IN (
                        N'IX_DirectConversations_UserLowId_UserHighId',
                        N'IX_DirectMessages_ConversationId_SentAt_Id')
                    """)
                .ToListAsync();
            requiredIndexes.Should().BeEquivalentTo(
                "IX_DirectConversations_UserLowId_UserHighId",
                "IX_DirectMessages_ConversationId_SentAt_Id");
        }
        finally
        {
            if (seed != null) await CleanupAsync(options, seed);
        }
    }

    private static (Guid Low, Guid High) Canonical(Guid first, Guid second) =>
        first.CompareTo(second) < 0 ? (first, second) : (second, first);

    private static async Task CleanupAsync(
        DbContextOptions<ApplicationDbContext> options,
        DirectSeed seed)
    {
        await using var context = new ApplicationDbContext(options);
        var userIds = new[]
        {
            seed.UserAId, seed.UserBId, seed.UserCId, seed.OutsiderId,
            seed.InactiveUserId, seed.DeletedUserId
        };
        var conversationIds = await context.DirectConversations
            .Where(item => userIds.Contains(item.UserLowId) || userIds.Contains(item.UserHighId))
            .Select(item => item.Id)
            .ToListAsync();
        await context.DirectMessages
            .Where(item => item.ConversationId != null && conversationIds.Contains(item.ConversationId.Value))
            .ExecuteDeleteAsync();
        await context.DirectConversationParticipants
            .Where(item => conversationIds.Contains(item.ConversationId))
            .ExecuteDeleteAsync();
        await context.DirectConversations
            .Where(item => conversationIds.Contains(item.Id))
            .ExecuteDeleteAsync();
        await context.WorkspaceMembers.Where(item => userIds.Contains(item.UserId)).ExecuteDeleteAsync();
        await context.Workspaces
            .Where(item => item.Id == seed.WorkspaceAId || item.Id == seed.WorkspaceBId)
            .ExecuteDeleteAsync();
        await context.Users.Where(item => userIds.Contains(item.Id)).ExecuteDeleteAsync();
    }
}
