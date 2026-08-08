using System.Security.Claims;
using System.Text.Json;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using Moq;
using TaskManagement.API.Controllers;
using TaskManagement.API.Hubs;
using TaskManagement.Application.DTOs.WorkTask;
using TaskManagement.Application.Interfaces;
using TaskManagement.Domain.Entities;
using TaskManagement.Infrastructure.Data;
using TaskManagement.Infrastructure.Services;
using TaskStatus = TaskManagement.Domain.Entities.TaskStatus;

namespace TaskManagement.Tests.Logic;

public sealed class PersonalWorkDataTests
{
    [Fact]
    public async Task AssignedScope_ReturnsOnlyCurrentActiveAssignments()
    {
        await using var fixture = await PersonalWorkFixture.CreateAsync();

        var userA = await fixture.Service.SearchTasksAsync(
            fixture.UserAId, null, null, null, null, scope: "assigned");
        var userB = await fixture.Service.SearchTasksAsync(
            fixture.UserBId, null, null, null, null, scope: "assigned");

        userA.Select(task => task.Id).Should().Contain(fixture.AssignedToAId);
        userA.Select(task => task.Id).Should().NotContain(fixture.AssignedToBId);
        userA.Select(task => task.Id).Should().NotContain(fixture.InactiveAssignmentId);
        userB.Select(task => task.Id).Should().Contain(fixture.AssignedToBId);
        userB.Select(task => task.Id).Should().NotContain(fixture.AssignedToAId);
    }

    [Fact]
    public async Task OutsiderAndInactiveUsers_CannotReadPersonalProjectData()
    {
        await using var fixture = await PersonalWorkFixture.CreateAsync();

        var outsider = await fixture.Service.SearchTasksAsync(
            fixture.OutsiderId, null, null, null, null, scope: "suggested");
        var inactiveAction = () => fixture.Service.SearchTasksAsync(
            fixture.InactiveUserId, null, null, null, null, scope: "assigned");
        var deletedAction = () => fixture.Service.SearchTasksAsync(
            fixture.DeletedUserId, null, null, null, null, scope: "assigned");

        outsider.Should().BeEmpty();
        await inactiveAction.Should().ThrowAsync<UnauthorizedAccessException>();
        await deletedAction.Should().ThrowAsync<UnauthorizedAccessException>();
    }

    [Fact]
    public async Task CreatedScope_UsesAuthenticatedReporterAsCreator()
    {
        await using var fixture = await PersonalWorkFixture.CreateAsync();

        var userA = await fixture.Service.SearchTasksAsync(
            fixture.UserAId, null, null, null, null, scope: "created");

        userA.Select(task => task.Id).Should().Contain(fixture.CreatedByAId);
        userA.Select(task => task.Id).Should().NotContain(fixture.CreatedByBId);
    }

    [Fact]
    public async Task FollowingAndWorkedScopes_UseServerSideActivitySources()
    {
        await using var fixture = await PersonalWorkFixture.CreateAsync();

        var following = await fixture.Service.SearchTasksAsync(
            fixture.UserAId, null, null, null, null, scope: "following");
        var worked = await fixture.Service.SearchTasksAsync(
            fixture.UserAId, null, null, null, null, scope: "worked");

        following.Select(task => task.Id).Should().Contain(fixture.FollowedId);
        following.Select(task => task.Id).Should().NotContain(fixture.DeletedFollowedId);
        worked.Select(task => task.Id).Should().Contain(fixture.WorkedOnId);
        worked.Select(task => task.Id).Should().NotContain(fixture.ViewedOnlyId);
    }

    [Fact]
    public async Task SuggestedScope_ExcludesUnauthorizedDeletedAndCompletedTasks()
    {
        await using var fixture = await PersonalWorkFixture.CreateAsync();

        var suggested = await fixture.Service.SearchTasksAsync(
            fixture.UserAId, null, null, null, null, scope: "suggested");
        var ids = suggested.Select(task => task.Id).ToHashSet();

        ids.Should().Contain(fixture.OverdueId);
        ids.Should().Contain(fixture.InProgressId);
        ids.Should().NotContain(fixture.UnauthorizedProjectTaskId);
        ids.Should().NotContain(fixture.DeletedTaskId);
        ids.Should().NotContain(fixture.CompletedId);
    }

