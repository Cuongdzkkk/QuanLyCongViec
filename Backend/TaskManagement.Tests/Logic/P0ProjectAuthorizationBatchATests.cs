using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Claims;
using FluentAssertions;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using TaskManagement.Application.Common;
using TaskManagement.Domain.Entities;
using TaskManagement.Infrastructure.Data;
using TaskStatusEntity = TaskManagement.Domain.Entities.TaskStatus;

namespace TaskManagement.Tests.Logic;

[Collection("ProjectAccessPolicy")]
public sealed class P0ProjectAuthorizationBatchATests
{
    [Fact]
    public async Task UnrelatedUserCannotReadOrWriteBatchAProjectResourcesWhenRestrictionsAreOff()
    {
        var previous = ProjectAccessPolicy.RestrictionsEnabled;
        try
        {
            await using var factory = new ChatApplicationFactory();
            var fixture = await SeedAsync(factory);
            ProjectAccessPolicy.Configure(false);

            using var userA = CreateClient(factory, fixture.UserAId);
            using var userB = CreateClient(factory, fixture.UserBId);

            (await userA.GetAsync($"/api/comments/Project/{fixture.ProjectBId}")).StatusCode.Should().Be(HttpStatusCode.Forbidden);
            (await userA.PostAsync($"/api/comments/Project/{fixture.ProjectBId}", CommentForm("blocked"))).StatusCode.Should().Be(HttpStatusCode.Forbidden);
            (await userA.PostAsync($"/api/comments/WorkTask/{fixture.TaskBId}", CommentFormWithFile("blocked"))).StatusCode.Should().Be(HttpStatusCode.Forbidden);
            (await userA.GetAsync($"/api/projects/{fixture.ProjectBId}/comments")).StatusCode.Should().Be(HttpStatusCode.Forbidden);
            (await userA.PostAsJsonAsync(
                $"/api/projects/{fixture.ProjectBId}/Comments",
                new { workTaskId = fixture.TaskBId, content = "blocked" })).StatusCode.Should().Be(HttpStatusCode.Forbidden);

            foreach (var tab in new[] { "lessons", "risks", "decisions", "updates" })
            {
                (await userA.GetAsync($"/api/projects/{fixture.ProjectBId}/{tab}")).StatusCode.Should().Be(HttpStatusCode.Forbidden);
                (await userA.PostAsJsonAsync($"/api/projects/{fixture.ProjectBId}/{tab}", new { text = "blocked" })).StatusCode.Should().Be(HttpStatusCode.Forbidden);
            }

            (await userA.GetAsync($"/api/projects/{fixture.ProjectBId}/work-items")).StatusCode.Should().Be(HttpStatusCode.Forbidden);

            // The same project ID must not authorize a task/comment owned by another project.
            (await userA.GetAsync($"/api/projects/{fixture.ProjectAId}/WorkTasks/{fixture.TaskBId}/comments")).StatusCode.Should().Be(HttpStatusCode.NotFound);
            (await userA.PutAsJsonAsync(
                $"/api/projects/{fixture.ProjectAId}/WorkTasks/{fixture.TaskBId}/comments/{fixture.UserACommentOnProjectBId}",
                new { content = "blocked" })).StatusCode.Should().Be(HttpStatusCode.NotFound);

            // A valid member retains access to the same project-owned resources.
            (await userB.GetAsync($"/api/comments/Project/{fixture.ProjectBId}")).StatusCode.Should().Be(HttpStatusCode.OK);
            (await userB.GetAsync($"/api/projects/{fixture.ProjectBId}/lessons")).StatusCode.Should().Be(HttpStatusCode.OK);
            (await userB.GetAsync($"/api/projects/{fixture.ProjectBId}/risks")).StatusCode.Should().Be(HttpStatusCode.OK);
            (await userB.GetAsync($"/api/projects/{fixture.ProjectBId}/decisions")).StatusCode.Should().Be(HttpStatusCode.OK);
            (await userB.GetAsync($"/api/projects/{fixture.ProjectBId}/updates")).StatusCode.Should().Be(HttpStatusCode.OK);
            (await userB.GetAsync($"/api/projects/{fixture.ProjectBId}/work-items")).StatusCode.Should().Be(HttpStatusCode.OK);
            (await userB.PostAsJsonAsync(
                $"/api/projects/{fixture.ProjectBId}/lessons",
                new { text = "valid member write" })).StatusCode.Should().Be(HttpStatusCode.Created);
            (await userB.PostAsync(
                $"/api/comments/Project/{fixture.ProjectBId}",
                CommentForm("valid member comment"))).StatusCode.Should().Be(HttpStatusCode.OK);
        }
        finally
        {
            ProjectAccessPolicy.Configure(previous);
        }
    }

