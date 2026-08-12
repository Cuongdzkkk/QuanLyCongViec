using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Moq;
using TaskManagement.Application.Interfaces;
using TaskManagement.Domain.Entities;
using TaskManagement.Infrastructure.Data;
using TaskManagement.Infrastructure.Services;
using TaskStatus = TaskManagement.Domain.Entities.TaskStatus;

namespace TaskManagement.Tests.Logic;

public sealed class ModuleDetailSqlServerTests
{
    [Fact]
    [Trait("Category", "SqlServerIntegration")]
    public async Task SqlServerPersistsManyToManyRelationAndReturnsScopedTasks()
    {
        var connectionString = SqlServerTestConfiguration.ConnectionString("SprintAModule01Integration");
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseSqlServer(connectionString)
            .Options;
        var seed = ModuleSqlSeed.Create();

        try
        {
            await using (var setup = new ApplicationDbContext(options))
            {
                await setup.Database.EnsureCreatedAsync();
                await seed.InsertAsync(setup);
            }

            await using (var queryContext = new ApplicationDbContext(options))
            {
                var service = new WorkTaskService(
                    queryContext,
                    Mock.Of<IGamificationService>());
                var detail = await service.GetModuleDetailAsync(
                    seed.ProjectId,
                    seed.ModuleAId,
                    seed.UserId,
                    page: 1,
                    pageSize: 20);

                detail.Should().NotBeNull();
                detail!.Tasks.Items.Select(task => task.Id)
                    .Should().Equal(seed.NewerTaskId, seed.MultiModuleTaskId);
                detail.TaskCount.Should().Be(2);
                detail.CompletedCount.Should().Be(1);
                detail.InProgressCount.Should().Be(1);
                detail.Tasks.Items.Single(task => task.Id == seed.MultiModuleTaskId)
                    .SprintId.Should().Be(seed.SprintId);
            }

            await using (var duplicateContext = new ApplicationDbContext(options))
            {
                duplicateContext.IssueModules.Add(new IssueModule
                {
                    WorkTaskId = seed.MultiModuleTaskId,
                    ModuleId = seed.ModuleAId,
                    AssignedAt = DateTime.UtcNow
                });
                var duplicateSave = () => duplicateContext.SaveChangesAsync();
                await duplicateSave.Should().ThrowAsync<DbUpdateException>();
            }

            await using var verification = new ApplicationDbContext(options);
            (await verification.IssueModules.CountAsync(link =>
                link.WorkTaskId == seed.MultiModuleTaskId)).Should().Be(2);
            (await verification.TaskAssignments.SingleAsync(assignment =>
                assignment.WorkTaskId == seed.MultiModuleTaskId &&
                assignment.UserId == seed.UserId)).Status.Should().BeTrue();
            (await verification.WorkTasks.SingleAsync(task =>
                task.Id == seed.MultiModuleTaskId)).ProjectId.Should().Be(seed.ProjectId);
        }
        finally
        {
            await CleanupAsync(options, seed);
        }
    }

    private static async Task CleanupAsync(
        DbContextOptions<ApplicationDbContext> options,
        ModuleSqlSeed seed)
    {
        try
        {
            await using var context = new ApplicationDbContext(options);
            await context.IssueModules
                .Where(link =>
                    link.ModuleId == seed.ModuleAId ||
                    link.ModuleId == seed.ModuleBId)
                .ExecuteDeleteAsync();
            await context.TaskAssignments
                .Where(assignment =>
                    assignment.WorkTaskId == seed.MultiModuleTaskId ||
                    assignment.WorkTaskId == seed.NewerTaskId)
                .ExecuteDeleteAsync();
            await context.WorkTasks
                .IgnoreQueryFilters()
                .Where(task =>
                    task.Id == seed.MultiModuleTaskId ||
                    task.Id == seed.NewerTaskId)
                .ExecuteDeleteAsync();
            await context.Sprints
                .IgnoreQueryFilters()
                .Where(sprint => sprint.Id == seed.SprintId)
                .ExecuteDeleteAsync();
            await context.Modules
                .Where(module =>
                    module.Id == seed.ModuleAId ||
                    module.Id == seed.ModuleBId)
                .ExecuteDeleteAsync();
            await context.TaskStatuses
                .Where(status =>
                    status.Id == seed.DoneStatusId ||
                    status.Id == seed.ProgressStatusId)
                .ExecuteDeleteAsync();
            await context.TaskTypes
                .Where(type => type.Id == seed.TaskTypeId)
                .ExecuteDeleteAsync();
            await context.ProjectMembers
                .Where(member =>
                    member.ProjectId == seed.ProjectId &&
                    member.UserId == seed.UserId)
                .ExecuteDeleteAsync();
            await context.WorkspaceMembers
                .Where(member =>
                    member.WorkspaceId == seed.WorkspaceId &&
                    member.UserId == seed.UserId)
                .ExecuteDeleteAsync();
            await context.Projects
                .IgnoreQueryFilters()
                .Where(project => project.Id == seed.ProjectId)
                .ExecuteDeleteAsync();
            await context.Workspaces
                .IgnoreQueryFilters()
                .Where(workspace => workspace.Id == seed.WorkspaceId)
                .ExecuteDeleteAsync();
            await context.Users
                .IgnoreQueryFilters()
                .Where(user => user.Id == seed.UserId)
                .ExecuteDeleteAsync();
        }
        catch
        {
            // Cleanup is best-effort and never drops the dedicated integration database.
        }
    }

