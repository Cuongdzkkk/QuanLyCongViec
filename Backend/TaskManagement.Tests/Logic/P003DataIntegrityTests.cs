using System.Security.Claims;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Moq;
using TaskManagement.Application.DTOs.Sprint;
using TaskManagement.Application.DTOs.WorkTask;
using TaskManagement.Application.Interfaces;
using TaskManagement.Domain.Entities;
using TaskManagement.Infrastructure.Data;
using TaskManagement.Infrastructure.Services;

namespace TaskManagement.Tests.Logic;

public sealed class P003DataIntegrityTests
{
    [Fact]
    public async Task TC_DEP_003_DependencyGraph_RejectsDirectAndThreeLevelCycles()
    {
        await using var fixture = await DependencyFixture.CreateInMemoryAsync(3);
        await fixture.Service.AddOrUpdateAsync(fixture.ProjectId, fixture.Tasks[0], fixture.Tasks[1], "blocks");

        var direct = () => fixture.Service.AddOrUpdateAsync(
            fixture.ProjectId, fixture.Tasks[1], fixture.Tasks[0], "blocks");
        await direct.Should().ThrowAsync<InvalidOperationException>().WithMessage("*cycle*");

        await fixture.Service.AddOrUpdateAsync(fixture.ProjectId, fixture.Tasks[1], fixture.Tasks[2], "blocks");
        var threeLevel = () => fixture.Service.AddOrUpdateAsync(
            fixture.ProjectId, fixture.Tasks[2], fixture.Tasks[0], "blocks");
        await threeLevel.Should().ThrowAsync<InvalidOperationException>().WithMessage("*cycle*");
        (await fixture.Context.TaskDependencies.CountAsync()).Should().Be(2);
    }

    [Fact]
    public async Task TC_DEP_025_DependencyGraph_AllowsDagAndLongChainButRejectsClosingEdge()
    {
        await using var fixture = await DependencyFixture.CreateInMemoryAsync(25);

        for (var index = 0; index < fixture.Tasks.Count - 1; index++)
        {
            await fixture.Service.AddOrUpdateAsync(
                fixture.ProjectId, fixture.Tasks[index], fixture.Tasks[index + 1], "blocks");
        }

        var closingEdge = () => fixture.Service.AddOrUpdateAsync(
            fixture.ProjectId, fixture.Tasks[^1], fixture.Tasks[0], "blocks");
        await closingEdge.Should().ThrowAsync<InvalidOperationException>().WithMessage("*cycle*");

        await using var dag = await DependencyFixture.CreateInMemoryAsync(4);
        await dag.Service.AddOrUpdateAsync(dag.ProjectId, dag.Tasks[0], dag.Tasks[1], "blocks");
        await dag.Service.AddOrUpdateAsync(dag.ProjectId, dag.Tasks[0], dag.Tasks[2], "blocks");
        await dag.Service.AddOrUpdateAsync(dag.ProjectId, dag.Tasks[1], dag.Tasks[3], "blocks");
        await dag.Service.AddOrUpdateAsync(dag.ProjectId, dag.Tasks[2], dag.Tasks[3], "blocks");
        (await dag.Context.TaskDependencies.CountAsync()).Should().Be(4);
    }

    [Fact]
    public async Task DependencyGraph_DuplicateSelfAndCrossProjectEdgesAreRejectedOrIdempotent()
    {
        await using var fixture = await DependencyFixture.CreateInMemoryAsync(2);
        (await fixture.Service.AddOrUpdateAsync(
            fixture.ProjectId, fixture.Tasks[0], fixture.Tasks[1], "blocks"))
            .Should().Be(TaskDependencyMutation.Created);
        (await fixture.Service.AddOrUpdateAsync(
            fixture.ProjectId, fixture.Tasks[0], fixture.Tasks[1], "blocks"))
            .Should().Be(TaskDependencyMutation.Unchanged);
        (await fixture.Context.TaskDependencies.CountAsync()).Should().Be(1);

        var self = () => fixture.Service.AddOrUpdateAsync(
            fixture.ProjectId, fixture.Tasks[0], fixture.Tasks[0], "blocks");
        await self.Should().ThrowAsync<ArgumentException>().WithMessage("*itself*");

        var otherProjectId = Guid.NewGuid();
        fixture.Context.Projects.Add(new Project
        {
            Id = otherProjectId,
            WorkspaceId = fixture.WorkspaceId,
            CreatorId = fixture.UserId,
            Name = "Other",
            Identifier = "OTH",
            Status = true
        });
        var otherTask = await fixture.AddTaskAsync(otherProjectId, "Other task");
        var crossProject = () => fixture.Service.AddOrUpdateAsync(
            fixture.ProjectId, fixture.Tasks[0], otherTask, "blocks");
        await crossProject.Should().ThrowAsync<ArgumentException>().WithMessage("*requested project*");
    }