    [Fact]
    public async Task SummaryCounts_MatchTheSamePersonalLists()
    {
        await using var fixture = await PersonalWorkFixture.CreateAsync();

        var summary = await fixture.Service.GetPersonalWorkSummaryAsync(fixture.UserAId);
        var assigned = await fixture.Service.SearchTasksAsync(
            fixture.UserAId, null, null, null, null, scope: "assigned");
        var created = await fixture.Service.SearchTasksAsync(
            fixture.UserAId, null, null, null, null, scope: "created");
        var following = await fixture.Service.SearchTasksAsync(
            fixture.UserAId, null, null, null, null, scope: "following");
        var worked = await fixture.Service.SearchTasksAsync(
            fixture.UserAId, null, null, null, null, scope: "worked");

        summary.Assigned.Should().Be(assigned.Count);
        summary.Created.Should().Be(created.Count);
        summary.Following.Should().Be(following.Count);
        summary.WorkedOn.Should().Be(worked.Count);
        summary.Overdue.Should().Be(assigned.Count(task =>
            task.DueDate < DateTime.UtcNow &&
            task.StatusName is not "DONE" and not "COMPLETED"));
        summary.Completed.Should().Be(assigned.Count(task =>
            task.StatusName is "DONE" or "COMPLETED"));
    }

    [Fact]
    public async Task Pagination_IsDeterministicWithoutMissingOrDuplicateTasks()
    {
        await using var fixture = await PersonalWorkFixture.CreateAsync();

        var first = await fixture.Service.GetPersonalWorkPageAsync(
            fixture.UserAId, "assigned", page: 1, pageSize: 2);
        var second = await fixture.Service.GetPersonalWorkPageAsync(
            fixture.UserAId, "assigned", page: 2, pageSize: 2);
        var all = await fixture.Service.GetPersonalWorkPageAsync(
            fixture.UserAId, "assigned", page: 1, pageSize: 100);

        first.Items.Select(task => task.Id)
            .Should().NotIntersectWith(second.Items.Select(task => task.Id));
        first.Items.Concat(second.Items).Select(task => task.Id)
            .Should().BeEquivalentTo(all.Items.Select(task => task.Id));
        first.TotalCount.Should().Be(all.TotalCount);
    }

    [Fact]
    public async Task PersonalWorkEndpoint_UsesJwtClaimAndHasNoClientUserIdOverride()
    {
        var userAId = Guid.NewGuid();
        var service = new Mock<IWorkTaskService>();
        service
            .Setup(item => item.GetPersonalWorkPageAsync(userAId, "assigned", 1, 25))
            .ReturnsAsync(new PersonalWorkPageDto
            {
                Page = 1,
                PageSize = 25
            });
        var controller = new WorkTasksController(
            service.Object,
            Mock.Of<IHubContext<KanbanHub>>())
        {
            ControllerContext = BuildControllerContext(userAId)
        };

        var result = await controller.GetPersonalWork("assigned", 1, 25);

        result.Should().BeOfType<OkObjectResult>();
        service.Verify(
            item => item.GetPersonalWorkPageAsync(userAId, "assigned", 1, 25),
            Times.Once);
    }

