using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Net.Http.Headers;
using System.Security.Claims;
using System.Text.Json;
using FluentAssertions;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using TaskManagement.Application.Common;
using TaskManagement.Domain.Entities;
using TaskManagement.Infrastructure.Data;

namespace TaskManagement.Tests.Logic;

[Collection("ProjectAccessPolicy")]
public sealed class ProjectAuthorizationEndpointTests
{
    [Fact]
    public async Task ActiveMemberIsListedAndCanReadWhileUnrelatedUserIsHiddenAndDenied()
    {
        await using var factory = new ChatApplicationFactory();
        var memberId = Guid.NewGuid();
        var outsiderId = Guid.NewGuid();
        var projectId = Guid.NewGuid();
        await SeedProjectAsync(factory, memberId, outsiderId, projectId, memberActive: true);

        using var memberClient = CreateClient(factory, memberId);
        using var outsiderClient = CreateClient(factory, outsiderId);

        (await ContainsProjectAsync(memberClient, "/api/projects", projectId)).Should().BeTrue();
        (await ContainsProjectAsync(memberClient, "/api/projects/discovery", projectId)).Should().BeTrue();
        (await ContainsProjectAsync(outsiderClient, "/api/projects", projectId)).Should().BeFalse();
        (await ContainsProjectAsync(outsiderClient, "/api/projects/discovery", projectId)).Should().BeFalse();
        (await memberClient.GetAsync($"/api/projects/{projectId}")).StatusCode.Should().Be(HttpStatusCode.OK);
        (await outsiderClient.GetAsync($"/api/projects/{projectId}")).StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task InactiveProjectMemberIsHiddenAndDenied()
    {
        await using var factory = new ChatApplicationFactory();
        var memberId = Guid.NewGuid();
        var outsiderId = Guid.NewGuid();
        var projectId = Guid.NewGuid();
        await SeedProjectAsync(factory, memberId, outsiderId, projectId, memberActive: false);

        using var client = CreateClient(factory, memberId);
        (await ContainsProjectAsync(client, "/api/projects", projectId)).Should().BeFalse();
        (await ContainsProjectAsync(client, "/api/projects/discovery", projectId)).Should().BeFalse();
        (await client.GetAsync($"/api/projects/{projectId}")).StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task WorkspaceAdminAndSystemRoleWithoutProjectMembershipRemainDenied()
    {
        await using var factory = new ChatApplicationFactory();
        var adminId = Guid.NewGuid();
        var outsiderId = Guid.NewGuid();
        var projectId = Guid.NewGuid();
        await SeedProjectAsync(
            factory,
            adminId,
            outsiderId,
            projectId,
            memberActive: false,
            workspaceRole: "ADMIN",
            systemRole: "Admin");

        using var client = CreateClient(factory, adminId);
        (await ContainsProjectAsync(client, "/api/projects", projectId)).Should().BeFalse();
        (await ContainsProjectAsync(client, "/api/projects/discovery", projectId)).Should().BeFalse();
        (await client.GetAsync($"/api/projects/{projectId}")).StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task RestrictionsOffAllowsWorkspaceScopedAccessButNotGlobalAccess()
    {
        var previous = ProjectAccessPolicy.RestrictionsEnabled;
        try
        {
            await using var factory = new ChatApplicationFactory();
            var workspaceMemberId = Guid.NewGuid();
            var outsiderId = Guid.NewGuid();
            var projectId = Guid.NewGuid();
            await SeedProjectAsync(factory, workspaceMemberId, outsiderId, projectId, memberActive: false);
            ProjectAccessPolicy.Configure(false);

            using var workspaceClient = CreateClient(factory, workspaceMemberId);
            using var outsiderClient = CreateClient(factory, outsiderId);
            (await workspaceClient.GetAsync($"/api/projects/{projectId}")).StatusCode.Should().Be(HttpStatusCode.OK);
            (await outsiderClient.GetAsync($"/api/projects/{projectId}")).StatusCode.Should().Be(HttpStatusCode.Forbidden);
        }
        finally
        {
            ProjectAccessPolicy.Configure(previous);
        }
    }

    private static HttpClient CreateClient(ChatApplicationFactory factory, Guid userId)
    {
        var client = factory.CreateClient();
        var options = factory.Services
            .GetRequiredService<IOptionsMonitor<JwtBearerOptions>>()
            .Get(JwtBearerDefaults.AuthenticationScheme);
        var credentials = new SigningCredentials(options.TokenValidationParameters.IssuerSigningKey, SecurityAlgorithms.HmacSha256);
        var token = new JwtSecurityTokenHandler().WriteToken(new JwtSecurityToken(
            issuer: options.TokenValidationParameters.ValidIssuer,
            audience: options.TokenValidationParameters.ValidAudience,
            claims: [new Claim(ClaimTypes.NameIdentifier, userId.ToString())],
            expires: DateTime.UtcNow.AddMinutes(10),
            signingCredentials: credentials));
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return client;
    }

    private static async Task<bool> ContainsProjectAsync(HttpClient client, string path, Guid projectId)
    {
        using var response = await client.GetAsync(path);
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var data = document.RootElement.GetProperty("data");
        return data.EnumerateArray().Any(item => item.GetProperty("id").GetGuid() == projectId);
    }

    private static async Task SeedProjectAsync(
        ChatApplicationFactory factory,
        Guid memberId,
        Guid outsiderId,
        Guid projectId,
        bool memberActive,
        string workspaceRole = "MEMBER",
        string? systemRole = null)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var workspaceId = Guid.NewGuid();
        context.Users.AddRange(
            NewUser(memberId, "member"),
            NewUser(outsiderId, "outsider"));
        var role = systemRole == null ? null : new Role { Id = Guid.NewGuid(), Name = systemRole };
        if (role != null)
        {
            context.Roles.Add(role);
            context.UserRoles.Add(new UserRole { UserId = memberId, RoleId = role.Id });
        }
        context.Workspaces.Add(new Workspace
        {
            Id = workspaceId,
            Name = "Test workspace",
            Slug = $"workspace-{workspaceId:N}",
            OwnerId = memberId,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        });
        context.WorkspaceMembers.Add(new WorkspaceMember
        {
            WorkspaceId = workspaceId,
            UserId = memberId,
            WorkspaceRole = workspaceRole,
            IsActive = true,
            JoinedAt = DateTime.UtcNow
        });
        context.Projects.Add(new Project
        {
            Id = projectId,
            WorkspaceId = workspaceId,
            CreatorId = memberId,
            Name = "Authorized project",
            Identifier = "AUTH",
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        });
        context.ProjectMembers.Add(new ProjectMember
        {
            ProjectId = projectId,
            UserId = memberId,
            ProjectRole = "Developer",
            Status = memberActive,
            JoinedAt = DateTime.UtcNow
        });
        await context.SaveChangesAsync();
    }

    private static User NewUser(Guid id, string name) => new()
    {
        Id = id,
        Email = $"{name}-{id:N}@test.local",
        FullName = name,
        PasswordHash = "test-only",
        IsActive = true,
        CreatedAt = DateTime.UtcNow
    };
}