    [Fact]
    public async Task CrossProjectCommentDeleteAndAttachmentMetadataAreForbidden()
    {
        var previous = ProjectAccessPolicy.RestrictionsEnabled;
        try
        {
            await using var factory = new ChatApplicationFactory();
            var fixture = await SeedAsync(factory);
            ProjectAccessPolicy.Configure(false);
            using var userA = CreateClient(factory, fixture.UserAId);

            (await userA.DeleteAsync($"/api/comments/{fixture.UserACommentOnProjectBId}")).StatusCode
                .Should().BeOneOf(HttpStatusCode.Forbidden, HttpStatusCode.NotFound);
            (await userA.GetAsync($"/api/private-attachments/comments/{fixture.AttachmentOnProjectBId}")).StatusCode.Should().Be(HttpStatusCode.Forbidden);
        }
        finally
        {
            ProjectAccessPolicy.Configure(previous);
        }
    }

    private static MultipartFormDataContent CommentForm(string content)
    {
        var form = new MultipartFormDataContent();
        form.Add(new StringContent(content), "content");
        return form;
    }

    private static MultipartFormDataContent CommentFormWithFile(string content)
    {
        var form = CommentForm(content);
        var file = new ByteArrayContent([1]);
        file.Headers.ContentType = new MediaTypeHeaderValue("text/plain");
        form.Add(file, "files", "attachment.txt");
        return form;
    }