    [Fact]
    public async Task PersonalActivity_ReturnsOnlyTheAuthenticatedUsersRecords()
    {
        await using var fixture = await PersonalWorkFixture.CreateAsync();
        fixture.Context.SiteAuditLogs.AddRange(
            new SiteAuditLog
            {
                Id = Guid.NewGuid(),
                EntityId = fixture.ProjectAId,
                EntityType = "Project",
                Action = "Update",
                UserId = fixture.UserAId,
                CreatedAt = DateTime.UtcNow
            },
            new SiteAuditLog
            {
                Id = Guid.NewGuid(),
                EntityId = fixture.ProjectAId,
                EntityType = "Project",
                Action = "Update",
                UserId = fixture.UserBId,
                CreatedAt = DateTime.UtcNow
            });
        await fixture.Context.SaveChangesAsync();
        var controller = new SiteAuditLogsController(fixture.Context)
        {
            ControllerContext = BuildControllerContext(fixture.UserAId)
        };

        var result = await controller.Get(null, null, 100);

        var ok = result.Should().BeOfType<OkObjectResult>().Subject;
        using var json = JsonDocument.Parse(JsonSerializer.Serialize(ok.Value));
        var items = json.RootElement
            .GetProperty("data")
            .GetProperty("items")
            .EnumerateArray()
            .ToList();
        items.Should().ContainSingle();
        items[0].GetProperty("user").GetString().Should().Be("User A");
    }

    private static ControllerContext BuildControllerContext(Guid userId)
    {
        return new ControllerContext
        {
            HttpContext = new DefaultHttpContext
            {
                User = new ClaimsPrincipal(new ClaimsIdentity(
                    new[] { new Claim(ClaimTypes.NameIdentifier, userId.ToString()) },
                    "TestAuth"))
            }
        };
    }

    private sealed class PersonalWorkFixture : IAsyncDisposable
    {
        private PersonalWorkFixture(ApplicationDbContext context)
        {
            Context = context;
            Service = new WorkTaskService(context, Mock.Of<IGamificationService>());
        }

        public ApplicationDbContext Context { get; }
        public WorkTaskService Service { get; }
        public Guid UserAId { get; } = Guid.NewGuid();
        public Guid UserBId { get; } = Guid.NewGuid();
        public Guid OutsiderId { get; } = Guid.NewGuid();
        public Guid InactiveUserId { get; } = Guid.NewGuid();
        public Guid DeletedUserId { get; } = Guid.NewGuid();
        public Guid WorkspaceId { get; } = Guid.NewGuid();
        public Guid ProjectAId { get; } = Guid.NewGuid();
        public Guid ProjectBId { get; } = Guid.NewGuid();
        public Guid AssignedToAId { get; } = Guid.NewGuid();
        public Guid AssignedToBId { get; } = Guid.NewGuid();
        public Guid CreatedByAId { get; } = Guid.NewGuid();
        public Guid CreatedByBId { get; } = Guid.NewGuid();
        public Guid FollowedId { get; } = Guid.NewGuid();
        public Guid DeletedFollowedId { get; } = Guid.NewGuid();
        public Guid WorkedOnId { get; } = Guid.NewGuid();
        public Guid ViewedOnlyId { get; } = Guid.NewGuid();
        public Guid InactiveAssignmentId { get; } = Guid.NewGuid();
        public Guid UnauthorizedProjectTaskId { get; } = Guid.NewGuid();
        public Guid DeletedTaskId { get; } = Guid.NewGuid();
        public Guid OverdueId { get; } = Guid.NewGuid();
        public Guid CompletedId { get; } = Guid.NewGuid();
        public Guid InProgressId { get; } = Guid.NewGuid();

        public static async Task<PersonalWorkFixture> CreateAsync()
        {
            var options = new DbContextOptionsBuilder<ApplicationDbContext>()
                .UseInMemoryDatabase(Guid.NewGuid().ToString())
                .Options;
            var fixture = new PersonalWorkFixture(new ApplicationDbContext(options));
            await fixture.SeedAsync();
            return fixture;
        }

