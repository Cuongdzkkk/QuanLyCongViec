using System.Security.Claims;
using System.Text.Json;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Moq;
using TaskManagement.API.Controllers;
using TaskManagement.Application.DTOs.Collaboration;
using TaskManagement.Application.Interfaces;
using TaskManagement.Domain.Entities;
using TaskManagement.Infrastructure.Data;
using TaskManagement.Infrastructure.Services;

namespace TaskManagement.Tests.Logic;

public sealed class DirectConversationServiceTests
{
    [Fact]
    public async Task ReverseFindOrCreateReturnsOneConversationAndTwoParticipants()
    {
        await using var context = CreateContext();
        var seed = await DirectSeed.InsertAsync(context);
        var service = new DirectConversationService(context);

        var first = await service.FindOrCreateAsync(seed.UserAId, seed.UserBId);
        var reverse = await service.FindOrCreateAsync(seed.UserBId, seed.UserAId);

        reverse.ConversationId.Should().Be(first.ConversationId);
        (await context.DirectConversations.CountAsync()).Should().Be(1);
        (await context.DirectConversationParticipants.CountAsync()).Should().Be(2);
    }

    [Fact]
    public async Task SelfMissingInactiveDeletedAndOutsideScopeParticipantsAreRejected()
    {
        await using var context = CreateContext();
        var seed = await DirectSeed.InsertAsync(context);
        var service = new DirectConversationService(context);

        await service.Invoking(item => item.FindOrCreateAsync(seed.UserAId, seed.UserAId))
            .Should().ThrowAsync<ArgumentException>();
        await service.Invoking(item => item.FindOrCreateAsync(seed.UserAId, Guid.NewGuid()))
            .Should().ThrowAsync<DirectParticipantNotFoundException>();
        await service.Invoking(item => item.FindOrCreateAsync(seed.UserAId, seed.InactiveUserId))
            .Should().ThrowAsync<DirectParticipantNotFoundException>();
        await service.Invoking(item => item.FindOrCreateAsync(seed.UserAId, seed.DeletedUserId))
            .Should().ThrowAsync<DirectParticipantNotFoundException>();
        await service.Invoking(item => item.FindOrCreateAsync(seed.UserAId, seed.OutsiderId))
            .Should().ThrowAsync<DirectParticipantNotFoundException>();
    }

    [Fact]
    public async Task JwtSenderPersistsAcrossContextsAndOtherParticipantCanRead()
    {
        var databaseName = $"dm-{Guid.NewGuid():N}";
        Guid conversationId;
        DirectSeed seed;
        await using (var write = CreateContext(databaseName))
        {
            seed = await DirectSeed.InsertAsync(write);
            var service = new DirectConversationService(write);
            conversationId = (await service.FindOrCreateAsync(seed.UserAId, seed.UserBId)).ConversationId;
            var message = await service.SendAsync(
                conversationId, seed.UserAId, "  Tiếng Việt\n<script>alert('x')</script>  ");
            message.Sender.UserId.Should().Be(seed.UserAId);
            message.Content.Should().Be("Tiếng Việt\n<script>alert('x')</script>");
            JsonSerializer.Serialize(message).Should().Contain("\\u003Cscript");
        }

        await using var read = CreateContext(databaseName);
        var history = await new DirectConversationService(read)
            .GetHistoryAsync(conversationId, seed.UserBId, 1, 20);
        history.TotalCount.Should().Be(1);
        history.Items.Single().Sender.UserId.Should().Be(seed.UserAId);
        (await read.DirectMessages.SingleAsync()).ConversationId.Should().Be(conversationId);
    }

    [Fact]
    public async Task OutsiderCannotEnumerateReadOrSend()
    {
        await using var context = CreateContext();
        var seed = await DirectSeed.InsertAsync(context);
        var service = new DirectConversationService(context);
        var conversation = await service.FindOrCreateAsync(seed.UserAId, seed.UserBId);

        await service.Invoking(item =>
                item.GetHistoryAsync(conversation.ConversationId, seed.UserCId, 1, 20))
            .Should().ThrowAsync<DirectConversationNotFoundException>();
        await service.Invoking(item =>
                item.SendAsync(conversation.ConversationId, seed.UserCId, "blocked"))
            .Should().ThrowAsync<DirectConversationNotFoundException>();
        await service.Invoking(item =>
                item.GetHistoryAsync(Guid.NewGuid(), seed.UserCId, 1, 20))
            .Should().ThrowAsync<DirectConversationNotFoundException>();
    }

