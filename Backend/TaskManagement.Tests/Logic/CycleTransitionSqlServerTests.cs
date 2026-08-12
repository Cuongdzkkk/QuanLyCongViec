using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using TaskManagement.Application.Common;
using TaskManagement.Application.DTOs.Sprint;
using TaskManagement.Domain.Entities;
using TaskManagement.Domain.Rules;
using TaskManagement.Infrastructure.Data;
using TaskManagement.Infrastructure.Services;
using TaskStatus = TaskManagement.Domain.Entities.TaskStatus;

namespace TaskManagement.Tests.Logic;

public sealed class CycleTransitionSqlServerTests
{
    [Fact]
    [Trait("Category", "SqlServerIntegration")]
    public async Task ConcurrentCloseAndStart_KeepOneActiveCycleAndRetainTaskData()
    {
        var connectionString = SqlServerTestConfiguration.ConnectionString("SprintACycle01Integration");
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseSqlServer(connectionString)
            .Options;
        var seed = CycleSqlSeed.Create();

        try
        {
            await using (var setup = new ApplicationDbContext(options))
            {
                await setup.Database.EnsureCreatedAsync();
                await seed.InsertAsync(setup);
            }

            var concurrentClose = await Task.WhenAll(
                CloseAsync(options, seed.ProjectAId, seed.ActiveAId, seed.UserId),
                CloseAsync(options, seed.ProjectAId, seed.ActiveAId, seed.UserId));
            concurrentClose.Should().OnlyContain(item => item.State == SprintStates.Completed);

            var concurrentStarts = await Task.WhenAll(
                CaptureStartAsync(options, seed.ProjectAId, seed.NextA1Id),
                CaptureStartAsync(options, seed.ProjectAId, seed.NextA2Id));
            concurrentStarts.Count(item => item == "started").Should().Be(1);
            concurrentStarts.Count(item => item == "ACTIVE_CYCLE_EXISTS").Should().Be(1);

            var closeStartRace = await Task.WhenAll(
                CaptureCloseAsync(options, seed.ProjectBId, seed.ActiveBId, seed.UserId),
                CaptureStartAsync(options, seed.ProjectBId, seed.NextBId));
            closeStartRace.Should().OnlyContain(item =>
                item == "completed" ||
                item == "started" ||
                item == "ACTIVE_CYCLE_EXISTS");

            await using var verification = new ApplicationDbContext(options);
            (await verification.Sprints.CountAsync(item =>
                item.ProjectId == seed.ProjectAId &&
                item.State == SprintStates.Active)).Should().Be(1);
            (await verification.Sprints.CountAsync(item =>
                item.ProjectId == seed.ProjectBId &&
                item.State == SprintStates.Active)).Should().BeLessThanOrEqualTo(1);
            (await verification.Sprints.SingleAsync(item => item.Id == seed.ActiveAId))
                .State.Should().Be(SprintStates.Completed);

            var task = await verification.WorkTasks
                .IgnoreQueryFilters()
                .SingleAsync(item => item.Id == seed.TaskId);
            task.SprintId.Should().BeNull();
            task.TaskStatusId.Should().Be(seed.StatusId);
            (await verification.TaskAssignments.SingleAsync(item =>
                item.WorkTaskId == seed.TaskId &&
                item.UserId == seed.UserId)).Status.Should().BeTrue();
            (await verification.AuditLogs.CountAsync(item =>
                item.WorkTaskId == seed.TaskId &&
                item.FieldChanged == "SPRINT_CARRY_OVER")).Should().Be(1);
        }
        finally
        {
            await CleanupAsync(options, seed);
        }
    }

    private static async Task<SprintResponseDto> CloseAsync(
        DbContextOptions<ApplicationDbContext> options,
        Guid projectId,
        Guid sprintId,
        Guid userId)
    {
        await using var context = new ApplicationDbContext(options);
        return await new SprintService(context).CloseAsync(
            projectId,
            sprintId,
            new CloseSprintDto(),
            userId);
    }

    private static async Task<string> CaptureCloseAsync(
        DbContextOptions<ApplicationDbContext> options,
        Guid projectId,
        Guid sprintId,
        Guid userId)
    {
        try
        {
            await CloseAsync(options, projectId, sprintId, userId);
            return "completed";
        }
        catch (SprintTransitionException ex)
        {
            return ex.Code;
        }
    }

    private static async Task<string> CaptureStartAsync(
        DbContextOptions<ApplicationDbContext> options,
        Guid projectId,
        Guid sprintId)
    {
        try
        {
            await using var context = new ApplicationDbContext(options);
            await new SprintService(context).StartAsync(projectId, sprintId);
            return "started";
        }
        catch (SprintTransitionException ex)
        {
            return ex.Code;
        }
    }

