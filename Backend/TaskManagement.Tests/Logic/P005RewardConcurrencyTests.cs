using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using TaskManagement.Domain.Entities;
using TaskManagement.Infrastructure.Data;
using TaskManagement.Infrastructure.Services;

namespace TaskManagement.Tests.Logic;

public sealed class P005RewardConcurrencyTests
{
    private const string PreviousMigration = "20260718001618_PreserveTaskAssignmentHistory";

    [Fact]
    [Trait("Database", "SqlServer")]
    public async Task TC_GAMIFY_003_RewardRetryRollbackAndSecondDoneRemainBalanced()
    {
        var options = SqlOptions($"TaskManagement_P005_Reward_{Guid.NewGuid():N}");
        await using var setup = new ApplicationDbContext(options);
        try
        {
            await setup.Database.MigrateAsync();
            var seed = await SeedTaskAsync(setup);

            await using (var firstContext = new ApplicationDbContext(options))
            {
                var rewards = new GamificationService(firstContext);
                await rewards.ApplyStatusChangeRewardsAsync(seed.TaskId, seed.UserId, "In Progress", "Done");
                await rewards.ApplyStatusChangeRewardsAsync(seed.TaskId, seed.UserId, "In Progress", "Done");
            }

            int firstBalance;
            await using (var verification = new ApplicationDbContext(options))
            {
                firstBalance = (await verification.UserWallets.SingleAsync()).TotalPoints;
                firstBalance.Should().BeGreaterThan(0);
                (await verification.PointTransactions.CountAsync(entry => entry.TransactionType == "TaskBaseReward"))
                    .Should().Be(1, "a retry cannot create a second reward event");
            }

            await using (var rollbackContext = new ApplicationDbContext(options))
            {
                var rewards = new GamificationService(rollbackContext);
                await rewards.ApplyStatusChangeRewardsAsync(seed.TaskId, seed.UserId, "Done", "In Progress");
                await rewards.ApplyStatusChangeRewardsAsync(seed.TaskId, seed.UserId, "Done", "In Progress");
                await rewards.ApplyStatusChangeRewardsAsync(seed.TaskId, seed.UserId, "In Progress", "Done");
            }

            await using (var verification = new ApplicationDbContext(options))
            {
                (await verification.UserWallets.SingleAsync()).TotalPoints.Should().Be(firstBalance);
                var originals = await verification.PointTransactions
                    .Where(entry => entry.TransactionType == "TaskBaseReward")
                    .OrderBy(entry => entry.CreatedAt)
                    .ToListAsync();
                originals.Should().HaveCount(2, "a new Done cycle is a new immutable reward event");
                var reversals = await verification.PointTransactions
                    .Where(entry => entry.TransactionType == "TaskRewardRollback")
                    .ToListAsync();
                reversals.Should().ContainSingle(entry => entry.ReversalOfTransactionId == originals[0].Id);
                (await verification.PointTransactions.Select(entry => entry.IdempotencyKey).Where(key => key != null).Distinct().CountAsync())
                    .Should().Be(await verification.PointTransactions.CountAsync(entry => entry.IdempotencyKey != null));
            }
        }
        finally
        {
            await setup.Database.EnsureDeletedAsync();
        }
    }

    [Fact]
    [Trait("Database", "SqlServer")]
    public async Task TC_GAMIFY_003_ConcurrentDoneTransitionCreatesOneRewardEvent()
    {
        var options = SqlOptions($"TaskManagement_P005_RewardRace_{Guid.NewGuid():N}");
        await using var setup = new ApplicationDbContext(options);
        try
        {
            await setup.Database.MigrateAsync();
            var seed = await SeedTaskAsync(setup);
            await using var contextA = new ApplicationDbContext(options);
            await using var contextB = new ApplicationDbContext(options);
            var start = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            var first = Task.Run(async () =>
            {
                await start.Task;
                await new GamificationService(contextA)
                    .ApplyStatusChangeRewardsAsync(seed.TaskId, seed.UserId, "In Progress", "Done");
            });
            var second = Task.Run(async () =>
            {
                await start.Task;
                await new GamificationService(contextB)
                    .ApplyStatusChangeRewardsAsync(seed.TaskId, seed.UserId, "In Progress", "Done");
            });

            start.SetResult();
            await Task.WhenAll(first, second);

            await using var verification = new ApplicationDbContext(options);
            (await verification.PointTransactions.CountAsync(entry => entry.TransactionType == "TaskBaseReward"))
                .Should().Be(1);
            var wallet = await verification.UserWallets.SingleAsync();
            wallet.TotalPoints.Should().Be(await verification.PointTransactions.SumAsync(entry => entry.Amount));
        }
        finally
        {
            await setup.Database.EnsureDeletedAsync();
        }
    }

