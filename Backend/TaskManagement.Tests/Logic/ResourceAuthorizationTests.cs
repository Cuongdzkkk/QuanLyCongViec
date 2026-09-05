using System.Security.Claims;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Abstractions;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using Moq;
using TaskManagement.API.Filters;
using TaskManagement.Application.Common;
using TaskManagement.Application.DTOs.WorkTask;
using TaskManagement.Application.Interfaces;
using TaskManagement.Domain.Entities;
using TaskManagement.Infrastructure.Data;
using TaskManagement.Infrastructure.Services;

namespace TaskManagement.Tests.Logic;

[Collection("ProjectAccessPolicy")]
public sealed class ResourceAuthorizationTests
{
    [Theory]
    [InlineData("PM")]
    [InlineData("pm")]
    [InlineData("Pm")]
    public async Task ProjectManagerRoleCasing_HasSameSprintPermission(string role)
    {
        await using var fixture = await AuthorizationFixture.CreateAsync(role);

        var result = await fixture.Service.AuthorizeProjectAsync(
            fixture.UserId,
            fixture.ProjectId,
            ResourcePermissionCodes.SprintManage);

        result.Succeeded.Should().BeTrue();
        ProjectExecutionRuleHelper.NormalizeProjectRole(result.ProjectRole).Should().Be("pm");
    }

    [Fact]
    public async Task ActiveWorkspaceAndProjectMember_CanReadResources()
    {
        await using var fixture = await AuthorizationFixture.CreateAsync("Developer");

        (await fixture.Service.AuthorizeWorkspaceAsync(
            fixture.UserId,
            fixture.WorkspaceId,
            ResourcePermissionCodes.WorkspaceRead)).Succeeded.Should().BeTrue();
        (await fixture.Service.AuthorizeProjectAsync(
            fixture.UserId,
            fixture.ProjectId,
            ResourcePermissionCodes.ProjectRead)).Succeeded.Should().BeTrue();
    }

    [Fact]
    public async Task WorkspaceOwner_CanReadWithoutMembershipRow()
    {
        await using var fixture = await AuthorizationFixture.CreateAsync("Developer");
        var workspace = await fixture.Context.Workspaces.SingleAsync(item => item.Id == fixture.WorkspaceId);
        workspace.OwnerId = fixture.UserId;
        fixture.Context.WorkspaceMembers.RemoveRange(fixture.Context.WorkspaceMembers);
        await fixture.Context.SaveChangesAsync();

        (await fixture.Service.AuthorizeWorkspaceAsync(
            fixture.UserId,
            fixture.WorkspaceId,
            ResourcePermissionCodes.WorkspaceRead)).Succeeded.Should().BeTrue();
    }

    [Fact]
    public async Task ActiveTeamMember_CanReadWorkspaceWithoutDirectInvitation()
    {
        await using var fixture = await AuthorizationFixture.CreateAsync("Developer");
        fixture.Context.WorkspaceMembers.RemoveRange(fixture.Context.WorkspaceMembers);
        var departmentId = Guid.NewGuid();
        fixture.Context.Departments.Add(new Department
        {
            Id = departmentId,
            Name = "Product Team",
            IsActive = true,
            CreatedAt = DateTime.UtcNow
        });
        fixture.Context.DepartmentMembers.Add(new DepartmentMember
        {
            DepartmentId = departmentId,
            UserId = fixture.UserId,
            JoinedAt = DateTime.UtcNow
        });
        fixture.Context.WorkspaceDepartmentAccesses.Add(new WorkspaceDepartmentAccess
        {
            WorkspaceId = fixture.WorkspaceId,
            DepartmentId = departmentId,
            GrantedByUserId = Guid.NewGuid(),
            GrantedAt = DateTime.UtcNow
        });
        await fixture.Context.SaveChangesAsync();

        (await fixture.Service.AuthorizeWorkspaceAsync(
            fixture.UserId,
            fixture.WorkspaceId,
            ResourcePermissionCodes.WorkspaceRead)).Succeeded.Should().BeTrue();

        var membership = await fixture.Context.DepartmentMembers.SingleAsync();
        membership.LeftAt = DateTime.UtcNow;
        await fixture.Context.SaveChangesAsync();

        (await fixture.Service.AuthorizeWorkspaceAsync(
            fixture.UserId,
            fixture.WorkspaceId,
            ResourcePermissionCodes.WorkspaceRead)).Succeeded.Should().BeTrue();
    }

