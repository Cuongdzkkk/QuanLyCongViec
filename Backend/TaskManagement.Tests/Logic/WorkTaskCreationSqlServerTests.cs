using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Moq;
using TaskManagement.Application.DTOs.WorkTask;
using TaskManagement.Application.Interfaces;
using TaskManagement.Domain.Entities;
using TaskManagement.Infrastructure.Data;
using TaskManagement.Infrastructure.Services;

namespace TaskManagement.Tests.Logic;

public sealed class WorkTaskCreationSqlServerTests
{
    [Fact]
    [Trait("Category", "SqlServerIntegration")]
    public async Task ConcurrentTaskCreationAllocatesUniqueProjectSequenceIds()
    {
        if (!await IsAvailableAsync()) return;

        var databaseName = $"TaskManagement_TaskCreation_{Guid.NewGuid():N}";
        await using var setupContext = CreateContext(databaseName);
        await setupContext.Database.MigrateAsync();
        try
        {
            var now = DateTime.UtcNow;
            var userId = Guid.NewGuid();
            var workspaceId = Guid.NewGuid();
            var projectId = Guid.NewGuid();
            setupContext.Users.Add(new User
            {
                Id = userId, Email = "task-sequence@test.local", FullName = "Task Sequence User",
                PasswordHash = "test", IsActive = true, CreatedAt = now, UpdatedAt = now
            });
            setupContext.Workspaces.Add(new Workspace
            {
                Id = workspaceId, OwnerId = userId, Name = "Task Sequence Workspace",
                Slug = $"task-sequence-{Guid.NewGuid():N}", CreatedAt = now, UpdatedAt = now
            });
            setupContext.WorkspaceMembers.Add(new WorkspaceMember
            {
                WorkspaceId = workspaceId, UserId = userId, WorkspaceRole = "OWNER",
                IsActive = true, JoinedAt = now
            });
            setupContext.Projects.Add(new Project
            {
                Id = projectId, WorkspaceId = workspaceId, CreatorId = userId,
                Name = "Task Sequence Project", Identifier = "SEQ", Status = true,
                IssueSequence = 0, CreatedAt = now, UpdatedAt = now
            });
            setupContext.ProjectMembers.Add(new ProjectMember
            {
                ProjectId = projectId, UserId = userId, Status = true, JoinedAt = now,
                ProjectRole = "PM"
            });
            await setupContext.SaveChangesAsync();

            var results = await Task.WhenAll(Enumerable.Range(1, 8).Select(index => Task.Run(async () =>
            {
                await using var context = CreateContext(databaseName);
                var service = new WorkTaskService(context, Mock.Of<IGamificationService>());
                return await service.CreateAsync(userId, new CreateWorkTaskDto
                {
                    ProjectId = projectId,
                    Title = $"Concurrent task {index}",
                    StatusName = "TO DO",
                    Priority = 3
                });
            })));

            results.Select(result => result.SequenceId).Should().OnlyHaveUniqueItems();
            results.Select(result => result.SequenceId).Should().BeEquivalentTo(
                Enumerable.Range(1, 8).Select(sequence => $"SEQ-{sequence}"));

            await using var verifyContext = CreateContext(databaseName);
            (await verifyContext.Projects.Where(project => project.Id == projectId)
                .Select(project => project.IssueSequence).SingleAsync()).Should().Be(8);
            (await verifyContext.WorkTasks.CountAsync(task => task.ProjectId == projectId)).Should().Be(8);
        }
        finally
        {
            await setupContext.Database.EnsureDeletedAsync();
        }
    }

    private static ApplicationDbContext CreateContext(string databaseName)
        => new(new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseSqlServer(SqlServerTestConnection.Build(databaseName), sql => sql.EnableRetryOnFailure())
            .Options);

    private static async Task<bool> IsAvailableAsync()
    {
        try
        {
            await using var context = CreateContext($"TaskManagement_Probe_{Guid.NewGuid():N}");
            return await context.Database.CanConnectAsync();
        }
        catch
        {
            return false;
        }
    }
}
