using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using TaskManagement.Domain.Entities;
using TaskManagement.Infrastructure.Data;
using TaskManagement.Infrastructure.Services;

namespace TaskManagement.Tests.Logic;

public sealed class CollaborationReadStateSqlServerTests
{
    private const string ConnectionString =
        "Server=(localdb)\\MSSQLLocalDB;Database=SprintACollabReadState01IntegrationV2;" +
        "Trusted_Connection=True;MultipleActiveResultSets=true;TrustServerCertificate=True";

    [Fact]
    [Trait("Category", "SqlServerIntegration")]
    public async Task ConcurrentMarksKeepOneCursorAtTheNewestMessage()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseSqlServer(ConnectionString, sqlServer => sqlServer.EnableRetryOnFailure())
            .Options;
        DirectSeed? seed = null;
        Guid conversationId = Guid.Empty;
        try
        {
            await using (var setup = new ApplicationDbContext(options))
            {
                await setup.Database.EnsureCreatedAsync();
                seed = await DirectSeed.InsertAsync(setup);
                var direct = new DirectConversationService(setup);
                conversationId = (await direct.FindOrCreateAsync(
                    seed.UserAId, seed.UserBId)).ConversationId;
                await direct.SendAsync(conversationId, seed.UserAId, "one");
                await direct.SendAsync(conversationId, seed.UserAId, "two");
            }

            List<Guid> orderedIds;
            await using (var ordering = new ApplicationDbContext(options))
            {
                orderedIds = await ordering.DirectMessages.AsNoTracking()
                    .Where(message => message.ConversationId == conversationId)
                    .OrderBy(message => message.SentAt)
                    .ThenBy(message => message.Id)
                    .Select(message => message.Id)
                    .ToListAsync();
            }

            await using (var olderContext = new ApplicationDbContext(options))
            await using (var newerContext = new ApplicationDbContext(options))
            {
                var older = CreateReadService(olderContext).MarkDirectConversationReadAsync(
                    conversationId, seed.UserBId, orderedIds[0]);
                var newer = CreateReadService(newerContext).MarkDirectConversationReadAsync(
                    conversationId, seed.UserBId, orderedIds[1]);
                await Task.WhenAll(older, newer);
            }

            await using var verify = new ApplicationDbContext(options);
            var state = await verify.DirectConversationReadStates.AsNoTracking()
                .SingleAsync(item =>
                    item.ConversationId == conversationId &&
                    item.UserId == seed.UserBId);
            state.LastReadMessageId.Should().Be(orderedIds[1]);
            (await verify.DirectConversationReadStates.CountAsync(item =>
                item.ConversationId == conversationId && item.UserId == seed.UserBId))
                .Should().Be(1);
        }
        finally
        {
            if (seed != null) await CleanupAsync(options, seed, conversationId);
        }
    }

    private static CollaborationReadStateService CreateReadService(
        ApplicationDbContext context) =>
        new(
            context,
            new ChannelTextService(context, new ResourceAuthorizationService(context)),
            new DirectConversationService(context));

    private static async Task CleanupAsync(
        DbContextOptions<ApplicationDbContext> options,
        DirectSeed seed,
        Guid conversationId)
    {
        await using var context = new ApplicationDbContext(options);
        if (conversationId != Guid.Empty)
        {
            await context.DirectConversationReadStates
                .Where(item => item.ConversationId == conversationId)
                .ExecuteDeleteAsync();
            await context.DirectMessages
                .Where(item => item.ConversationId == conversationId)
                .ExecuteDeleteAsync();
            await context.DirectConversationParticipants
                .Where(item => item.ConversationId == conversationId)
                .ExecuteDeleteAsync();
            await context.DirectConversations
                .Where(item => item.Id == conversationId)
                .ExecuteDeleteAsync();
        }
        var userIds = new[]
        {
            seed.UserAId, seed.UserBId, seed.UserCId, seed.OutsiderId,
            seed.InactiveUserId, seed.DeletedUserId
        };
        await context.WorkspaceMembers
            .Where(item => userIds.Contains(item.UserId))
            .ExecuteDeleteAsync();
        await context.Workspaces
            .Where(item => item.Id == seed.WorkspaceAId || item.Id == seed.WorkspaceBId)
            .ExecuteDeleteAsync();
        await context.Users.Where(item => userIds.Contains(item.Id)).ExecuteDeleteAsync();
    }
}
