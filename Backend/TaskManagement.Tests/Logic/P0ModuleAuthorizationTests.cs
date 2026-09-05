using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text.Json;
using FluentAssertions;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using Moq;
using TaskManagement.API.Controllers;
using TaskManagement.Application.Interfaces;
using TaskManagement.Domain.Entities;
using TaskManagement.Infrastructure.Data;
using TaskStatusEntity = TaskManagement.Domain.Entities.TaskStatus;

namespace TaskManagement.Tests.Logic;

public sealed class P0ModuleAuthorizationTests
{
    [Fact]
    public async Task Intakes_RuntimeManagerMemberOutsider_EnforcesPermissionMatrix()
    {
        await using var factory = new ChatApplicationFactory();
        var fixture = await SeedFixtureAsync(factory);
        using var manager = CreateAuthenticatedClient(factory, fixture.ManagerId);
        using var member = CreateAuthenticatedClient(factory, fixture.MemberId);
        using var outsider = CreateAuthenticatedClient(factory, fixture.OutsiderId);

        var createResponse = await manager.PostAsJsonAsync(
            "/api/projects/" + fixture.ProjectId + "/intakes",
            new
            {
                title = "Runtime intake",
                description = "A/B/C authorization evidence",
                source = "FORM",
                priority = 2,
                desiredDueDate = DateTime.UtcNow.AddDays(2)
            });
        createResponse.StatusCode.Should().Be(HttpStatusCode.Created);
        var createdJson = JsonDocument.Parse(await createResponse.Content.ReadAsStringAsync());
        var intakeId = createdJson.RootElement.GetProperty("data").GetProperty("id").GetGuid();

        var memberList = await member.GetAsync("/api/projects/" + fixture.ProjectId + "/intakes");
        memberList.StatusCode.Should().Be(HttpStatusCode.OK);
        var memberJson = JsonDocument.Parse(await memberList.Content.ReadAsStringAsync());
        memberJson.RootElement.GetProperty("data").GetArrayLength().Should().Be(1);
        memberJson.RootElement.GetProperty("permissions").GetProperty("canCreate").GetBoolean().Should().BeTrue();
        memberJson.RootElement.GetProperty("permissions").GetProperty("canReview").GetBoolean().Should().BeFalse();

        (await member.PutAsJsonAsync(
            "/api/projects/" + fixture.ProjectId + "/intakes/" + intakeId + "/review",
            new { status = "Accepted" })).StatusCode.Should().Be(HttpStatusCode.Forbidden);

        (await outsider.GetAsync(
            "/api/projects/" + fixture.ProjectId + "/intakes")).StatusCode.Should().Be(HttpStatusCode.Forbidden);
        (await outsider.PostAsJsonAsync(
            "/api/projects/" + fixture.ProjectId + "/intakes",
            new { title = "Blocked outsider", source = "FORM", priority = 3 })).StatusCode
            .Should().Be(HttpStatusCode.Forbidden);

        (await manager.PostAsJsonAsync(
            "/api/projects/" + fixture.ProjectId + "/intakes",
            new { title = "Invalid priority", source = "FORM", priority = 9 })).StatusCode
            .Should().Be(HttpStatusCode.BadRequest);
        (await manager.PutAsJsonAsync(
            "/api/projects/" + fixture.ProjectId + "/intakes/" + intakeId + "/review",
            new { status = "Escalated" })).StatusCode.Should().Be(HttpStatusCode.BadRequest);

        var accepted = await manager.PutAsJsonAsync(
            "/api/projects/" + fixture.ProjectId + "/intakes/" + intakeId + "/review",
            new { status = "Accepted" });
        accepted.StatusCode.Should().Be(HttpStatusCode.OK);

        var memberCreate = await member.PostAsJsonAsync(
            "/api/projects/" + fixture.ProjectId + "/intakes",
            new { title = "Member intake", source = "MANUAL", priority = 3 });
        memberCreate.StatusCode.Should().Be(HttpStatusCode.Created);

        await using var scope = factory.Services.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var storedIntake = await context.Intakes.AsNoTracking().SingleAsync(item => item.Id == intakeId);
        storedIntake.Status.Should().Be("Accepted");
        storedIntake.ReviewedById.Should().Be(fixture.ManagerId);
        storedIntake.CreatedIssueId.Should().NotBeNull();
        (await context.WorkTasks.AsNoTracking().SingleAsync()).ReporterId.Should().Be(fixture.ManagerId);
        (await context.Projects.AsNoTracking().SingleAsync()).IssueSequence.Should().Be(1);
    }

