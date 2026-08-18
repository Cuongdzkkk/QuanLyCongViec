using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Moq;
using TaskManagement.Application.Interfaces;
using TaskManagement.Application.DTOs.WorkTask;
using TaskManagement.Domain.Entities;
using TaskManagement.Domain.Rules;
using TaskManagement.Infrastructure.Data;
using TaskManagement.Infrastructure.Services;

namespace TaskManagement.Tests.Logic;

public sealed class SprintTaskAssignmentTests
{
    [Fact]
    public async Task UpcomingSprint_AllowsTaskToBeAddedAndRemoved()
    {
        await using var fixture = await AssignmentFixture.CreateAsync();
        await using var context = fixture.CreateContext();
        var service = new WorkTaskService(context, Mock.Of<IGamificationService>());

        var task = await context.WorkTasks.SingleAsync();
        var added = await service.UpdateAsync(task.Id, fixture.UserId, fixture.Update(task.RowVersion, fixture.UpcomingSprintId));

        added.SprintId.Should().Be(fixture.UpcomingSprintId);

        var refreshed = await context.WorkTasks.AsNoTracking().SingleAsync(item => item.Id == task.Id);
        var removed = await service.UpdateAsync(task.Id, fixture.UserId, fixture.Update(refreshed.RowVersion, null));

        removed.SprintId.Should().BeNull();
    }

    [Fact]
    public void SprintStatePolicy_LocksCompletedAndExpiredButAllowsUpcoming()
    {
        var now = DateTime.UtcNow;
        SprintStatePolicy.IsTaskMutationLocked(new Sprint
        {
            State = SprintStates.Planned,
            Status = false,
            EndDate = now.AddDays(2)
        }, now).Should().BeFalse();
        SprintStatePolicy.IsTaskMutationLocked(new Sprint
        {
            State = SprintStates.Completed,
            Status = false,
            EndDate = now.AddDays(2)
        }, now).Should().BeTrue();
        SprintStatePolicy.IsTaskMutationLocked(new Sprint
        {
            State = SprintStates.Active,
            Status = true,
            EndDate = now.AddDays(-1)
        }, now).Should().BeTrue();
    }

    private sealed class AssignmentFixture : IAsyncDisposable
    {
        private readonly DbContextOptions<ApplicationDbContext> _options;

        private AssignmentFixture(DbContextOptions<ApplicationDbContext> options)
        {
            _options = options;
        }

        public Guid UserId { get; } = Guid.NewGuid();
        public Guid WorkspaceId { get; } = Guid.NewGuid();
        public Guid ProjectId { get; } = Guid.NewGuid();
        public Guid SprintId { get; } = Guid.NewGuid();
        public Guid UpcomingSprintId { get; } = Guid.NewGuid();
        public Guid StatusId { get; } = Guid.NewGuid();
        public Guid TaskTypeId { get; } = Guid.NewGuid();
        public Guid TaskId { get; } = Guid.NewGuid();

        public static async Task<AssignmentFixture> CreateAsync()
        {
            var options = new DbContextOptionsBuilder<ApplicationDbContext>()
                .UseInMemoryDatabase(Guid.NewGuid().ToString("N"))
                .Options;
            var fixture = new AssignmentFixture(options);
            await using var context = fixture.CreateContext();
            var now = DateTime.UtcNow;
            context.Users.Add(new User
            {
                Id = fixture.UserId,
                Email = $"sprint-assignment-{fixture.UserId:N}@example.test",
                FullName = "Sprint Assignment User",
                PasswordHash = "unused",
                IsActive = true
            });
            context.Workspaces.Add(new Workspace
            {
                Id = fixture.WorkspaceId,
                OwnerId = fixture.UserId,
                Name = "Sprint Assignment Workspace",
                Slug = $"sprint-assignment-{fixture.WorkspaceId:N}"
            });
            context.Projects.Add(new Project
            {
                Id = fixture.ProjectId,
                WorkspaceId = fixture.WorkspaceId,
                CreatorId = fixture.UserId,
                Name = "Sprint Assignment Project",
                Identifier = "SAP",
                Status = true,
                CreatedAt = now,
                UpdatedAt = now
            });
            context.ProjectMembers.Add(new ProjectMember
            {
                ProjectId = fixture.ProjectId,
                UserId = fixture.UserId,
                ProjectRole = "PM",
                Status = true
            });
            context.TaskStatuses.Add(new TaskManagement.Domain.Entities.TaskStatus
            {
                Id = fixture.StatusId,
                ProjectId = fixture.ProjectId,
                Name = "To Do"
            });
            context.TaskTypes.Add(new TaskType
            {
                Id = fixture.TaskTypeId,
                ProjectId = fixture.ProjectId,
                Name = "Task"
            });
            context.Sprints.Add(new Sprint
            {
                Id = fixture.UpcomingSprintId,
                ProjectId = fixture.ProjectId,
                Name = "Upcoming Sprint",
                State = SprintStates.Planned,
                Status = false,
                StartDate = now.AddDays(1),
                EndDate = now.AddDays(8),
                CreatedAt = now
            });
            context.WorkTasks.Add(new WorkTask
            {
                Id = fixture.TaskId,
                ProjectId = fixture.ProjectId,
                WorkspaceId = fixture.WorkspaceId,
                TaskTypeId = fixture.TaskTypeId,
                TaskStatusId = fixture.StatusId,
                ReporterId = fixture.UserId,
                Title = "Assignable task",
                SequenceId = "SAP-1",
                CreatedAt = now,
                UpdatedAt = now
            });
            await context.SaveChangesAsync();
            return fixture;
        }

        public ApplicationDbContext CreateContext() => new(_options);

        public UpdateWorkTaskDto Update(byte[] rowVersion, Guid? sprintId) => new()
        {
            Title = "Assignable task",
            Description = null,
            SprintId = sprintId,
            TaskTypeId = TaskTypeId,
            RowVersion = rowVersion
        };

        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }
}
