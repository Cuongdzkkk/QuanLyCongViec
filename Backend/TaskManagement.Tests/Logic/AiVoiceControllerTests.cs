using System.Security.Claims;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Moq;
using TaskManagement.API.Controllers;
using TaskManagement.Application.Interfaces;
using TaskManagement.Infrastructure.Data;
using TaskManagement.Infrastructure.Services;

namespace TaskManagement.Tests.Logic
{
    public class AiVoiceControllerTests : IDisposable
    {
        private readonly ApplicationDbContext _context;
        private readonly Mock<IAiService> _aiService = new();
        private readonly Guid _userId = Guid.NewGuid();

        public AiVoiceControllerTests()
        {
            var options = new DbContextOptionsBuilder<ApplicationDbContext>()
                .UseInMemoryDatabase(Guid.NewGuid().ToString())
                .Options;
            _context = new ApplicationDbContext(options);
        }

        [Fact]
        public async Task TranscribeAudio_ValidWave_UsesAuthenticatedUserAndLanguage()
        {
            _aiService
                .Setup(service => service.TranscribeAudioAsync(
                    _userId,
                    "vi",
                    "audio/wav",
                    It.Is<byte[]>(bytes => bytes.Length == 44),
                    It.IsAny<CancellationToken>()))
                .ReturnsAsync("Tạo task kiểm thử giọng nói");
            var controller = CreateController();

            var result = await controller.TranscribeAudio(CreateWaveFile(), "vi", CancellationToken.None);

            result.Should().BeOfType<OkObjectResult>();
            _aiService.VerifyAll();
        }

        [Fact]
        public async Task TranscribeAudio_UnsupportedLanguage_Returns400WithoutCallingProvider()
        {
            var controller = CreateController();

            var result = await controller.TranscribeAudio(CreateWaveFile(), "fr", CancellationToken.None);

            result.Should().BeOfType<BadRequestObjectResult>();
            _aiService.Verify(service => service.TranscribeAudioAsync(
                It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<byte[]>(), It.IsAny<CancellationToken>()),
                Times.Never);
        }

        [Fact]
        public async Task TranscribeAudio_InvalidWaveContent_Returns400WithoutCallingProvider()
        {
            var bytes = new byte[44];
            var controller = CreateController();

            var result = await controller.TranscribeAudio(CreateFile(bytes, "audio/wav"), "auto", CancellationToken.None);

            result.Should().BeOfType<BadRequestObjectResult>();
            _aiService.Verify(service => service.TranscribeAudioAsync(
                It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<byte[]>(), It.IsAny<CancellationToken>()),
                Times.Never);
        }

        [Fact]
        public async Task TranscribeAudio_UnsupportedMimeType_Returns400WithoutCallingProvider()
        {
            var controller = CreateController();

            var result = await controller.TranscribeAudio(CreateFile(CreateWaveBytes(), "audio/webm"), "en", CancellationToken.None);

            result.Should().BeOfType<BadRequestObjectResult>();
            _aiService.Verify(service => service.TranscribeAudioAsync(
                It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<byte[]>(), It.IsAny<CancellationToken>()),
                Times.Never);
        }

        private AiController CreateController()
        {
            var controller = new AiController(
                _aiService.Object,
                Mock.Of<IAiAttachmentService>(),
                Mock.Of<IWorkTaskService>(),
                Mock.Of<IProjectService>(),
                Mock.Of<IGoalService>(),
                _context,
                new ResourceAuthorizationService(_context));
            controller.ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext
                {
                    User = new ClaimsPrincipal(new ClaimsIdentity(new[]
                    {
                        new Claim(ClaimTypes.NameIdentifier, _userId.ToString())
                    }, "TestAuth"))
                }
            };
            return controller;
        }

        private static IFormFile CreateWaveFile() => CreateFile(CreateWaveBytes(), "audio/wav");

        private static IFormFile CreateFile(byte[] bytes, string contentType) => new FormFile(
            new MemoryStream(bytes), 0, bytes.Length, "audio", "voice-recording.wav")
        {
            Headers = new HeaderDictionary(),
            ContentType = contentType
        };

        private static byte[] CreateWaveBytes()
        {
            var bytes = new byte[44];
            "RIFF"u8.CopyTo(bytes.AsSpan(0, 4));
            "WAVE"u8.CopyTo(bytes.AsSpan(8, 4));
            "fmt "u8.CopyTo(bytes.AsSpan(12, 4));
            "data"u8.CopyTo(bytes.AsSpan(36, 4));
            return bytes;
        }

        public void Dispose()
        {
            _context.Dispose();
        }
    }
}
