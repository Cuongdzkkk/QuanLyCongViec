using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Moq;
using TaskManagement.Application.Common;
using TaskManagement.Application.DTOs.AI;
using TaskManagement.Application.Interfaces;
using TaskManagement.Infrastructure.AI;
using TaskManagement.Infrastructure.Data;
using TaskManagement.Infrastructure.Services;

namespace TaskManagement.Tests.Logic;

public sealed class ZenMuxTranscriptionConfigurationTests
{
    [Fact]
    public void ComposeUsesZenMuxKeyAndTranscriptionModelWithoutGeminiMapping()
    {
        var compose = FindRepositoryFile("docker-compose.yml");

        compose.Should().Contain("ZenMux__ApiKey: \"${ZENMUX_API_KEY}\"");
        compose.Should().Contain("ZenMux__TranscriptionModel: \"${ZENMUX_TRANSCRIPTION_MODEL:-qwen/qwen3-asr-flash}\"");
        compose.Should().NotContain("Gemini__ApiKey");
        compose.Should().NotContain("AIza");
    }

    [Fact]
    public async Task MissingZenMuxKey_TranscriptionIsUnavailableWithoutReservingCredit()
    {
        var credits = CreateCredits();
        var service = CreateService(new Dictionary<string, string?>(), credits.Object, new StubHandler());

        var exception = await Assert.ThrowsAsync<AiProviderException>(() =>
            service.TranscribeAudioAsync(Guid.NewGuid(), "vi", "audio/wav", ValidWaveBytes()));

        exception.Kind.Should().Be(AiProviderErrorKind.Unavailable);
        credits.Verify(item => item.ReserveAsync(
            It.IsAny<Guid>(), It.IsAny<int>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task ConfiguredZenMuxKey_TranscriptionReachesProviderPathWithDocumentedPayload()
    {
        var credits = CreateCredits();
        var handler = new StubHandler();
        var configuration = new Dictionary<string, string?>
        {
            ["ZenMux:ApiKey"] = new string('k', 32),
            ["ZenMux:BaseUrl"] = "https://zenmux.test/api/v1",
            ["ZenMux:TranscriptionModel"] = "qwen/qwen3-asr-flash"
        };
        var service = CreateService(configuration, credits.Object, handler);

        var audioBytes = ValidWaveBytes();
        var transcript = await service.TranscribeAudioAsync(Guid.NewGuid(), "vi", "audio/wav", audioBytes);

        transcript.Should().Be("Xin chao");
        handler.Request.Should().NotBeNull();
        handler.Request!.Method.Should().Be(HttpMethod.Post);
        handler.Request.RequestUri!.ToString().Should().Be("https://zenmux.test/api/v1/audio/transcriptions");
        handler.Request.Headers.Authorization.Should().Be(new AuthenticationHeaderValue("Bearer", new string('k', 32)));

        using var payload = JsonDocument.Parse(handler.RequestBody!);
        payload.RootElement.GetProperty("model").GetString().Should().Be("qwen/qwen3-asr-flash");
        payload.RootElement.GetProperty("language").GetString().Should().Be("vi");
        payload.RootElement.GetProperty("input_audio").GetProperty("format").GetString().Should().Be("wav");
        payload.RootElement.GetProperty("input_audio").GetProperty("data").GetString()
            .Should().Be(Convert.ToBase64String(audioBytes));
    }

    [Fact]
    public async Task ProviderAuthenticationError_DoesNotExposeConfiguredKey()
    {
        var key = new string('k', 32);
        var handler = new StubHandler(HttpStatusCode.Unauthorized);
        var client = new ZenMuxAiClient(
            new HttpClient(handler),
            new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ZenMux:ApiKey"] = key
            }).Build());

        var exception = await Assert.ThrowsAsync<AiTranscriptionProviderException>(() =>
            client.TranscribeAudioAsync("auto", "wav", ValidWaveBytes()));

        exception.Kind.Should().Be(AiTranscriptionProviderErrorKind.Authentication);
        exception.Message.Should().NotContain(key);
    }

    [Fact]
    public async Task ProviderBadRequest_IsReportedAsInvalidTranscriptionRequest()
    {
        var client = new ZenMuxAiClient(
            new HttpClient(new StubHandler(HttpStatusCode.BadRequest)),
            new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ZenMux:ApiKey"] = new string('k', 32)
            }).Build());

        var exception = await Assert.ThrowsAsync<AiTranscriptionProviderException>(() =>
            client.TranscribeAudioAsync("auto", "wav", ValidWaveBytes()));

        exception.Kind.Should().Be(AiTranscriptionProviderErrorKind.InvalidRequest);
    }

    [Fact]
    public async Task ProviderRateLimit_UsesSafeRateLimitedError()
    {
        var client = new ZenMuxAiClient(
            new HttpClient(new StubHandler(HttpStatusCode.TooManyRequests)),
            new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ZenMux:ApiKey"] = new string('k', 32)
            }).Build());

        var exception = await Assert.ThrowsAsync<AiProviderException>(() =>
            client.TranscribeAudioAsync("auto", "wav", ValidWaveBytes()));

        exception.Kind.Should().Be(AiProviderErrorKind.RateLimited);
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
        var providerClient = new ZenMuxAiClient(new HttpClient(handler), configuration);
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

    private sealed class StubHandler(HttpStatusCode statusCode = HttpStatusCode.OK) : HttpMessageHandler
    {
        public HttpRequestMessage? Request { get; private set; }
        public string? RequestBody { get; private set; }

        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            Request = request;
            RequestBody = request.Content is null
                ? null
                : await request.Content.ReadAsStringAsync(cancellationToken);

            return new HttpResponseMessage(statusCode)
            {
                Content = new StringContent(
                    statusCode == HttpStatusCode.OK
                        ? "{\"text\":\"Xin chao\",\"usage\":{\"total_tokens\":7}}"
                        : "{\"error\":{\"message\":\"provider failure\"}}",
                    Encoding.UTF8,
                    "application/json")
            };
        }
    }
}