    [Fact]
    public async Task InactiveWorkspaceMember_IsDeniedForWorkspaceAndProject()
    {
        await using var fixture = await AuthorizationFixture.CreateAsync("PM", workspaceActive: false);

        (await fixture.Service.AuthorizeWorkspaceAsync(
            fixture.UserId,
            fixture.WorkspaceId,
            ResourcePermissionCodes.WorkspaceRead)).Succeeded.Should().BeFalse();
        (await fixture.Service.AuthorizeProjectAsync(
            fixture.UserId,
            fixture.ProjectId,
            ResourcePermissionCodes.ProjectRead)).Succeeded.Should().BeFalse();
    }

    [Fact]
    public async Task DirectProjectMember_GetsLimitedWorkspaceShellWithoutWorkspaceMembership()
    {
        await using var fixture = await AuthorizationFixture.CreateAsync("PM");
        fixture.Context.WorkspaceMembers.Remove(await fixture.Context.WorkspaceMembers.SingleAsync());
        await fixture.Context.SaveChangesAsync();

        (await fixture.Service.AuthorizeWorkspaceAsync(
            fixture.UserId,
            fixture.WorkspaceId,
            ResourcePermissionCodes.WorkspaceRead)).Succeeded.Should().BeTrue();
        (await fixture.Service.AuthorizeProjectAsync(
            fixture.UserId,
            fixture.ProjectId,
            ResourcePermissionCodes.SprintManage)).Succeeded.Should().BeTrue();
    }

    [Fact]
    public async Task Developer_CanReadButCannotManageSprint()
    {
        await using var fixture = await AuthorizationFixture.CreateAsync("Developer");

        (await fixture.Service.AuthorizeProjectAsync(
            fixture.UserId,
            fixture.ProjectId,
            ResourcePermissionCodes.ProjectRead)).Succeeded.Should().BeTrue();
        (await fixture.Service.AuthorizeProjectAsync(
            fixture.UserId,
            fixture.ProjectId,
            ResourcePermissionCodes.SprintManage)).Succeeded.Should().BeFalse();
    }

    [Fact]
    public async Task GuestWorkspaceMember_SeesOnlyExplicitlyGrantedProject()
    {
        await using var fixture = await AuthorizationFixture.CreateAsync("Developer");
        var member = await fixture.Context.WorkspaceMembers.SingleAsync();
        member.WorkspaceRole = "GUEST";
        var hiddenProjectId = Guid.NewGuid();
        fixture.Context.Projects.Add(new Project
        {
            Id = hiddenProjectId,
            WorkspaceId = fixture.WorkspaceId,
            CreatorId = fixture.UserId,
            Name = "Hidden project",
            Identifier = "HID",
            NetworkType = "Public"
        });
        await fixture.Context.SaveChangesAsync();

        var visibleIds = await fixture.Service.GetAccessibleProjectIdsAsync(fixture.UserId);

        visibleIds.Should().Contain(fixture.ProjectId);
        visibleIds.Should().NotContain(hiddenProjectId);
        (await fixture.Service.AuthorizeWorkspaceAsync(fixture.UserId, fixture.WorkspaceId, ResourcePermissionCodes.WorkspaceRead)).Succeeded.Should().BeTrue();
        (await fixture.Service.AuthorizeProjectAsync(fixture.UserId, hiddenProjectId, ResourcePermissionCodes.ProjectRead)).Succeeded.Should().BeFalse();
    }

    [Fact]
    public async Task WorkspaceMember_SeesPublicProjectButNotPrivateProjectWithoutMembership()
    {
        await using var fixture = await AuthorizationFixture.CreateAsync("Developer");
        fixture.Context.ProjectMembers.Remove(await fixture.Context.ProjectMembers.SingleAsync());
        var privateProjectId = Guid.NewGuid();
        fixture.Context.Projects.Add(new Project
        {
            Id = privateProjectId,
            WorkspaceId = fixture.WorkspaceId,
            CreatorId = fixture.UserId,
            Name = "Private project",
            Identifier = "PRI",
            NetworkType = "Private"
        });
        await fixture.Context.SaveChangesAsync();

        var visibleIds = await fixture.Service.GetAccessibleProjectIdsAsync(fixture.UserId);

        visibleIds.Should().Contain(fixture.ProjectId);
        visibleIds.Should().NotContain(privateProjectId);
    }

