using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Moq;
using TaskManagement.Application.DTOs.AI;
using TaskManagement.Application.DTOs.WorkTask;
using TaskManagement.Application.Interfaces;
using TaskManagement.Domain.Entities;
using TaskManagement.Infrastructure.AI;
using TaskManagement.Infrastructure.Data;
using TaskManagement.Infrastructure.Services;

namespace TaskManagement.Tests.Logic;

public sealed class AiAuthorizationSweepTests
{
    [Fact]
    public async Task PlatformAdminWithoutProjectMembership_CannotCreateBacklogItemsInForeignProject()
    {
        await using var context = CreateContext();
        var platformAdminId = Guid.NewGuid();
        var projectOwnerId = Guid.NewGuid();
        var workspaceId = Guid.NewGuid();
        var projectId = Guid.NewGuid();
        var role = new Role { Id = Guid.NewGuid(), Name = "Admin" };
        var owner = NewUser(projectOwnerId, "owner");
        var platformAdmin = NewUser(platformAdminId, "platform-admin");
        platformAdmin.UserRoles.Add(new UserRole { UserId = platformAdminId, RoleId = role.Id, Role = role });

        context.Roles.Add(role);
        context.Users.AddRange(platformAdmin, owner);
        context.Workspaces.Add(new Workspace
        {
            Id = workspaceId,
            Name = "Foreign workspace",
            Slug = "foreign-workspace",
            OwnerId = projectOwnerId,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        });
        context.Projects.Add(new Project
        {
            Id = projectId,
            WorkspaceId = workspaceId,
            Name = "Foreign project",
            Identifier = "FOR",
            CreatorId = projectOwnerId,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        });
        context.ProjectMembers.Add(new ProjectMember
        {
            ProjectId = projectId,
            UserId = projectOwnerId,
            ProjectRole = "PM",
            Status = true,
            JoinedAt = DateTime.UtcNow
        });
        await context.SaveChangesAsync();

        var workTaskService = new Mock<IWorkTaskService>();
        workTaskService
            .Setup(service => service.CreateAsync(It.IsAny<Guid>(), It.IsAny<CreateWorkTaskDto>()))
            .ReturnsAsync(new WorkTaskResponseDto { ProjectId = projectId, Title = "created" });
        var service = CreateService(context, workTaskService.Object);

        var action = () => service.CreateBacklogItemsFromAnalysisAsync(platformAdminId, new AiCreateBacklogFromAnalysisRequestDto
        {
            ProjectId = projectId,
            Repository = "DinhTuanKhoiTB01696/QuanLyCongViec",
            SelectedItems =
            [
                new AiRepositoryBacklogItemDto
                {
                    Title = "Foreign backlog item",
                    SuggestedHours = 2,
                    Reasoning = "test"
                }
            ]
        });

        await action.Should().ThrowAsync<UnauthorizedAccessException>();
        workTaskService.Verify(
            service => service.CreateAsync(It.IsAny<Guid>(), It.IsAny<CreateWorkTaskDto>()),
            Times.Never);
    }

    private static GeminiAiService CreateService(ApplicationDbContext context, IWorkTaskService workTaskService)
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ZenMux:ApiKey"] = "test-key",
                ["ZenMux:BaseUrl"] = "https://zenmux.test/api/v1",
                ["ZenMux:Model"] = "test-model"
            })
            .Build();
        return new GeminiAiService(
            context,
            new HttpClient(),
            new ZenMuxAiClient(new HttpClient(), configuration),
            workTaskService,
            Mock.Of<IAiCreditUsageService>(),
            configuration);
    }

    private static ApplicationDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new ApplicationDbContext(options);
    }

    private static User NewUser(Guid id, string name) => new()
    {
        Id = id,
        Email = $"{name}@test.local",
        FullName = name,
        PasswordHash = "test-only",
        IsActive = true,
        CreatedAt = DateTime.UtcNow,
        UpdatedAt = DateTime.UtcNow
    };
}
