using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text.Json;
using FluentAssertions;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using TaskManagement.Domain.Entities;
using TaskManagement.Infrastructure.Data;
using TaskStatusEntity = TaskManagement.Domain.Entities.TaskStatus;

namespace TaskManagement.Tests.Logic;

public sealed class P1NotificationAuthorizationTests
{
    [Fact]
    public async Task UnauthorizedActorCannotCreateTaskCommentOrReminderEvents()
    {
        await using var factory = new ChatApplicationFactory();
        var fixture = await SeedAsync(factory);
        using var userA = CreateClient(factory, fixture.UserAId);

        AssertBlocked(await userA.PostAsJsonAsync(
            "/api/notifications/events/task-assigned",
            new
            {
                projectId = fixture.ProjectBId,
                taskId = fixture.TaskBId,
                assigneeUserId = fixture.UserBId,
                projectName = "Project B",
                taskTitle = "Task B"
            }));
        AssertBlocked(await userA.PostAsJsonAsync(
            "/api/notifications/events/task-status-changed",
            new
            {
                projectId = fixture.ProjectBId,
                taskId = fixture.TaskBId,
                projectName = "Project B",
                taskTitle = "Task B",
                statusName = "Done"
            }));
        AssertBlocked(await userA.PostAsJsonAsync(
            "/api/notifications/events/comment-added",
            new
            {
                projectId = fixture.ProjectBId,
                taskId = fixture.TaskBId,
                projectName = "Project B",
                taskTitle = "Task B"
            }));
        AssertBlocked(await userA.PostAsJsonAsync(
            "/api/notifications/events/task-reminded",
            new
            {
                projectId = fixture.ProjectBId,
                taskId = fixture.TaskBId,
                assigneeUserId = fixture.UserBId,
                projectName = "Project B",
                taskTitle = "Task B"
            }));

        await using var scope = factory.Services.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        (await context.Notifications.CountAsync()).Should().Be(0);
    }

    [Fact]
    public async Task MixedProjectAndTaskIdsCannotAuthorizeNotificationEvents()
    {
        await using var factory = new ChatApplicationFactory();
        var fixture = await SeedAsync(factory);
        using var userA = CreateClient(factory, fixture.UserAId);

        AssertBlocked(await userA.PostAsJsonAsync(
            "/api/notifications/events/task-assigned",
            new { projectId = fixture.ProjectBId, taskId = fixture.TaskAId, assigneeUserId = fixture.UserBId }));
        AssertBlocked(await userA.PostAsJsonAsync(
            "/api/notifications/events/task-status-changed",
            new { projectId = fixture.ProjectBId, taskId = fixture.TaskAId, statusName = "Done" }));
        AssertBlocked(await userA.PostAsJsonAsync(
            "/api/notifications/events/comment-added",
            new { projectId = fixture.ProjectBId, taskId = fixture.TaskAId }));
        AssertBlocked(await userA.PostAsJsonAsync(
            "/api/notifications/events/task-reminded",
            new { projectId = fixture.ProjectBId, taskId = fixture.TaskAId, assigneeUserId = fixture.UserBId }));
    }

    [Fact]
    public async Task AuthorizedActorCanCreateTaskCommentAndReminderEvents()
    {
        await using var factory = new ChatApplicationFactory();
        var fixture = await SeedAsync(factory);
        using var userA = CreateClient(factory, fixture.UserAId);

        (await userA.PostAsJsonAsync(
            "/api/notifications/events/task-assigned",
            new
            {
                projectId = fixture.ProjectAId,
                taskId = fixture.TaskAId,
                assigneeUserId = fixture.UserBId,
                projectName = "Project A",
                taskTitle = "Task A"
            })).StatusCode.Should().Be(HttpStatusCode.OK);
        (await userA.PostAsJsonAsync(
            "/api/notifications/events/task-status-changed",
            new
            {
                projectId = fixture.ProjectAId,
                taskId = fixture.TaskAId,
                projectName = "Project A",
                taskTitle = "Task A",
                statusName = "Done"
            })).StatusCode.Should().Be(HttpStatusCode.OK);
        (await userA.PostAsJsonAsync(
            "/api/notifications/events/comment-added",
            new
            {
                projectId = fixture.ProjectAId,
                taskId = fixture.TaskAId,
                projectName = "Project A",
                taskTitle = "Task A"
            })).StatusCode.Should().Be(HttpStatusCode.OK);
        (await userA.PostAsJsonAsync(
            "/api/notifications/events/task-reminded",
            new
            {
                projectId = fixture.ProjectAId,
                taskId = fixture.TaskAId,
                assigneeUserId = fixture.UserBId,
                projectName = "Project A",
                taskTitle = "Task A"
            })).StatusCode.Should().Be(HttpStatusCode.OK);

        await using var scope = factory.Services.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        (await context.Notifications.CountAsync()).Should().BeGreaterThan(0);
    }