    [Fact]
    [Trait("Database", "SqlServer")]
    public async Task TC_GAMIFY_003_LedgerFailureRollsBackWalletMutation()
    {
        var options = SqlOptions($"TaskManagement_P005_RewardFailure_{Guid.NewGuid():N}");
        await using var setup = new ApplicationDbContext(options);
        try
        {
            await setup.Database.MigrateAsync();
            var seed = await SeedTaskAsync(setup);
            await using var failingContext = new FailingRewardDbContext(options);

            var action = () => new GamificationService(failingContext)
                .ApplyStatusChangeRewardsAsync(seed.TaskId, seed.UserId, "In Progress", "Done");
            await action.Should().ThrowAsync<InvalidOperationException>()
                .WithMessage("Injected ledger persistence failure");

            await using var verification = new ApplicationDbContext(options);
            (await verification.UserWallets.SingleAsync()).TotalPoints.Should().Be(0);
            (await verification.PointTransactions.CountAsync()).Should().Be(0);
        }
        finally
        {
            await setup.Database.EnsureDeletedAsync();
        }
    }

    [Fact]
    [Trait("Database", "SqlServer")]
    public async Task TC_TASK_024_And_TC_RBAC_050_TwoWritersWithSameRowVersionRejectSecondWrite()
    {
        var options = SqlOptions($"TaskManagement_P005_RowVersion_{Guid.NewGuid():N}");
        await using var setup = new ApplicationDbContext(options);
        try
        {
            await setup.Database.MigrateAsync();
            var seed = await SeedTaskAsync(setup);

            await using var contextA = new ApplicationDbContext(options);
            await using var contextB = new ApplicationDbContext(options);
            var taskA = await contextA.WorkTasks.SingleAsync(task => task.Id == seed.TaskId);
            var taskB = await contextB.WorkTasks.SingleAsync(task => task.Id == seed.TaskId);
            taskA.RowVersion.Should().Equal(taskB.RowVersion);

            taskA.Title = "First writer";
            taskB.Description = "Stale second writer";
            await contextA.SaveChangesAsync();
            var staleWrite = () => contextB.SaveChangesAsync();
            await staleWrite.Should().ThrowAsync<DbUpdateConcurrencyException>();

            await using var verification = new ApplicationDbContext(options);
            var current = await verification.WorkTasks.AsNoTracking().SingleAsync(task => task.Id == seed.TaskId);
            current.Title.Should().Be("First writer");
            current.Description.Should().BeNull("the stale request must not overwrite even a different field");
            current.RowVersion.Should().NotEqual(taskB.RowVersion);
        }
        finally
        {
            await setup.Database.EnsureDeletedAsync();
        }
    }

    [Fact]
    [Trait("Database", "SqlServer")]
    public async Task ImmutableRewardLedgerMigration_UpDownAndReapplyPasses()
    {
        var options = SqlOptions($"TaskManagement_P005_Migration_{Guid.NewGuid():N}");
        await using var context = new ApplicationDbContext(options);
        try
        {
            var migrator = context.GetService<IMigrator>();
            await migrator.MigrateAsync();
            (await HasColumnAsync(context, "PointTransactions", "IdempotencyKey")).Should().BeTrue();
            (await HasColumnAsync(context, "PointTransactions", "ReversalOfTransactionId")).Should().BeTrue();

            await migrator.MigrateAsync(PreviousMigration);
            (await HasColumnAsync(context, "PointTransactions", "IdempotencyKey")).Should().BeFalse();

            await migrator.MigrateAsync();
            (await HasColumnAsync(context, "PointTransactions", "IdempotencyKey")).Should().BeTrue();
        }
        finally
        {
            await context.Database.EnsureDeletedAsync();
        }
    }

