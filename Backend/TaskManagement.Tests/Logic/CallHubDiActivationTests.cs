using System.Reflection;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using TaskManagement.API.Hubs;
using TaskManagement.API.Services;
using TaskManagement.Application.Interfaces;

namespace TaskManagement.Tests.Logic;

public sealed class CallHubDiActivationTests
{
    [Fact]
    public void SignalRStyleActivatorResolvesCallHubWithOneCompleteConstructor()
    {
        var services = new ServiceCollection();
        services.AddSingleton<ICallRoomRegistry, CallRoomRegistry>();
        services.AddSingleton<ICallRoomAuthorizationService>(Mock.Of<ICallRoomAuthorizationService>());
        services.AddSingleton<ICallTranscriptionProvider>(Mock.Of<ICallTranscriptionProvider>());
        services.AddSingleton<ICallTranscriptService>(Mock.Of<ICallTranscriptService>());
        services.AddSingleton<ICallChatService>(Mock.Of<ICallChatService>());
        services.AddSingleton<ICallCaptionResultDispatcher>(Mock.Of<ICallCaptionResultDispatcher>());

        using var provider = services.BuildServiceProvider();
        typeof(CallHub).GetConstructors(BindingFlags.Instance | BindingFlags.Public)
            .Should().ContainSingle();

        var hub = ActivatorUtilities.CreateInstance<CallHub>(provider);

        hub.Should().NotBeNull();
    }
}