    [Fact]
    public async Task NotificationReadsAreCurrentUserScopedAndMarkReadIsIdempotent()
    {
        await using var factory = new ChatApplicationFactory();
        var fixture = await SeedAsync(factory);
        using var userA = CreateClient(factory, fixture.UserAId);

        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            context.Notifications.AddRange(
                Notification(fixture.UserAId, "user-a notification", DateTime.UtcNow),
                Notification(fixture.UserBId, "user-b notification", DateTime.UtcNow.AddMinutes(1)));
            await context.SaveChangesAsync();
        }

        var list = await userA.GetFromJsonAsync<JsonElement>("/api/notifications");
        list.GetProperty("data").GetArrayLength().Should().Be(1);
        list.GetProperty("data")[0].GetProperty("content").GetString().Should().Be("user-a notification");

        var ownNotificationId = await GetNotificationIdAsync(factory, fixture.UserAId);
        var otherNotificationId = await GetNotificationIdAsync(factory, fixture.UserBId);

        (await userA.PutAsync($"/api/notifications/{otherNotificationId}/read", content: null))
            .StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await userA.PutAsync($"/api/notifications/{ownNotificationId}/read", content: null))
            .StatusCode.Should().Be(HttpStatusCode.OK);
        (await userA.PutAsync($"/api/notifications/{ownNotificationId}/read", content: null))
            .StatusCode.Should().Be(HttpStatusCode.OK);

        var unread = await userA.GetFromJsonAsync<JsonElement>("/api/notifications/unread-count");
        unread.GetProperty("data").GetInt32().Should().Be(0);
    }

    [Fact]
    public async Task NotificationListUnreadCountMatchesFullUnreadCount()
    {
        await using var factory = new ChatApplicationFactory();
        var fixture = await SeedAsync(factory);
        using var userA = CreateClient(factory, fixture.UserAId);

        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var now = DateTime.UtcNow;
            context.Notifications.AddRange(Enumerable.Range(0, 55).Select(index =>
                Notification(fixture.UserAId, $"notification-{index}", now.AddMinutes(-index))));
            await context.SaveChangesAsync();
        }

        var list = await userA.GetFromJsonAsync<JsonElement>("/api/notifications");
        var fullCount = await userA.GetFromJsonAsync<JsonElement>("/api/notifications/unread-count");

        list.GetProperty("data").GetArrayLength().Should().Be(50);
        list.GetProperty("unreadCount").GetInt32()
            .Should().Be(fullCount.GetProperty("data").GetInt32());
        fullCount.GetProperty("data").GetInt32().Should().Be(55);
    }

    private static void AssertBlocked(HttpResponseMessage response) =>
        response.StatusCode.Should().BeOneOf(HttpStatusCode.Forbidden, HttpStatusCode.NotFound);

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

    private static async Task<Guid> GetNotificationIdAsync(ChatApplicationFactory factory, Guid userId)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        return await context.Notifications
            .Where(notification => notification.UserId == userId)
            .Select(notification => notification.Id)
            .SingleAsync();
    }

    private static Notification Notification(Guid userId, string content, DateTime createdAt) => new()
    {
        Id = Guid.NewGuid(),
        UserId = userId,
        Title = "Test notification",
        Content = content,
        NotificationType = "TEST",
        CreatedAt = createdAt,
        IsRead = false
    };

    private static async Task<Fixture> SeedAsync(ChatApplicationFactory factory)
    {
        var fixture = new Fixture(
            Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(),
            Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(),
            Guid.NewGuid(), Guid.NewGuid());
        await using var scope = factory.Services.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var now = DateTime.UtcNow;

        context.Users.AddRange(
            User(fixture.UserAId, "user-a"),
            User(fixture.UserBId, "user-b"));
        context.Workspaces.AddRange(
            Workspace(fixture.WorkspaceAId, fixture.UserAId, "Workspace A", now),
            Workspace(fixture.WorkspaceBId, fixture.UserBId, "Workspace B", now));
        context.WorkspaceMembers.AddRange(
            new WorkspaceMember { WorkspaceId = fixture.WorkspaceAId, UserId = fixture.UserAId, WorkspaceRole = "MEMBER", IsActive = true, JoinedAt = now },
            new WorkspaceMember { WorkspaceId = fixture.WorkspaceBId, UserId = fixture.UserBId, WorkspaceRole = "MEMBER", IsActive = true, JoinedAt = now });
        context.Projects.AddRange(
            Project(fixture.ProjectAId, fixture.WorkspaceAId, fixture.UserAId, "Project A", now),
            Project(fixture.ProjectBId, fixture.WorkspaceBId, fixture.UserBId, "Project B", now));
        context.ProjectMembers.AddRange(
            new ProjectMember { ProjectId = fixture.ProjectAId, UserId = fixture.UserAId, ProjectRole = "Developer", Status = true, JoinedAt = now },
            new ProjectMember { ProjectId = fixture.ProjectBId, UserId = fixture.UserBId, ProjectRole = "Developer", Status = true, JoinedAt = now });
        context.TaskStatuses.AddRange(
            new TaskStatusEntity { Id = fixture.TaskStatusAId, ProjectId = fixture.ProjectAId, Name = "To Do", Position = 1 },
            new TaskStatusEntity { Id = fixture.TaskStatusBId, ProjectId = fixture.ProjectBId, Name = "To Do", Position = 1 });
        context.TaskTypes.AddRange(
            new TaskType { Id = fixture.TaskTypeAId, ProjectId = fixture.ProjectAId, Name = "Task" },
            new TaskType { Id = fixture.TaskTypeBId, ProjectId = fixture.ProjectBId, Name = "Task" });
        context.WorkTasks.AddRange(
            WorkTask(fixture.TaskAId, fixture.ProjectAId, fixture.WorkspaceAId, fixture.UserAId, fixture.TaskStatusAId, fixture.TaskTypeAId, "Task A", now),
            WorkTask(fixture.TaskBId, fixture.ProjectBId, fixture.WorkspaceBId, fixture.UserBId, fixture.TaskStatusBId, fixture.TaskTypeBId, "Task B", now));
        context.TaskAssignments.AddRange(
            new TaskAssignment { WorkTaskId = fixture.TaskAId, UserId = fixture.UserBId, Status = true },
            new TaskAssignment { WorkTaskId = fixture.TaskBId, UserId = fixture.UserBId, Status = true });
        context.Comments.Add(new Comment
        {
            Id = fixture.CommentBId,
            EntityType = "WorkTask",
            EntityId = fixture.TaskBId,
            UserId = fixture.UserBId,
            Content = "Comment B",
            CreatedAt = now,
            UpdatedAt = now
        });
        await context.SaveChangesAsync();
        return fixture;
    }

    private static User User(Guid id, string name) => new()
    {
        Id = id,
        Email = $"{name}-{id:N}@test.local",
        FullName = name,
        PasswordHash = "test-only",
        IsActive = true,
        CreatedAt = DateTime.UtcNow
    };

    private static Workspace Workspace(Guid id, Guid ownerId, string name, DateTime now) => new()
    {
        Id = id,
        Name = name,
        Slug = $"{name.Replace(" ", "-")}-{id:N}",
        OwnerId = ownerId,
        CreatedAt = now,
        UpdatedAt = now
    };

    private static Project Project(Guid id, Guid workspaceId, Guid creatorId, string name, DateTime now) => new()
    {
        Id = id,
        WorkspaceId = workspaceId,
        CreatorId = creatorId,
        Name = name,
        Identifier = name.Replace(" ", string.Empty, StringComparison.Ordinal).ToUpperInvariant(),
        CreatedAt = now,
        UpdatedAt = now,
        Status = true
    };

    private static WorkTask WorkTask(
        Guid id,
        Guid projectId,
        Guid workspaceId,
        Guid reporterId,
        Guid taskStatusId,
        Guid taskTypeId,
        string title,
        DateTime now) => new()
    {
        Id = id,
        ProjectId = projectId,
        WorkspaceId = workspaceId,
        ReporterId = reporterId,
        TaskStatusId = taskStatusId,
        TaskTypeId = taskTypeId,
        Title = title,
        CreatedAt = now,
        UpdatedAt = now
    };

    private sealed record Fixture(
        Guid UserAId,
        Guid UserBId,
        Guid WorkspaceAId,
        Guid WorkspaceBId,
        Guid ProjectAId,
        Guid ProjectBId,
        Guid TaskAId,
        Guid TaskBId,
        Guid TaskStatusAId,
        Guid TaskStatusBId,
        Guid TaskTypeAId,
        Guid TaskTypeBId,
        Guid CommentBId);
}
