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
public sealed class P0ProjectAuthorizationBatchBTests
{
    [Fact]
    public async Task UnrelatedUserCannotImportOrExportProjectTasks()
    {
        await using var factory = new ChatApplicationFactory();
        var fixture = await SeedAsync(factory);
        using var userA = CreateClient(factory, fixture.UserAId);
        using var userB = CreateClient(factory, fixture.UserBId);

        (await userA.GetAsync($"/api/projects/{fixture.ProjectBId}/WorkTasks/export"))
            .StatusCode.Should().Be(HttpStatusCode.Forbidden);
        (await userA.PostAsJsonAsync(
            $"/api/projects/{fixture.ProjectBId}/WorkTasks/import",
            new[] { new { title = "cross-project import" } }))
            .StatusCode.Should().Be(HttpStatusCode.Forbidden);

        (await userB.GetAsync($"/api/projects/{fixture.ProjectBId}/WorkTasks/export"))
            .StatusCode.Should().Be(HttpStatusCode.OK);
        (await userB.PostAsJsonAsync(
            $"/api/projects/{fixture.ProjectBId}/WorkTasks/import",
            new[] { new { title = "valid project import" } }))
            .StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task UnrelatedUserCannotReadOrMutateLegacyContingencyRoutes()
    {
        await using var factory = new ChatApplicationFactory();
        var fixture = await SeedAsync(factory);
        using var userA = CreateClient(factory, fixture.UserAId);
        var createPlan = new { name = "blocked plan", riskLevel = "Low" };
        var createTask = new { title = "blocked contingency task" };

        (await userA.GetAsync($"/api/worktasks/{fixture.TaskBId}/contingency-plans"))
            .StatusCode.Should().Be(HttpStatusCode.Forbidden);
        (await userA.GetAsync($"/api/worktasks/{fixture.TaskBId}/contingency-plans/{fixture.PlanBId}"))
            .StatusCode.Should().Be(HttpStatusCode.Forbidden);
        (await userA.PostAsJsonAsync($"/api/worktasks/{fixture.TaskBId}/contingency-plans", createPlan))
            .StatusCode.Should().Be(HttpStatusCode.Forbidden);
        (await userA.PutAsJsonAsync($"/api/worktasks/{fixture.TaskBId}/contingency-plans/{fixture.PlanBId}", createPlan))
            .StatusCode.Should().Be(HttpStatusCode.Forbidden);
        (await userA.DeleteAsync($"/api/worktasks/{fixture.TaskBId}/contingency-plans/{fixture.PlanBId}"))
            .StatusCode.Should().Be(HttpStatusCode.Forbidden);
        (await userA.PostAsJsonAsync($"/api/worktasks/{fixture.TaskBId}/contingency-plans/{fixture.PlanBId}/tasks/create", createTask))
            .StatusCode.Should().Be(HttpStatusCode.Forbidden);
        (await userA.PostAsJsonAsync($"/api/worktasks/{fixture.TaskBId}/contingency-plans/{fixture.PlanBId}/tasks", fixture.TaskBId))
            .StatusCode.Should().Be(HttpStatusCode.Forbidden);
        (await userA.DeleteAsync($"/api/worktasks/{fixture.TaskBId}/contingency-plans/{fixture.PlanBId}/tasks/{fixture.LinkBId}"))
            .StatusCode.Should().Be(HttpStatusCode.Forbidden);
        (await userA.PostAsync($"/api/worktasks/{fixture.TaskBId}/contingency-plans/{fixture.PlanBId}/tasks/{fixture.LinkBId}/activate", content: null))
            .StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task MixedProjectAndTaskIdsCannotUseContingencyRoutes()
    {
        await using var factory = new ChatApplicationFactory();
        var fixture = await SeedAsync(factory);
        using var userA = CreateClient(factory, fixture.UserAId);

        (await userA.GetAsync($"/api/projects/{fixture.ProjectAId}/worktasks/{fixture.TaskBId}/contingency-plans"))
            .StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task AuthorizedMemberAndAdminCanUseTheirProjectContingencyData()
    {
        await using var factory = new ChatApplicationFactory();
        var fixture = await SeedAsync(factory);
        using var member = CreateClient(factory, fixture.UserBId);
        using var admin = CreateClient(factory, fixture.AdminId);

        (await member.GetAsync($"/api/worktasks/{fixture.TaskBId}/contingency-plans"))
            .StatusCode.Should().Be(HttpStatusCode.OK);
        (await admin.GetAsync($"/api/projects/{fixture.ProjectBId}/worktasks/{fixture.TaskBId}/contingency-plans"))
            .StatusCode.Should().Be(HttpStatusCode.OK);
        (await admin.GetAsync($"/api/projects/{fixture.ProjectBId}/WorkTasks/export"))
            .StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task AuthorizedMemberCannotLinkAFallbackTaskFromAnotherProject()
    {
        await using var factory = new ChatApplicationFactory();
        var fixture = await SeedAsync(factory);
        using var member = CreateClient(factory, fixture.UserBId);

        (await member.PostAsJsonAsync(
            $"/api/worktasks/{fixture.TaskBId}/contingency-plans/{fixture.PlanBId}/tasks",
            fixture.TaskAId)).StatusCode.Should().Be(HttpStatusCode.BadRequest);
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
        var fixture = new Fixture(
            Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(),
            Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid());
        await using var scope = factory.Services.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var now = DateTime.UtcNow;
        var workspaceId = Guid.NewGuid();
        var statusAId = Guid.NewGuid();
        var statusBId = Guid.NewGuid();
        var typeAId = Guid.NewGuid();
        var typeBId = Guid.NewGuid();
        var adminRole = new Role { Id = Guid.NewGuid(), Name = "Admin" };

        context.Users.AddRange(
            User(fixture.UserAId, "user-a"),
            User(fixture.UserBId, "user-b"),
            User(fixture.AdminId, "admin"));
        context.Roles.Add(adminRole);
        context.UserRoles.Add(new UserRole { UserId = fixture.AdminId, RoleId = adminRole.Id });
        context.Workspaces.Add(new Workspace
        {
            Id = workspaceId,
            Name = "Batch B workspace",
            Slug = $"batch-b-{workspaceId:N}",
            OwnerId = fixture.UserAId,
            CreatedAt = now,
            UpdatedAt = now
        });
        context.WorkspaceMembers.AddRange(
            WorkspaceMember(workspaceId, fixture.UserAId, "MEMBER", now),
            WorkspaceMember(workspaceId, fixture.UserBId, "MEMBER", now),
            WorkspaceMember(workspaceId, fixture.AdminId, "ADMIN", now));
        context.Projects.AddRange(
            Project(fixture.ProjectAId, workspaceId, fixture.UserAId, "Project A"),
            Project(fixture.ProjectBId, workspaceId, fixture.UserBId, "Project B"));
        context.ProjectMembers.AddRange(
            ProjectMember(fixture.ProjectAId, fixture.UserAId, "Developer", now),
            ProjectMember(fixture.ProjectBId, fixture.UserBId, "PM", now),
            ProjectMember(fixture.ProjectBId, fixture.AdminId, "Admin", now));
        context.TaskStatuses.AddRange(
            new TaskStatusEntity { Id = statusAId, ProjectId = fixture.ProjectAId, Name = "To Do" },
            new TaskStatusEntity { Id = statusBId, ProjectId = fixture.ProjectBId, Name = "To Do" });
        context.TaskTypes.AddRange(
            new TaskType { Id = typeAId, ProjectId = fixture.ProjectAId, Name = "Task" },
            new TaskType { Id = typeBId, ProjectId = fixture.ProjectBId, Name = "Task" });
        context.WorkTasks.AddRange(
            WorkTask(fixture.TaskAId, fixture.ProjectAId, workspaceId, statusAId, typeAId, fixture.UserAId, "Project A task"),
            WorkTask(fixture.TaskBId, fixture.ProjectBId, workspaceId, statusBId, typeBId, fixture.UserBId, "Project B task"));
        context.ContingencyPlans.Add(new ContingencyPlan
        {
            Id = fixture.PlanBId,
            WorkTaskId = fixture.TaskBId,
            Name = "Project B plan",
            RiskLevel = "Low",
            CreatedAt = now,
            UpdatedAt = now
        });
        context.ContingencyPlanTasks.Add(new ContingencyPlanTask
        {
            Id = fixture.LinkBId,
            ContingencyPlanId = fixture.PlanBId,
            Title = "Project B fallback",
            CreatedAt = now
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

    private static WorkspaceMember WorkspaceMember(Guid workspaceId, Guid userId, string role, DateTime now) => new()
    {
        WorkspaceId = workspaceId,
        UserId = userId,
        WorkspaceRole = role,
        IsActive = true,
        JoinedAt = now
    };

    private static ProjectMember ProjectMember(Guid projectId, Guid userId, string role, DateTime now) => new()
    {
        ProjectId = projectId,
        UserId = userId,
        ProjectRole = role,
        Status = true,
        JoinedAt = now
    };

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

    private static WorkTask WorkTask(
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
        CreatedAt = DateTime.UtcNow,
        UpdatedAt = DateTime.UtcNow
    };

    private sealed record Fixture(
        Guid UserAId,
        Guid UserBId,
        Guid AdminId,
        Guid ProjectAId,
        Guid ProjectBId,
        Guid TaskAId,
        Guid TaskBId,
        Guid PlanBId,
        Guid LinkBId);
}
