using System.Security.Claims;
using FluentAssertions;
using Microsoft.AspNetCore.SignalR;
using Moq;
using TaskManagement.API.Hubs;
using TaskManagement.API.Services;
using TaskManagement.Application.DTOs.Collaboration;
using TaskManagement.Application.Interfaces;

namespace TaskManagement.Tests.Logic;

public sealed class CallAi2ConsentTests
{
    [Fact]
    public void CallStartsOffAndRequiresEveryParticipantToAccept()
    {
        var registry = new CallRoomRegistry();
        var room = JoinTwo(registry, out var first, out var second);

        registry.GetAiState(room).State.Should().Be(CallAiStates.Off);
        var waiting = registry.RequestAiTranscription(room, first);
        waiting.State.Should().Be(CallAiStates.WaitingForConsent);
        waiting.Participants.Should().OnlyContain(item => item.ConsentStatus == CallConsentStatuses.Pending);

        var afterFirst = registry.RespondToAiConsent(room, first, waiting.CallSessionId, waiting.ConsentGeneration, true);
        afterFirst.State.Should().Be(CallAiStates.WaitingForConsent);
        var active = registry.RespondToAiConsent(room, second, waiting.CallSessionId, waiting.ConsentGeneration, true);
        active.State.Should().Be(CallAiStates.Active);
    }

