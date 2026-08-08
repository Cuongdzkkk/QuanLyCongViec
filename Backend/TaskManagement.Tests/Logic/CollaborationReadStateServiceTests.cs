using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using TaskManagement.Application.Interfaces;
using TaskManagement.Domain.Entities;
using TaskManagement.Infrastructure.Data;
using TaskManagement.Infrastructure.Services;

namespace TaskManagement.Tests.Logic;

public sealed class CollaborationReadStateServiceTests
{
    [Fact]
    public async Task ChannelCursorIsPerUserPersistentIdempotentAndMonotonic()
    {
        var databaseName = $"channel-read-{Guid.NewGuid():N}";
        ChannelSeed seed;
        Guid projectId;
        Guid firstMessageId;
        Guid secondMessageId;
        DateTime persistedReadAt;
        await using (var context = CreateContext(databaseName))
        {
            seed = await ChannelSeed.InsertAsync(context);
            projectId = (await context.CollaborationChannels
                .SingleAsync(item => item.Id == seed.ChannelAId)).ProjectId;
            var text = CreateChannelTextService(context);
            firstMessageId = (await text.SendAsync(seed.ChannelAId, seed.UserAId, "one")).MessageId;
            secondMessageId = (await text.SendAsync(seed.ChannelAId, seed.UserAId, "two")).MessageId;

            var channelListA = await CreateChannelService(context)
                .DiscoverAsync(projectId, seed.UserAId, 1, 20);
            var channelListB = await CreateChannelService(context)
                .DiscoverAsync(projectId, seed.UserBId, 1, 20);
            channelListA.Items.Single(item => item.ChannelId == seed.ChannelAId)
                .UnreadCount.Should().Be(0);
            channelListB.Items.Single(item => item.ChannelId == seed.ChannelAId)
                .UnreadCount.Should().Be(2);

            var reads = CreateReadService(context);
            (await reads.MarkChannelReadAsync(
                seed.ChannelAId, seed.UserBId, firstMessageId)).UnreadCount.Should().Be(1);
            var latest = await reads.MarkChannelReadAsync(
                seed.ChannelAId, seed.UserBId, secondMessageId);
            latest.UnreadCount.Should().Be(0);
            persistedReadAt = latest.LastReadAt!.Value;

            var repeated = await reads.MarkChannelReadAsync(
                seed.ChannelAId, seed.UserBId, secondMessageId);
            var regressed = await reads.MarkChannelReadAsync(
                seed.ChannelAId, seed.UserBId, firstMessageId);
            repeated.LastReadAt.Should().Be(persistedReadAt);
            regressed.LastReadMessageId.Should().Be(secondMessageId);
            regressed.LastReadAt.Should().Be(persistedReadAt);
        }

        await using var fresh = CreateContext(databaseName);
        var persisted = await CreateChannelService(fresh)
            .DiscoverAsync(projectId, seed.UserBId, 1, 20);
        persisted.Items.Single(item => item.ChannelId == seed.ChannelAId)
            .UnreadCount.Should().Be(0);
        await CreateChannelTextService(fresh).SendAsync(seed.ChannelAId, seed.UserAId, "three");
        var afterNewMessage = await CreateChannelService(fresh)
            .DiscoverAsync(projectId, seed.UserBId, 1, 20);
        afterNewMessage.Items.Single(item => item.ChannelId == seed.ChannelAId)
            .UnreadCount.Should().Be(1);
    }

    [Fact]
    public async Task ChannelOutsiderAndCrossChannelCursorAreRejected()
    {
        await using var context = CreateContext();
        var seed = await ChannelSeed.InsertAsync(context);
        var foreignMessage = new ChannelMessage
        {
            Id = Guid.NewGuid(),
            CollaborationChannelId = seed.ChannelBId,
            SenderId = seed.OutsiderId,
            Content = "other channel",
            SentAt = DateTime.UtcNow
        };
        context.ChannelMessages.Add(foreignMessage);
        await context.SaveChangesAsync();
        var service = CreateReadService(context);

        await service.Invoking(item => item.MarkChannelReadAsync(
                seed.ChannelAId, seed.OutsiderId, foreignMessage.Id))
            .Should().ThrowAsync<ChannelNotFoundException>();
        await service.Invoking(item => item.MarkChannelReadAsync(
                seed.ChannelAId, seed.UserBId, foreignMessage.Id))
            .Should().ThrowAsync<CollaborationMessageNotFoundException>();
    }