    private sealed record ModuleSqlSeed(
        Guid UserId,
        Guid WorkspaceId,
        Guid ProjectId,
        Guid ModuleAId,
        Guid ModuleBId,
        Guid SprintId,
        Guid DoneStatusId,
        Guid ProgressStatusId,
        Guid TaskTypeId,
        Guid MultiModuleTaskId,
        Guid NewerTaskId)
    {
        public static ModuleSqlSeed Create() => new(
            Guid.NewGuid(),
            Guid.NewGuid(),
            Guid.NewGuid(),
            Guid.NewGuid(),
            Guid.NewGuid(),
            Guid.NewGuid(),
            Guid.NewGuid(),
            Guid.NewGuid(),
            Guid.NewGuid(),
            Guid.NewGuid(),
            Guid.NewGuid());

        public async Task InsertAsync(ApplicationDbContext context)
        {
            var now = DateTime.UtcNow;
            context.Users.Add(new User
            {
                Id = UserId,
                Email = $"module-sql-{UserId:N}@example.test",
                FullName = "Module SQL Manager",
                PasswordHash = "unused",
                IsActive = true,
                CreatedAt = now,
                UpdatedAt = now
            });
            context.Workspaces.Add(new Workspace
            {
                Id = WorkspaceId,
                OwnerId = UserId,
                Name = "Module SQL Workspace",
                Slug = $"module-sql-{WorkspaceId:N}",
                CreatedAt = now,
                UpdatedAt = now
            });
            context.WorkspaceMembers.Add(new WorkspaceMember
            {
                WorkspaceId = WorkspaceId,
                UserId = UserId,
                WorkspaceRole = "OWNER",
                JoinedAt = now,
                IsActive = true
            });
            context.Projects.Add(new Project
            {
                Id = ProjectId,
                WorkspaceId = WorkspaceId,
                CreatorId = UserId,
                Name = "Module SQL Project",
                Identifier = "MSQ",
                Status = true,
                CreatedAt = now,
                UpdatedAt = now
            });
            context.ProjectMembers.Add(new ProjectMember
            {
                ProjectId = ProjectId,
                UserId = UserId,
                ProjectRole = "PM",
                JoinedAt = now,
                Status = true
            });
            context.Modules.AddRange(
                new TaskManagement.Domain.Entities.Module
                {
                    Id = ModuleAId,
                    ProjectId = ProjectId,
                    Name = "SQL Module A",
                    Status = "InProgress",
                    CreatedAt = now,
                    UpdatedAt = now
                },
                new TaskManagement.Domain.Entities.Module
                {
                    Id = ModuleBId,
                    ProjectId = ProjectId,
                    Name = "SQL Module B",
                    Status = "Backlog",
                    CreatedAt = now,
                    UpdatedAt = now
                });
            context.Sprints.Add(new Sprint
            {
                Id = SprintId,
                ProjectId = ProjectId,
                Name = "SQL Module Sprint",
                StartDate = now.AddDays(-2),
                EndDate = now.AddDays(2),
                State = "Active",
                Status = true,
                CreatedAt = now.AddDays(-3)
            });
            context.TaskStatuses.AddRange(
                new TaskStatus
                {
                    Id = DoneStatusId,
                    ProjectId = ProjectId,
                    Name = "Done"
                },
                new TaskStatus
                {
                    Id = ProgressStatusId,
                    ProjectId = ProjectId,
                    Name = "In Progress"
                });
            context.TaskTypes.Add(new TaskType
            {
                Id = TaskTypeId,
                ProjectId = ProjectId,
                Name = "Task"
            });
            context.WorkTasks.AddRange(
                Task(
                    MultiModuleTaskId,
                    DoneStatusId,
                    "MSQ-1",
                    now.AddMinutes(-2),
                    SprintId),
                Task(
                    NewerTaskId,
                    ProgressStatusId,
                    "MSQ-2",
                    now.AddMinutes(-1),
                    null));
            context.TaskAssignments.Add(new TaskAssignment
            {
                WorkTaskId = MultiModuleTaskId,
                UserId = UserId,
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
                new IssueModule
                {
                    WorkTaskId = NewerTaskId,
                    ModuleId = ModuleAId,
                    AssignedAt = now
                });
            await context.SaveChangesAsync();
        }

        private WorkTask Task(
            Guid id,
            Guid statusId,
            string sequenceId,
            DateTime updatedAt,
            Guid? sprintId) => new()
        {
            Id = id,
            ProjectId = ProjectId,
            WorkspaceId = WorkspaceId,
            SprintId = sprintId,
            TaskStatusId = statusId,
            TaskTypeId = TaskTypeId,
            ReporterId = UserId,
            AssignedUserId = UserId,
            Title = $"SQL task {sequenceId}",
            SequenceId = sequenceId,
            CreatedAt = updatedAt.AddHours(-1),
            UpdatedAt = updatedAt
        };
    }
}
