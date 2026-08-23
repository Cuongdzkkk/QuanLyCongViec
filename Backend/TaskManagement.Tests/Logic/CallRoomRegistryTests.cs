using FluentAssertions;
using Microsoft.AspNetCore.SignalR;
using Moq;
using System.Security.Claims;
using TaskManagement.API.Hubs;
using TaskManagement.API.Services;
using TaskManagement.Application.DTOs.Collaboration;
using TaskManagement.Application.Interfaces;

namespace TaskManagement.Tests.Logic;

public sealed class CallRoomRegistryTests
{
    [Fact]
    public void CallHubRequiresAuthentication()
    {
        typeof(CallHub).GetCustomAttributes(typeof(Microsoft.AspNetCore.Authorization.AuthorizeAttribute), true)
            .Should().NotBeEmpty();
    }

    [Fact]
    public async Task JoinUsesServerClaimIdentityAndCanonicalAuthorization()
    {
        var claimUserId = Guid.NewGuid();
        var projectId = Guid.NewGuid();
        var registry = new CallRoomRegistry();
        var authorization = new Mock<ICallRoomAuthorizationService>();
        authorization.Setup(item => item.AuthorizeVoiceRoomJoinAsync(projectId, claimUserId, It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        authorization.Setup(item => item.GetParticipantProfileAsync(claimUserId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new CallParticipantProfile(claimUserId, "Server profile", null));
        var hub = CreateHub(registry, authorization.Object, "claim-connection", claimUserId);

        await hub.JoinVoiceRoom(projectId.ToString(), "general");

        authorization.Verify(item => item.AuthorizeVoiceRoomJoinAsync(projectId, claimUserId, It.IsAny<CancellationToken>()), Times.Once);
        registry.GetRoomParticipants($"project:{projectId:N}:voice:general").Single().UserId.Should().Be(claimUserId);
        registry.GetRoomParticipants($"project:{projectId:N}:voice:general").Single().DisplayName.Should().Be("Server profile");
    }

    [Fact]
    public async Task UnrelatedProjectUserCannotJoin()
    {
        var userId = Guid.NewGuid();
        var projectId = Guid.NewGuid();
        var authorization = new Mock<ICallRoomAuthorizationService>();
        authorization.Setup(item => item.AuthorizeVoiceRoomJoinAsync(projectId, userId, It.IsAny<CancellationToken>()))
            .ThrowsAsync(new UnauthorizedAccessException());
        var hub = CreateHub(new CallRoomRegistry(), authorization.Object, "outsider", userId);

        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => hub.JoinVoiceRoom(projectId.ToString(), "general"));
    }

    [Fact]
    public async Task SpoofedSenderIdentityIsIgnored()
    {
        var senderUserId = Guid.NewGuid();
        var targetUserId = Guid.NewGuid();
        var projectId = Guid.NewGuid();
        var roomId = $"project:{projectId:N}:voice:general";
        var registry = new CallRoomRegistry();
        registry.Join(new CallRoomParticipant(roomId, "sender", senderUserId, "Sender", null, true, false, false));
        registry.Join(new CallRoomParticipant(roomId, "target", targetUserId, "Target", null, true, false, false));
        var targetProxy = new Mock<ISingleClientProxy>();
        var clients = new Mock<IHubCallerClients>();
        clients.Setup(item => item.Client("target")).Returns(targetProxy.Object);
        var hub = CreateHub(registry, new Mock<ICallRoomAuthorizationService>().Object, "sender", senderUserId, clients.Object);

        await hub.SendWebRtcOffer(roomId, "target", new { userId = targetUserId, type = "offer" });

        var envelope = (CallOfferDto)((object?[])targetProxy.Invocations.Single().Arguments[1]!)[0]!;
        envelope.FromUserId.Should().Be(senderUserId);
        envelope.FromUserId.Should().NotBe(targetUserId);
    }

    [Fact]
    public async Task SignalingRequiresSenderAndTargetInSameRoom()
    {
        var projectId = Guid.NewGuid();
        var sender = Guid.NewGuid();
        var registry = new CallRoomRegistry();
        registry.Join(new CallRoomParticipant($"project:{projectId:N}:voice:a", "sender", sender, "Sender", null, true, false, false));
        registry.Join(new CallRoomParticipant($"project:{projectId:N}:voice:b", "target", Guid.NewGuid(), "Target", null, true, false, false));
        var hub = CreateHub(registry, new Mock<ICallRoomAuthorizationService>().Object, "sender", sender);

        await Assert.ThrowsAsync<HubException>(() => hub.SendWebRtcOffer($"project:{projectId:N}:voice:a", "target", new { type = "offer" }));
        await Assert.ThrowsAsync<HubException>(() => hub.SendWebRtcAnswer($"project:{projectId:N}:voice:a", "target", new { type = "answer" }));
        await Assert.ThrowsAsync<HubException>(() => hub.SendIceCandidate($"project:{projectId:N}:voice:a", "target", new { candidate = "ice" }));
    }

    [Fact]
    public async Task CrossProjectRoomGuessingIsDenied()
    {
        var projectId = Guid.NewGuid();
        var guessedProjectId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var registry = new CallRoomRegistry();
        registry.Join(new CallRoomParticipant($"project:{projectId:N}:voice:general", "sender", userId, "Sender", null, true, false, false));
        var hub = CreateHub(registry, new Mock<ICallRoomAuthorizationService>().Object, "sender", userId);

        await Assert.ThrowsAsync<HubException>(() => hub.SendIceCandidate($"project:{guessedProjectId:N}:voice:general", "sender", new { candidate = "ice" }));
    }

    [Fact]
    public async Task SignalingAfterLeaveIsRejected()
    {
        var projectId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var roomId = $"project:{projectId:N}:voice:general";
        var registry = new CallRoomRegistry();
        registry.Join(new CallRoomParticipant(roomId, "sender", userId, "Sender", null, true, false, false));
        registry.Join(new CallRoomParticipant(roomId, "target", Guid.NewGuid(), "Target", null, true, false, false));
        var hub = CreateHub(registry, new Mock<ICallRoomAuthorizationService>().Object, "sender", userId);
        registry.Leave(roomId, "sender");

        await Assert.ThrowsAsync<HubException>(() => hub.SendWebRtcOffer(roomId, "target", new { type = "offer" }));
        registry.IsParticipantInRoom(roomId, "sender").Should().BeFalse();
    }

    [Fact]
    public async Task HubAuthorizesJoinAndRelaysOnlyToCurrentRoomMember()
    {
        var userId = Guid.NewGuid();
        var projectId = Guid.NewGuid();
        var registry = new CallRoomRegistry();
        var authorization = new Mock<ICallRoomAuthorizationService>();
        authorization.Setup(item => item.AuthorizeVoiceRoomJoinAsync(projectId, userId, It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        authorization.Setup(item => item.GetParticipantProfileAsync(userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new CallParticipantProfile(userId, "Caller", null));
        var groups = new Mock<IGroupManager>();
        var callerProxy = new Mock<IClientProxy>();
        var targetProxy = new Mock<ISingleClientProxy>();
        var clients = new Mock<IHubCallerClients>();
        clients.Setup(item => item.OthersInGroup(It.IsAny<string>())).Returns(callerProxy.Object);
        clients.Setup(item => item.Client("target-connection")).Returns(targetProxy.Object);
        var context = new Mock<HubCallerContext>();
        context.SetupGet(item => item.ConnectionId).Returns("caller-connection");
        context.SetupGet(item => item.ConnectionAborted).Returns(CancellationToken.None);
        context.SetupGet(item => item.User).Returns(new ClaimsPrincipal(new ClaimsIdentity(
            [new Claim(ClaimTypes.NameIdentifier, userId.ToString())], "test")));
        var hub = new CallHub(
            registry,
            authorization.Object,
            Mock.Of<ICallTranscriptionProvider>(),
            Mock.Of<ICallTranscriptService>(),
            Mock.Of<ICallChatService>())
        {
            Context = context.Object,
            Groups = groups.Object,
            Clients = clients.Object
        };

        await hub.JoinVoiceRoom(projectId.ToString(), "general");
        registry.Join(new CallRoomParticipant(
            $"project:{projectId:N}:voice:general", "target-connection", Guid.NewGuid(), "Target", null, true, false, false));

        await hub.SendWebRtcOffer(
            $"project:{projectId:N}:voice:general",
            "target-connection",
            new { type = "offer", sdp = "ephemeral" });

        targetProxy.Verify(item => item.SendCoreAsync(
            CallRealtimeEvents.WebRtcOffer,
            It.IsAny<object?[]>(),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task CallChatRequiresMembershipAndBroadcastsCanonicalMessage()
    {
        var registry = new CallRoomRegistry();
        var authorization = new Mock<ICallRoomAuthorizationService>();
        var userId = Guid.NewGuid();
        var roomId = "project:room:voice:general";
        registry.Join(new CallRoomParticipant(roomId, "caller", userId, "Caller", null, true, false, false));
        registry.TryGetCallSessionId(roomId, "caller", out var sessionId).Should().BeTrue();

        var chat = new Mock<ICallChatService>();
        var message = new CallChatMessageDto(Guid.NewGuid(), sessionId, roomId, userId, "Caller", "hello", DateTime.UtcNow, "client-1");
        chat.Setup(item => item.GetHistoryAsync(roomId, sessionId, 100, It.IsAny<CancellationToken>()))
            .ReturnsAsync([message]);
        chat.Setup(item => item.CreateAsync(roomId, sessionId, userId, "Caller", "hello", "client-1", It.IsAny<CancellationToken>()))
            .ReturnsAsync(message);
        var group = new Mock<IClientProxy>();
        var clients = new Mock<IHubCallerClients>();
        clients.Setup(item => item.Group(roomId)).Returns(group.Object);
        var hub = CreateHub(registry, authorization.Object, "caller", userId, clients.Object, chat.Object);

        (await hub.GetCallChatHistory(roomId)).Should().ContainSingle();
        await hub.SendCallMessage(roomId, "hello", "client-1");

        group.Verify(item => item.SendCoreAsync(
            CallRealtimeEvents.CallMessageCreated,
            It.IsAny<object?[]>(),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public void RoomEnforcesSixDistinctParticipantsAndReturnsAuthorizedSnapshot()
    {
        var registry = new CallRoomRegistry();
        var roomId = "project:room:voice:general";

        for (var index = 0; index < ICallRoomRegistry.MaximumParticipants; index++)
        {
            var result = registry.Join(CreateParticipant(roomId, index));
            result.Accepted.Should().BeTrue();
            result.Snapshot.Participants.Should().HaveCount(index + 1);
        }

        var full = registry.Join(CreateParticipant(roomId, 99));
        full.Accepted.Should().BeFalse();
        full.RoomFull.Should().BeTrue();
        full.Snapshot.Participants.Should().HaveCount(ICallRoomRegistry.MaximumParticipants);
    }

    [Fact]
    public void EmptyRoomEndsSessionAndRejoiningCreatesANewCallSession()
    {
        var registry = new CallRoomRegistry();
        var roomId = "project:room:voice:general";
        var userId = Guid.NewGuid();
        var participant = new CallRoomParticipant(roomId, "connection", userId, "Caller", null, true, false, false);

        var first = registry.Join(participant);
        var firstSession = first.Snapshot.AiState.CallSessionId;
        registry.Leave(roomId, participant.ConnectionId).Should().NotBeNull();

        var second = registry.Join(participant);

        second.Snapshot.AiState.CallSessionId.Should().NotBe(firstSession);
    }

    [Fact]
    public void ReconnectingUserReplacesTransportWithoutDuplicatingParticipant()
    {
        var registry = new CallRoomRegistry();
        var roomId = "project:room:voice:general";
        var userId = Guid.NewGuid();
        var first = new CallRoomParticipant(roomId, "connection-old", userId, "Caller", null, true, false, false);
        var second = new CallRoomParticipant(roomId, "connection-new", userId, "Caller", null, true, false, false);

        registry.Join(first).Accepted.Should().BeTrue();
        var result = registry.Join(second);

        result.Accepted.Should().BeTrue();
        result.ReplacedParticipants.Should().ContainSingle(item => item.ConnectionId == "connection-old");
        result.Snapshot.Participants.Should().ContainSingle(item => item.ConnectionId == "connection-new");
        registry.IsParticipantInRoom(roomId, "connection-old").Should().BeFalse();
        registry.IsParticipantInRoom(roomId, "connection-new").Should().BeTrue();

        var repeated = registry.Join(second);
        repeated.ReplacedParticipants.Should().BeEmpty();
        repeated.Snapshot.Participants.Should().ContainSingle(item => item.ConnectionId == "connection-new");

        var other = registry.Join(new CallRoomParticipant(
            roomId, "connection-other", Guid.NewGuid(), "Other", null, true, false, false));
        other.Snapshot.Participants.Should().HaveCount(2);
    }

    [Fact]
    public async Task RaiseHandAndReactionPublishOneEventEach()
    {
        var roomId = "project:room:voice:general";
        var userId = Guid.NewGuid();
        var registry = new CallRoomRegistry();
        registry.Join(new CallRoomParticipant(roomId, "connection", userId, "Caller", null, true, false, false));
        var groupProxy = new Mock<IClientProxy>();
        var clients = new Mock<IHubCallerClients>();
        clients.Setup(item => item.Group(roomId)).Returns(groupProxy.Object);
        var hub = CreateHub(registry, Mock.Of<ICallRoomAuthorizationService>(), "connection", userId, clients.Object);

        await hub.SetRaiseHand(roomId, true);
        await hub.SendCallReaction(roomId, "👏");

        groupProxy.Invocations.Should().HaveCount(2);
        groupProxy.Invocations.Select(invocation => invocation.Arguments[0]).Should().ContainInOrder(
            CallRealtimeEvents.ParticipantHandChanged,
            CallRealtimeEvents.CallReactionAdded);
    }

    [Fact]
    public void MediaStateIsOwnedByConnectionAndDisconnectRemovesParticipant()
    {
        var registry = new CallRoomRegistry();
        var participant = CreateParticipant("room", 1);
        registry.Join(participant).Accepted.Should().BeTrue();

        registry.TryUpdateMediaState(
            "room",
            participant.ConnectionId,
            new CallParticipantMediaStateDto(false, true, true),
            out var updated).Should().BeTrue();
        updated.MicrophoneEnabled.Should().BeFalse();
        updated.CameraEnabled.Should().BeTrue();
        updated.ScreenSharing.Should().BeTrue();

        registry.RemoveConnection(participant.ConnectionId).Should().ContainSingle();
        registry.IsParticipantInRoom("room", participant.ConnectionId).Should().BeFalse();
    }

    [Fact]
    public void RelayingCanBeGuardedByRoomMembershipLookup()
    {
        var registry = new CallRoomRegistry();
        var sender = CreateParticipant("room-a", 1);
        var target = CreateParticipant("room-a", 2);
        registry.Join(sender);
        registry.Join(target);

        registry.IsParticipantInRoom("room-a", sender.ConnectionId).Should().BeTrue();
        registry.IsParticipantInRoom("room-a", target.ConnectionId).Should().BeTrue();
        registry.IsParticipantInRoom("room-b", target.ConnectionId).Should().BeFalse();
    }

    private static CallRoomParticipant CreateParticipant(string roomId, int index) => new(
        roomId,
        $"connection-{index}",
        Guid.Parse($"00000000-0000-0000-0000-{index + 1:000000000000}"),
        $"User {index}",
        null,
        true,
        false,
        false);

    private static CallHub CreateHub(
        ICallRoomRegistry registry,
        ICallRoomAuthorizationService authorization,
        string connectionId,
        Guid userId,
        IHubCallerClients? clients = null,
        ICallChatService? callChat = null)
    {
        var context = new Mock<HubCallerContext>();
        context.SetupGet(item => item.ConnectionId).Returns(connectionId);
        context.SetupGet(item => item.ConnectionAborted).Returns(CancellationToken.None);
        context.SetupGet(item => item.User).Returns(new ClaimsPrincipal(new ClaimsIdentity(
            [new Claim(ClaimTypes.NameIdentifier, userId.ToString())], "test")));
        var callerClients = clients;
        if (callerClients == null)
        {
            var defaultClients = new Mock<IHubCallerClients>();
            defaultClients.Setup(item => item.OthersInGroup(It.IsAny<string>()))
                .Returns(new Mock<IClientProxy>().Object);
            callerClients = defaultClients.Object;
        }
        return new CallHub(
            registry,
            authorization,
            Mock.Of<ICallTranscriptionProvider>(),
            Mock.Of<ICallTranscriptService>(),
            callChat ?? Mock.Of<ICallChatService>())
        {
            Context = context.Object,
            Groups = new Mock<IGroupManager>().Object,
            Clients = callerClients
        };
    }
}