        private async Task SeedAsync()
        {
            var now = DateTime.UtcNow;
            var toDoStatusId = Guid.NewGuid();
            var doneStatusId = Guid.NewGuid();
            var inProgressStatusId = Guid.NewGuid();
            var taskTypeId = Guid.NewGuid();

            Context.Users.AddRange(
                User(UserAId, "a@example.test", "User A"),
                User(UserBId, "b@example.test", "User B"),
                User(OutsiderId, "outsider@example.test", "Outsider"),
                User(InactiveUserId, "inactive@example.test", "Inactive", active: false),
                User(DeletedUserId, "deleted@example.test", "Deleted", deleted: true));
            Context.Workspaces.Add(new Workspace
            {
                Id = WorkspaceId,
                Name = "Workspace 1",
                Slug = "workspace-1",
                OwnerId = UserAId,
                CreatedAt = now,
                UpdatedAt = now
            });
            Context.WorkspaceMembers.AddRange(
                WorkspaceMember(UserAId),
                WorkspaceMember(UserBId));
            Context.Projects.AddRange(
                Project(ProjectAId, "Project A", UserAId),
                Project(ProjectBId, "Project B", UserBId));
            Context.ProjectMembers.AddRange(
                ProjectMember(ProjectAId, UserAId),
                ProjectMember(ProjectAId, UserBId),
                ProjectMember(ProjectBId, UserBId));
            Context.TaskStatuses.AddRange(
                new TaskStatus { Id = toDoStatusId, ProjectId = ProjectAId, Name = "TO DO" },
                new TaskStatus { Id = doneStatusId, ProjectId = ProjectAId, Name = "DONE" },
                new TaskStatus { Id = inProgressStatusId, ProjectId = ProjectAId, Name = "IN PROGRESS" },
                new TaskStatus { Id = Guid.NewGuid(), ProjectId = ProjectBId, Name = "TO DO" });
            var projectBStatusId = Context.TaskStatuses.Local.Single(status =>
                status.ProjectId == ProjectBId).Id;
            Context.TaskTypes.AddRange(
                new TaskManagement.Domain.Entities.TaskType
                {
                    Id = taskTypeId,
                    ProjectId = ProjectAId,
                    Name = "Task"
                },
                new TaskManagement.Domain.Entities.TaskType
                {
                    Id = Guid.NewGuid(),
                    ProjectId = ProjectBId,
                    Name = "Task"
                });
            var projectBTaskTypeId = Context.TaskTypes.Local.Single(type =>
                type.ProjectId == ProjectBId).Id;

            Context.WorkTasks.AddRange(
                Task(AssignedToAId, "Assigned A", UserBId, UserAId, toDoStatusId, taskTypeId, now.AddMinutes(-1)),
                Task(AssignedToBId, "Assigned B", UserAId, UserBId, toDoStatusId, taskTypeId, now.AddMinutes(-2)),
                Task(CreatedByAId, "Created A", UserAId, null, toDoStatusId, taskTypeId, now.AddMinutes(-3)),
                Task(CreatedByBId, "Created B", UserBId, null, toDoStatusId, taskTypeId, now.AddMinutes(-4)),
                Task(FollowedId, "Followed", UserBId, null, toDoStatusId, taskTypeId, now.AddMinutes(-5)),
                Task(DeletedFollowedId, "Deleted followed", UserBId, null, toDoStatusId, taskTypeId, now.AddMinutes(-6), deleted: true),
                Task(WorkedOnId, "Worked on", UserBId, null, toDoStatusId, taskTypeId, now.AddMinutes(-7)),
                Task(ViewedOnlyId, "Viewed only", UserBId, UserBId, toDoStatusId, taskTypeId, now.AddMinutes(-8)),
                Task(InactiveAssignmentId, "Inactive assignment", UserBId, null, toDoStatusId, taskTypeId, now.AddMinutes(-9)),
                Task(DeletedTaskId, "Deleted", UserAId, UserAId, toDoStatusId, taskTypeId, now.AddMinutes(-10), deleted: true),
                Task(OverdueId, "Overdue", UserAId, UserAId, toDoStatusId, taskTypeId, now.AddMinutes(-11), now.AddDays(-1)),
                Task(CompletedId, "Completed", UserAId, UserAId, doneStatusId, taskTypeId, now.AddMinutes(-12)),
                Task(InProgressId, "In progress", UserAId, UserAId, inProgressStatusId, taskTypeId, now.AddMinutes(-13)),
                Task(
                    UnauthorizedProjectTaskId,
                    "Project B private",
                    UserAId,
                    UserAId,
                    projectBStatusId,
                    projectBTaskTypeId,
                    now.AddMinutes(-14),
                    projectId: ProjectBId));
            Context.TaskAssignments.AddRange(
                Assignment(AssignedToAId, UserAId, active: true),
                Assignment(AssignedToBId, UserBId, active: true),
                Assignment(InactiveAssignmentId, UserAId, active: false));
            Context.TaskSubscribers.AddRange(
                new TaskSubscriber
                {
                    WorkTaskId = FollowedId,
                    UserId = UserAId,
                    SubscribedAt = now
                },
                new TaskSubscriber
                {
                    WorkTaskId = DeletedFollowedId,
                    UserId = UserAId,
                    SubscribedAt = now
                });
            Context.AuditLogs.Add(new AuditLog
            {
                Id = Guid.NewGuid(),
                WorkTaskId = WorkedOnId,
                UserId = UserAId,
                FieldChanged = "Title",
                CreatedAt = now
            });
            Context.RecentViews.Add(new RecentView
            {
                Id = Guid.NewGuid(),
                UserId = UserAId,
                EntityType = "Task",
                EntityId = ViewedOnlyId,
                Title = "Viewed only",
                ViewedAt = now
            });

            await Context.SaveChangesAsync();
        }

