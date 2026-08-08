using System.Security.Claims;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Moq;
using TaskManagement.API.Controllers;
using TaskManagement.Application.Common;
using TaskManagement.Application.DTOs.Sprint;
using TaskManagement.Application.Interfaces;
using TaskManagement.Domain.Entities;
using TaskManagement.Domain.Rules;
using TaskManagement.Infrastructure.Data;
using TaskManagement.Infrastructure.Services;
using TaskStatus = TaskManagement.Domain.Entities.TaskStatus;

namespace TaskManagement.Tests.Logic;

public sealed class CycleTransitionTests
{
    [Fact]
    public async Task CloseThenStart_PersistsStatesAndRetainsTaskAssignment()
    {
        await using var fixture = await CycleFixture.CreateAsync();
        await using var context = fixture.CreateContext();
        var service = new SprintService(context);

        var closed = await service.CloseAsync(
            fixture.ProjectAId,
            fixture.ActiveCycleId,
            new CloseSprintDto(),
            fixture.ManagerId);
        var closeRetry = await service.CloseAsync(
            fixture.ProjectAId,
            fixture.ActiveCycleId,
            new CloseSprintDto(),
            fixture.ManagerId);
        var started = await service.StartAsync(fixture.ProjectAId, fixture.NextCycleId);
        var startRetry = await service.StartAsync(fixture.ProjectAId, fixture.NextCycleId);

        closed.State.Should().Be(SprintStates.Completed);
        closed.CompletedAt.Should().NotBeNull();
        closeRetry.Id.Should().Be(closed.Id);
        started.State.Should().Be(SprintStates.Active);
        started.StartedAt.Should().NotBeNull();
        startRetry.Id.Should().Be(started.Id);
        (await context.Sprints.CountAsync(item =>
            item.ProjectId == fixture.ProjectAId &&
            item.State == SprintStates.Active)).Should().Be(1);

        var task = await context.WorkTasks
            .IgnoreQueryFilters()
            .SingleAsync(item => item.Id == fixture.TaskId);
        task.SprintId.Should().BeNull("the established close-to-backlog rule must be preserved");
        task.TaskStatusId.Should().Be(fixture.InProgressStatusId);
        (await context.TaskAssignments.SingleAsync(item =>
            item.WorkTaskId == fixture.TaskId &&
            item.UserId == fixture.ManagerId)).Status.Should().BeTrue();
        (await context.AuditLogs.CountAsync(item =>
            item.WorkTaskId == fixture.TaskId &&
            item.FieldChanged == "SPRINT_CARRY_OVER")).Should().Be(1);

        await using var reloaded = fixture.CreateContext();
        (await reloaded.Sprints.SingleAsync(item => item.Id == fixture.ActiveCycleId))
            .State.Should().Be(SprintStates.Completed);
        (await reloaded.Sprints.SingleAsync(item => item.Id == fixture.NextCycleId))
            .State.Should().Be(SprintStates.Active);
    }

    [Fact]
    public async Task InvalidAndCrossProjectTransitions_AreRejectedWithoutMutation()
    {
        await using var fixture = await CycleFixture.CreateAsync();
        await using var context = fixture.CreateContext();
        var service = new SprintService(context);

        var closePlanned = () => service.CloseAsync(
            fixture.ProjectAId,
            fixture.NextCycleId,
            new CloseSprintDto(),
            fixture.ManagerId);
        var startCompleted = () => service.StartAsync(
            fixture.ProjectAId,
            fixture.CompletedCycleId);
        var closeOtherProject = () => service.CloseAsync(
            fixture.ProjectAId,
            fixture.OtherProjectActiveCycleId,
            new CloseSprintDto(),
            fixture.ManagerId);
        var startWhileActive = () => service.StartAsync(
            fixture.ProjectAId,
            fixture.NextCycleId);

        (await closePlanned.Should().ThrowAsync<SprintTransitionException>())
            .Which.Code.Should().Be("CYCLE_NOT_ACTIVE");
        (await startCompleted.Should().ThrowAsync<SprintTransitionException>())
            .Which.Code.Should().Be("CYCLE_ALREADY_COMPLETED");
        await closeOtherProject.Should().ThrowAsync<KeyNotFoundException>();
        (await startWhileActive.Should().ThrowAsync<SprintTransitionException>())
            .Which.Code.Should().Be("ACTIVE_CYCLE_EXISTS");

        (await context.Sprints.SingleAsync(item => item.Id == fixture.NextCycleId))
            .State.Should().Be(SprintStates.Planned);
    }