    [Fact]
    public async Task ConversationListIsIsolatedAndOrderedDeterministically()
    {
        await using var context = CreateContext();
        var seed = await DirectSeed.InsertAsync(context);
        var service = new DirectConversationService(context);
        var ab = await service.FindOrCreateAsync(seed.UserAId, seed.UserBId);
        var ac = await service.FindOrCreateAsync(seed.UserAId, seed.UserCId);
        await service.SendAsync(ab.ConversationId, seed.UserAId, "older");
        await service.SendAsync(ac.ConversationId, seed.UserCId, "newer");

        var listA = await service.ListAsync(seed.UserAId, 1, 20);
        var listB = await service.ListAsync(seed.UserBId, 1, 20);

        listA.Items.Select(item => item.ConversationId).Should()
            .Equal(ac.ConversationId, ab.ConversationId);
        listA.Ordering.Should().Be(DirectConversationService.ConversationOrdering);
        listB.Items.Should().ContainSingle(item => item.ConversationId == ab.ConversationId);
    }

    [Fact]
    public async Task HistoryPaginationUsesTimestampAndMessageIdTieBreaker()
    {
        await using var context = CreateContext();
        var seed = await DirectSeed.InsertAsync(context);
        var service = new DirectConversationService(context);
        var conversation = await service.FindOrCreateAsync(seed.UserAId, seed.UserBId);
        var timestamp = new DateTime(2026, 7, 28, 14, 0, 0, DateTimeKind.Utc);
        for (var index = 1; index <= 7; index++)
        {
            context.DirectMessages.Add(new DirectMessage
            {
                Id = Guid.Parse($"00000000-0000-0000-0000-{index:000000000000}"),
                ConversationId = conversation.ConversationId,
                SenderId = seed.UserAId,
                ReceiverId = seed.UserBId,
                Content = $"message-{index}",
                SentAt = timestamp
            });
        }
        await context.SaveChangesAsync();

        var pages = new[]
        {
            await service.GetHistoryAsync(conversation.ConversationId, seed.UserAId, 1, 3),
            await service.GetHistoryAsync(conversation.ConversationId, seed.UserAId, 2, 3),
            await service.GetHistoryAsync(conversation.ConversationId, seed.UserAId, 3, 3)
        };
        var messages = pages.SelectMany(page => page.Items).ToList();

        messages.Should().HaveCount(7);
        messages.Select(message => message.MessageId).Should().OnlyHaveUniqueItems();
        messages.Select(message => message.MessageId).Should().BeInDescendingOrder();
        pages.Should().OnlyContain(page => page.TotalCount == 7);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData(" \r\n ")]
    public async Task EmptyContentIsRejected(string? content)
    {
        await using var context = CreateContext();
        var seed = await DirectSeed.InsertAsync(context);
        var service = new DirectConversationService(context);
        var conversation = await service.FindOrCreateAsync(seed.UserAId, seed.UserBId);

        await service.Invoking(item =>
                item.SendAsync(conversation.ConversationId, seed.UserAId, content))
            .Should().ThrowAsync<ArgumentException>();
    }

    [Fact]
    public async Task OverLimitContentAndInactiveCurrentUserAreRejected()
    {
        await using var context = CreateContext();
        var seed = await DirectSeed.InsertAsync(context);
        var service = new DirectConversationService(context);
        var conversation = await service.FindOrCreateAsync(seed.UserAId, seed.UserBId);

        await service.Invoking(item =>
                item.SendAsync(conversation.ConversationId, seed.UserAId, new string('x', 4001)))
            .Should().ThrowAsync<ArgumentException>();
        var userA = await context.Users.FindAsync(seed.UserAId);
        userA!.IsActive = false;
        await context.SaveChangesAsync();
        await service.Invoking(item =>
                item.SendAsync(conversation.ConversationId, seed.UserAId, "blocked"))
            .Should().ThrowAsync<DirectParticipantNotFoundException>();
    }