    private static HttpClient CreateClient(ChatApplicationFactory factory, Guid userId)
    {
        var client = factory.CreateClient();
        var options = factory.Services
            .GetRequiredService<IOptionsMonitor<JwtBearerOptions>>()
            .Get(JwtBearerDefaults.AuthenticationScheme);
        var credentials = new SigningCredentials(
            options.TokenValidationParameters.IssuerSigningKey!,
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

    private static async Task<Fixture> SeedAsync(ChatApplicationFactory factory)
    {
        var fixture = new Fixture(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid());
        await using var scope = factory.Services.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        var now = DateTime.UtcNow;
        var workspaceId = Guid.NewGuid();
        var statusId = Guid.NewGuid();
        var typeId = Guid.NewGuid();

        context.Users.AddRange(
            User(fixture.UserAId, "user-a"),
            User(fixture.UserBId, "user-b"));
        context.Workspaces.Add(new Workspace
        {
            Id = workspaceId,
            Name = "Batch A workspace",
            Slug = $"batch-a-{workspaceId:N}",
            OwnerId = fixture.UserAId,
            CreatedAt = now,
            UpdatedAt = now
        });
        context.WorkspaceMembers.AddRange(
            new WorkspaceMember { WorkspaceId = workspaceId, UserId = fixture.UserAId, WorkspaceRole = "MEMBER", IsActive = true, JoinedAt = now },
            new WorkspaceMember { WorkspaceId = workspaceId, UserId = fixture.UserBId, WorkspaceRole = "MEMBER", IsActive = true, JoinedAt = now });
        context.Projects.AddRange(
            Project(fixture.ProjectAId, workspaceId, fixture.UserAId, "Project A"),
            Project(fixture.ProjectBId, workspaceId, fixture.UserBId, "Project B"));
        context.ProjectMembers.AddRange(
            new ProjectMember { ProjectId = fixture.ProjectAId, UserId = fixture.UserAId, ProjectRole = "Developer", Status = true, JoinedAt = now },
            new ProjectMember { ProjectId = fixture.ProjectBId, UserId = fixture.UserBId, ProjectRole = "PM", Status = true, JoinedAt = now });
        context.TaskStatuses.Add(new TaskStatusEntity { Id = statusId, ProjectId = fixture.ProjectBId, Name = "To Do" });
        context.TaskTypes.Add(new TaskType { Id = typeId, ProjectId = fixture.ProjectBId, Name = "Task" });
        context.WorkTasks.Add(new WorkTask
        {
            Id = fixture.TaskBId,
            ProjectId = fixture.ProjectBId,
            WorkspaceId = workspaceId,
            TaskStatusId = statusId,
            TaskTypeId = typeId,
            ReporterId = fixture.UserBId,
            Title = "Project B task",
            CreatedAt = now,
            UpdatedAt = now
        });
        context.ProjectLessons.Add(new ProjectLesson { Id = Guid.NewGuid(), ProjectId = fixture.ProjectBId, CreatorId = fixture.UserBId, Text = "Lesson B", CreatedAt = now });
        context.ProjectRisks.Add(new ProjectRisk { Id = Guid.NewGuid(), ProjectId = fixture.ProjectBId, CreatorId = fixture.UserBId, Text = "Risk B", CreatedAt = now });
        context.ProjectDecisions.Add(new ProjectDecision { Id = Guid.NewGuid(), ProjectId = fixture.ProjectBId, CreatorId = fixture.UserBId, Text = "Decision B", CreatedAt = now });
        context.ProjectUpdates.Add(new ProjectUpdate { Id = Guid.NewGuid(), ProjectId = fixture.ProjectBId, CreatorId = fixture.UserBId, Content = "Update B", Status = "Open", CreatedAt = now });

        context.Comments.AddRange(
            new Comment
            {
                Id = fixture.UserACommentOnProjectBId,
                EntityType = "Project",
                EntityId = fixture.ProjectBId,
                UserId = fixture.UserAId,
                Content = "Legacy cross-project comment",
                CreatedAt = now,
                UpdatedAt = now
            },
            new Comment
            {
                Id = fixture.TaskCommentBId,
                EntityType = "WorkTask",
                EntityId = fixture.TaskBId,
                UserId = fixture.UserBId,
                Content = "Task comment B",
                CreatedAt = now,
                UpdatedAt = now
            });
        context.CommentAttachments.Add(new CommentAttachment
        {
            Id = fixture.AttachmentOnProjectBId,
            CommentId = fixture.TaskCommentBId,
            UploadedByUserId = fixture.UserBId,
            FileName = "attachment.txt",
            FileUrl = "/uploads/comments/attachment.txt",
            ContentType = "text/plain",
            FileSize = 1,
            CreatedAt = now
        });
        await context.SaveChangesAsync();
        return fixture;
    }

    private static Project Project(Guid id, Guid workspaceId, Guid creatorId, string name) => new()
    {
        Id = id,
        WorkspaceId = workspaceId,
        CreatorId = creatorId,
        Name = name,
        Identifier = name.Replace(" ", string.Empty, StringComparison.Ordinal).ToUpperInvariant(),
        CreatedAt = DateTime.UtcNow,
        UpdatedAt = DateTime.UtcNow,
        Status = true
    };

    private static User User(Guid id, string name) => new()
    {
        Id = id,
        Email = $"{name}-{id:N}@test.local",
        FullName = name,
        PasswordHash = "test-only",
        IsActive = true,
        CreatedAt = DateTime.UtcNow
    };

    private sealed record Fixture(
        Guid UserAId,
        Guid UserBId,
        Guid ProjectAId,
        Guid ProjectBId,
        Guid TaskBId,
        Guid UserACommentOnProjectBId,
        Guid TaskCommentBId,
        Guid AttachmentOnProjectBId,
        Guid UnusedId);
}
