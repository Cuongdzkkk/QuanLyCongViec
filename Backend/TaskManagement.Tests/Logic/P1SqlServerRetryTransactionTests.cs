using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Moq;
using TaskManagement.Application.DTOs.Project;
using TaskManagement.Application.Interfaces;
using TaskManagement.Domain.Entities;
using TaskManagement.Infrastructure.Data;
using TaskManagement.Infrastructure.Services;

namespace TaskManagement.Tests.Logic;

public sealed class P1SqlServerRetryTransactionTests
{
    [Fact]
    [Trait("Database", "SqlServer")]
    public async Task AddExistingMember_WithSqlServerRetryingExecutionStrategy_CommitsAtomically()
    {
        var databaseName = $"TaskManagement_P1_RetryMember_{Guid.NewGuid():N}";
        await using var context = CreateSqlContext(databaseName);
        try
        {
            await context.Database.EnsureCreatedAsync();
            var seed = await SeedMemberAsync(context);
            var service = new ProjectMemberService(
                context,
                Mock.Of<IEmailService>(),
                new ConfigurationBuilder().Build());

            var added = await service.AddExistingMemberAsync(
                    seed.ProjectId,
                    new AddExistingProjectMemberRequestDto { UserId = seed.MemberId, Role = "Developer" });

            added.UserId.Should().Be(seed.MemberId);
            (await context.ProjectMembers.CountAsync(member =>
                    member.ProjectId == seed.ProjectId && member.UserId == seed.MemberId))
                .Should().Be(1);
        }
        finally
        {
            await context.Database.EnsureDeletedAsync();
        }
    }

    [Fact]
    [Trait("Database", "SqlServer")]
    public async Task TaskDependency_WithSqlServerRetryingExecutionStrategy_IsAtomicAndIdempotent()
    {
        var databaseName = $"TaskManagement_P1_RetryDependency_{Guid.NewGuid():N}";
        await using var context = CreateSqlContext(databaseName);
        try
        {
            await context.Database.EnsureCreatedAsync();
            var seed = await SeedDependencyAsync(context);
            var service = new TaskDependencyService(context);

            (await service.AddOrUpdateAsync(
                    seed.ProjectId,
                    seed.TaskA,
                    seed.TaskB,
                    "blocks"))
                .Should().Be(TaskDependencyMutation.Created);
            (await service.AddOrUpdateAsync(seed.ProjectId, seed.TaskA, seed.TaskB, "blocks"))
                .Should().Be(TaskDependencyMutation.Unchanged);
            (await context.TaskDependencies.CountAsync(edge =>
                    edge.PredecessorTaskId == seed.TaskA && edge.SuccessorTaskId == seed.TaskB))
                .Should().Be(1);
        }
        finally
        {
            await context.Database.EnsureDeletedAsync();
        }
    }

    private static ApplicationDbContext CreateSqlContext(string databaseName)
    {
        var connectionString =
            $"Server=(localdb)\\MSSQLLocalDB;Initial Catalog={databaseName};" +
            "Integrated Security=true;TrustServerCertificate=true;Encrypt=false;Connect Timeout=30";
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseSqlServer(connectionString, sqlServer => sqlServer.EnableRetryOnFailure())
            .Options;
        return new ApplicationDbContext(options);
    }

    private static async Task<(Guid ProjectId, Guid MemberId)> SeedMemberAsync(ApplicationDbContext context)
    {
        var ownerId = Guid.NewGuid();
        var memberId = Guid.NewGuid();
        var workspaceId = Guid.NewGuid();
        var projectId = Guid.NewGuid();
        context.Users.AddRange(
            new User { Id = ownerId, Email = $"owner-{ownerId:N}@example.com", PasswordHash = "unused", IsActive = true },
            new User { Id = memberId, Email = $"member-{memberId:N}@example.com", PasswordHash = "unused", IsActive = true });
        context.Workspaces.Add(new Workspace
        {
            Id = workspaceId,
            OwnerId = ownerId,
            Name = "Workspace",
            Slug = $"ws-{workspaceId:N}"
        });
        context.WorkspaceMembers.AddRange(
            new WorkspaceMember { WorkspaceId = workspaceId, UserId = ownerId, WorkspaceRole = "OWNER", IsActive = true },
            new WorkspaceMember { WorkspaceId = workspaceId, UserId = memberId, WorkspaceRole = "MEMBER", IsActive = true });
        context.Projects.Add(new Project
        {
            Id = projectId,
            WorkspaceId = workspaceId,
            CreatorId = ownerId,
            Name = "Project",
            Identifier = $"P1{projectId:N}"[..10],
            Status = true
        });
        context.ProjectMembers.Add(new ProjectMember
        {
            ProjectId = projectId,
            UserId = ownerId,
            ProjectRole = "PM",
            Status = true,
            JoinedAt = DateTime.UtcNow
        });
        context.Roles.Add(new Role { Id = Guid.NewGuid(), Name = "Developer" });
        await context.SaveChangesAsync();
        return (projectId, memberId);
    }

    private static async Task<(Guid ProjectId, Guid TaskA, Guid TaskB)> SeedDependencyAsync(
        ApplicationDbContext context)
    {
        var userId = Guid.NewGuid();
        var workspaceId = Guid.NewGuid();
        var projectId = Guid.NewGuid();
        var statusId = Guid.NewGuid();
        var typeId = Guid.NewGuid();
        var taskA = Guid.NewGuid();
        var taskB = Guid.NewGuid();
        context.Users.Add(new User
        {
            Id = userId,
            Email = $"owner-{userId:N}@example.com",
            PasswordHash = "unused",
            IsActive = true
        });
        context.Workspaces.Add(new Workspace
        {
            Id = workspaceId,
            OwnerId = userId,
            Name = "Workspace",
            Slug = $"ws-{workspaceId:N}"
        });
        context.Projects.Add(new Project
        {
            Id = projectId,
            WorkspaceId = workspaceId,
            CreatorId = userId,
            Name = "Project",
            Identifier = $"D1{projectId:N}"[..10],
            Status = true
        });
        context.TaskStatuses.Add(new TaskManagement.Domain.Entities.TaskStatus
        {
            Id = statusId,
            ProjectId = projectId,
            Name = "To Do"
        });
        context.TaskTypes.Add(new TaskType { Id = typeId, ProjectId = projectId, Name = "Task" });
        context.WorkTasks.AddRange(
            new WorkTask
            {
                Id = taskA,
                ProjectId = projectId,
                WorkspaceId = workspaceId,
                TaskStatusId = statusId,
                TaskTypeId = typeId,
                ReporterId = userId,
                Title = "Task A",
                SequenceId = $"A-{taskA:N}",
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            },
            new WorkTask
            {
                Id = taskB,
                ProjectId = projectId,
                WorkspaceId = workspaceId,
                TaskStatusId = statusId,
                TaskTypeId = typeId,
                ReporterId = userId,
                Title = "Task B",
                SequenceId = $"B-{taskB:N}",
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            });
        await context.SaveChangesAsync();
        return (projectId, taskA, taskB);
    }
}
