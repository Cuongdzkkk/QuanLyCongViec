using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Net.Http.Headers;
using System.Security.Claims;
using FluentAssertions;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using Moq;
using TaskManagement.Application.Interfaces;
using TaskManagement.Domain.Entities;
using TaskManagement.Infrastructure.Data;
using TaskManagement.Infrastructure.Services;

namespace TaskManagement.Tests.Logic;

[Collection("ProjectAccessPolicy")]
public sealed class P1ProjectLifecycleIntegrityTests
{
    [Fact]
    public async Task RestoreDeleted_AuthorizedProjectManagerCanRestoreDeletedProject()
    {
        await using var factory = new ChatApplicationFactory();
        var userId = Guid.NewGuid();
        var projectId = Guid.NewGuid();
        await SeedDeletedProjectAsync(factory, userId, projectId, "PM");

        using var client = CreateClient(factory, userId);
        using var response = await client.PutAsync($"/api/projects/{projectId}/restore-deleted", content: null);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        await using var scope = factory.Services.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        (await context.Projects.IgnoreQueryFilters().SingleAsync(project => project.Id == projectId))
            .IsDeleted.Should().BeFalse();
    }

    [Fact]
    public async Task RestoreDeleted_UnauthorizedUserCannotRestoreAnotherProject()
    {
        await using var factory = new ChatApplicationFactory();
        var ownerId = Guid.NewGuid();
        var outsiderId = Guid.NewGuid();
        var projectId = Guid.NewGuid();
        await SeedDeletedProjectAsync(factory, ownerId, projectId, "PM");
        await SeedUserAsync(factory, outsiderId);

        using var client = CreateClient(factory, outsiderId);
        using var response = await client.PutAsync($"/api/projects/{projectId}/restore-deleted", content: null);

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task RestoreDeleted_UserFromAnotherProjectCannotRestoreDeletedProject()
    {
        await using var factory = new ChatApplicationFactory();
        var userAId = Guid.NewGuid();
        var projectAId = Guid.NewGuid();
        var ownerBId = Guid.NewGuid();
        var projectBId = Guid.NewGuid();
        await SeedDeletedProjectAsync(factory, userAId, projectAId, "PM");
        await SeedDeletedProjectAsync(factory, ownerBId, projectBId, "PM");

        using var client = CreateClient(factory, userAId);
        using var response = await client.PutAsync($"/api/projects/{projectBId}/restore-deleted", content: null);

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task RestoreDeleted_ActiveProjectFollowsExistingSafeContract()
    {
        await using var factory = new ChatApplicationFactory();
        var userId = Guid.NewGuid();
        var projectId = Guid.NewGuid();
        await SeedDeletedProjectAsync(factory, userId, projectId, "PM", isDeleted: false);

        using var client = CreateClient(factory, userId);
        using var response = await client.PutAsync($"/api/projects/{projectId}/restore-deleted", content: null);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task NormalProjectRead_DoesNotIncludeDeletedProjectForAuthorizedMember()
    {
        await using var factory = new ChatApplicationFactory();
        var userId = Guid.NewGuid();
        var projectId = Guid.NewGuid();
        await SeedDeletedProjectAsync(factory, userId, projectId, "PM");

        using var client = CreateClient(factory, userId);
        using var response = await client.GetAsync($"/api/projects/{projectId}");

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task PermanentDelete_RemovesDependenciesReferencingEitherTaskSideAndKeepsUnrelatedEdges()
    {
        await using var context = CreateContext();
        var fixture = await SeedDependencyGraphAsync(context);
        var service = new ProjectService(
            context,
            new HttpContextAccessor(),
            Mock.Of<IResourceAuthorizationService>());

        await service.PermanentDeleteAsync(fixture.ProjectAId);

        (await context.TaskDependencies.AnyAsync(edge =>
                edge.PredecessorTaskId == fixture.TaskA1Id || edge.SuccessorTaskId == fixture.TaskA1Id ||
                edge.PredecessorTaskId == fixture.TaskA2Id || edge.SuccessorTaskId == fixture.TaskA2Id))
            .Should().BeFalse();
        (await context.TaskDependencies.CountAsync(edge =>
                edge.PredecessorTaskId == fixture.TaskB1Id && edge.SuccessorTaskId == fixture.TaskB2Id))
                .Should().Be(1);
    }

    [Fact]
    [Trait("Database", "SqlServer")]
    public async Task PermanentDelete_SqlServerRemovesDependenciesReferencingEitherTaskSide()
    {
        var databaseName = $"TaskManagement_P1_Lifecycle_{Guid.NewGuid():N}";
        await using var context = CreateSqlContext(databaseName);
        try
        {
            await context.Database.EnsureCreatedAsync();
            var fixture = await SeedDependencyGraphAsync(context);
            var service = new ProjectService(
                context,
                new HttpContextAccessor(),
                Mock.Of<IResourceAuthorizationService>());

            await service.PermanentDeleteAsync(fixture.ProjectAId);

            (await context.TaskDependencies.CountAsync(edge =>
                    edge.PredecessorTaskId == fixture.TaskA1Id || edge.SuccessorTaskId == fixture.TaskA1Id ||
                    edge.PredecessorTaskId == fixture.TaskA2Id || edge.SuccessorTaskId == fixture.TaskA2Id))
                .Should().Be(0);
            (await context.TaskDependencies.CountAsync(edge =>
                    edge.PredecessorTaskId == fixture.TaskB1Id && edge.SuccessorTaskId == fixture.TaskB2Id))
                .Should().Be(1);
        }
        finally
        {
            await context.Database.EnsureDeletedAsync();
        }
    }

    [Fact]
    public async Task PermanentDelete_RemovesCommentAttachmentRowAndPhysicalFile()
    {
        var root = CreateStorageRoot();
        await using var context = CreateContext();
        var fixture = await SeedCommentAsync(context, "Project", "deleted.pdf");
        var filePath = WriteCommentFile(root, "deleted.pdf");
        var service = CreateProjectService(context, root);

        await service.PermanentDeleteAsync(fixture.ProjectId);

        (await context.CommentAttachments.AnyAsync(attachment => attachment.Id == fixture.AttachmentId))
            .Should().BeFalse();
        File.Exists(filePath).Should().BeFalse();
        Directory.Delete(root, recursive: true);
    }

    [Fact]
    public async Task SoftDelete_PreservesCommentAttachmentAndPhysicalFile()
    {
        var root = CreateStorageRoot();
        await using var context = CreateContext();
        var fixture = await SeedCommentAsync(context, "Project", "soft-delete.pdf");
        var filePath = WriteCommentFile(root, "soft-delete.pdf");
        var service = CreateProjectService(context, root);

        await service.SoftDeleteAsync(fixture.ProjectId);

        (await context.CommentAttachments.AnyAsync(attachment => attachment.Id == fixture.AttachmentId))
            .Should().BeTrue();
        File.Exists(filePath).Should().BeTrue();
        Directory.Delete(root, recursive: true);
    }

    [Fact]
    public async Task Archive_PreservesCommentAttachmentAndPhysicalFile()
    {
        var root = CreateStorageRoot();
        await using var context = CreateContext();
        var fixture = await SeedCommentAsync(context, "Project", "archive.pdf");
        var filePath = WriteCommentFile(root, "archive.pdf");
        var service = CreateProjectService(context, root);

        await service.ArchiveAsync(fixture.ProjectId);

        (await context.CommentAttachments.AnyAsync(attachment => attachment.Id == fixture.AttachmentId))
            .Should().BeTrue();
        File.Exists(filePath).Should().BeTrue();
        Directory.Delete(root, recursive: true);
    }

    [Fact]
    public async Task PermanentDelete_DoesNotDeletePhysicalFileStillReferencedBySurvivingComment()
    {
        var root = CreateStorageRoot();
        await using var context = CreateContext();
        var deleted = await SeedCommentAsync(context, "Project", "shared.pdf");
        var surviving = await SeedCommentAsync(context, "Project", "shared.pdf");
        var sharedFileName = "shared.pdf";
        var sharedFileUrl = $"/uploads/comments/{sharedFileName}";
        var attachments = await context.CommentAttachments
            .Where(attachment => attachment.Id == deleted.AttachmentId || attachment.Id == surviving.AttachmentId)
            .ToListAsync();
        attachments.ForEach(attachment => attachment.FileUrl = sharedFileUrl);
        var filePath = WriteCommentFile(root, sharedFileName);
        var service = CreateProjectService(context, root);

        await service.PermanentDeleteAsync(deleted.ProjectId);

        (await context.CommentAttachments.AnyAsync(attachment => attachment.Id == deleted.AttachmentId))
            .Should().BeFalse();
        (await context.CommentAttachments.AnyAsync(attachment => attachment.Id == surviving.AttachmentId))
            .Should().BeTrue();
        File.Exists(filePath).Should().BeTrue();
        Directory.Delete(root, recursive: true);
    }

    [Fact]
    public async Task PermanentDelete_ToleratesMissingCommentPhysicalFile()
    {
        var root = CreateStorageRoot();
        await using var context = CreateContext();
        var fixture = await SeedCommentAsync(context, "Project", "missing.pdf");
        var service = CreateProjectService(context, root);

        var action = () => service.PermanentDeleteAsync(fixture.ProjectId);

        await action.Should().NotThrowAsync();
        (await context.CommentAttachments.AnyAsync(attachment => attachment.Id == fixture.AttachmentId))
            .Should().BeFalse();
        Directory.Delete(root, recursive: true);
    }

    [Fact]
    public async Task PermanentDelete_ContinuesAfterPhysicalFileDeletionFailure()
    {
        var root = CreateStorageRoot();
        await using var context = CreateContext();
        var fixture = await SeedCommentAsync(context, "Project", "directory-instead-of-file");
        var fileName = "directory-instead-of-file";
        var directoryPath = Path.Combine(root, "uploads", "comments", fileName);
        Directory.CreateDirectory(directoryPath);
        var attachment = await context.CommentAttachments.SingleAsync(item => item.Id == fixture.AttachmentId);
        attachment.FileUrl = $"/uploads/comments/{fileName}";
        await context.SaveChangesAsync();
        var service = CreateProjectService(context, root);

        await service.PermanentDeleteAsync(fixture.ProjectId);

        (await context.CommentAttachments.AnyAsync(item => item.Id == fixture.AttachmentId))
            .Should().BeFalse();
        Directory.Delete(root, recursive: true);
    }

    private static async Task SeedDeletedProjectAsync(
        ChatApplicationFactory factory,
        Guid userId,
        Guid projectId,
        string projectRole,
        bool isDeleted = true)
    {
        await SeedUserAsync(factory, userId);
        await using var scope = factory.Services.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var workspaceId = Guid.NewGuid();
        context.Workspaces.Add(new Workspace
        {
            Id = workspaceId,
            Name = "Deleted project workspace",
            Slug = $"deleted-{workspaceId:N}",
            OwnerId = userId,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        });
        context.WorkspaceMembers.Add(new WorkspaceMember
        {
            WorkspaceId = workspaceId,
            UserId = userId,
            WorkspaceRole = "MEMBER",
            IsActive = true,
            JoinedAt = DateTime.UtcNow
        });
        context.Projects.Add(new Project
        {
            Id = projectId,
            WorkspaceId = workspaceId,
            CreatorId = userId,
            Name = "Deleted project",
            Identifier = $"DEL{projectId:N}"[..8],
            IsDeleted = isDeleted,
            Status = true,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        });
        context.ProjectMembers.Add(new ProjectMember
        {
            ProjectId = projectId,
            UserId = userId,
            ProjectRole = projectRole,
            Status = true,
            JoinedAt = DateTime.UtcNow
        });
        await context.SaveChangesAsync();
    }

    private static async Task SeedUserAsync(ChatApplicationFactory factory, Guid userId)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        if (await context.Users.AnyAsync(user => user.Id == userId)) return;
        context.Users.Add(new User
        {
            Id = userId,
            Email = $"lifecycle-{userId:N}@test.local",
            FullName = "Lifecycle user",
            PasswordHash = "test-only",
            IsActive = true,
            CreatedAt = DateTime.UtcNow
        });
        await context.SaveChangesAsync();
    }

    private static HttpClient CreateClient(ChatApplicationFactory factory, Guid userId)
    {
        var client = factory.CreateClient();
        var options = factory.Services
            .GetRequiredService<IOptionsMonitor<JwtBearerOptions>>()
            .Get(JwtBearerDefaults.AuthenticationScheme);
        var credentials = new SigningCredentials(
            options.TokenValidationParameters.IssuerSigningKey,
            SecurityAlgorithms.HmacSha256);
        var token = new JwtSecurityTokenHandler().WriteToken(new JwtSecurityToken(
            issuer: options.TokenValidationParameters.ValidIssuer,
            audience: options.TokenValidationParameters.ValidAudience,
            claims: [new Claim(ClaimTypes.NameIdentifier, userId.ToString())],
            expires: DateTime.UtcNow.AddMinutes(10),
            signingCredentials: credentials));
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return client;
    }

    private static ApplicationDbContext CreateContext() => new(
        new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString("N"))
            .Options);

    private static ApplicationDbContext CreateSqlContext(string databaseName) => new(
        new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseSqlServer(
                $"Server=(localdb)\\MSSQLLocalDB;Initial Catalog={databaseName};Integrated Security=true;TrustServerCertificate=true;Encrypt=false;Connect Timeout=30")
            .Options);

    private static ProjectService CreateProjectService(ApplicationDbContext context, string root) => new(
        context,
        new HttpContextAccessor(),
        Mock.Of<IResourceAuthorizationService>(),
        new TestEnvironment(root));

    private static string CreateStorageRoot()
    {
        var root = Path.Combine(Path.GetTempPath(), $"sprinta-project-lifecycle-{Guid.NewGuid():N}");
        Directory.CreateDirectory(root);
        return root;
    }

    private static string WriteCommentFile(string root, string fileName)
    {
        var directory = Path.Combine(root, "uploads", "comments");
        Directory.CreateDirectory(directory);
        var path = Path.Combine(directory, fileName);
        File.WriteAllBytes(path, "comment attachment"u8.ToArray());
        return path;
    }

    private static async Task<CommentFixture> SeedCommentAsync(
        ApplicationDbContext context,
        string entityType,
        string fileName = "comment.pdf")
    {
        var userId = Guid.NewGuid();
        var workspaceId = Guid.NewGuid();
        var projectId = Guid.NewGuid();
        var commentId = Guid.NewGuid();
        var attachmentId = Guid.NewGuid();
        context.Users.Add(new User { Id = userId, Email = $"comment-{userId:N}@test.local", PasswordHash = "test-only" });
        context.Workspaces.Add(new Workspace { Id = workspaceId, Name = "Comment workspace", Slug = $"comment-{workspaceId:N}", OwnerId = userId });
        context.Projects.Add(new Project { Id = projectId, WorkspaceId = workspaceId, CreatorId = userId, Name = "Comment project", Identifier = $"C{projectId:N}"[..8] });
        context.Comments.Add(new Comment
        {
            Id = commentId,
            EntityId = projectId,
            EntityType = entityType,
            UserId = userId,
            Content = "comment"
        });
        context.CommentAttachments.Add(new CommentAttachment
        {
            Id = attachmentId,
            CommentId = commentId,
            UploadedByUserId = userId,
            FileName = fileName,
            FileUrl = $"/uploads/comments/{fileName}",
            ContentType = "application/pdf",
            FileSize = 17
        });
        await context.SaveChangesAsync();
        return new(projectId, attachmentId);
    }

    private static async Task<DependencyGraphFixture> SeedDependencyGraphAsync(ApplicationDbContext context)
    {
        var userId = Guid.NewGuid();
        var workspaceId = Guid.NewGuid();
        var projectAId = Guid.NewGuid();
        var projectBId = Guid.NewGuid();
        var statusAId = Guid.NewGuid();
        var statusBId = Guid.NewGuid();
        var typeAId = Guid.NewGuid();
        var typeBId = Guid.NewGuid();
        var taskA1Id = Guid.NewGuid();
        var taskA2Id = Guid.NewGuid();
        var taskB1Id = Guid.NewGuid();
        var taskB2Id = Guid.NewGuid();
        context.Users.Add(new User
        {
            Id = userId,
            Email = $"dependency-{userId:N}@test.local",
            PasswordHash = "test-only",
            IsActive = true
        });
        context.Workspaces.Add(new Workspace
        {
            Id = workspaceId,
            Name = "Dependency workspace",
            Slug = $"dependency-{workspaceId:N}",
            OwnerId = userId
        });
        context.Projects.AddRange(
            new Project { Id = projectAId, WorkspaceId = workspaceId, CreatorId = userId, Name = "Project A", Identifier = "PA" },
            new Project { Id = projectBId, WorkspaceId = workspaceId, CreatorId = userId, Name = "Project B", Identifier = "PB" });
        context.TaskStatuses.AddRange(
            new TaskManagement.Domain.Entities.TaskStatus { Id = statusAId, ProjectId = projectAId, Name = "To Do" },
            new TaskManagement.Domain.Entities.TaskStatus { Id = statusBId, ProjectId = projectBId, Name = "To Do" });
        context.TaskTypes.AddRange(
            new TaskType { Id = typeAId, ProjectId = projectAId, Name = "Task" },
            new TaskType { Id = typeBId, ProjectId = projectBId, Name = "Task" });
        context.WorkTasks.AddRange(
            NewTask(taskA1Id, projectAId, workspaceId, statusAId, typeAId, userId, "A1"),
            NewTask(taskA2Id, projectAId, workspaceId, statusAId, typeAId, userId, "A2"),
            NewTask(taskB1Id, projectBId, workspaceId, statusBId, typeBId, userId, "B1"),
            NewTask(taskB2Id, projectBId, workspaceId, statusBId, typeBId, userId, "B2"));
        context.TaskDependencies.AddRange(
            Edge(taskA1Id, taskA2Id),
            Edge(taskA1Id, taskB1Id),
            Edge(taskB1Id, taskA2Id),
            Edge(taskB1Id, taskB2Id));
        await context.SaveChangesAsync();
        return new(projectAId, taskA1Id, taskA2Id, taskB1Id, taskB2Id);
    }

    private static WorkTask NewTask(
        Guid id,
        Guid projectId,
        Guid workspaceId,
        Guid statusId,
        Guid typeId,
        Guid reporterId,
        string title) => new()
        {
            Id = id,
            ProjectId = projectId,
            WorkspaceId = workspaceId,
            TaskStatusId = statusId,
            TaskTypeId = typeId,
            ReporterId = reporterId,
            Title = title,
            SequenceId = $"{title}-{id:N}",
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

    private static TaskDependency Edge(Guid predecessorId, Guid successorId) => new()
    {
        PredecessorTaskId = predecessorId,
        SuccessorTaskId = successorId,
        DependencyType = 1
    };

    private sealed record DependencyGraphFixture(
        Guid ProjectAId,
        Guid TaskA1Id,
        Guid TaskA2Id,
        Guid TaskB1Id,
        Guid TaskB2Id);

    private sealed record CommentFixture(Guid ProjectId, Guid AttachmentId);

    private sealed class TestEnvironment(string root) : IHostEnvironment
    {
        public string EnvironmentName { get; set; } = "Testing";
        public string ApplicationName { get; set; } = "TaskManagement.Tests";
        public string ContentRootPath { get; set; } = root;
        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
    }
}
