using System.Security.Claims;
using FluentAssertions;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using TaskManagement.API.Hubs;
using TaskManagement.API.Services;
using TaskManagement.Application.DTOs.Collaboration;
using TaskManagement.Application.Interfaces;

namespace TaskManagement.Tests.Logic;

public sealed class CallCaptionResultDispatcherTests
{
    [Fact]
    public async Task InterimAndFinalResultsKeepTheExistingEventsPayloadsAndRoomTarget()
    {
        var fixture = CreateFixture();
        await using var services = fixture.Services;
        var dispatcher = fixture.Dispatcher;
        var source = fixture.Source;

        await dispatcher.DeliverAsync(source, Result("interim fixture", isFinal: false));
        await dispatcher.DeliverAsync(source, Result("final fixture", isFinal: true));

        fixture.Clients.Verify(item => item.Group(source.RoomId), Times.Exactly(2));
        fixture.Proxy.Verify(item => item.SendCoreAsync(
            CallRealtimeEvents.CallTranscriptInterim,
            It.Is<object?[]>(arguments => IsInterimPayload(arguments, source)),
            It.IsAny<CancellationToken>()), Times.Once);
        fixture.Proxy.Verify(item => item.SendCoreAsync(
            CallRealtimeEvents.CallTranscriptChunkAdded,
            It.Is<object?[]>(arguments => IsFinalPayload(arguments, source)),
            It.IsAny<CancellationToken>()), Times.Once);
        fixture.Transcripts.AppendCalls.Should().Be(1);
    }

    [Fact]
    public async Task EmptyOrUnauthorizedResultsDoNotBroadcast()
    {
        var fixture = CreateFixture();
        await using var services = fixture.Services;

        await fixture.Dispatcher.DeliverAsync(fixture.Source, Result("   ", isFinal: false));
        fixture.Registry.StopAiTranscription(fixture.Source.RoomId, fixture.Source.SpeakerConnectionId!);
        await fixture.Dispatcher.DeliverAsync(fixture.Source, Result("not authorized", isFinal: false));

        fixture.Proxy.Verify(item => item.SendCoreAsync(
            It.IsAny<string>(), It.IsAny<object?[]>(), It.IsAny<CancellationToken>()), Times.Never);
        fixture.Transcripts.AppendCalls.Should().Be(0);
    }

