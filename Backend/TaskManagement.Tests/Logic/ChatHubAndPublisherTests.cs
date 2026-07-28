using System.Security.Claims;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Moq;
using TaskManagement.API.Controllers;
using TaskManagement.API.Hubs;
using TaskManagement.API.Services;
using TaskManagement.Application.DTOs.Collaboration;
using TaskManagement.Application.Interfaces;

namespace TaskManagement.Tests.Logic;

public sealed class ChatHubAndPublisherTests
{
    [Fact]
    public async Task HubUsesSanitizedErrorsAndDeterministicServerGroupNames()
    {
        var userId = Guid.NewGuid();
        var channelId = Guid.NewGuid();
        var conversationId = Guid.NewGuid();
        var authorization = new Mock<ICollaborationRealtimeAuthorizationService>();
        authorization.Setup(item => item.AuthorizeChannelJoinAsync(
                channelId,
                userId,
                It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        authorization.Setup(item => item.AuthorizeDirectConversationJoinAsync(
                conversationId,
                userId,
                It.IsAny<CancellationToken>()))
            .ThrowsAsync(new DirectConversationNotFoundException());
        var groups = new Mock<IGroupManager>();
        groups.Setup(item => item.AddToGroupAsync(
                "connection-1",
                ChatRealtimeGroups.Channel(channelId),
                It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        groups.Setup(item => item.RemoveFromGroupAsync(
                "connection-1",
                ChatRealtimeGroups.Channel(channelId),
                It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        var hub = CreateHub(authorization.Object, groups.Object, userId);

        await hub.JoinChannel(channelId.ToString());
        await hub.LeaveChannel(channelId.ToString());
        await hub.Invoking(item => item.JoinDirectConversation(conversationId.ToString()))
            .Should().ThrowAsync<HubException>()
            .WithMessage("CONVERSATION_NOT_FOUND_OR_FORBIDDEN");
        await hub.Invoking(item => item.JoinChannel("invalid"))
            .Should().ThrowAsync<HubException>()
            .WithMessage("INVALID_ID");

        groups.VerifyAll();
        authorization.VerifyAll();
    }

    [Fact]
    public async Task HubReturnsAuthInactiveAndJoinFailedCodesWithoutRawException()
    {
        var channelId = Guid.NewGuid();
        var groups = Mock.Of<IGroupManager>();
        var authorization = new Mock<ICollaborationRealtimeAuthorizationService>();
        var unauthenticated = CreateHub(
            authorization.Object,
            groups,
            userId: null);

        await unauthenticated.Invoking(item => item.JoinChannel(channelId.ToString()))
            .Should().ThrowAsync<HubException>()
            .WithMessage("AUTH_REQUIRED");

        var inactiveUserId = Guid.NewGuid();
        authorization.Setup(item => item.AuthorizeChannelJoinAsync(
                channelId,
                inactiveUserId,
                It.IsAny<CancellationToken>()))
            .ThrowsAsync(new CollaborationRealtimeUserInactiveException());
        var inactive = CreateHub(
            authorization.Object,
            groups,
            inactiveUserId);
        await inactive.Invoking(item => item.JoinChannel(channelId.ToString()))
            .Should().ThrowAsync<HubException>()
            .WithMessage("USER_INACTIVE");

        var failedUserId = Guid.NewGuid();
        authorization.Setup(item => item.AuthorizeChannelJoinAsync(
                channelId,
                failedUserId,
                It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("provider secret detail"));
        var failed = CreateHub(
            authorization.Object,
            groups,
            failedUserId);
        await failed.Invoking(item => item.JoinChannel(channelId.ToString()))
            .Should().ThrowAsync<HubException>()
            .WithMessage("JOIN_FAILED");
    }

    [Fact]
    public async Task PublisherSendsPersistedChannelDtoOnceToWholeGroup()
    {
        var channelId = Guid.NewGuid();
        var messageId = Guid.NewGuid();
        var senderId = Guid.NewGuid();
        var proxy = new Mock<IClientProxy>();
        var clients = new Mock<IHubClients>();
        clients.Setup(item => item.Group(ChatRealtimeGroups.Channel(channelId)))
            .Returns(proxy.Object);
        var context = new Mock<IHubContext<ChatHub>>();
        context.SetupGet(item => item.Clients).Returns(clients.Object);
        ChannelMessageCreatedEventDto? captured = null;
        proxy.Setup(item => item.SendCoreAsync(
                ChatRealtimeEvents.ChannelMessageCreated,
                It.IsAny<object?[]>(),
                It.IsAny<CancellationToken>()))
            .Callback<string, object?[], CancellationToken>((_, arguments, _) =>
                captured = arguments.Single().Should().BeOfType<ChannelMessageCreatedEventDto>().Subject)
            .Returns(Task.CompletedTask);
        var publisher = new ChatRealtimePublisher(
            context.Object,
            Mock.Of<ILogger<ChatRealtimePublisher>>());
        var persisted = new ChannelMessageDto(
            messageId,
            channelId,
            "persisted",
            new ChannelMessageSenderDto(senderId, "Sender", null),
            DateTime.UtcNow,
            messageId);

        await publisher.PublishChannelMessageCreatedAsync(persisted);

        captured.Should().NotBeNull();
        captured!.MessageId.Should().Be(messageId);
        captured.Sender.UserId.Should().Be(senderId);
        proxy.Verify(item => item.SendCoreAsync(
            ChatRealtimeEvents.ChannelMessageCreated,
            It.IsAny<object?[]>(),
            It.IsAny<CancellationToken>()), Times.Once);
        clients.VerifyAll();
    }

    [Fact]
    public async Task PublisherSendsPersistedDirectDtoOnceToConversationGroup()
    {
        var conversationId = Guid.NewGuid();
        var messageId = Guid.NewGuid();
        var senderId = Guid.NewGuid();
        var proxy = new Mock<IClientProxy>();
        var clients = new Mock<IHubClients>();
        clients.Setup(item => item.Group(
                ChatRealtimeGroups.DirectConversation(conversationId)))
            .Returns(proxy.Object);
        var context = new Mock<IHubContext<ChatHub>>();
        context.SetupGet(item => item.Clients).Returns(clients.Object);
        DirectMessageCreatedEventDto? captured = null;
        proxy.Setup(item => item.SendCoreAsync(
                ChatRealtimeEvents.DirectMessageCreated,
                It.IsAny<object?[]>(),
                It.IsAny<CancellationToken>()))
            .Callback<string, object?[], CancellationToken>((_, arguments, _) =>
                captured = arguments.Single().Should().BeOfType<DirectMessageCreatedEventDto>().Subject)
            .Returns(Task.CompletedTask);
        var publisher = new ChatRealtimePublisher(
            context.Object,
            Mock.Of<ILogger<ChatRealtimePublisher>>());
        var persisted = new DirectMessageDto(
            messageId,
            conversationId,
            "persisted",
            new DirectMessageSenderDto(senderId, "Sender", null),
            DateTime.UtcNow);

        await publisher.PublishDirectMessageCreatedAsync(persisted);

        captured.Should().NotBeNull();
        captured!.MessageId.Should().Be(messageId);
        captured.Sender.UserId.Should().Be(senderId);
        proxy.Verify(item => item.SendCoreAsync(
            ChatRealtimeEvents.DirectMessageCreated,
            It.IsAny<object?[]>(),
            It.IsAny<CancellationToken>()), Times.Once);
        clients.VerifyAll();
    }

    [Fact]
    public async Task PersistenceAndPermissionFailuresDoNotPublishEvents()
    {
        var userId = Guid.NewGuid();
        var entityId = Guid.NewGuid();
        var channelService = new Mock<IChannelTextService>();
        var directService = new Mock<IDirectConversationService>();
        var publisher = new Mock<ICollaborationRealtimePublisher>();
        channelService.Setup(item => item.SendAsync(
                entityId,
                userId,
                "db failure",
                It.IsAny<CancellationToken>()))
            .ThrowsAsync(new DbUpdateException("test failure"));
        directService.Setup(item => item.SendAsync(
                entityId,
                userId,
                "forbidden",
                It.IsAny<CancellationToken>()))
            .ThrowsAsync(new DirectConversationNotFoundException());
        var channelController = WithUser(
            new ChannelMessagesController(channelService.Object, publisher.Object),
            userId);
        var directController = WithUser(
            new DirectConversationsController(directService.Object, publisher.Object),
            userId);

        await channelController.Invoking(item => item.Send(
                entityId,
                new SendChannelMessageRequestDto { Content = "db failure" }))
            .Should().ThrowAsync<DbUpdateException>();
        (await directController.Send(
                entityId,
                new SendDirectMessageRequestDto("forbidden")))
            .Should().BeOfType<NotFoundObjectResult>();

        publisher.Verify(item => item.PublishChannelMessageCreatedAsync(
            It.IsAny<ChannelMessageDto>(),
            It.IsAny<CancellationToken>()), Times.Never);
        publisher.Verify(item => item.PublishDirectMessageCreatedAsync(
            It.IsAny<DirectMessageDto>(),
            It.IsAny<CancellationToken>()), Times.Never);
    }

    private static ChatHub CreateHub(
        ICollaborationRealtimeAuthorizationService authorization,
        IGroupManager groups,
        Guid? userId)
    {
        var identity = userId.HasValue
            ? new ClaimsIdentity(
                [new Claim(ClaimTypes.NameIdentifier, userId.Value.ToString())],
                "test")
            : new ClaimsIdentity();
        var context = new Mock<HubCallerContext>();
        context.SetupGet(item => item.ConnectionId).Returns("connection-1");
        context.SetupGet(item => item.ConnectionAborted).Returns(CancellationToken.None);
        context.SetupGet(item => item.User).Returns(new ClaimsPrincipal(identity));
        return new ChatHub(
            authorization,
            Mock.Of<ILogger<ChatHub>>())
        {
            Context = context.Object,
            Groups = groups
        };
    }

    private static TController WithUser<TController>(
        TController controller,
        Guid userId)
        where TController : ControllerBase
    {
        controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext
            {
                User = new ClaimsPrincipal(new ClaimsIdentity(
                    [new Claim(ClaimTypes.NameIdentifier, userId.ToString())],
                    "test"))
            }
        };
        return controller;
    }
}