    [Fact]
    public async Task TC_GOAL_005_GoalCreate_UsesExplicitSelectedWorkspaceForMultiWorkspaceUser()
    {
        await using var context = CreateInMemoryContext();
        var userId = Guid.NewGuid();
        var oldWorkspaceId = Guid.NewGuid();
        var selectedWorkspaceId = Guid.NewGuid();
        context.Users.Add(new User
        {
            Id = userId,
            Email = "goal-owner@example.com",
            FullName = "Goal Owner",
            PasswordHash = "unused",
            IsActive = true
        });
        context.Workspaces.AddRange(
            new Workspace { Id = oldWorkspaceId, OwnerId = userId, Name = "Old", Slug = "old" },
            new Workspace { Id = selectedWorkspaceId, OwnerId = userId, Name = "Selected", Slug = "selected" });
        context.WorkspaceMembers.AddRange(
            new WorkspaceMember { WorkspaceId = oldWorkspaceId, UserId = userId, WorkspaceRole = "OWNER", IsActive = true, JoinedAt = DateTime.UtcNow.AddYears(-1) },
            new WorkspaceMember { WorkspaceId = selectedWorkspaceId, UserId = userId, WorkspaceRole = "MEMBER", IsActive = true, JoinedAt = DateTime.UtcNow });
        await context.SaveChangesAsync();

        var accessor = AuthenticatedAccessor(userId);
        var service = new GoalService(context, accessor);
        await service.CreateAsync(userId, selectedWorkspaceId, new { title = "Selected goal" });

        var goal = await context.Goals.SingleAsync();
        goal.WorkspaceId.Should().Be(selectedWorkspaceId);
        (await context.Workspaces.CountAsync()).Should().Be(2, "no fallback workspace may be created");

        var missingContext = () => service.CreateAsync(userId, Guid.Empty, new { title = "No context" });
        await missingContext.Should().ThrowAsync<ArgumentException>().WithMessage("*explicit workspace*");
    }

    [Fact]
    public async Task TC_TASK_038_WorkTaskCreate_MissingOrInvalidReporterIsRejectedBeforePersistence()
    {
        await using var context = CreateInMemoryContext();
        var projectId = Guid.NewGuid();
        var statusId = Guid.NewGuid();
        context.Projects.Add(new Project
        {
            Id = projectId,
            WorkspaceId = Guid.NewGuid(),
            Name = "Project",
            Identifier = "PRJ",
            Status = true
        });
        context.TaskStatuses.Add(new TaskManagement.Domain.Entities.TaskStatus
        {
            Id = statusId,
            ProjectId = projectId,
            Name = "To Do"
        });
        await context.SaveChangesAsync();

        var service = new WorkTaskService(context, Mock.Of<IGamificationService>());
        var dto = new CreateWorkTaskDto { ProjectId = projectId, Title = "Task", StatusName = "To Do" };

        await FluentActions.Invoking(() => service.CreateAsync(Guid.Empty, dto))
            .Should().ThrowAsync<UnauthorizedAccessException>();
        await FluentActions.Invoking(() => service.CreateAsync(Guid.NewGuid(), dto))
            .Should().ThrowAsync<UnauthorizedAccessException>();
        (await context.WorkTasks.CountAsync()).Should().Be(0);
    }

