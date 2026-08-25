using FluentAssertions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Moq;
using TaskManagement.Application.DTOs.Collaboration;
using TaskManagement.Infrastructure.Services;

namespace TaskManagement.Tests.Logic;

public sealed class MeetingAiContractTests
{
    [Fact]
    public void AnalysisStaysUnavailableWithoutExplicitEnablementAndProviderSecret()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["MeetingAi:Enabled"] = "true",
                ["MeetingAi:TranscriptChunkSize"] = "8"
            })
            .Build();
        var service = new MeetingAiAnalysisService(
            Mock.Of<IServiceScopeFactory>(),
            configuration,
            Mock.Of<ILogger<MeetingAiAnalysisService>>());

        service.IsConfigured.Should().BeFalse();
        service.ProviderName.Should().Be("Unavailable");
        service.TranscriptChunkSize.Should().Be(8);
    }

    [Theory]
    [InlineData(1, 4)]
    [InlineData(8, 8)]
    [InlineData(99, 20)]
    public void TranscriptWindowIsBounded(int configured, int expected)
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["MeetingAi:Enabled"] = "true",
                ["MeetingAi:TranscriptChunkSize"] = configured.ToString(),
                ["ZenMux:ApiKey"] = "test-only"
            })
            .Build();
        var service = new MeetingAiAnalysisService(
            Mock.Of<IServiceScopeFactory>(),
            configuration,
            Mock.Of<ILogger<MeetingAiAnalysisService>>());

        service.IsConfigured.Should().BeTrue();
        service.TranscriptChunkSize.Should().Be(expected);
    }

    [Fact]
    public void MeetingReportNeverClaimsAutomaticWorkItemCreation()
    {
        var report = new MeetingAiReportDto(
            Guid.NewGuid(), Guid.NewGuid(), "general", "COMPLETED", 8,
            new MeetingAiCompactStateDto("Summary", [], [], [], [], []),
            DateTimeOffset.UtcNow, DateTimeOffset.UtcNow);

        report.AutoCreatesTasks.Should().BeFalse();
    }
}
