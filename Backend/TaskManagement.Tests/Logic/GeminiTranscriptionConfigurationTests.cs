using System.Net;
using System.Text;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Moq;
using TaskManagement.Application.DTOs.AI;
using TaskManagement.Application.Interfaces;
using TaskManagement.Infrastructure.AI;
using TaskManagement.Infrastructure.Data;
using TaskManagement.Infrastructure.Services;

namespace TaskManagement.Tests.Logic;

public sealed class GeminiTranscriptionConfigurationTests
{
    [Fact]
    public void ComposeMapsGeminiApiKeyToApiContainer()
    {
        var compose = FindRepositoryFile("docker-compose.yml");

        Assert.Contains("Gemini__ApiKey: \"${Gemini__ApiKey}\"", compose);
        Assert.DoesNotContain("AIza", compose, StringComparison.Ordinal);
    }

    [Fact]
    public async Task MissingGeminiKey_TranscriptionThrowsUnavailableConfiguration()
    {
        var credits = CreateCredits();
        var service = CreateService(new Dictionary<string, string?>(), credits.Object, new StubHandler());

        var act = () => service.TranscribeAudioAsync(Guid.NewGuid(), "vi", "audio/wav", ValidWaveBytes());

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(act);
        Assert.Equal("Chưa cấu hình Gemini API key.", exception.Message);
        Mock.Get(credits.Object).Verify(item => item.ReserveAsync(
            It.IsAny<Guid>(), It.IsAny<int>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task ConfiguredGeminiKey_TranscriptionReachesProviderPath()
    {
        var credits = CreateCredits();
        var handler = new StubHandler();
        var service = CreateService(
            new Dictionary<string, string?>
            {
                ["Gemini:ApiKey"] = new string('x', 32),
                ["Gemini:Model"] = "test-model"
            },
            credits.Object,
            handler);

        var transcript = await service.TranscribeAudioAsync(Guid.NewGuid(), "vi", "audio/wav", ValidWaveBytes());

        Assert.Equal("Xin chao", transcript);
        Assert.Equal(HttpMethod.Post, handler.Request?.Method);
        Assert.Contains("test-model:generateContent", handler.Request?.RequestUri?.ToString());
        Mock.Get(credits.Object).Verify(item => item.ReserveAsync(
            It.IsAny<Guid>(), 1, It.Is<string>(value => value.StartsWith("ai-transcription:")), It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task MissingGeminiKey_ErrorMessageIsSafe()
    {
        var credits = CreateCredits();
        var service = CreateService(
            new Dictionary<string, string?> { ["Gemini:ApiKey"] = "" },
            credits.Object,
            new StubHandler());

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.TranscribeAudioAsync(Guid.NewGuid(), "auto", "audio/wav", ValidWaveBytes()));

        Assert.Equal("Chưa cấu hình Gemini API key.", exception.Message);
    }

    private static Mock<IAiCreditUsageService> CreateCredits()
    {
        var credits = new Mock<IAiCreditUsageService>();
        credits.Setup(item => item.GetUsageAsync(
                It.IsAny<Guid>(), It.IsAny<DateTime>(), It.IsAny<DateTime>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new AiCreditUsageDto());
        credits.Setup(item => item.ReserveAsync(
                It.IsAny<Guid>(), It.IsAny<int>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new AiCreditReservationResult(Guid.NewGuid(), true, "Reserved", 1));
        credits.Setup(item => item.FinalizeReservationAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        credits.Setup(item => item.ReleaseReservationAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        return credits;
    }

    private static GeminiAiService CreateService(
        Dictionary<string, string?> settings,
        IAiCreditUsageService credits,
        HttpMessageHandler handler)
    {
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(settings).Build();
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        var context = new ApplicationDbContext(options);
        var providerClient = new ZenMuxAiClient(new HttpClient(new StubHandler()), configuration);
        return new GeminiAiService(
            context,
            new HttpClient(handler),
            providerClient,
            Mock.Of<IWorkTaskService>(),
            credits,
            configuration);
    }

    private static byte[] ValidWaveBytes()
    {
        var bytes = new byte[44];
        Encoding.ASCII.GetBytes("RIFF").CopyTo(bytes, 0);
        Encoding.ASCII.GetBytes("WAVE").CopyTo(bytes, 8);
        Encoding.ASCII.GetBytes("fmt ").CopyTo(bytes, 12);
        Encoding.ASCII.GetBytes("data").CopyTo(bytes, 36);
        return bytes;
    }

    private static string FindRepositoryFile(string fileName)
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            var candidate = Path.Combine(directory.FullName, fileName);
            if (File.Exists(candidate)) return File.ReadAllText(candidate);
            directory = directory.Parent;
        }

        throw new FileNotFoundException(fileName);
    }

    private sealed class StubHandler : HttpMessageHandler
    {
        public HttpRequestMessage? Request { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Request = request;
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(
                    "{\"candidates\":[{\"content\":{\"parts\":[{\"text\":\"Xin chao\"}]}}]}",
                    Encoding.UTF8,
                    "application/json")
            });
        }
    }
}