    [Fact]
    public async Task TC_SPRINT_020_SprintRollover_RejectsCrossProjectAndAllowsSameProject()
    {
        await using var context = CreateInMemoryContext();
        var userId = Guid.NewGuid();
        var workspaceId = Guid.NewGuid();
        var sourceProjectId = Guid.NewGuid();
        var otherProjectId = Guid.NewGuid();
        var sourceSprintId = Guid.NewGuid();
        var validTargetId = Guid.NewGuid();
        var invalidTargetId = Guid.NewGuid();
        context.Users.Add(new User { Id = userId, Email = "rollover@example.com", PasswordHash = "unused", IsActive = true });
        context.Workspaces.Add(new Workspace { Id = workspaceId, OwnerId = userId, Name = "Workspace", Slug = "ws" });
        context.Projects.AddRange(
            new Project { Id = sourceProjectId, WorkspaceId = workspaceId, CreatorId = userId, Name = "Source", Identifier = "SRC", Status = true },
            new Project { Id = otherProjectId, WorkspaceId = workspaceId, CreatorId = userId, Name = "Other", Identifier = "OTH", Status = true });
        context.Sprints.AddRange(
            new Sprint { Id = sourceSprintId, ProjectId = sourceProjectId, Name = "Source", Status = true, StartDate = DateTime.UtcNow.AddDays(-2), EndDate = DateTime.UtcNow.AddDays(2) },
            new Sprint { Id = validTargetId, ProjectId = sourceProjectId, Name = "Valid", Status = false, StartDate = DateTime.UtcNow.AddDays(3), EndDate = DateTime.UtcNow.AddDays(6) },
            new Sprint { Id = invalidTargetId, ProjectId = otherProjectId, Name = "Invalid", Status = false, StartDate = DateTime.UtcNow.AddDays(3), EndDate = DateTime.UtcNow.AddDays(6) });
        await context.SaveChangesAsync();

        var service = new SprintService(context);
        await FluentActions.Invoking(() => service.CloseAsync(
                sourceSprintId,
                new CloseSprintDto { TargetSprintId = invalidTargetId },
                userId))
            .Should().ThrowAsync<ArgumentException>().WithMessage("*same project and workspace*");

        await service.CloseAsync(sourceSprintId, new CloseSprintDto { TargetSprintId = validTargetId }, userId);
        (await context.Sprints.FindAsync(sourceSprintId))!.Status.Should().BeFalse();
    }

    private static ApplicationDbContext CreateInMemoryContext()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString("N"))
            .ConfigureWarnings(warnings => warnings.Ignore(
                Microsoft.EntityFrameworkCore.Diagnostics.InMemoryEventId.TransactionIgnoredWarning))
            .Options;
        return new ApplicationDbContext(options);
    }

    private static IHttpContextAccessor AuthenticatedAccessor(Guid userId) =>
        new HttpContextAccessor
        {
            HttpContext = new DefaultHttpContext
            {
                User = new ClaimsPrincipal(new ClaimsIdentity(
                    new[] { new Claim(ClaimTypes.NameIdentifier, userId.ToString()) },
                    "TestAuth"))
            }
        };

    private sealed class DependencyFixture : IAsyncDisposable
    {
        private DependencyFixture(ApplicationDbContext context, Guid workspaceId, Guid projectId, Guid userId)
        {
            Context = context;
            WorkspaceId = workspaceId;
            ProjectId = projectId;
            UserId = userId;
            Service = new TaskDependencyService(context);
        }

        public ApplicationDbContext Context { get; }
        public TaskDependencyService Service { get; }
        public Guid WorkspaceId { get; }
        public Guid ProjectId { get; }
        public Guid UserId { get; }
        public List<Guid> Tasks { get; } = new();

        public static async Task<DependencyFixture> CreateInMemoryAsync(int taskCount)
        {
            var context = CreateInMemoryContext();
            var userId = Guid.NewGuid();
            var workspaceId = Guid.NewGuid();
            var projectId = Guid.NewGuid();
            context.Users.Add(new User { Id = userId, Email = $"{userId:N}@example.com", PasswordHash = "unused", IsActive = true });
            context.Workspaces.Add(new Workspace { Id = workspaceId, OwnerId = userId, Name = "Workspace", Slug = $"ws-{workspaceId:N}" });
            context.Projects.Add(new Project { Id = projectId, WorkspaceId = workspaceId, CreatorId = userId, Name = "Project", Identifier = "PRJ", Status = true });
            await context.SaveChangesAsync();

            var fixture = new DependencyFixture(context, workspaceId, projectId, userId);
            for (var index = 0; index < taskCount; index++)
            {
                fixture.Tasks.Add(await fixture.AddTaskAsync(projectId, $"Task {index}"));
            }
            return fixture;
        }

        public async Task<Guid> AddTaskAsync(Guid projectId, string title)
        {
            var statusId = Guid.NewGuid();
            var typeId = Guid.NewGuid();
            Context.TaskStatuses.Add(new TaskManagement.Domain.Entities.TaskStatus { Id = statusId, ProjectId = projectId, Name = "To Do" });
            Context.TaskTypes.Add(new TaskType { Id = typeId, ProjectId = projectId, Name = "Task" });
            var id = Guid.NewGuid();
            Context.WorkTasks.Add(new WorkTask
            {
                Id = id,
                ProjectId = projectId,
                WorkspaceId = WorkspaceId,
                TaskStatusId = statusId,
                TaskTypeId = typeId,
                ReporterId = UserId,
                Title = title,
                SequenceId = $"PRJ-{Guid.NewGuid():N}",
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            });
            await Context.SaveChangesAsync();
            return id;
        }

        public async ValueTask DisposeAsync()
        {
            await Context.DisposeAsync();
        }
    }
}