    [Fact]
    public async Task IntegrationHubAndInbox_RuntimeUsers_AreIsolatedAndProjectScoped()
    {
        await using var factory = new ChatApplicationFactory();
        var fixture = await SeedFixtureAsync(factory);
        using var manager = CreateAuthenticatedClient(factory, fixture.ManagerId);
        using var member = CreateAuthenticatedClient(factory, fixture.MemberId);
        using var outsider = CreateAuthenticatedClient(factory, fixture.OutsiderId);

        var integrations = await manager.GetAsync("/api/integrations");
        integrations.StatusCode.Should().Be(HttpStatusCode.OK);
        var integrationsBody = await integrations.Content.ReadAsStringAsync();
        integrationsBody.Should().Contain("manager@sprinta.test");
        integrationsBody.Should().NotContain("member@sprinta.test");

        var managerInbox = await manager.GetAsync("/api/inbox");
        managerInbox.StatusCode.Should().Be(HttpStatusCode.OK);
        var managerInboxBody = await managerInbox.Content.ReadAsStringAsync();
        managerInboxBody.Should().Contain("Manager inbox item");
        managerInboxBody.Should().NotContain("Member inbox item");

        (await manager.PatchAsync(
            "/api/inbox/" + fixture.MemberInboxId + "/read",
            null)).StatusCode.Should().Be(HttpStatusCode.NotFound);

        (await manager.PostAsJsonAsync(
            "/api/inbox/" + fixture.ManagerInboxId + "/create-task",
            new { projectId = fixture.ProjectId })).StatusCode.Should().Be(HttpStatusCode.OK);
        (await member.PostAsJsonAsync(
            "/api/inbox/" + fixture.MemberInboxId + "/create-task",
            new { projectId = fixture.ProjectId })).StatusCode.Should().Be(HttpStatusCode.OK);
        (await outsider.PostAsJsonAsync(
            "/api/inbox/" + fixture.OutsiderInboxId + "/create-task",
            new { projectId = fixture.ProjectId })).StatusCode.Should().Be(HttpStatusCode.Forbidden);

        var outsiderOptions = await outsider.GetAsync("/api/inbox/create-task-options");
        outsiderOptions.StatusCode.Should().Be(HttpStatusCode.OK);
        var optionsJson = JsonDocument.Parse(await outsiderOptions.Content.ReadAsStringAsync());
        optionsJson.RootElement.GetProperty("data").GetProperty("projects").GetArrayLength().Should().Be(0);

        await using var scope = factory.Services.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var tasks = await context.WorkTasks.AsNoTracking().OrderBy(item => item.ReporterId).ToListAsync();
        tasks.Should().HaveCount(2);
        tasks.Select(item => item.ReporterId).Should().BeEquivalentTo(
            new[] { fixture.ManagerId, fixture.MemberId });
        (await context.InboxItems.AsNoTracking().SingleAsync(item => item.Id == fixture.OutsiderInboxId))
            .CreatedTaskId.Should().BeNull();
    }

