using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using TaskManagement.Domain.Entities;
using TaskManagement.Infrastructure.Data;
using TaskManagement.Infrastructure.Services;

namespace TaskManagement.Tests.Logic;

public sealed class RewardSystemV1SqlServerTests
{
    [Fact]
    [Trait("Database", "SqlServer")]
    public async Task RewardMigrationHasValidForeignKeysAndConcurrentCompletionCreatesOneAward()
    {
        var databaseName = $"TaskManagement_RewardV1_{Guid.NewGuid():N}";
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseSqlServer(SqlServerTestConfiguration.ConnectionString(databaseName)).Options;
        await using var setup = new ApplicationDbContext(options);
        try
        {
            await setup.Database.MigrateAsync();
            (await ScalarAsync(setup, "SELECT COUNT(*) FROM sys.indexes WHERE object_id = OBJECT_ID('RewardPointEvents') AND name = 'IX_RewardPointEvents_WorkTaskId_UserId' AND is_unique = 1")).Should().Be(1);
            (await ScalarAsync(setup, "SELECT COUNT(*) FROM sys.indexes WHERE object_id = OBJECT_ID('RewardGrants') AND name = 'IX_RewardGrants_RewardDefinitionId_SeasonId_RecipientUserId' AND is_unique = 1")).Should().Be(1);
            (await ScalarAsync(setup, "SELECT COUNT(*) FROM sys.foreign_keys WHERE parent_object_id = OBJECT_ID('RewardSeasons')")).Should().Be(2);

            var seed = await SeedAsync(setup);
            await using var contextA = new ApplicationDbContext(options);
            await using var contextB = new ApplicationDbContext(options);
            var start = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            var first = Task.Run(async () => { await start.Task; await new RewardSystemService(contextA).HandleTaskStatusChangeAsync(seed.TaskId, seed.ManagerId, "To Do", "Done"); });
            var second = Task.Run(async () => { await start.Task; await new RewardSystemService(contextB).HandleTaskStatusChangeAsync(seed.TaskId, seed.ManagerId, "To Do", "Done"); });
            start.SetResult();
            await Task.WhenAll(first, second);

            await using var verification = new ApplicationDbContext(options);
            (await verification.RewardPointEvents.CountAsync()).Should().Be(1);
            (await verification.RewardPointEvents.CountAsync(item => item.Status == "Pending")).Should().Be(1);
        }
        finally
        {
            await setup.Database.EnsureDeletedAsync();
        }
    }

    private static async Task<int> ScalarAsync(ApplicationDbContext context, string sql)
    {
        await context.Database.OpenConnectionAsync();
        try
        {
            await using var command = context.Database.GetDbConnection().CreateCommand();
            command.CommandText = sql;
            return Convert.ToInt32(await command.ExecuteScalarAsync());
        }
        finally
        {
            await context.Database.CloseConnectionAsync();
        }
    }

    private static async Task<(Guid TaskId, Guid ManagerId)> SeedAsync(ApplicationDbContext context)
    {
        var managerId = Guid.NewGuid();
        var memberId = Guid.NewGuid();
        var workspaceId = Guid.NewGuid();
        var projectId = Guid.NewGuid();
        var taskId = Guid.NewGuid();
        var statusId = Guid.NewGuid();
        var typeId = Guid.NewGuid();
        var now = DateTime.UtcNow;
        context.Users.AddRange(
            new User { Id = managerId, Email = $"reward-sql-manager-{managerId:N}@example.com", PasswordHash = "unused", IsActive = true },
            new User { Id = memberId, Email = $"reward-sql-member-{memberId:N}@example.com", PasswordHash = "unused", IsActive = true });
        context.Workspaces.Add(new Workspace { Id = workspaceId, OwnerId = managerId, Name = "Reward SQL", Slug = $"reward-sql-{workspaceId:N}", Timezone = "Asia/Ho_Chi_Minh" });
        context.Projects.Add(new Project { Id = projectId, WorkspaceId = workspaceId, CreatorId = managerId, Name = "Reward SQL", Identifier = $"R{projectId:N}"[..8], Status = true });
        context.ProjectMembers.AddRange(
            new ProjectMember { ProjectId = projectId, UserId = managerId, ProjectRole = "PM", Status = true, JoinedAt = now },
            new ProjectMember { ProjectId = projectId, UserId = memberId, ProjectRole = "DEV", Status = true, JoinedAt = now });
        context.TaskStatuses.Add(new TaskManagement.Domain.Entities.TaskStatus { Id = statusId, ProjectId = projectId, Name = "To Do" });
        context.TaskTypes.Add(new TaskType { Id = typeId, ProjectId = projectId, Name = "Task" });
        context.WorkTasks.Add(new WorkTask { Id = taskId, ProjectId = projectId, WorkspaceId = workspaceId, TaskStatusId = statusId, TaskTypeId = typeId, ReporterId = memberId, AssignedUserId = memberId, StoryPoints = 3, Title = "SQL reward task", CreatedAt = now, UpdatedAt = now });
        context.TaskAssignments.Add(new TaskAssignment { WorkTaskId = taskId, UserId = memberId, Status = true, EstimatedHours = 8, ContributionWeight = 1 });
        context.RewardSeasons.Add(new RewardSeason { Id = Guid.NewGuid(), ProjectId = projectId, Name = "SQL Season", Type = "Custom", StartAt = DateTimeOffset.UtcNow.AddDays(-1), EndAt = DateTimeOffset.UtcNow.AddDays(1), Status = "Active", CreatedBy = managerId, CreatedAt = DateTimeOffset.UtcNow });
        await context.SaveChangesAsync();
        return (taskId, managerId);
    }
}