public sealed class P003SqlServerIntegrationTests
{
    [Fact]
    [Trait("Database", "SqlServer")]
    public async Task TC_DEP_025_DependencyCycleValidation_RunsAtomicallyOnRealSqlServer()
    {
        var databaseName = $"TaskManagement_P003_{Guid.NewGuid():N}";
        var connectionString =
            $"Server=KHOI\\SQLEXPRESS;Database={databaseName};Trusted_Connection=True;TrustServerCertificate=True;Encrypt=False;Connect Timeout=30";
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseSqlServer(connectionString)
            .Options;

        await using var context = new ApplicationDbContext(options);
        try
        {
            await context.Database.EnsureCreatedAsync();
            var fixture = await SeedSqlGraphAsync(context);
            var service = new TaskDependencyService(context);

            await service.AddOrUpdateAsync(fixture.ProjectId, fixture.TaskA, fixture.TaskB, "blocks");
            await service.AddOrUpdateAsync(fixture.ProjectId, fixture.TaskB, fixture.TaskC, "blocks");
            await FluentActions.Invoking(() => service.AddOrUpdateAsync(
                    fixture.ProjectId, fixture.TaskC, fixture.TaskA, "blocks"))
                .Should().ThrowAsync<InvalidOperationException>().WithMessage("*cycle*");

            (await context.TaskDependencies.CountAsync()).Should().Be(2);

            context.TaskDependencies.RemoveRange(await context.TaskDependencies.ToListAsync());
            await context.SaveChangesAsync();

            await using var contextA = new ApplicationDbContext(options);
            await using var contextB = new ApplicationDbContext(options);
            var start = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            var first = AttemptAsync(async () =>
            {
                await start.Task;
                await new TaskDependencyService(contextA).AddOrUpdateAsync(
                    fixture.ProjectId, fixture.TaskA, fixture.TaskB, "blocks");
            });
            var second = AttemptAsync(async () =>
            {
                await start.Task;
                await new TaskDependencyService(contextB).AddOrUpdateAsync(
                    fixture.ProjectId, fixture.TaskB, fixture.TaskA, "blocks");
            });

            start.SetResult();
            var outcomes = await Task.WhenAll(first, second);
            outcomes.Count(error => error == null).Should().Be(1);
            outcomes.Count(error => error is InvalidOperationException).Should().Be(1);

            await using var verification = new ApplicationDbContext(options);
            (await verification.TaskDependencies.CountAsync()).Should().Be(1,
                "the project-scoped SQL application lock must serialize competing cycle inserts");
        }
        finally
        {
            await context.Database.EnsureDeletedAsync();
        }
    }

    private static async Task<Exception?> AttemptAsync(Func<Task> action)
    {
        try
        {
            await action();
            return null;
        }
        catch (Exception ex)
        {
            return ex;
        }
    }

    private static async Task<(Guid ProjectId, Guid TaskA, Guid TaskB, Guid TaskC)> SeedSqlGraphAsync(
        ApplicationDbContext context)
    {
        var userId = Guid.NewGuid();
        var workspaceId = Guid.NewGuid();
        var projectId = Guid.NewGuid();
        var statusId = Guid.NewGuid();
        var typeId = Guid.NewGuid();
        context.Users.Add(new User { Id = userId, Email = $"{userId:N}@example.com", PasswordHash = "unused", IsActive = true });
        context.Workspaces.Add(new Workspace { Id = workspaceId, OwnerId = userId, Name = "Workspace", Slug = $"ws-{workspaceId:N}" });
        context.Projects.Add(new Project { Id = projectId, WorkspaceId = workspaceId, CreatorId = userId, Name = "Project", Identifier = "SQL", Status = true });
        context.TaskStatuses.Add(new TaskManagement.Domain.Entities.TaskStatus { Id = statusId, ProjectId = projectId, Name = "To Do" });
        context.TaskTypes.Add(new TaskType { Id = typeId, ProjectId = projectId, Name = "Task" });

        var taskIds = new[] { Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid() };
        context.WorkTasks.AddRange(taskIds.Select((id, index) => new WorkTask
        {
            Id = id,
            ProjectId = projectId,
            WorkspaceId = workspaceId,
            TaskStatusId = statusId,
            TaskTypeId = typeId,
            ReporterId = userId,
            Title = $"Task {index}",
            SequenceId = $"SQL-{index + 1}",
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        }));
        await context.SaveChangesAsync();
        return (projectId, taskIds[0], taskIds[1], taskIds[2]);
    }
}