    [Fact]
    public async Task CapturedStreamingCallbackRunsAfterHubInvocationWithoutRetainingTheHub()
    {
        var fixture = CreateFixture();
        await using var services = fixture.Services;
        var provider = new CapturingStreamingProvider();
        var context = new Mock<HubCallerContext>();
        context.SetupGet(item => item.ConnectionId).Returns(fixture.Source.SpeakerConnectionId!);
        context.SetupGet(item => item.ConnectionAborted).Returns(CancellationToken.None);
        context.SetupGet(item => item.User).Returns(new ClaimsPrincipal(new ClaimsIdentity(
            [new Claim(ClaimTypes.NameIdentifier, fixture.Source.SpeakerUserId.ToString())], "test")));
        var hub = new CallHub(
            fixture.Registry,
            Mock.Of<ICallRoomAuthorizationService>(),
            provider,
            Mock.Of<ICallChatService>(),
            fixture.Dispatcher)
        {
            Context = context.Object
        };

        await hub.SubmitCallAudioChunk(
            fixture.Source.RoomId,
            fixture.Source.CallSessionId.ToString(),
            fixture.Source.ConsentGeneration,
            fixture.Source.MimeType,
            [1, 2, 3, 4],
            fixture.Source.StartedAt,
            fixture.Source.EndedAt,
            fixture.Source.Language);

        provider.OnResult.Should().NotBeNull();
        provider.OnResult!.Target.Should().BeSameAs(fixture.Dispatcher);
        provider.CanContinue!.Target.Should().NotBeOfType<CallHub>();
        await provider.OnResult(fixture.Source, Result("delayed fixture", isFinal: false));

        fixture.Proxy.Verify(item => item.SendCoreAsync(
            CallRealtimeEvents.CallTranscriptInterim,
            It.IsAny<object?[]>(),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    private static DispatcherFixture CreateFixture()
    {
        var registry = new CallRoomRegistry();
        var projectId = Guid.NewGuid();
        var roomId = $"project:{projectId:N}:voice:general";
        var connectionId = "speaker-connection";
        var speakerId = Guid.NewGuid();
        registry.Join(new CallRoomParticipant(roomId, connectionId, speakerId, "Speaker", null, true, false, false));
        var requested = registry.RequestAiTranscription(roomId, connectionId);
        registry.RespondToAiConsent(roomId, connectionId, requested.CallSessionId, requested.ConsentGeneration, true);

        var transcriptService = new RecordingTranscriptService(projectId);
        var serviceCollection = new ServiceCollection();
        serviceCollection.AddScoped<ICallTranscriptService>(_ => transcriptService);
        var services = serviceCollection.BuildServiceProvider();
        var proxy = new Mock<IClientProxy>();
        proxy.Setup(item => item.SendCoreAsync(
                It.IsAny<string>(), It.IsAny<object?[]>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        var clients = new Mock<IHubClients>();
        clients.Setup(item => item.Group(It.IsAny<string>())).Returns(proxy.Object);
        var hubContext = new Mock<IHubContext<CallHub>>();
        hubContext.SetupGet(item => item.Clients).Returns(clients.Object);
        var dispatcher = new CallCaptionResultDispatcher(
            registry,
            hubContext.Object,
            services.GetRequiredService<IServiceScopeFactory>());
        var source = new CallAudioChunk(
            requested.CallSessionId,
            roomId,
            speakerId,
            "Speaker",
            "audio/linear16;rate=16000;channels=1",
            [],
            DateTimeOffset.Parse("2026-08-30T00:00:00Z"),
            DateTimeOffset.Parse("2026-08-30T00:00:00.250Z"),
            requested.ConsentGeneration,
            connectionId,
            "vi");
        return new DispatcherFixture(services, registry, dispatcher, source, transcriptService, clients, proxy);
    }

    private static CallTranscriptionResult Result(string text, bool isFinal) => new(
        text,
        DateTimeOffset.Parse("2026-08-30T00:00:00Z"),
        DateTimeOffset.Parse("2026-08-30T00:00:00.250Z"),
        0.95,
        isFinal,
        isFinal,
        "Deepgram",
        0.25);

    private static bool IsInterimPayload(object?[] arguments, CallAudioChunk source)
    {
        var interim = arguments.Length == 1 ? arguments[0] as CallTranscriptInterimDto : null;
        return interim is not null &&
            interim.CallSessionId == source.CallSessionId &&
            interim.SpeakerUserId == source.SpeakerUserId &&
            interim.SpeakerDisplayName == source.SpeakerDisplayName &&
            interim.Text == "interim fixture";
    }

    private static bool IsFinalPayload(object?[] arguments, CallAudioChunk source)
    {
        var chunk = arguments.Length == 1 ? arguments[0] as CallTranscriptChunkDto : null;
        return chunk is not null &&
            chunk.CallSessionId == source.CallSessionId &&
            chunk.SpeakerUserId == source.SpeakerUserId &&
            chunk.Text == "final fixture";
    }

    private sealed record DispatcherFixture(
        ServiceProvider Services,
        CallRoomRegistry Registry,
        CallCaptionResultDispatcher Dispatcher,
        CallAudioChunk Source,
        RecordingTranscriptService Transcripts,
        Mock<IHubClients> Clients,
        Mock<IClientProxy> Proxy);

    private sealed class RecordingTranscriptService(Guid projectId) : ICallTranscriptService
    {
        public int AppendCalls { get; private set; }

        public Task<CallTranscriptChunkDto?> AppendAsync(
            CallAudioChunk source,
            CallTranscriptionResult result,
            CancellationToken cancellationToken = default)
        {
            AppendCalls++;
            return Task.FromResult<CallTranscriptChunkDto?>(new(
                Guid.NewGuid(),
                source.CallSessionId,
                projectId,
                "general",
                source.SpeakerUserId,
                source.SpeakerDisplayName,
                result.StartedAt,
                result.EndedAt,
                result.Text.Trim(),
                result.Confidence));
        }

        public Task<IReadOnlyList<CallTranscriptChunkDto>> GetAsync(
            Guid requestedProjectId,
            string voiceChannelId,
            Guid callSessionId,
            CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<CallTranscriptChunkDto>>([]);
    }

    private sealed class CapturingStreamingProvider : ICallStreamingTranscriptionProvider
    {
        public Func<CallAudioChunk, CallTranscriptionResult, Task>? OnResult { get; private set; }
        public Func<bool>? CanContinue { get; private set; }
        public bool IsConfigured => true;
        public string ProviderName => "Deepgram";
        public IReadOnlyList<string> SupportedLanguages => ["vi", "en"];
        public string DefaultLanguage => "vi";

        public Task SubmitAsync(
            CallAudioChunk chunk,
            Func<CallAudioChunk, CallTranscriptionResult, Task> onResult,
            Func<bool> canContinue,
            CancellationToken cancellationToken = default)
        {
            OnResult = onResult;
            CanContinue = canContinue;
            return Task.CompletedTask;
        }

        public Task StopAsync(string roomId, Guid callSessionId, Guid speakerUserId, long consentGeneration, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task StopRoomAsync(string roomId, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task<CallTranscriptionResult?> TranscribeAsync(CallAudioChunk chunk, CancellationToken cancellationToken = default) => Task.FromResult<CallTranscriptionResult?>(null);
    }
}
