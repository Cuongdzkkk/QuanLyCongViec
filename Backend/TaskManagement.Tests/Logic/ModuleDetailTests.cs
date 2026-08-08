using System.Security.Claims;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Moq;
using TaskManagement.API.Controllers;
using TaskManagement.Application.Common;
using TaskManagement.Application.DTOs.Module;
using TaskManagement.Application.DTOs.WorkTask;
using TaskManagement.Application.Interfaces;
using TaskManagement.Domain.Entities;
using TaskManagement.Infrastructure.Data;
using TaskManagement.Infrastructure.Services;
using TaskStatus = TaskManagement.Domain.Entities.TaskStatus;

namespace TaskManagement.Tests.Logic;

public class ModuleDetailTests
{
    [Fact]
    public async Task Detail_UsesIssueModuleMembershipAndReturnsScopedSummary()
    {
        await using var fixture = await ModuleDetailFixture.CreateAsync();
        await using var context = fixture.CreateContext();
        var service = CreateService(context);

        var detail = await service.GetModuleDetailAsync(
            fixture.ProjectId,
            fixture.ModuleAId,
            fixture.ManagerId,
            page: 1,
            pageSize: 20);

        detail.Should().NotBeNull();
        detail!.Id.Should().Be(fixture.ModuleAId);
        detail.ProjectId.Should().Be(fixture.ProjectId);
        detail.WorkspaceId.Should().Be(fixture.WorkspaceId);
        detail.TaskCount.Should().Be(2);
        detail.CompletedCount.Should().Be(1);
        detail.InProgressCount.Should().Be(1);
        detail.OverdueCount.Should().Be(1);
        detail.ProgressPercent.Should().Be(50);
        detail.Tasks.TotalCount.Should().Be(2);
        detail.Tasks.Items.Select(task => task.Id).Should().Equal(
            fixture.InProgressTaskId,
            fixture.MultiModuleTaskId);
        detail.Tasks.Items.Should().OnlyContain(task =>
            task.ProjectId == fixture.ProjectId &&
            task.ModuleId == fixture.ModuleAId &&
            task.ProjectName == "Module Project");

        var multiModuleTask = detail.Tasks.Items.Single(task => task.Id == fixture.MultiModuleTaskId);
        multiModuleTask.SprintId.Should().Be(fixture.SprintId);
        multiModuleTask.SprintName.Should().Be("Module Sprint");
        multiModuleTask.AssignedUserId.Should().Be(fixture.ManagerId);
        multiModuleTask.Assignees.Should().ContainSingle(
            assignee => assignee.UserId == fixture.ManagerId);
    }

    [Fact]
    public async Task Detail_AppliesVisibilityAndActiveMembershipRules()
    {
        await using var fixture = await ModuleDetailFixture.CreateAsync();
        await using var context = fixture.CreateContext();
        var service = CreateService(context);

        var memberDetail = await service.GetModuleDetailAsync(
            fixture.ProjectId,
            fixture.ModuleAId,
            fixture.MemberId,
            page: 1,
            pageSize: 20);

        memberDetail.Should().NotBeNull();
        memberDetail!.Tasks.Items.Select(task => task.Id)
            .Should().Equal(fixture.MultiModuleTaskId);
        memberDetail.TaskCount.Should().Be(1);
        memberDetail.CompletedCount.Should().Be(1);

        var outsiderCall = () => service.GetModuleDetailAsync(
            fixture.ProjectId,
            fixture.ModuleAId,
            fixture.OutsiderId,
            page: 1,
            pageSize: 20);
        await outsiderCall.Should().ThrowAsync<UnauthorizedAccessException>();

        var inactiveCall = () => service.GetModuleDetailAsync(
            fixture.ProjectId,
            fixture.ModuleAId,
            fixture.InactiveMemberId,
            page: 1,
            pageSize: 20);
        await inactiveCall.Should().ThrowAsync<UnauthorizedAccessException>();
    }

