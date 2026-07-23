using System.Security.Claims;
using FluentAssertions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using TaskManagement.API.Controllers;
using TaskManagement.API.Security;
using TaskManagement.Domain.Entities;
using TaskManagement.Infrastructure.Data;

namespace TaskManagement.Tests.Logic;

public sealed class P006UploadSecurityTests
{
    [Fact]
    public async Task HOST_06_SpoofedMimeAndSignatureAreRejected()
    {
        var file = FormFile("fake.png", "image/png", "%PDF-not-an-image"u8.ToArray());
        var action = () => UploadSecurity.ReadPublicImageAsync(file);
        await action.Should().ThrowAsync<InvalidDataException>().WithMessage("*signature*");
    }

    [Fact]
    public async Task HOST_06_PathTraversalFilenameIsRejected()
    {
        var file = FormFile("../avatar.png", "image/png", PngBytes());
        var action = () => UploadSecurity.ReadPublicImageAsync(file);
        await action.Should().ThrowAsync<InvalidDataException>().WithMessage("*filename*");
        var resolve = () => UploadSecurity.ResolveUnderRoot(Path.GetTempPath(), "../outside.pdf");
        resolve.Should().Throw<InvalidDataException>();
    }

    [Fact]
    public async Task HOST_06_FileOverProfileLimitIsRejected()
    {
        var file = FormFile("large.png", "image/png", new byte[5 * 1024 * 1024 + 1]);
        var action = () => UploadSecurity.ReadPublicImageAsync(file);
        await action.Should().ThrowAsync<InvalidDataException>().WithMessage("*5MB*");
    }

    [Fact]
    public async Task HOST_06_PublicAvatarAndCoverImageProfileAcceptsRealSignature()
    {
        var upload = await UploadSecurity.ReadPublicImageAsync(FormFile("avatar.png", "image/png", PngBytes()));
        upload.Extension.Should().Be(".png");
        upload.MimeType.Should().Be("image/png");
    }

    [Fact]
    public async Task HOST_06_PrivateAttachmentAnonymousAndOutsideWorkspaceAreDenied()
    {
        typeof(PrivateAttachmentsController).GetCustomAttributes(typeof(AuthorizeAttribute), true).Should().NotBeEmpty();
        var root = Path.Combine(Path.GetTempPath(), $"SprintA-P006-{Guid.NewGuid():N}");
        Directory.CreateDirectory(Path.Combine(root, "uploads", "comments"));
        var options = new DbContextOptionsBuilder<ApplicationDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString("N")).Options;
        await using var context = new ApplicationDbContext(options);
        var workspaceId = Guid.NewGuid();
        var projectId = Guid.NewGuid();
        var taskId = Guid.NewGuid();
        var commentId = Guid.NewGuid();
        var attachmentId = Guid.NewGuid();
        var authorId = Guid.NewGuid();
        var fileName = $"{Guid.NewGuid():N}.pdf";
        await File.WriteAllBytesAsync(Path.Combine(root, "uploads", "comments", fileName), "%PDF-private"u8.ToArray());
        context.Projects.Add(new Project { Id = projectId, WorkspaceId = workspaceId, Name = "Private", Identifier = "PVT" });
        context.WorkTasks.Add(new WorkTask { Id = taskId, ProjectId = projectId, WorkspaceId = workspaceId, ReporterId = authorId, Title = "Private task" });
        context.Comments.Add(new Comment { Id = commentId, EntityType = "WorkTask", EntityId = taskId, UserId = authorId, Content = "private" });
        context.CommentAttachments.Add(new CommentAttachment
        {
            Id = attachmentId,
            CommentId = commentId,
            UploadedByUserId = authorId,
            FileName = "private.pdf",
            FileUrl = $"/uploads/comments/{fileName}",
            ContentType = "application/pdf"
        });
        await context.SaveChangesAsync();
        var controller = new PrivateAttachmentsController(context, new TestEnvironment(root));

        controller.ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext() };
        (await controller.DownloadCommentAttachment(attachmentId)).Should().BeOfType<UnauthorizedResult>();

        var outsiderId = Guid.NewGuid();
        controller.ControllerContext.HttpContext.User = new ClaimsPrincipal(new ClaimsIdentity(
            new[] { new Claim(ClaimTypes.NameIdentifier, outsiderId.ToString()) }, "test"));
        (await controller.DownloadCommentAttachment(attachmentId)).Should().BeOfType<ForbidResult>();
        Directory.Delete(root, recursive: true);
    }

    private static FormFile FormFile(string name, string contentType, byte[] bytes) => new(new MemoryStream(bytes), 0, bytes.Length, "file", name)
    {
        Headers = new HeaderDictionary(),
        ContentType = contentType
    };

    private static byte[] PngBytes() => [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A, 0, 0, 0, 0];

    private sealed class TestEnvironment(string root) : IWebHostEnvironment
    {
        public string WebRootPath { get; set; } = Path.Combine(root, "wwwroot");
        public IFileProvider WebRootFileProvider { get; set; } = new NullFileProvider();
        public string ApplicationName { get; set; } = "TaskManagement.Tests";
        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
        public string ContentRootPath { get; set; } = root;
        public string EnvironmentName { get; set; } = "Testing";
    }
}
