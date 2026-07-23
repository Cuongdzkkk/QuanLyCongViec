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
    public async Task UserOutsideWorkspace_IsDeniedEvenWithProjectMemberRow()
    {
        await using var fixture = await AuthorizationFixture.CreateAsync("PM");
        fixture.Context.WorkspaceMembers.Remove(await fixture.Context.WorkspaceMembers.SingleAsync());
        await fixture.Context.SaveChangesAsync();

        (await fixture.Service.AuthorizeWorkspaceAsync(
            fixture.UserId,
            fixture.WorkspaceId,
            ResourcePermissionCodes.WorkspaceRead)).Succeeded.Should().BeFalse();
        (await fixture.Service.AuthorizeProjectAsync(
            fixture.UserId,
            fixture.ProjectId,
            ResourcePermissionCodes.SprintManage)).Succeeded.Should().BeFalse();
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
            context.Users.Add(user);
            context.Workspaces.Add(new Workspace
            {
                Id = workspaceId,
                Name = "Workspace",
                Slug = "workspace",
                OwnerId = userId,
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
