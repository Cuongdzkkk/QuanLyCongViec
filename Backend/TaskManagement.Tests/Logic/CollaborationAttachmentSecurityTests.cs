using System.Security.Claims;
using FluentAssertions;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.FileProviders;
using Moq;
using TaskManagement.API.Controllers;
using TaskManagement.API.Security;
using TaskManagement.API.Services;
using TaskManagement.Application.DTOs.Collaboration;
using TaskManagement.Application.Interfaces;

namespace TaskManagement.Tests.Logic;

public sealed class CollaborationAttachmentSecurityTests : IDisposable
{
    private readonly string _root = Path.Combine(
        Path.GetTempPath(), $"sprinta-collab-attachments-{Guid.NewGuid():N}");

    [Fact]
    public async Task ValidatorInfersContentTypeSanitizesUnicodeLeafAndRejectsDangerousContent()
    {
        var pdf = CreateFile("%PDF-1.7\nfixture"u8.ToArray(), "../../Báo cáo.pdf", "text/html");
        var validated = await UploadSecurity.ReadCollaborationFileAsync(pdf);

        validated.OriginalFileName.Should().Be("Báo cáo.pdf");
        validated.MimeType.Should().Be("application/pdf");
        validated.Bytes.Should().Equal("%PDF-1.7\nfixture"u8.ToArray());

        await FluentActions.Invoking(() => UploadSecurity.ReadCollaborationFileAsync(
                CreateFile("<svg><script/></svg>"u8.ToArray(), "payload.svg", "image/svg+xml")))
            .Should().ThrowAsync<InvalidDataException>();
        await FluentActions.Invoking(() => UploadSecurity.ReadCollaborationFileAsync(
                CreateFile([0x4D, 0x5A, 1, 2], "payload.exe", "application/pdf")))
            .Should().ThrowAsync<InvalidDataException>();
        await FluentActions.Invoking(() => UploadSecurity.ReadCollaborationFileAsync(
                CreateFile([1, 2, 3], "fake.png", "image/png")))
            .Should().ThrowAsync<InvalidDataException>();
        await FluentActions.Invoking(() => UploadSecurity.ReadCollaborationFileAsync(
                CreateFile(new byte[10 * 1024 * 1024 + 1], "large.pdf", "application/pdf")))
            .Should().ThrowAsync<InvalidDataException>();
    }

    [Fact]
    public async Task StorageUsesRandomPrivateKeyAndDeletesOnlyResolvedFiles()
    {
        var storage = new CollaborationAttachmentStorage(new TestEnvironment(_root));
        var stored = await storage.StoreAsync([
            new ValidatedUpload("report.pdf", ".pdf", "application/pdf", "%PDF-1.7"u8.ToArray())
        ]);

        stored.Should().ContainSingle();
        stored[0].StorageKey.ToLowerInvariant().Should().NotContain("report");
        File.Exists(storage.ResolvePath(stored[0].StorageKey)).Should().BeTrue();

        storage.Delete(stored);
        File.Exists(storage.ResolvePath(stored[0].StorageKey)).Should().BeFalse();
        storage.Invoking(item => item.ResolvePath("../outside.pdf"))
            .Should().Throw<InvalidDataException>();
    }

    [Fact]
    public async Task DatabaseFailureDeletesStoredFilesAndStorageFailureCreatesNoMessage()
    {
        var userId = Guid.NewGuid();
        var channelId = Guid.NewGuid();
        var pending = new PendingCollaborationAttachmentDto(
            Guid.NewGuid(), $"{Guid.NewGuid():N}.pdf", "report.pdf", "application/pdf", 12);
        var service = new Mock<IChannelTextService>();
        var storage = new Mock<ICollaborationAttachmentStorage>();
        storage.Setup(item => item.StoreAsync(
                It.IsAny<IReadOnlyList<ValidatedUpload>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([pending]);
        service.Setup(item => item.SendWithAttachmentsAsync(
                channelId, userId, "hello", It.IsAny<IReadOnlyList<PendingCollaborationAttachmentDto>>(),
                It.IsAny<CancellationToken>()))
            .ThrowsAsync(new DbUpdateException("fixture database failure"));
        var controller = WithUser(new ChannelMessagesController(
            service.Object,
            Mock.Of<ICollaborationReadStateService>(),
            Mock.Of<ICollaborationRealtimePublisher>(),
            storage.Object), userId);
        var form = new CollaborationMessageForm
        {
            Content = "hello",
            Files = [CreateFile("%PDF-1.7\nfixture"u8.ToArray(), "report.pdf", "application/pdf")]
        };

        (await controller.SendWithAttachments(channelId, form)).Should().BeOfType<ObjectResult>()
            .Which.StatusCode.Should().Be(500);
        storage.Verify(item => item.Delete(It.Is<IEnumerable<PendingCollaborationAttachmentDto>>(
            values => values.Single().AttachmentId == pending.AttachmentId)), Times.Once);

        storage.Reset();
        storage.Setup(item => item.StoreAsync(
                It.IsAny<IReadOnlyList<ValidatedUpload>>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new IOException("fixture storage failure"));
        (await controller.SendWithAttachments(channelId, form)).Should().BeOfType<ObjectResult>()
            .Which.StatusCode.Should().Be(500);
        service.Verify(item => item.SendWithAttachmentsAsync(
            It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<string>(),
            It.IsAny<IReadOnlyList<PendingCollaborationAttachmentDto>>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    private static FormFile CreateFile(byte[] bytes, string fileName, string contentType) => new(
        new MemoryStream(bytes), 0, bytes.Length, "files", fileName)
    {
        Headers = new HeaderDictionary(),
        ContentType = contentType
    };

    private static ChannelMessagesController WithUser(ChannelMessagesController controller, Guid userId)
    {
        controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext
            {
                User = new ClaimsPrincipal(new ClaimsIdentity(
                    [new Claim(ClaimTypes.NameIdentifier, userId.ToString())], "test"))
            }
        };
        return controller;
    }

    public void Dispose()
    {
        if (Directory.Exists(_root)) Directory.Delete(_root, recursive: true);
    }

    private sealed class TestEnvironment(string contentRoot) : IWebHostEnvironment
    {
        public string ApplicationName { get; set; } = "TaskManagement.Tests";
        public IFileProvider WebRootFileProvider { get; set; } = new NullFileProvider();
        public string WebRootPath { get; set; } = contentRoot;
        public string EnvironmentName { get; set; } = "Testing";
        public string ContentRootPath { get; set; } = contentRoot;
        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
    }
}
