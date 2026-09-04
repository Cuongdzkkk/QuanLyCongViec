using System.Security.Claims;
using System.Text.Json;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using TaskManagement.API.Controllers;
using TaskManagement.Domain.Entities;
using TaskManagement.Infrastructure.Data;
using TaskManagement.Infrastructure.Services;

namespace TaskManagement.Tests.Logic;

public sealed class WorkspaceHierarchyAccessTests
{
    [Fact]
    public async Task GetMyWorkspaces_ReturnsOnlyOwnedDirectAndTeamAccessibleChildSites()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        await using var context = new ApplicationDbContext(options);

        var currentUser = NewUser("dev@example.com", "Dev Admin");
        var khoi = NewUser("khoi@example.com", "Nguyễn Minh Khôi");
        var otherOwner = NewUser("other@example.com", "Other Owner");
        var ownSite = NewWorkspace("Dev Site", currentUser.Id);
        var directSite = NewWorkspace("Khôi Direct", khoi.Id);
        var teamSite = NewWorkspace("Khôi Team", khoi.Id);
        var hiddenSite = NewWorkspace("Hidden Site", otherOwner.Id);
        var team = new Department
        {
            Id = Guid.NewGuid(),
            Name = "Delivery Team",
            IsActive = true,
            CreatedAt = DateTime.UtcNow
        };

        context.Users.AddRange(currentUser, khoi, otherOwner);
        context.Workspaces.AddRange(ownSite, directSite, teamSite, hiddenSite);
        context.WorkspaceMembers.Add(new WorkspaceMember
        {
            WorkspaceId = directSite.Id,
            UserId = currentUser.Id,
            WorkspaceRole = "MEMBER",
            IsActive = true,
            JoinedAt = DateTime.UtcNow
        });
        context.Departments.Add(team);
        context.DepartmentMembers.Add(new DepartmentMember
        {
            DepartmentId = team.Id,
            UserId = currentUser.Id,
            JoinedAt = DateTime.UtcNow
        });
        context.WorkspaceDepartmentAccesses.Add(new WorkspaceDepartmentAccess
        {
            WorkspaceId = teamSite.Id,
            DepartmentId = team.Id,
            GrantedByUserId = khoi.Id,
            GrantedAt = DateTime.UtcNow
        });
        await context.SaveChangesAsync();

        var controller = new WorkspacesController(context, new ResourceAuthorizationService(context))
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext
                {
                    User = new ClaimsPrincipal(new ClaimsIdentity(
                        [new Claim(ClaimTypes.NameIdentifier, currentUser.Id.ToString())],
                        "TestAuth"))
                }
            }
        };

        var result = await controller.GetMyWorkspaces();

        var ok = result.Should().BeOfType<OkObjectResult>().Subject;
        using var json = JsonDocument.Parse(JsonSerializer.Serialize(ok.Value));
        var sites = json.RootElement.GetProperty("data").EnumerateArray().ToList();
        sites.Select(site => site.GetProperty("Name").GetString())
            .Should().BeEquivalentTo("Dev Site", "Khôi Direct", "Khôi Team");
        sites.Should().NotContain(site => site.GetProperty("Name").GetString() == "Hidden Site");
        AccessSourceFor(sites, "Dev Site").Should().Be("OWNER");
        AccessSourceFor(sites, "Khôi Direct").Should().Be("DIRECT");
        AccessSourceFor(sites, "Khôi Team").Should().Be("TEAM");
        sites.Where(site => site.GetProperty("Name").GetString()!.StartsWith("Khôi"))
            .Should().OnlyContain(site => site.GetProperty("OwnerId").GetGuid() == khoi.Id);
    }

    private static string? AccessSourceFor(IEnumerable<JsonElement> sites, string name) =>
        sites.Single(site => site.GetProperty("Name").GetString() == name)
            .GetProperty("AccessSource")
            .GetString();

    private static User NewUser(string email, string fullName) => new()
    {
        Id = Guid.NewGuid(),
        Email = email,
        FullName = fullName,
        PasswordHash = "unused",
        IsActive = true
    };

    private static Workspace NewWorkspace(string name, Guid ownerId) => new()
    {
        Id = Guid.NewGuid(),
        Name = name,
        Slug = name.ToLowerInvariant().Replace(' ', '-'),
        OwnerId = ownerId,
        CreatedAt = DateTime.UtcNow,
        UpdatedAt = DateTime.UtcNow
    };
}