    [Fact]
    public async Task DirectCursorIsPerUserPersistentIdempotentAndMonotonic()
    {
        var databaseName = $"dm-read-{Guid.NewGuid():N}";
        DirectSeed seed;
        Guid conversationId;
        Guid firstMessageId;
        Guid secondMessageId;
        await using (var context = CreateContext(databaseName))
        {
            seed = await DirectSeed.InsertAsync(context);
            var direct = new DirectConversationService(context);
            conversationId = (await direct.FindOrCreateAsync(
                seed.UserAId, seed.UserBId)).ConversationId;
            firstMessageId = (await direct.SendAsync(
                conversationId, seed.UserAId, "one")).MessageId;
            secondMessageId = (await direct.SendAsync(
                conversationId, seed.UserAId, "two")).MessageId;

            (await direct.ListAsync(seed.UserAId, 1, 20)).Items.Single()
                .UnreadCount.Should().Be(0);
            (await direct.ListAsync(seed.UserBId, 1, 20)).Items.Single()
                .UnreadCount.Should().Be(2);
            var reads = CreateReadService(context);
            (await reads.MarkDirectConversationReadAsync(
                conversationId, seed.UserBId, firstMessageId)).UnreadCount.Should().Be(1);
            var latest = await reads.MarkDirectConversationReadAsync(
                conversationId, seed.UserBId, secondMessageId);
            latest.UnreadCount.Should().Be(0);
            var readAt = latest.LastReadAt;
            (await reads.MarkDirectConversationReadAsync(
                conversationId, seed.UserBId, secondMessageId)).LastReadAt.Should().Be(readAt);
            var older = await reads.MarkDirectConversationReadAsync(
                conversationId, seed.UserBId, firstMessageId);
            older.LastReadMessageId.Should().Be(secondMessageId);
            older.LastReadAt.Should().Be(readAt);
        }

        await using var fresh = CreateContext(databaseName);
        (await new DirectConversationService(fresh).ListAsync(seed.UserBId, 1, 20))
            .Items.Single().UnreadCount.Should().Be(0);
        await new DirectConversationService(fresh)
            .SendAsync(conversationId, seed.UserAId, "three");
        (await new DirectConversationService(fresh).ListAsync(seed.UserBId, 1, 20))
            .Items.Single().UnreadCount.Should().Be(1);
    }

    [Fact]
    public async Task DirectOutsiderAndCrossConversationCursorAreRejected()
    {
        await using var context = CreateContext();
        var seed = await DirectSeed.InsertAsync(context);
        var direct = new DirectConversationService(context);
        var ab = await direct.FindOrCreateAsync(seed.UserAId, seed.UserBId);
        var ac = await direct.FindOrCreateAsync(seed.UserAId, seed.UserCId);
        var foreignMessage = await direct.SendAsync(
            ac.ConversationId, seed.UserAId, "other conversation");
        var service = CreateReadService(context);

        await service.Invoking(item => item.MarkDirectConversationReadAsync(
                ab.ConversationId, seed.UserCId, foreignMessage.MessageId))
            .Should().ThrowAsync<DirectConversationNotFoundException>();
        await service.Invoking(item => item.MarkDirectConversationReadAsync(
                ab.ConversationId, seed.UserBId, foreignMessage.MessageId))
            .Should().ThrowAsync<CollaborationMessageNotFoundException>();
    }

    [Fact]
    public async Task UnreadUpdateTargetsOnlyOtherParticipantsAndUsesPersistedCounts()
    {
        await using var context = CreateContext();
        var channelSeed = await ChannelSeed.InsertAsync(context);
        var channelMessage = await CreateChannelTextService(context)
            .SendAsync(channelSeed.ChannelAId, channelSeed.UserAId, "channel unread");
        var readService = CreateReadService(context);
        var channelUpdates = await readService
            .GetChannelUnreadUpdatesForMessageAsync(channelMessage.MessageId);

        channelUpdates.Should().ContainSingle(update =>
            update.UserId == channelSeed.UserBId && update.State.UnreadCount == 1);
        channelUpdates.Should().NotContain(update => update.UserId == channelSeed.UserAId);
    }

    private static ApplicationDbContext CreateContext(string? databaseName = null) =>
        new(new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(databaseName ?? Guid.NewGuid().ToString())
            .Options);

    private static ChannelTextService CreateChannelTextService(ApplicationDbContext context) =>
        new(context, new ResourceAuthorizationService(context));

    private static CollaborationChannelService CreateChannelService(ApplicationDbContext context) =>
        new(context, new ResourceAuthorizationService(context));

    private static CollaborationReadStateService CreateReadService(ApplicationDbContext context) =>
        new(
            context,
            CreateChannelTextService(context),
            new DirectConversationService(context));
}