    [Fact]
    public async Task Detail_ExcludesDisabledOrCrossProjectModuleAndPaginatesDeterministically()
    {
        await using var fixture = await ModuleDetailFixture.CreateAsync();
        await using var context = fixture.CreateContext();
        var service = CreateService(context);

        var firstPage = await service.GetModuleDetailAsync(
            fixture.ProjectId,
            fixture.ModuleAId,
            fixture.ManagerId,
            page: 1,
            pageSize: 1);
        var secondPage = await service.GetModuleDetailAsync(
            fixture.ProjectId,
            fixture.ModuleAId,
            fixture.ManagerId,
            page: 2,
            pageSize: 1);
        var disabled = await service.GetModuleDetailAsync(
            fixture.ProjectId,
            fixture.DisabledModuleId,
            fixture.ManagerId,
            page: 1,
            pageSize: 20);
        var wrongProject = await service.GetModuleDetailAsync(
            fixture.OtherProjectId,
            fixture.ModuleAId,
            fixture.ManagerId,
            page: 1,
            pageSize: 20);
        var beyondRange = await service.GetModuleDetailAsync(
            fixture.ProjectId,
            fixture.ModuleAId,
            fixture.ManagerId,
            page: 3,
            pageSize: 1);

        firstPage!.Tasks.Items.Should().ContainSingle()
            .Which.Id.Should().Be(fixture.InProgressTaskId);
        firstPage.Tasks.TotalPages.Should().Be(2);
        firstPage.Tasks.HasPreviousPage.Should().BeFalse();
        firstPage.Tasks.HasNextPage.Should().BeTrue();
        secondPage!.Tasks.Items.Should().ContainSingle()
            .Which.Id.Should().Be(fixture.MultiModuleTaskId);
        secondPage.Tasks.HasPreviousPage.Should().BeTrue();
        secondPage.Tasks.HasNextPage.Should().BeFalse();
        beyondRange!.Tasks.Items.Should().BeEmpty();
        beyondRange.Tasks.TotalCount.Should().Be(2);
        beyondRange.Tasks.Page.Should().Be(3);
        disabled.Should().BeNull();
        wrongProject.Should().BeNull();
    }

    [Fact]
    public async Task Detail_RejectsInactiveProjectOrDeletedWorkspace()
    {
        await using var fixture = await ModuleDetailFixture.CreateAsync();
        await using var context = fixture.CreateContext();
        var service = CreateService(context);

        var project = await context.Projects.SingleAsync(item => item.Id == fixture.ProjectId);
        project.Status = false;
        await context.SaveChangesAsync();
        (await service.GetModuleDetailAsync(
            fixture.ProjectId,
            fixture.ModuleAId,
            fixture.ManagerId,
            1,
            20)).Should().BeNull();

        project.Status = true;
        var workspace = await context.Workspaces.SingleAsync(
            item => item.Id == fixture.WorkspaceId);
        workspace.IsDeleted = true;
        await context.SaveChangesAsync();
        (await service.GetModuleDetailAsync(
            fixture.ProjectId,
            fixture.ModuleAId,
            fixture.ManagerId,
            1,
            20)).Should().BeNull();
    }

    [Fact]
    public async Task Detail_IsReadOnlyAcrossReload()
    {
        await using var fixture = await ModuleDetailFixture.CreateAsync();
        DateTime originalTaskUpdatedAt;
        DateTime originalModuleUpdatedAt;
        await using (var context = fixture.CreateContext())
        {
            originalTaskUpdatedAt = await context.WorkTasks
                .Where(task => task.Id == fixture.MultiModuleTaskId)
                .Select(task => task.UpdatedAt)
                .SingleAsync();
            originalModuleUpdatedAt = await context.Modules
                .Where(module => module.Id == fixture.ModuleAId)
                .Select(module => module.UpdatedAt)
                .SingleAsync();

            var service = CreateService(context);
            await service.GetModuleDetailAsync(
                fixture.ProjectId,
                fixture.ModuleAId,
                fixture.ManagerId,
                page: 1,
                pageSize: 20);
            context.ChangeTracker.HasChanges().Should().BeFalse();
        }

        await using var reloaded = fixture.CreateContext();
        (await reloaded.WorkTasks
            .Where(task => task.Id == fixture.MultiModuleTaskId)
            .Select(task => task.UpdatedAt)
            .SingleAsync()).Should().Be(originalTaskUpdatedAt);
        (await reloaded.Modules
            .Where(module => module.Id == fixture.ModuleAId)
            .Select(module => module.UpdatedAt)
            .SingleAsync()).Should().Be(originalModuleUpdatedAt);
        (await reloaded.IssueModules.CountAsync(link =>
            link.WorkTaskId == fixture.MultiModuleTaskId)).Should().Be(2);
    }