    private static DbContextOptions<ApplicationDbContext> SqlOptions(string databaseName) =>
        new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseSqlServer($"Server=KHOI\\SQLEXPRESS;Database={databaseName};Trusted_Connection=True;TrustServerCertificate=True;Encrypt=False;Connect Timeout=30")
            .Options;

    private static async Task<(Guid TaskId, Guid UserId)> SeedTaskAsync(ApplicationDbContext context)
    {
        var userId = Guid.NewGuid();
        var workspaceId = Guid.NewGuid();
        var projectId = Guid.NewGuid();
        var statusId = Guid.NewGuid();
        var typeId = Guid.NewGuid();
        var taskId = Guid.NewGuid();
        context.Users.Add(new User { Id = userId, Email = $"reward-{userId:N}@example.com", PasswordHash = "unused", IsActive = true });
        context.Workspaces.Add(new Workspace { Id = workspaceId, OwnerId = userId, Name = "Workspace", Slug = $"ws-{workspaceId:N}" });
        context.WorkspaceMembers.Add(new WorkspaceMember { WorkspaceId = workspaceId, UserId = userId, WorkspaceRole = "OWNER", IsActive = true });
        context.Projects.Add(new Project { Id = projectId, WorkspaceId = workspaceId, CreatorId = userId, Name = "Project", Identifier = "RWD", Status = true });
        context.ProjectMembers.Add(new ProjectMember { ProjectId = projectId, UserId = userId, ProjectRole = "PM", Status = true, JoinedAt = DateTime.UtcNow });
        context.TaskStatuses.Add(new TaskManagement.Domain.Entities.TaskStatus { Id = statusId, ProjectId = projectId, Name = "In Progress" });
        context.TaskTypes.Add(new TaskType { Id = typeId, ProjectId = projectId, Name = "Task" });
        context.WorkTasks.Add(new WorkTask
        {
            Id = taskId,
            ProjectId = projectId,
            WorkspaceId = workspaceId,
            TaskStatusId = statusId,
            TaskTypeId = typeId,
            ReporterId = userId,
            Title = "Concurrent task",
            StoryPoints = 3,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        });
        context.UserWallets.Add(new UserWallet { UserId = userId, TotalPoints = 0, Level = 1 });
        await context.SaveChangesAsync();
        return (taskId, userId);
    }

    private static async Task<bool> HasColumnAsync(ApplicationDbContext context, string tableName, string columnName)
    {
        await context.Database.OpenConnectionAsync();
        try
        {
            await using var command = context.Database.GetDbConnection().CreateCommand();
            command.CommandText = "SELECT COUNT(*) FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME = @table AND COLUMN_NAME = @column";
            var table = command.CreateParameter();
            table.ParameterName = "@table";
            table.Value = tableName;
            command.Parameters.Add(table);
            var column = command.CreateParameter();
            column.ParameterName = "@column";
            column.Value = columnName;
            command.Parameters.Add(column);
            return Convert.ToInt32(await command.ExecuteScalarAsync()) == 1;
        }
        finally
        {
            await context.Database.CloseConnectionAsync();
        }
    }

    private sealed class FailingRewardDbContext : ApplicationDbContext
    {
        public FailingRewardDbContext(DbContextOptions<ApplicationDbContext> options) : base(options)
        {
        }

        public override Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
        {
            if (ChangeTracker.Entries<PointTransaction>().Any(entry => entry.State == EntityState.Added))
            {
                throw new InvalidOperationException("Injected ledger persistence failure");
            }

            return base.SaveChangesAsync(cancellationToken);
        }
    }
}