    private static async Task CleanupAsync(
        DbContextOptions<ApplicationDbContext> options,
        CycleSqlSeed seed)
    {
        try
        {
            await using var context = new ApplicationDbContext(options);
            await context.AuditLogs
                .Where(item => item.WorkTaskId == seed.TaskId)
                .ExecuteDeleteAsync();
            await context.TaskAssignments
                .Where(item => item.WorkTaskId == seed.TaskId)
                .ExecuteDeleteAsync();
            await context.WorkTasks
                .IgnoreQueryFilters()
                .Where(item => item.Id == seed.TaskId)
                .ExecuteDeleteAsync();
            await context.TaskStatuses
                .Where(item => item.Id == seed.StatusId)
                .ExecuteDeleteAsync();
            await context.TaskTypes
                .Where(item => item.Id == seed.TaskTypeId)
                .ExecuteDeleteAsync();
            await context.Sprints
                .IgnoreQueryFilters()
                .Where(item => item.ProjectId == seed.ProjectAId || item.ProjectId == seed.ProjectBId)
                .ExecuteDeleteAsync();
            await context.Projects
                .IgnoreQueryFilters()
                .Where(item => item.Id == seed.ProjectAId || item.Id == seed.ProjectBId)
                .ExecuteDeleteAsync();
            await context.Workspaces
                .IgnoreQueryFilters()
                .Where(item => item.Id == seed.WorkspaceId)
                .ExecuteDeleteAsync();
            await context.Users
                .IgnoreQueryFilters()
                .Where(item => item.Id == seed.UserId)
                .ExecuteDeleteAsync();
        }
        catch
        {
            // Cleanup is best-effort and never drops the dedicated integration database.
        }
    }

    private sealed record CycleSqlSeed(
        Guid UserId,
        Guid WorkspaceId,
        Guid ProjectAId,
        Guid ProjectBId,
        Guid ActiveAId,
        Guid NextA1Id,
        Guid NextA2Id,
        Guid ActiveBId,
        Guid NextBId,
        Guid StatusId,
        Guid TaskTypeId,
        Guid TaskId)
    {
        public static CycleSqlSeed Create() => new(
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
            Guid.NewGuid(),
            Guid.NewGuid());

        public async Task InsertAsync(ApplicationDbContext context)
        {
            var now = DateTime.UtcNow;
            context.Users.Add(new User
            {
                Id = UserId,
                Email = $"cycle-sql-{UserId:N}@example.test",
                FullName = "Cycle SQL User",
                PasswordHash = "unused",
                IsActive = true
            });
            context.Workspaces.Add(new Workspace
            {
                Id = WorkspaceId,
                OwnerId = UserId,
                Name = "Cycle SQL Workspace",
                Slug = $"cycle-sql-{WorkspaceId:N}"
            });
            context.Projects.AddRange(
                Project(ProjectAId, "Cycle SQL A", "CSA"),
                Project(ProjectBId, "Cycle SQL B", "CSB"));
            context.Sprints.AddRange(
                Cycle(ActiveAId, ProjectAId, SprintStates.Active, now.AddDays(-5), now.AddDays(5)),
                Cycle(NextA1Id, ProjectAId, SprintStates.Planned, now.AddDays(6), now.AddDays(12)),
                Cycle(NextA2Id, ProjectAId, SprintStates.Planned, now.AddDays(13), now.AddDays(19)),
                Cycle(ActiveBId, ProjectBId, SprintStates.Active, now.AddDays(-4), now.AddDays(4)),
                Cycle(NextBId, ProjectBId, SprintStates.Planned, now.AddDays(5), now.AddDays(11)));
            context.TaskStatuses.Add(new TaskStatus
            {
                Id = StatusId,
                ProjectId = ProjectAId,
                Name = "In Progress"
            });
            context.TaskTypes.Add(new TaskType
            {
                Id = TaskTypeId,
                ProjectId = ProjectAId,
                Name = "Task"
            });
            context.WorkTasks.Add(new WorkTask
            {
                Id = TaskId,
                ProjectId = ProjectAId,
                WorkspaceId = WorkspaceId,
                SprintId = ActiveAId,
                TaskStatusId = StatusId,
                TaskTypeId = TaskTypeId,
                ReporterId = UserId,
                AssignedUserId = UserId,
                Title = "Cycle SQL retained task",
                SequenceId = "CSA-1",
                CreatedAt = now,
                UpdatedAt = now
            });
            context.TaskAssignments.Add(new TaskAssignment
            {
                WorkTaskId = TaskId,
                UserId = UserId,
                Status = true
            });
            await context.SaveChangesAsync();
        }

        private Project Project(Guid id, string name, string identifier) => new()
        {
            Id = id,
            WorkspaceId = WorkspaceId,
            CreatorId = UserId,
            Name = name,
            Identifier = identifier,
            Status = true,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        private static Sprint Cycle(
            Guid id,
            Guid projectId,
            string state,
            DateTime start,
            DateTime end) => new()
        {
            Id = id,
            ProjectId = projectId,
            Name = state,
            State = state,
            Status = state == SprintStates.Active,
            StartedAt = state == SprintStates.Active ? start : null,
            StartDate = start,
            EndDate = end,
            CreatedAt = start.AddDays(-1)
        };
    }
}