    [Fact]
    public async Task IntegrationOAuthState_IsSignedAndRejectsTamperingBeforeTokenExchange()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString("N"))
            .Options;
        await using var context = new ApplicationDbContext(options);
        var userId = Guid.NewGuid();
        context.Users.Add(NewUser(userId, "oauth-state@sprinta.test"));
        await context.SaveChangesAsync();

        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["IntegrationOAuth:Gmail:ClientId"] = "test-client",
                ["IntegrationOAuth:Gmail:ClientSecret"] = "test-secret",
                ["IntegrationOAuth:Gmail:RedirectUri"] = "http://localhost:5136/api/integrations/gmail/callback",
                ["Frontend:BaseUrl"] = "http://localhost:5173"
            })
            .Build();
        var controller = new IntegrationsController(
            context,
            configuration,
            Mock.Of<IHttpClientFactory>(),
            new EphemeralDataProtectionProvider(),
            Mock.Of<IGoogleCalendarIntegrationService>(),
            Mock.Of<IOAuthStateStore>())
        {
            ControllerContext = ControllerContextFor(userId)
        };

        var connect = controller.ConnectGmail().Should().BeOfType<OkObjectResult>().Subject;
        var connectJson = JsonSerializer.SerializeToDocument(connect.Value);
        var authorizationUrl = connectJson.RootElement
            .GetProperty("data")
            .GetProperty("authorizationUrl")
            .GetString();
        authorizationUrl.Should().NotBeNullOrWhiteSpace();
        var state = QueryHelpers.ParseQuery(new Uri(authorizationUrl!).Query)["state"].ToString();
        state.Should().NotContain(userId.ToString());

        var index = state.Length / 2;
        var replacement = state[index] == 'A' ? 'B' : 'A';
        var tamperedState = state[..index] + replacement + state[(index + 1)..];
        var callback = await controller.GmailCallback(
            "test-code",
            tamperedState,
            null,
            null);

        var redirect = callback.Should().BeOfType<RedirectResult>().Subject;
        redirect.Url.Should().Contain("connected=error");
    }

    private static async Task<P0Fixture> SeedFixtureAsync(ChatApplicationFactory factory)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var now = DateTime.UtcNow;
        var managerId = Guid.NewGuid();
        var memberId = Guid.NewGuid();
        var outsiderId = Guid.NewGuid();
        var workspaceId = Guid.NewGuid();
        var projectId = Guid.NewGuid();
        var managerInboxId = Guid.NewGuid();
        var memberInboxId = Guid.NewGuid();
        var outsiderInboxId = Guid.NewGuid();

        context.Users.AddRange(
            NewUser(managerId, "manager@sprinta.test"),
            NewUser(memberId, "member@sprinta.test"),
            NewUser(outsiderId, "outsider@sprinta.test"));
        context.Workspaces.Add(new Workspace
        {
            Id = workspaceId,
            Name = "P0 Runtime Workspace",
            Slug = "p0-" + Guid.NewGuid().ToString("N"),
            OwnerId = managerId,
            CreatedAt = now,
            UpdatedAt = now
        });
        context.WorkspaceMembers.AddRange(
            NewWorkspaceMember(workspaceId, managerId, "OWNER", now),
            NewWorkspaceMember(workspaceId, memberId, "MEMBER", now),
            NewWorkspaceMember(workspaceId, outsiderId, "MEMBER", now));
        context.Projects.Add(new Project
        {
            Id = projectId,
            WorkspaceId = workspaceId,
            CreatorId = managerId,
            Name = "P0 Runtime Project",
            Identifier = "P0R",
            NetworkType = "Private",
            Status = true,
            StartDate = now.Date,
            CreatedAt = now,
            UpdatedAt = now
        });
        context.ProjectMembers.AddRange(
            NewProjectMember(projectId, managerId, "PM", now),
            NewProjectMember(projectId, memberId, "Developer", now));
        context.TaskStatuses.Add(new TaskStatusEntity
        {
            Id = Guid.NewGuid(),
            ProjectId = projectId,
            Name = "TO DO",
            Position = 1
        });
        context.TaskTypes.Add(new TaskType
        {
            Id = Guid.NewGuid(),
            ProjectId = projectId,
            Name = "Task"
        });
        context.IntegrationAccounts.AddRange(
            NewIntegrationAccount(managerId, "manager@sprinta.test"),
            NewIntegrationAccount(memberId, "member@sprinta.test"));
        context.InboxItems.AddRange(
            NewInboxItem(managerInboxId, managerId, "Manager inbox item", now),
            NewInboxItem(memberInboxId, memberId, "Member inbox item", now),
            NewInboxItem(outsiderInboxId, outsiderId, "Outsider inbox item", now));
        await context.SaveChangesAsync();

        return new P0Fixture(
            managerId,
            memberId,
            outsiderId,
            workspaceId,
            projectId,
            managerInboxId,
            memberInboxId,
            outsiderInboxId);
    }

    private static User NewUser(Guid id, string email) => new()
    {
        Id = id,
        Email = email,
        FullName = email.Split('@')[0],
        PasswordHash = "test-only",
        IsActive = true,
        CreatedAt = DateTime.UtcNow,
        UpdatedAt = DateTime.UtcNow
    };

    private static WorkspaceMember NewWorkspaceMember(
        Guid workspaceId,
        Guid userId,
        string role,
        DateTime joinedAt) => new()
    {
        WorkspaceId = workspaceId,
        UserId = userId,
        WorkspaceRole = role,
        IsActive = true,
        JoinedAt = joinedAt
    };

    private static ProjectMember NewProjectMember(
        Guid projectId,
        Guid userId,
        string role,
        DateTime joinedAt) => new()
    {
        ProjectId = projectId,
        UserId = userId,
        ProjectRole = role,
        Status = true,
        JoinedAt = joinedAt
    };

    private static IntegrationAccount NewIntegrationAccount(Guid userId, string email) => new()
    {
        Id = Guid.NewGuid(),
        UserId = userId,
        Provider = "google-calendar",
        AccountEmail = email,
        AccessToken = "protected-test-value",
        Scopes = "calendar.readonly",
        IsActive = true,
        CreatedAt = DateTime.UtcNow,
        UpdatedAt = DateTime.UtcNow
    };

    private static InboxItem NewInboxItem(Guid id, Guid userId, string title, DateTime now) => new()
    {
        Id = id,
        UserId = userId,
        Source = "email",
        Provider = "gmail",
        ExternalId = Guid.NewGuid().ToString("N"),
        Title = title,
        Content = "Runtime permission fixture",
        CreatedAt = now,
        UpdatedAt = now
    };

    private static HttpClient CreateAuthenticatedClient(
        ChatApplicationFactory factory,
        Guid userId)
    {
        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", CreateToken(factory, userId));
        return client;
    }

    private static string CreateToken(ChatApplicationFactory factory, Guid userId)
    {
        var options = factory.Services
            .GetRequiredService<IOptionsMonitor<JwtBearerOptions>>()
            .Get(JwtBearerDefaults.AuthenticationScheme);
        var validation = options.TokenValidationParameters;
        var credentials = new SigningCredentials(
            validation.IssuerSigningKey,
            SecurityAlgorithms.HmacSha256);
        var expiresAt = DateTime.UtcNow.AddMinutes(10);
        return new JwtSecurityTokenHandler().WriteToken(new JwtSecurityToken(
            issuer: validation.ValidIssuer,
            audience: validation.ValidAudience,
            claims:
            [
                new Claim(ClaimTypes.NameIdentifier, userId.ToString())
            ],
            notBefore: expiresAt.AddMinutes(-10),
            expires: expiresAt,
            signingCredentials: credentials));
    }

    private static ControllerContext ControllerContextFor(Guid userId) => new()
    {
        HttpContext = new DefaultHttpContext
        {
            User = new ClaimsPrincipal(new ClaimsIdentity(
                new[] { new Claim(ClaimTypes.NameIdentifier, userId.ToString()) },
                "TestAuth"))
        }
    };

    private sealed record P0Fixture(
        Guid ManagerId,
        Guid MemberId,
        Guid OutsiderId,
        Guid WorkspaceId,
        Guid ProjectId,
        Guid ManagerInboxId,
        Guid MemberInboxId,
        Guid OutsiderInboxId);
}
