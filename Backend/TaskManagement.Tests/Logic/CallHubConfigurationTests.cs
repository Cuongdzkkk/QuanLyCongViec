using FluentAssertions;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Options;
using TaskManagement.API.Hubs;

namespace TaskManagement.Tests.Logic;

public sealed class CallHubConfigurationTests
{
    [Fact]
    public void CallHubLimitConstantsRemainBoundedAt128Kb()
    {
        CallHub.MaximumReceiveMessageSize.Should().Be(131072);
        CallHub.MaximumReceiveMessageSize.Should().BeLessThanOrEqualTo(128 * 1024);
        CallHub.MaximumReceiveMessageSize.Should().BeGreaterThan(0);
    }

    [Fact]
    public void CallHubOptionsTypeIsAvailableForScopedConfiguration()
    {
        typeof(HubOptions<CallHub>).GetProperty(nameof(HubOptions.MaximumReceiveMessageSize))
            .Should().NotBeNull();
    }
}
