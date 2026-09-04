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

namespace TaskManagement.Tests.Logic;

public sealed class P0ProjectAuthorizationBatchCTests
{
    [Fact]
    public async Task UnrelatedUserCannotReadOrMutateProjectFollowers()
    {
        await using var factory = new ChatApplicationFactory();
        var fixture = await SeedAsync(factory);
        using var userA = CreateClient(factory, fixture.UserAId);
        using var userB = CreateClient(factory, fixture.UserBId);

        AssertBlocked(await userA.GetAsync($"/api/workspaces/{fixture.WorkspaceBId}/followers"));
        AssertBlocked(await userA.GetAsync(
            $"/api/workspaces/{fixture.WorkspaceBId}/followers/entity?entityType=Project&entityId={fixture.ProjectBId}"));
        AssertBlocked(await userA.PostAsJsonAsync(
            $"/api/workspaces/{fixture.WorkspaceBId}/followers/entity?entityType=Project&entityId={fixture.ProjectBId}",
            new { userIds = new[] { fixture.UserBId } }));
        AssertBlocked(await userA.PostAsync(
            $"/api/workspaces/{fixture.WorkspaceBId}/followers/toggle?entityType=Project&entityId={fixture.ProjectBId}",
            content: null));
        AssertBlocked(await userA.GetAsync(
            $"/api/workspaces/{fixture.WorkspaceBId}/followers/entity?entityType=Project&entityId={fixture.ProjectBId}"));

        (await userB.GetAsync(
            $"/api/workspaces/{fixture.WorkspaceBId}/followers/entity?entityType=Project&entityId={fixture.ProjectBId}"))
            .StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task FollowerMixedWorkspaceAndEntityIdsCannotBypassAuthorization()
    {
        await using var factory = new ChatApplicationFactory();
        var fixture = await SeedAsync(factory);
        using var userA = CreateClient(factory, fixture.UserAId);

        AssertBlocked(await userA.GetAsync(
            $"/api/workspaces/{fixture.WorkspaceAId}/followers/entity?entityType=Project&entityId={fixture.ProjectBId}"));
        AssertBlocked(await userA.PostAsync(
            $"/api/workspaces/{fixture.WorkspaceAId}/followers/toggle?entityType=Project&entityId={fixture.ProjectBId}",
            content: null));
    }

    [Fact]
    public async Task AuthorizedMemberCanUseFollowerOperations()
    {
        await using var factory = new ChatApplicationFactory();
        var fixture = await SeedAsync(factory);
        using var userA = CreateClient(factory, fixture.UserAId);

        (await userA.GetAsync(
            $"/api/workspaces/{fixture.WorkspaceAId}/followers/entity?entityType=Project&entityId={fixture.ProjectAId}"))
            .StatusCode.Should().Be(HttpStatusCode.OK);
        (await userA.PostAsJsonAsync(
            $"/api/workspaces/{fixture.WorkspaceAId}/followers/entity?entityType=Project&entityId={fixture.ProjectAId}",
            new { userIds = new[] { fixture.UserBId } })).StatusCode.Should().Be(HttpStatusCode.OK);
        (await userA.PostAsync(
            $"/api/workspaces/{fixture.WorkspaceAId}/followers/toggle?entityType=Project&entityId={fixture.ProjectAId}",
            content: null)).StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task UnrelatedUserCannotReadTeamOrUserKudos()
    {
        await using var factory = new ChatApplicationFactory();
        var fixture = await SeedAsync(factory);
        using var userA = CreateClient(factory, fixture.UserAId);
        using var userB = CreateClient(factory, fixture.UserBId);

        AssertBlocked(await userA.GetAsync($"/api/kudos/team/{fixture.TeamBId}"));
        AssertBlocked(await userA.GetAsync($"/api/kudos/user/{fixture.UserBId}"));
        (await userA.GetStringAsync("/api/kudos")).Should().NotContain("seeded team praise");
        var profile = await userA.GetFromJsonAsync<JsonElement>($"/api/users/{fixture.UserBId}");
        profile.GetProperty("data").GetProperty("kudos").GetArrayLength().Should().Be(0);
        var team = await userA.GetFromJsonAsync<JsonElement>($"/api/departments/{fixture.TeamBId}/full");
        team.GetProperty("data").GetProperty("kudos").GetArrayLength().Should().Be(0);

        (await userB.GetAsync($"/api/kudos/team/{fixture.TeamBId}")).StatusCode.Should().Be(HttpStatusCode.OK);
        (await userB.GetAsync($"/api/kudos/user/{fixture.UserBId}")).StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task UnrelatedUserCannotSendIndividualOrTeamKudos()
    {
        await using var factory = new ChatApplicationFactory();
        var fixture = await SeedAsync(factory);
        using var userA = CreateClient(factory, fixture.UserAId);

        AssertBlocked(await userA.PostAsJsonAsync("/api/kudos", new
        {
            ReceiverId = fixture.UserBId,
            Message = "cross-team individual praise"
        }));
        AssertBlocked(await userA.PostAsJsonAsync("/api/kudos", new
        {
            DepartmentId = fixture.TeamBId,
            Message = "cross-team team praise"
        }));
    }

    [Fact]
    public async Task AuthorizedMemberAndAdminCanReadAndSendPermittedKudos()
    {
        await using var factory = new ChatApplicationFactory();
        var fixture = await SeedAsync(factory);
        using var member = CreateClient(factory, fixture.UserBId);
        using var admin = CreateClient(factory, fixture.AdminId);

        (await member.GetAsync($"/api/kudos/team/{fixture.TeamBId}")).StatusCode.Should().Be(HttpStatusCode.OK);
        (await member.PostAsJsonAsync("/api/kudos", new
        {
            DepartmentId = fixture.TeamBId,
            Message = "permitted team praise"
        })).StatusCode.Should().Be(HttpStatusCode.OK);

        (await admin.GetAsync($"/api/kudos/team/{fixture.TeamBId}")).StatusCode.Should().Be(HttpStatusCode.OK);
        (await admin.PostAsJsonAsync("/api/kudos", new
        {
            ReceiverId = fixture.UserBId,
            Message = "admin permitted praise"
        })).StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task KudosRejectInconsistentReceiverAndTeamIds()
    {
        await using var factory = new ChatApplicationFactory();
        var fixture = await SeedAsync(factory);
        using var member = CreateClient(factory, fixture.UserBId);

        (await member.PostAsJsonAsync("/api/kudos", new
        {
            DepartmentId = fixture.TeamBId,
            ReceiverId = fixture.UserAId,
            Message = "inconsistent target"
        })).StatusCode.Should().Be(HttpStatusCode.BadRequest);
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

    private static async Task<Fixture> SeedAsync(ChatApplicationFactory factory)
    {
        var fixture = new Fixture(
            Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(),
            Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(),
            Guid.NewGuid(), Guid.NewGuid());
        await using var scope = factory.Services.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var now = DateTime.UtcNow;

        context.Users.AddRange(
            User(fixture.UserAId, "user-a"),
            User(fixture.UserBId, "user-b"),
            User(fixture.AdminId, "admin"));
        context.Workspaces.AddRange(
            Workspace(fixture.WorkspaceAId, fixture.UserAId, "Workspace A", now),
            Workspace(fixture.WorkspaceBId, fixture.UserBId, "Workspace B", now));
        context.WorkspaceMembers.AddRange(
            WorkspaceMember(fixture.WorkspaceAId, fixture.UserAId, "MEMBER", now),
            WorkspaceMember(fixture.WorkspaceBId, fixture.UserBId, "MEMBER", now),
            WorkspaceMember(fixture.WorkspaceAId, fixture.AdminId, "ADMIN", now),
            WorkspaceMember(fixture.WorkspaceBId, fixture.AdminId, "ADMIN", now));
        context.Projects.AddRange(
            Project(fixture.ProjectAId, fixture.WorkspaceAId, fixture.UserAId, "Project A", now),
            Project(fixture.ProjectBId, fixture.WorkspaceBId, fixture.UserBId, "Project B", now));
        context.ProjectMembers.AddRange(
            new ProjectMember { ProjectId = fixture.ProjectAId, UserId = fixture.UserAId, ProjectRole = "Developer", Status = true, JoinedAt = now },
            new ProjectMember { ProjectId = fixture.ProjectBId, UserId = fixture.UserBId, ProjectRole = "PM", Status = true, JoinedAt = now },
            new ProjectMember { ProjectId = fixture.ProjectBId, UserId = fixture.AdminId, ProjectRole = "Admin", Status = true, JoinedAt = now });
        context.Departments.AddRange(
            Department(fixture.TeamAId, "Team A", now),
            Department(fixture.TeamBId, "Team B", now));
        context.DepartmentMembers.AddRange(
            DepartmentMember(fixture.TeamAId, fixture.UserAId, now),
            DepartmentMember(fixture.TeamBId, fixture.UserBId, now),
            DepartmentMember(fixture.TeamAId, fixture.AdminId, now),
            DepartmentMember(fixture.TeamBId, fixture.AdminId, now));
        context.Kudos.Add(new Kudo
        {
            SenderId = fixture.UserBId,
            ReceiverId = fixture.UserBId,
            DepartmentId = fixture.TeamBId,
            Message = "seeded team praise",
            CreatedAt = now
        });
        context.EntityFollowers.Add(new EntityFollower
        {
            UserId = fixture.UserBId,
            EntityType = "Project",
            EntityId = fixture.ProjectBId,
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

    private static Workspace Workspace(Guid id, Guid ownerId, string name, DateTime now) => new()
    {
        Id = id,
        Name = name,
        Slug = $"{name.Replace(" ", "-")}-{id:N}",
        OwnerId = ownerId,
        CreatedAt = now,
        UpdatedAt = now
    };

    private static WorkspaceMember WorkspaceMember(Guid workspaceId, Guid userId, string role, DateTime now) => new()
    {
        WorkspaceId = workspaceId,
        UserId = userId,
        WorkspaceRole = role,
        IsActive = true,
        JoinedAt = now
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

    private static Department Department(Guid id, string name, DateTime now) => new()
    {
        Id = id,
        Name = name,
        IsActive = true,
        IsDeleted = false,
        CreatedAt = now
    };

    private static DepartmentMember DepartmentMember(Guid departmentId, Guid userId, DateTime now) => new()
    {
        DepartmentId = departmentId,
        UserId = userId,
        JoinedAt = now
    };

    private sealed record Fixture(
        Guid UserAId,
        Guid UserBId,
        Guid AdminId,
        Guid WorkspaceAId,
        Guid WorkspaceBId,
        Guid ProjectAId,
        Guid ProjectBId,
        Guid TeamAId,
        Guid TeamBId);
}