        private static User User(Guid id, string email, string name, bool active = true, bool deleted = false)
        {
            return new User
            {
                Id = id,
                Email = email,
                FullName = name,
                PasswordHash = "unused",
                IsActive = active,
                IsDeleted = deleted,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };
        }

        private WorkspaceMember WorkspaceMember(Guid userId)
        {
            return new WorkspaceMember
            {
                WorkspaceId = WorkspaceId,
                UserId = userId,
                WorkspaceRole = "MEMBER",
                IsActive = true,
                JoinedAt = DateTime.UtcNow
            };
        }

        private Project Project(Guid id, string name, Guid creatorId)
        {
            return new Project
            {
                Id = id,
                WorkspaceId = WorkspaceId,
                CreatorId = creatorId,
                Name = name,
                Identifier = name.EndsWith('A') ? "PRA" : "PRB",
                Status = true,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };
        }

        private static ProjectMember ProjectMember(Guid projectId, Guid userId)
        {
            return new ProjectMember
            {
                ProjectId = projectId,
                UserId = userId,
                ProjectRole = "Developer",
                Status = true,
                JoinedAt = DateTime.UtcNow
            };
        }

        private WorkTask Task(
            Guid id,
            string title,
            Guid reporterId,
            Guid? assignedUserId,
            Guid statusId,
            Guid taskTypeId,
            DateTime updatedAt,
            DateTime? dueDate = null,
            bool deleted = false,
            Guid? projectId = null)
        {
            var targetProjectId = projectId ?? ProjectAId;
            return new WorkTask
            {
                Id = id,
                WorkspaceId = WorkspaceId,
                ProjectId = targetProjectId,
                TaskStatusId = statusId,
                TaskTypeId = taskTypeId,
                ReporterId = reporterId,
                AssignedUserId = assignedUserId,
                Title = title,
                Priority = 2,
                DueDate = dueDate,
                IsDeleted = deleted,
                CreatedAt = updatedAt.AddHours(-1),
                UpdatedAt = updatedAt,
                SequenceId = id.ToString("N")[..8]
            };
        }

        private static TaskAssignment Assignment(Guid taskId, Guid userId, bool active)
        {
            return new TaskAssignment
            {
                WorkTaskId = taskId,
                UserId = userId,
                Status = active
            };
        }

        public ValueTask DisposeAsync()
        {
            return Context.DisposeAsync();
        }
    }
}