    [Fact]
    public void DeclineKeepsAiOffAndClientCannotRespondForAnotherConnection()
    {
        var registry = new CallRoomRegistry();
        var room = JoinTwo(registry, out var first, out var second);
        var requested = registry.RequestAiTranscription(room, first);

        registry.RespondToAiConsent(room, first, requested.CallSessionId, requested.ConsentGeneration, false)
            .State.Should().Be(CallAiStates.Off);
        var action = () => registry.RespondToAiConsent(room, second, requested.CallSessionId, requested.ConsentGeneration, true);
        action.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void NewParticipantPausesActiveAiAndRequiresConsentAgain()
    {
        var registry = new CallRoomRegistry();
        var room = JoinTwo(registry, out var first, out var second);
        var requested = registry.RequestAiTranscription(room, first);
        registry.RespondToAiConsent(room, first, requested.CallSessionId, requested.ConsentGeneration, true);
        registry.RespondToAiConsent(room, second, requested.CallSessionId, requested.ConsentGeneration, true)
            .State.Should().Be(CallAiStates.Active);

        var third = "third";
        registry.Join(new CallRoomParticipant(room, third, Guid.NewGuid(), "Third", null, true, false, false));
        var paused = registry.GetAiState(room);
        paused.State.Should().Be(CallAiStates.PausedConsent);
        paused.Participants.Single(item => item.DisplayName == "Third").ConsentStatus.Should().Be(CallConsentStatuses.Pending);
        registry.RespondToAiConsent(room, third, paused.CallSessionId, paused.ConsentGeneration, true)
            .State.Should().Be(CallAiStates.Active);
    }

    [Fact]
    public void LeavingPendingParticipantAllowsTheRemainingAcceptedParticipantsToResume()
    {
        var registry = new CallRoomRegistry();
        var room = JoinTwo(registry, out var first, out var second);
        var requested = registry.RequestAiTranscription(room, first);
        registry.RespondToAiConsent(room, first, requested.CallSessionId, requested.ConsentGeneration, true);
        registry.Leave(room, second);

        registry.GetAiState(room).State.Should().Be(CallAiStates.Active);
    }

    [Fact]
    public void StopAiReturnsOffAndInvalidatesTranscription()
    {
        var registry = new CallRoomRegistry();
        var room = JoinTwo(registry, out var first, out var second);
        var requested = registry.RequestAiTranscription(room, first);
        registry.RespondToAiConsent(room, first, requested.CallSessionId, requested.ConsentGeneration, true);
        registry.RespondToAiConsent(room, second, requested.CallSessionId, requested.ConsentGeneration, true);

        registry.StopAiTranscription(room, first).State.Should().Be(CallAiStates.Off);
        registry.TryAuthorizeTranscription(room, first, requested.CallSessionId, requested.ConsentGeneration, out _).Should().BeFalse();
    }

    [Fact]
    public void RejoinRequiresConsentForTheCurrentGeneration()
    {
        var registry = new CallRoomRegistry();
        var room = JoinTwo(registry, out var first, out var second);
        var requested = registry.RequestAiTranscription(room, first);
        registry.RespondToAiConsent(room, first, requested.CallSessionId, requested.ConsentGeneration, true);
        registry.RespondToAiConsent(room, second, requested.CallSessionId, requested.ConsentGeneration, true);
        var secondUserId = registry.GetRoomParticipants(room).Single(item => item.ConnectionId == second).UserId;
        registry.Leave(room, second);

        registry.Join(new CallRoomParticipant(room, "rejoined", secondUserId, "Second", null, true, false, false));
        registry.GetAiState(room).State.Should().Be(CallAiStates.PausedConsent);
    }

    [Fact]
    public async Task NoSttCallWithoutActiveConsentAndActiveSpeakerIsServerDerived()
    {
        var registry = new CallRoomRegistry();
        var projectId = Guid.NewGuid();
        var room = $"project:{projectId:N}:voice:general";
        var speakerId = Guid.NewGuid();
        registry.Join(new CallRoomParticipant(room, "speaker", speakerId, "Server speaker", null, true, false, false));
        var provider = new RecordingProvider();
        var hub = CreateHub(registry, "speaker", speakerId, provider);
        var state = registry.GetAiState(room);

        var withoutConsent = () => hub.SubmitCallAudioChunk(
            room, state.CallSessionId.ToString(), state.ConsentGeneration, "audio/webm", [1, 2, 3],
            DateTimeOffset.UtcNow, DateTimeOffset.UtcNow.AddSeconds(1));
        await withoutConsent.Should().ThrowAsync<HubException>();
        provider.Calls.Should().Be(0);

        var requested = registry.RequestAiTranscription(room, "speaker");
        registry.RespondToAiConsent(room, "speaker", requested.CallSessionId, requested.ConsentGeneration, true);
        await hub.SubmitCallAudioChunk(
            room, requested.CallSessionId.ToString(), requested.ConsentGeneration, "audio/webm", [1, 2, 3],
            DateTimeOffset.UtcNow, DateTimeOffset.UtcNow.AddSeconds(1));
        provider.Calls.Should().Be(1);
        provider.LastChunk!.SpeakerUserId.Should().Be(speakerId);
        provider.LastChunk.SpeakerDisplayName.Should().Be("Server speaker");
        provider.LastChunk.AudioBytes.Should().OnlyContain(item => item == 0);

        registry.Join(new CallRoomParticipant(room, "new-participant", Guid.NewGuid(), "New participant", null, true, false, false));
        var paused = registry.GetAiState(room);
        var afterPause = () => hub.SubmitCallAudioChunk(
            room, paused.CallSessionId.ToString(), paused.ConsentGeneration, "audio/webm", [1, 2, 3],
            DateTimeOffset.UtcNow, DateTimeOffset.UtcNow.AddSeconds(1));
        await afterPause.Should().ThrowAsync<HubException>();
        provider.Calls.Should().Be(1);
    }

    private static string JoinTwo(CallRoomRegistry registry, out string first, out string second)
    {
        var projectId = Guid.NewGuid();
        var room = $"project:{projectId:N}:voice:general";
        first = "first";
        second = "second";
        registry.Join(new CallRoomParticipant(room, first, Guid.NewGuid(), "First", null, true, false, false));
        registry.Join(new CallRoomParticipant(room, second, Guid.NewGuid(), "Second", null, true, false, false));
        return room;
    }

    private static CallHub CreateHub(CallRoomRegistry registry, string connectionId, Guid userId, RecordingProvider provider)
    {
        var context = new Mock<HubCallerContext>();
        context.SetupGet(item => item.ConnectionId).Returns(connectionId);
        context.SetupGet(item => item.ConnectionAborted).Returns(CancellationToken.None);
        context.SetupGet(item => item.User).Returns(new ClaimsPrincipal(new ClaimsIdentity(
            [new Claim(ClaimTypes.NameIdentifier, userId.ToString())], "test")));
        var clients = new Mock<IHubCallerClients>();
        clients.Setup(item => item.Group(It.IsAny<string>())).Returns(new Mock<IClientProxy>().Object);
        return new CallHub(
            registry,
            new Mock<ICallRoomAuthorizationService>().Object,
            provider,
            new NoopTranscriptService())
        {
            Context = context.Object,
            Clients = clients.Object
        };
    }

    private sealed class RecordingProvider : ICallTranscriptionProvider
    {
        public int Calls { get; private set; }
        public CallAudioChunk? LastChunk { get; private set; }
        public bool IsConfigured => true;

        public Task<CallTranscriptionResult?> TranscribeAsync(CallAudioChunk chunk, CancellationToken cancellationToken = default)
        {
            Calls++;
            LastChunk = chunk;
            return Task.FromResult<CallTranscriptionResult?>(null);
        }
    }

    private sealed class NoopTranscriptService : ICallTranscriptService
    {
        public Task<CallTranscriptChunkDto?> AppendAsync(CallAudioChunk source, CallTranscriptionResult result, CancellationToken cancellationToken = default) => Task.FromResult<CallTranscriptChunkDto?>(null);
        public Task<IReadOnlyList<CallTranscriptChunkDto>> GetAsync(Guid projectId, string voiceChannelId, Guid callSessionId, CancellationToken cancellationToken = default) => Task.FromResult<IReadOnlyList<CallTranscriptChunkDto>>([]);
    }
}