    [Fact]
    public async Task ForeignPrivateProject_IsDeniedForReadAndWrite()
    {
        await using var fixture = await AuthorizationFixture.CreateAsync("Developer");
        var foreignProjectId = Guid.NewGuid();
        fixture.Context.Projects.Add(new Project
        {
            Id = foreignProjectId,
            WorkspaceId = fixture.WorkspaceId,
            CreatorId = fixture.UserId,
            Name = "Foreign private project",
            Identifier = "FOR",
            NetworkType = "Private"
        });
        await fixture.Context.SaveChangesAsync();

        (await fixture.Service.AuthorizeProjectAsync(
            fixture.UserId,
            foreignProjectId,
            ResourcePermissionCodes.ProjectRead)).Succeeded.Should().BeFalse();
        (await fixture.Service.AuthorizeProjectAsync(
            fixture.UserId,
            foreignProjectId,
            ResourcePermissionCodes.ProjectWrite)).Succeeded.Should().BeFalse();
    }

    [Theory]
    [InlineData(false, false)]
    [InlineData(true, true)]
    public async Task InactiveOrDeletedUser_CannotManageSprint(bool isActive, bool isDeleted)
    {
        await using var fixture = await AuthorizationFixture.CreateAsync("PM");
        var user = await fixture.Context.Users.SingleAsync(item => item.Id == fixture.UserId);
        user.IsActive = isActive;
        user.IsDeleted = isDeleted;
        await fixture.Context.SaveChangesAsync();

        (await fixture.Service.AuthorizeProjectAsync(
            fixture.UserId,
            fixture.ProjectId,
            ResourcePermissionCodes.SprintManage)).Succeeded.Should().BeFalse();
    }

    [Fact]
    public async Task InactiveProject_CannotManageSprint()
    {
        await using var fixture = await AuthorizationFixture.CreateAsync("PM");
        var project = await fixture.Context.Projects.SingleAsync(item => item.Id == fixture.ProjectId);
        project.Status = false;
        await fixture.Context.SaveChangesAsync();

        (await fixture.Service.AuthorizeProjectAsync(
            fixture.UserId,
            fixture.ProjectId,
            ResourcePermissionCodes.SprintManage)).Succeeded.Should().BeFalse();
    }

    [Fact]
    public async Task AdminSystemRoleWithoutProjectMembership_CannotBypassProjectFilter()
    {
        await using var fixture = await AuthorizationFixture.CreateAsync(projectMemberActive: false);
        var filter = new ProjectAuthorizeFilter(ResourcePermissionCodes.SprintManage, fixture.Service);
        var httpContext = new DefaultHttpContext
        {
            User = new ClaimsPrincipal(new ClaimsIdentity(new[]
            {
                new Claim(ClaimTypes.NameIdentifier, fixture.UserId.ToString()),
                new Claim(ClaimTypes.Role, "Admin")
            }, "TestAuth"))
        };
        var routeData = new RouteData();
        routeData.Values["projectId"] = fixture.ProjectId.ToString();
        var actionContext = new ActionContext(httpContext, routeData, new ActionDescriptor());
        var executingContext = new ActionExecutingContext(
            actionContext,
            new List<IFilterMetadata>(),
            new Dictionary<string, object?>(),
            new object());
        var actionCalled = false;

        await filter.OnActionExecutionAsync(executingContext, () =>
        {
            actionCalled = true;
            return Task.FromResult(new ActionExecutedContext(actionContext, new List<IFilterMetadata>(), new object()));
        });

        actionCalled.Should().BeFalse();
        executingContext.Result.Should().BeOfType<ObjectResult>()
            .Which.StatusCode.Should().Be(StatusCodes.Status403Forbidden);
    }