    [Fact]
    public async Task Controller_UsesJwtIdentityAndReturnsExpectedStatusCodes()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString("N"))
            .Options;
        await using var context = new ApplicationDbContext(options);
        var service = new Mock<IWorkTaskService>();
        var projectId = Guid.NewGuid();
        var moduleId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        service.Setup(item => item.GetModuleDetailAsync(projectId, moduleId, userId, 2, 10))
            .ReturnsAsync(new ModuleDetailDto { Id = moduleId, ProjectId = projectId });

        var controller = new ModulesController(context, service.Object)
        {
            ControllerContext = ControllerContextFor(userId)
        };

        (await controller.GetDetail(projectId, moduleId, 2, 10))
            .Should().BeOfType<OkObjectResult>();
        service.Verify(
            item => item.GetModuleDetailAsync(projectId, moduleId, userId, 2, 10),
            Times.Once);

        service.Setup(item => item.GetModuleDetailAsync(projectId, moduleId, userId, 1, 20))
            .ReturnsAsync((ModuleDetailDto?)null);
        (await controller.GetDetail(projectId, moduleId))
            .Should().BeOfType<NotFoundObjectResult>();

        controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext()
        };
        (await controller.GetDetail(projectId, moduleId))
            .Should().BeOfType<UnauthorizedObjectResult>();
    }

    private static WorkTaskService CreateService(ApplicationDbContext context) =>
        new(context, Mock.Of<IGamificationService>());

    private static ControllerContext ControllerContextFor(Guid userId) =>
        new()
        {
            HttpContext = new DefaultHttpContext
            {
                User = new ClaimsPrincipal(new ClaimsIdentity(
                    new[] { new Claim(ClaimTypes.NameIdentifier, userId.ToString()) },
                    "Test"))
            }
        };

    private sealed class ModuleDetailFixture : IAsyncDisposable
    {
        private readonly DbContextOptions<ApplicationDbContext> _options;

        private ModuleDetailFixture(DbContextOptions<ApplicationDbContext> options)
        {
            _options = options;
        }

        public Guid ManagerId { get; } = Guid.NewGuid();
        public Guid MemberId { get; } = Guid.NewGuid();
        public Guid OutsiderId { get; } = Guid.NewGuid();
        public Guid InactiveMemberId { get; } = Guid.NewGuid();
        public Guid WorkspaceId { get; } = Guid.NewGuid();
        public Guid OtherWorkspaceId { get; } = Guid.NewGuid();
        public Guid ProjectId { get; } = Guid.NewGuid();
        public Guid OtherProjectId { get; } = Guid.NewGuid();
        public Guid ModuleAId { get; } = Guid.NewGuid();
        public Guid ModuleBId { get; } = Guid.NewGuid();
        public Guid DisabledModuleId { get; } = Guid.NewGuid();
        public Guid SprintId { get; } = Guid.NewGuid();
        public Guid MultiModuleTaskId { get; } = Guid.NewGuid();
        public Guid InProgressTaskId { get; } = Guid.NewGuid();

        public static async Task<ModuleDetailFixture> CreateAsync()
        {
            var options = new DbContextOptionsBuilder<ApplicationDbContext>()
                .UseInMemoryDatabase(Guid.NewGuid().ToString("N"))
                .Options;
            var fixture = new ModuleDetailFixture(options);
            await fixture.SeedAsync();
            return fixture;
        }

        public ApplicationDbContext CreateContext() => new(_options);

        private async Task SeedAsync()
        {
            await using var context = CreateContext();
            var now = DateTime.UtcNow;
            var doneStatusId = Guid.NewGuid();
            var progressStatusId = Guid.NewGuid();
            var typeId = Guid.NewGuid();
            var otherModuleTaskId = Guid.NewGuid();
            var unassignedTaskId = Guid.NewGuid();
            var deletedTaskId = Guid.NewGuid();
            var archivedTaskId = Guid.NewGuid();
            var otherProjectTaskId = Guid.NewGuid();

            context.Users.AddRange(
                User(ManagerId, "Module Manager", true),
                User(MemberId, "Module Member", true),
                User(OutsiderId, "Module Outsider", true),
                User(InactiveMemberId, "Inactive Member", false));
            context.Workspaces.AddRange(
                new Workspace
                {
                    Id = WorkspaceId,
                    OwnerId = ManagerId,
                    Name = "Module Workspace",
                    Slug = $"module-{WorkspaceId:N}",
                    CreatedAt = now,
                    UpdatedAt = now
                },
                new Workspace
                {
                    Id = OtherWorkspaceId,
                    OwnerId = ManagerId,
                    Name = "Other Workspace",
                    Slug = $"other-{OtherWorkspaceId:N}",
                    CreatedAt = now,
                    UpdatedAt = now
                });
            context.WorkspaceMembers.AddRange(
                WorkspaceMember(ManagerId, true),
                WorkspaceMember(MemberId, true),
                WorkspaceMember(InactiveMemberId, false));
            context.Projects.AddRange(
                Project(ProjectId, WorkspaceId, "Module Project", "MOD"),
                Project(OtherProjectId, OtherWorkspaceId, "Other Project", "OTH"));
            context.ProjectMembers.AddRange(
                ProjectMember(ManagerId, ProjectId, "PM", true),
                ProjectMember(MemberId, ProjectId, "DEV", true),
                ProjectMember(InactiveMemberId, ProjectId, "DEV", true));
            context.Modules.AddRange(
                Module(ModuleAId, ProjectId, "Module A", "InProgress", now),
                Module(ModuleBId, ProjectId, "Module B", "Backlog", now),
                Module(DisabledModuleId, ProjectId, "Disabled", "Disabled", now));
            context.Sprints.Add(new Sprint
            {
                Id = SprintId,
                ProjectId = ProjectId,
                Name = "Module Sprint",
                StartDate = now.AddDays(-5),
                EndDate = now.AddDays(5),
                State = "Active",
                Status = true,
                CreatedAt = now.AddDays(-6)
            });
            context.TaskStatuses.AddRange(
                new TaskStatus { Id = doneStatusId, ProjectId = ProjectId, Name = "Done" },
                new TaskStatus { Id = progressStatusId, ProjectId = ProjectId, Name = "In Progress" });
            context.TaskTypes.Add(new TaskType
            {
                Id = typeId,
                ProjectId = ProjectId,
                Name = "Task"
            });

            context.WorkTasks.AddRange(
                Task(MultiModuleTaskId, ProjectId, doneStatusId, typeId, "MOD-1",
                    now.AddMinutes(-2), sprintId: SprintId, assignedUserId: ManagerId),
                Task(InProgressTaskId, ProjectId, progressStatusId, typeId, "MOD-2",
                    now.AddMinutes(-1), dueDate: now.AddDays(-1), assignedUserId: ManagerId),
                Task(otherModuleTaskId, ProjectId, progressStatusId, typeId, "MOD-3", now),
                Task(unassignedTaskId, ProjectId, progressStatusId, typeId, "MOD-4", now),
                Task(deletedTaskId, ProjectId, progressStatusId, typeId, "MOD-5", now, isDeleted: true),
                Task(archivedTaskId, ProjectId, progressStatusId, typeId, "MOD-6", now, isArchived: true),
                Task(
                    otherProjectTaskId,
                    OtherProjectId,
                    progressStatusId,
                    typeId,
                    "OTH-1",
                    now,
                    workspaceId: OtherWorkspaceId));
            context.TaskAssignments.Add(new TaskAssignment
            {
                WorkTaskId = MultiModuleTaskId,
                UserId = ManagerId,
                Status = true
            });
            context.IssueModules.AddRange(
                new IssueModule
                {
                    WorkTaskId = MultiModuleTaskId,
                    ModuleId = ModuleBId,
                    AssignedAt = now.AddDays(-2)
                },
                new IssueModule
                {
                    WorkTaskId = MultiModuleTaskId,
                    ModuleId = ModuleAId,
                    AssignedAt = now.AddDays(-1)
                },
                Link(InProgressTaskId, ModuleAId, now),
                Link(otherModuleTaskId, ModuleBId, now),
                Link(deletedTaskId, ModuleAId, now),
                Link(archivedTaskId, ModuleAId, now),
                Link(otherProjectTaskId, ModuleAId, now));
            context.SystemSettings.Add(new SystemSetting
            {
                Id = Guid.NewGuid(),
                SettingGroup = "TaskVisibility",
                Key = InProgressTaskId.ToString(),
                Value = ProjectExecutionRuleHelper.BuildTaskVisibilityPayload(
                    new TaskVisibilityDto { Mode = "assigned" }),
                LastModifiedAt = now
            });

            await context.SaveChangesAsync();

            User User(Guid id, string name, bool isActive) => new()
            {
                Id = id,
                Email = $"{id:N}@example.test",
                FullName = name,
                PasswordHash = "unused",
                IsActive = isActive,
                CreatedAt = now,
                UpdatedAt = now
            };

            WorkspaceMember WorkspaceMember(Guid userId, bool active) => new()
            {
                WorkspaceId = WorkspaceId,
                UserId = userId,
                WorkspaceRole = "MEMBER",
                IsActive = active,
                JoinedAt = now
            };

            Project Project(
                Guid id,
                Guid workspaceId,
                string name,
                string identifier) => new()
            {
                Id = id,
                WorkspaceId = workspaceId,
                CreatorId = ManagerId,
                Name = name,
                Identifier = identifier,
                Status = true,
                CreatedAt = now,
                UpdatedAt = now
            };

            ProjectMember ProjectMember(
                Guid userId,
                Guid projectId,
                string role,
                bool active) => new()
            {
                ProjectId = projectId,
                UserId = userId,
                ProjectRole = role,
                Status = active,
                JoinedAt = now
            };

            TaskManagement.Domain.Entities.Module Module(
                Guid id,
                Guid projectId,
                string name,
                string status,
                DateTime timestamp) => new()
            {
                Id = id,
                ProjectId = projectId,
                Name = name,
                Status = status,
                CreatedAt = timestamp,
                UpdatedAt = timestamp
            };

            WorkTask Task(
                Guid id,
                Guid projectId,
                Guid statusId,
                Guid taskTypeId,
                string sequenceId,
                DateTime updatedAt,
                Guid? sprintId = null,
                DateTime? dueDate = null,
                Guid? assignedUserId = null,
                bool isDeleted = false,
                bool isArchived = false,
                Guid? workspaceId = null) => new()
            {
                Id = id,
                ProjectId = projectId,
                WorkspaceId = workspaceId ?? WorkspaceId,
                SprintId = sprintId,
                TaskStatusId = statusId,
                TaskTypeId = taskTypeId,
                ReporterId = ManagerId,
                AssignedUserId = assignedUserId,
                Title = $"Task {sequenceId}",
                SequenceId = sequenceId,
                DueDate = dueDate,
                IsDeleted = isDeleted,
                IsArchived = isArchived,
                CreatedAt = updatedAt.AddHours(-1),
                UpdatedAt = updatedAt
            };

            static IssueModule Link(Guid taskId, Guid moduleId, DateTime assignedAt) => new()
            {
                WorkTaskId = taskId,
                ModuleId = moduleId,
                AssignedAt = assignedAt
            };
        }

        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }
}