    [Fact]
    public async Task ControllerIgnoresForgedSenderAndUsesJwtIdentity()
    {
        var userAId = Guid.NewGuid();
        var userBId = Guid.NewGuid();
        var conversationId = Guid.NewGuid();
        var request = JsonSerializer.Deserialize<SendDirectMessageRequestDto>(
            $$"""{"content":"hello","senderId":"{{userBId}}","createdAt":"2000-01-01"}""",
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true })!;
        var service = new Mock<IDirectConversationService>();
        var publisher = new Mock<ICollaborationRealtimePublisher>();
        DirectMessageDto? persistedMessage = null;
        service.Setup(item => item.SendAsync(
                conversationId, userAId, "hello", It.IsAny<CancellationToken>()))
            .ReturnsAsync(persistedMessage = new DirectMessageDto(
                Guid.NewGuid(), conversationId, "hello",
                new DirectMessageSenderDto(userAId, "A", null), DateTime.UtcNow));
        publisher.Setup(item => item.PublishDirectMessageCreatedAsync(
                persistedMessage,
                It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        var controller = new DirectConversationsController(service.Object, publisher.Object)
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext
                {
                    User = new ClaimsPrincipal(new ClaimsIdentity(
                        [new Claim(ClaimTypes.NameIdentifier, userAId.ToString())], "test"))
                }
            }
        };

        var response = await controller.Send(conversationId, request);

        response.Should().BeOfType<ObjectResult>().Which.StatusCode.Should().Be(201);
        service.VerifyAll();
        publisher.VerifyAll();
        typeof(SendDirectMessageRequestDto).GetProperties().Select(item => item.Name)
            .Should().BeEquivalentTo(["Content"]);
        typeof(DirectMessageSenderDto).GetProperties().Select(item => item.Name)
            .Should().BeEquivalentTo(["UserId", "DisplayName", "AvatarUrl"]);
    }

    private static ApplicationDbContext CreateContext(string? databaseName = null) =>
        new(new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(databaseName ?? Guid.NewGuid().ToString("N"))
            .Options);
}

internal sealed record DirectSeed(
    Guid UserAId,
    Guid UserBId,
    Guid UserCId,
    Guid OutsiderId,
    Guid InactiveUserId,
    Guid DeletedUserId,
    Guid WorkspaceAId,
    Guid WorkspaceBId)
{
    public static async Task<DirectSeed> InsertAsync(ApplicationDbContext context)
    {
        var userA = User("a");
        var userB = User("b");
        var userC = User("c");
        var outsider = User("outsider");
        var inactive = User("inactive", active: false);
        var deleted = User("deleted", deleted: true);
        var workspaceA = Workspace("workspace-a", userA);
        var workspaceB = Workspace("workspace-b", outsider);
        context.AddRange(userA, userB, userC, outsider, inactive, deleted, workspaceA, workspaceB);
        context.WorkspaceMembers.AddRange(
            Member(workspaceA, userA),
            Member(workspaceA, userB),
            Member(workspaceA, userC),
            Member(workspaceA, inactive),
            Member(workspaceA, deleted),
            Member(workspaceB, outsider));
        await context.SaveChangesAsync();
        return new(
            userA.Id, userB.Id, userC.Id, outsider.Id, inactive.Id, deleted.Id,
            workspaceA.Id, workspaceB.Id);
    }

    private static User User(string key, bool active = true, bool deleted = false) => new()
    {
        Id = Guid.NewGuid(),
        Email = $"{key}-{Guid.NewGuid():N}@sprinta.test",
        FullName = $"User {key}",
        PasswordHash = "test",
        IsActive = active,
        IsDeleted = deleted,
        CreatedAt = DateTime.UtcNow
    };

    private static Workspace Workspace(string name, User owner) => new()
    {
        Id = Guid.NewGuid(),
        Slug = $"{name}-{Guid.NewGuid():N}",
        Name = name,
        Owner = owner,
        OwnerId = owner.Id,
        CreatedAt = DateTime.UtcNow,
        UpdatedAt = DateTime.UtcNow
    };

    private static WorkspaceMember Member(Workspace workspace, User user) => new()
    {
        Workspace = workspace,
        WorkspaceId = workspace.Id,
        User = user,
        UserId = user.Id,
        WorkspaceRole = "MEMBER",
        JoinedAt = DateTime.UtcNow,
        IsActive = true
    };
}