    [Fact]
    public async Task OpenSprint_TaskUpdateStillSucceedsForProjectManager()
    {
        await using var fixture = await AuthorizationFixture.CreateAsync("PM");
        var statusId = Guid.NewGuid();
        var taskTypeId = Guid.NewGuid();
        var sprintId = Guid.NewGuid();
        var taskId = Guid.NewGuid();
        fixture.Context.TaskStatuses.Add(new TaskManagement.Domain.Entities.TaskStatus
        {
            Id = statusId,
            ProjectId = fixture.ProjectId,
            Name = "To Do"
        });
        fixture.Context.TaskTypes.Add(new TaskType
        {
            Id = taskTypeId,
            ProjectId = fixture.ProjectId,
            Name = "Task"
        });
        fixture.Context.Sprints.Add(new Sprint
        {
            Id = sprintId,
            ProjectId = fixture.ProjectId,
            Name = "Open Sprint",
            Status = true,
            StartDate = DateTime.UtcNow.AddDays(-1),
            EndDate = DateTime.UtcNow.AddDays(7)
        });
        fixture.Context.WorkTasks.Add(new WorkTask
        {
            Id = taskId,
            WorkspaceId = fixture.WorkspaceId,
            ProjectId = fixture.ProjectId,
            SprintId = sprintId,
            TaskTypeId = taskTypeId,
            TaskStatusId = statusId,
            ReporterId = fixture.UserId,
            Title = "Before",
            SequenceId = "PRJ-1",
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        });
        await fixture.Context.SaveChangesAsync();
        var rowVersion = (await fixture.Context.WorkTasks.AsNoTracking().SingleAsync(task => task.Id == taskId)).RowVersion;
        var service = new WorkTaskService(fixture.Context, Mock.Of<IGamificationService>());

        var result = await service.UpdateAsync(taskId, fixture.UserId, new UpdateWorkTaskDto
        {
            Title = "After",
            RowVersion = rowVersion
        });

        result.Title.Should().Be("After");
        (await fixture.Context.WorkTasks.SingleAsync()).Title.Should().Be("After");
    }

    private sealed class AuthorizationFixture : IAsyncDisposable
    {
        private AuthorizationFixture(
            ApplicationDbContext context,
            Guid userId,
            Guid workspaceId,
            Guid projectId)
        {
            Context = context;
            UserId = userId;
            WorkspaceId = workspaceId;
            ProjectId = projectId;
            Service = new ResourceAuthorizationService(context);
        }

        public ApplicationDbContext Context { get; }
        public ResourceAuthorizationService Service { get; }
        public Guid UserId { get; }
        public Guid WorkspaceId { get; }
        public Guid ProjectId { get; }

        public static async Task<AuthorizationFixture> CreateAsync(
            string projectRole = "PM",
            bool workspaceActive = true,
            bool projectMemberActive = true)
        {
            var options = new DbContextOptionsBuilder<ApplicationDbContext>()
                .UseInMemoryDatabase(Guid.NewGuid().ToString())
                .Options;
            var context = new ApplicationDbContext(options);
            var userId = Guid.NewGuid();
            var ownerId = Guid.NewGuid();
            var workspaceId = Guid.NewGuid();
            var projectId = Guid.NewGuid();
            var user = new User
            {
                Id = userId,
                Email = "member@example.com",
                FullName = "Member",
                PasswordHash = "unused",
                IsActive = true
            };
            context.Users.AddRange(user, new User
            {
                Id = ownerId,
                Email = "owner@example.com",
                FullName = "Owner",
                PasswordHash = "unused",
                IsActive = true
            });
            context.Workspaces.Add(new Workspace
            {
                Id = workspaceId,
                Name = "Workspace",
                Slug = "workspace",
                OwnerId = ownerId,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            });
            context.WorkspaceMembers.Add(new WorkspaceMember
            {
                WorkspaceId = workspaceId,
                UserId = userId,
                WorkspaceRole = "MEMBER",
                IsActive = workspaceActive,
                JoinedAt = DateTime.UtcNow
            });
            context.Projects.Add(new Project
            {
                Id = projectId,
                WorkspaceId = workspaceId,
                CreatorId = userId,
                Name = "Project",
                Identifier = "PRJ"
            });
            context.ProjectMembers.Add(new ProjectMember
            {
                ProjectId = projectId,
                UserId = userId,
                ProjectRole = projectRole,
                Status = projectMemberActive,
                JoinedAt = DateTime.UtcNow
            });
            await context.SaveChangesAsync();
            return new AuthorizationFixture(context, userId, workspaceId, projectId);
        }

        public ValueTask DisposeAsync() => Context.DisposeAsync();
    }
}