    [Fact]
    public async Task OtherProjectActiveCycle_DoesNotBlockAndDeletedCyclesAreExcluded()
    {
        await using var fixture = await CycleFixture.CreateAsync();
        await using var context = fixture.CreateContext();
        var service = new SprintService(context);
        await service.CloseAsync(
            fixture.ProjectAId,
            fixture.ActiveCycleId,
            new CloseSprintDto(),
            fixture.ManagerId);

        var started = await service.StartAsync(fixture.ProjectAId, fixture.NextCycleId);
        var listed = await service.GetByProjectAsync(fixture.ProjectAId);

        started.State.Should().Be(SprintStates.Active);
        listed.Should().NotContain(item => item.Id == fixture.DeletedCycleId);
        listed.Select(item => item.Id).Should().Equal(
            listed.OrderBy(item => item.StartDate)
                .ThenBy(item => item.CreatedAt)
                .ThenBy(item => item.Id)
                .Select(item => item.Id));
    }

    [Fact]
    public async Task ControllerUsesJwtActorAndMapsTransitionErrors()
    {
        var projectId = Guid.NewGuid();
        var cycleId = Guid.NewGuid();
        var actorId = Guid.NewGuid();
        var service = new Mock<ISprintService>();
        service.Setup(item => item.CloseAsync(
                projectId,
                cycleId,
                It.IsAny<CloseSprintDto>(),
                actorId))
            .ReturnsAsync(new SprintResponseDto
            {
                Id = cycleId,
                ProjectId = projectId,
                State = SprintStates.Completed
            });
        service.Setup(item => item.StartAsync(projectId, cycleId))
            .ThrowsAsync(new SprintTransitionException(
                "CYCLE_ALREADY_COMPLETED",
                "Completed cycle cannot be started again."));
        var controller = new SprintsController(service.Object)
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext
                {
                    User = new ClaimsPrincipal(new ClaimsIdentity(
                        new[] { new Claim(ClaimTypes.NameIdentifier, actorId.ToString()) },
                        "TestAuth"))
                }
            }
        };

        var close = await controller.Close(projectId, cycleId, new CloseSprintDto());
        var start = await controller.Start(projectId, cycleId);

        close.Should().BeOfType<OkObjectResult>();
        start.Should().BeOfType<ConflictObjectResult>();
        service.Verify(item => item.CloseAsync(
            projectId,
            cycleId,
            It.IsAny<CloseSprintDto>(),
            actorId), Times.Once);
    }

    [Fact]
    public void ModelHasProjectScopedUniqueActiveCycleIndex()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString("N"))
            .Options;
        using var context = new ApplicationDbContext(options);

        var index = context.Model.FindEntityType(typeof(Sprint))!
            .GetIndexes()
            .Single(item => item.GetDatabaseName() == "UX_Sprints_Project_Active");

        index.IsUnique.Should().BeTrue();
        index.Properties.Select(item => item.Name).Should().Equal("ProjectId");
        index.GetFilter().Should().Contain("State");
        index.GetFilter().Should().Contain("IsDeleted");
    }

    private sealed class CycleFixture : IAsyncDisposable
    {
        private readonly DbContextOptions<ApplicationDbContext> _options;

        private CycleFixture(DbContextOptions<ApplicationDbContext> options)
        {
            _options = options;
        }

        public Guid ManagerId { get; } = Guid.NewGuid();
        public Guid WorkspaceId { get; } = Guid.NewGuid();
        public Guid ProjectAId { get; } = Guid.NewGuid();
        public Guid ProjectBId { get; } = Guid.NewGuid();
        public Guid ActiveCycleId { get; } = Guid.NewGuid();
        public Guid NextCycleId { get; } = Guid.NewGuid();
        public Guid CompletedCycleId { get; } = Guid.NewGuid();
        public Guid DeletedCycleId { get; } = Guid.NewGuid();
        public Guid OtherProjectActiveCycleId { get; } = Guid.NewGuid();
        public Guid InProgressStatusId { get; } = Guid.NewGuid();
        public Guid TaskTypeId { get; } = Guid.NewGuid();
        public Guid TaskId { get; } = Guid.NewGuid();

        public static async Task<CycleFixture> CreateAsync()
        {
            var options = new DbContextOptionsBuilder<ApplicationDbContext>()
                .UseInMemoryDatabase(Guid.NewGuid().ToString("N"))
                .ConfigureWarnings(warnings => warnings.Ignore(InMemoryEventId.TransactionIgnoredWarning))
                .Options;
            var fixture = new CycleFixture(options);
            await fixture.SeedAsync();
            return fixture;
        }

        public ApplicationDbContext CreateContext() => new(_options);

        private async Task SeedAsync()
        {
            await using var context = CreateContext();
            var now = DateTime.UtcNow;
            context.Users.Add(new User
            {
                Id = ManagerId,
                Email = $"cycle-manager-{ManagerId:N}@example.test",
                FullName = "Cycle Manager",
                PasswordHash = "unused",
                IsActive = true
            });
            context.Workspaces.Add(new Workspace
            {
                Id = WorkspaceId,
                OwnerId = ManagerId,
                Name = "Cycle Workspace",
                Slug = $"cycle-{WorkspaceId:N}"
            });
            context.Projects.AddRange(
                Project(ProjectAId, "Project A", "PRA"),
                Project(ProjectBId, "Project B", "PRB"));
            context.Sprints.AddRange(
                Cycle(ActiveCycleId, ProjectAId, "Active", SprintStates.Active, now.AddDays(-5), now.AddDays(5)),
                Cycle(NextCycleId, ProjectAId, "Next", SprintStates.Planned, now.AddDays(6), now.AddDays(12)),
                Cycle(CompletedCycleId, ProjectAId, "Completed", SprintStates.Completed, now.AddDays(-20), now.AddDays(-10)),
                Cycle(DeletedCycleId, ProjectAId, "Deleted", SprintStates.Planned, now.AddDays(13), now.AddDays(19), true),
                Cycle(OtherProjectActiveCycleId, ProjectBId, "Other active", SprintStates.Active, now.AddDays(-3), now.AddDays(3)));
            context.TaskStatuses.Add(new TaskStatus
            {
                Id = InProgressStatusId,
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
                SprintId = ActiveCycleId,
                TaskStatusId = InProgressStatusId,
                TaskTypeId = TaskTypeId,
                ReporterId = ManagerId,
                AssignedUserId = ManagerId,
                Title = "Retained task",
                SequenceId = "PRA-1",
                CreatedAt = now,
                UpdatedAt = now
            });
            context.TaskAssignments.Add(new TaskAssignment
            {
                WorkTaskId = TaskId,
                UserId = ManagerId,
                Status = true
            });
            await context.SaveChangesAsync();
        }

        private Project Project(Guid id, string name, string identifier) => new()
        {
            Id = id,
            WorkspaceId = WorkspaceId,
            CreatorId = ManagerId,
            Name = name,
            Identifier = identifier,
            Status = true,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        private static Sprint Cycle(
            Guid id,
            Guid projectId,
            string name,
            string state,
            DateTime start,
            DateTime end,
            bool isDeleted = false) => new()
        {
            Id = id,
            ProjectId = projectId,
            Name = name,
            State = state,
            Status = state == SprintStates.Active,
            StartedAt = state is SprintStates.Active or SprintStates.Completed ? start : null,
            CompletedAt = state == SprintStates.Completed ? end : null,
            StartDate = start,
            EndDate = end,
            IsDeleted = isDeleted,
            CreatedAt = start.AddDays(-1)
        };

        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }
}
