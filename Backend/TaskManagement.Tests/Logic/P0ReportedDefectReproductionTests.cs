using System.Security.Claims;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Options;
using Moq;
using TaskManagement.API.Controllers;
using TaskManagement.Application.Common;
using TaskManagement.Application.Configuration;
using TaskManagement.Application.DTOs.AI;
using TaskManagement.Application.DTOs.Auth;
using TaskManagement.Application.DTOs.Sprint;
using TaskManagement.Application.DTOs.WorkTask;
using TaskManagement.Application.Interfaces;
using TaskManagement.Domain.Entities;
using TaskManagement.Infrastructure.Data;
using TaskManagement.Infrastructure.Services;

namespace TaskManagement.Tests.Logic;

/// <summary>
/// Phase P0-00 evidence tests. Tests in this class intentionally encode the
/// documented expected behavior; a failure is evidence that the reported
/// defect is still reproducible. Run separately with Category=P0Reproduction.
/// </summary>
[Trait("Category", "P0Reproduction")]
public sealed class P0ReportedDefectReproductionTests
{
    private static ApplicationDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .ConfigureWarnings(warning => warning.Ignore(InMemoryEventId.TransactionIgnoredWarning))
            .Options;
        return new ApplicationDbContext(options);
    }

    [Fact]
    public void TC_AUTH_007_TooManyWrongOtpAttempts_LocksTheOtp()
    {
        using var cache = new MemoryCache(new MemoryCacheOptions());
        var service = CreateOtpService(cache);
        service.StoreOtp("user@example.com", "123456");

        for (var attempt = 0; attempt < 5; attempt++)
        {
            service.ValidateOtp("user@example.com", "000000").IsValid.Should().BeFalse();
        }

        service.ValidateOtp("user@example.com", "123456").IsValid.Should().BeFalse(
            "the OTP must be locked after the configured failed-attempt threshold");
    }

    [Fact]
    public async Task TC_AUTH_029_DeletedUser_CannotLogin()
    {
        await using var context = CreateContext();
        context.Users.Add(new User
        {
            Id = Guid.NewGuid(),
            Email = "deleted@example.com",
            FullName = "Deleted User",
            PasswordHash = BCrypt.Net.BCrypt.HashPassword("Password123!"),
            IsActive = true,
            IsDeleted = true,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        });
        await context.SaveChangesAsync();

        using var cache = new MemoryCache(new MemoryCacheOptions());
        var service = new AuthService(
            context,
            Mock.Of<IJwtService>(),
            Mock.Of<IConfiguration>(),
            CreateOtpService(cache),
            Mock.Of<IEmailService>());

        var action = () => service.LoginAsync(new LoginRequestDto
        {
            Email = "deleted@example.com",
            Password = "Password123!"
        });

        await action.Should().ThrowAsync<UnauthorizedAccessException>();
    }

    [Fact]
    public async Task TC_AUTH_029_InactiveUser_CannotRefreshToken()
    {
        await using var context = CreateContext();
        var userId = Guid.NewGuid();
        context.Users.Add(new User
        {
            Id = userId,
            Email = "inactive@example.com",
            FullName = "Inactive User",
            PasswordHash = "unused",
            IsActive = false,
            IsDeleted = false,
            RefreshToken = "current-refresh-token",
            RefreshTokenExpiryTime = DateTime.UtcNow.AddDays(1)
        });
        await context.SaveChangesAsync();

        var principal = new ClaimsPrincipal(new ClaimsIdentity(
            new[] { new Claim(ClaimTypes.NameIdentifier, userId.ToString()) },
            "ExpiredJwt"));
        var jwt = new Mock<IJwtService>();
        jwt.Setup(service => service.GetPrincipalFromExpiredToken("expired-access-token")).Returns(principal);
        jwt.Setup(service => service.GenerateAccessToken(It.IsAny<User>(), It.IsAny<IList<string>>())).Returns("new-access-token");
        jwt.Setup(service => service.GenerateRefreshToken()).Returns("new-refresh-token");

        using var cache = new MemoryCache(new MemoryCacheOptions());
        var service = new AuthService(
            context,
            jwt.Object,
            Mock.Of<IConfiguration>(),
            CreateOtpService(cache),
            Mock.Of<IEmailService>());

        var action = () => service.RefreshTokenAsync("expired-access-token", "current-refresh-token");

        await action.Should().ThrowAsync<UnauthorizedAccessException>();
    }

    [Fact]
    public async Task TC_GAMIFY_003_ActiveAssignment_DoesNotSkipRewardRollback()
    {
        await using var context = CreateContext();
        var projectId = Guid.NewGuid();
        var taskId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var statusId = Guid.NewGuid();

        context.Projects.Add(new Project
        {
            Id = projectId,
            Name = "Rewards",
            Identifier = "RWD",
            WorkspaceId = Guid.NewGuid(),
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        });
        context.TaskStatuses.Add(new TaskManagement.Domain.Entities.TaskStatus
        {
            Id = statusId,
            ProjectId = projectId,
            Name = "Done"
        });
        context.WorkTasks.Add(new WorkTask
        {
            Id = taskId,
            ProjectId = projectId,
            TaskStatusId = statusId,
            Title = "Assigned task",
            SequenceId = "RWD-1",
            ReporterId = userId,
            AssignedUserId = userId,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        });
        context.TaskAssignments.Add(new TaskAssignment
        {
            WorkTaskId = taskId,
            UserId = userId,
            Status = true
        });
        context.UserWallets.Add(new UserWallet { UserId = userId, TotalPoints = 10, Level = 1 });
        context.PointTransactions.Add(new PointTransaction
        {
            Id = Guid.NewGuid(),
            UserWalletUserId = userId,
            WorkTaskId = taskId,
            Amount = 10,
            TransactionType = "TaskBaseReward",
            Reason = "original reward",
            CreatedAt = DateTime.UtcNow
        });
        await context.SaveChangesAsync();

        await new GamificationService(context)
            .ApplyStatusChangeRewardsAsync(taskId, userId, "Done", "In Progress");

        (await context.UserWallets.SingleAsync()).TotalPoints.Should().Be(0);
        (await context.PointTransactions.CountAsync(item => item.TransactionType == "TaskRewardRollback"))
            .Should().Be(1);
    }

    [Fact]
    public async Task TC_SPRINT_020_CrossProjectTargetSprint_IsRejected()
    {
        await using var context = CreateContext();
        var sourceProjectId = Guid.NewGuid();
        var targetProjectId = Guid.NewGuid();
        var sourceSprintId = Guid.NewGuid();
        var targetSprintId = Guid.NewGuid();

        context.Projects.AddRange(
            new Project { Id = sourceProjectId, Name = "Source", Identifier = "SRC", WorkspaceId = Guid.NewGuid(), CreatedAt = DateTime.UtcNow },
            new Project { Id = targetProjectId, Name = "Target", Identifier = "TGT", WorkspaceId = Guid.NewGuid(), CreatedAt = DateTime.UtcNow });
        context.Sprints.AddRange(
            new Sprint { Id = sourceSprintId, ProjectId = sourceProjectId, Name = "Source sprint", Status = true, StartDate = DateTime.UtcNow.AddDays(-1), EndDate = DateTime.UtcNow.AddDays(5) },
            new Sprint { Id = targetSprintId, ProjectId = targetProjectId, Name = "Target sprint", Status = false, StartDate = DateTime.UtcNow.AddDays(6), EndDate = DateTime.UtcNow.AddDays(12) });
        await context.SaveChangesAsync();

        var action = () => new SprintService(context).CloseAsync(
            sourceSprintId,
            new CloseSprintDto { TargetSprintId = targetSprintId },
            Guid.NewGuid());

        await action.Should().ThrowAsync<ArgumentException>()
            .WithMessage("*cùng dự án*");
    }

    [Fact]
    public async Task TC_AI_002_DeveloperWithAdminSystemRole_StillCannotCreateCycle()
    {
        await using var context = CreateContext();
        var workspaceId = Guid.NewGuid();
        var projectId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var roleId = Guid.NewGuid();

        context.Users.Add(new User { Id = userId, Email = "developer@example.com", FullName = "Developer", PasswordHash = "unused", IsActive = true });
        context.Roles.Add(new Role { Id = roleId, Name = "admin" });
        context.UserRoles.Add(new UserRole { UserId = userId, RoleId = roleId });
        context.Workspaces.Add(new Workspace { Id = workspaceId, Name = "Workspace", Slug = "workspace", OwnerId = Guid.NewGuid() });
        context.WorkspaceMembers.Add(new WorkspaceMember { WorkspaceId = workspaceId, UserId = userId, WorkspaceRole = "MEMBER", IsActive = true });
        context.Projects.Add(new Project { Id = projectId, WorkspaceId = workspaceId, Name = "Project", Identifier = "PRJ", CreatorId = Guid.NewGuid() });
        context.ProjectMembers.Add(new ProjectMember { ProjectId = projectId, UserId = userId, ProjectRole = "Developer", Status = true });
        await context.SaveChangesAsync();

        var controller = new AiController(
            Mock.Of<IAiService>(),
            Mock.Of<IAiAttachmentService>(),
            Mock.Of<IWorkTaskService>(),
            Mock.Of<IProjectService>(),
            Mock.Of<IGoalService>(),
            context,
            new ResourceAuthorizationService(context))
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext
                {
                    User = new ClaimsPrincipal(new ClaimsIdentity(
                        new[] { new Claim(ClaimTypes.NameIdentifier, userId.ToString()) },
                        "TestAuth"))
                }
            }
        };

        var result = await controller.PreviewAction(new AiExecuteActionRequestDto
        {
            Type = "create_cycle",
            WorkspaceId = workspaceId,
            ProjectId = projectId,
            IdempotencyKey = "tc-ai-002",
            Payload = new Dictionary<string, object?> { ["name"] = "Unauthorized cycle" }
        });

        result.Should().BeOfType<ObjectResult>().Which.StatusCode.Should().Be(StatusCodes.Status403Forbidden);
        (await context.Sprints.CountAsync()).Should().Be(0);
    }

    [Theory]
    [InlineData("PM")]
    [InlineData("pm")]
    [InlineData("Pm")]
    public void TC_RBAC_009_ProjectRoleNormalization_IsCaseInsensitive(string role)
    {
        ProjectExecutionRuleHelper.NormalizeProjectRole(role).Should().Be("pm");
    }

    [Fact]
    public async Task TC_TASK_038_MissingAuthenticatedReporter_IsRejectedBeforePersistence()
    {
        await using var context = CreateContext();
        var projectId = Guid.NewGuid();
        var statusId = Guid.NewGuid();
        context.Projects.Add(new Project
        {
            Id = projectId,
            Name = "Project",
            Identifier = "PRJ",
            WorkspaceId = Guid.NewGuid(),
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        });
        context.TaskStatuses.Add(new TaskManagement.Domain.Entities.TaskStatus { Id = statusId, ProjectId = projectId, Name = "To Do" });
        await context.SaveChangesAsync();

        var service = new WorkTaskService(context, Mock.Of<IGamificationService>());
        try
        {
            await service.CreateAsync(Guid.Empty, new CreateWorkTaskDto
            {
                ProjectId = projectId,
                Title = "Missing reporter",
                StatusName = "To Do"
            });
        }
        catch
        {
            // The reported defect is that persistence is attempted with Guid.Empty.
            // A later mapping exception does not make that mutation safe.
        }

        (await context.WorkTasks.CountAsync(task => task.ReporterId == Guid.Empty)).Should().Be(0,
            "missing authentication must be rejected before any task is persisted");
    }

    [Fact]
    public async Task TC_WS_006_InactiveMember_CannotReadWorkspaceMembers()
    {
        await using var context = CreateContext();
        var workspaceId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        context.Users.Add(new User { Id = userId, Email = "inactive@example.com", FullName = "Inactive", PasswordHash = "unused", IsActive = true });
        context.Workspaces.Add(new Workspace { Id = workspaceId, Name = "Workspace", Slug = "ws", OwnerId = userId });
        context.WorkspaceMembers.Add(new WorkspaceMember { WorkspaceId = workspaceId, UserId = userId, WorkspaceRole = "MEMBER", IsActive = false });
        await context.SaveChangesAsync();

        var controller = new WorkspacesController(context, new ResourceAuthorizationService(context))
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext
                {
                    User = new ClaimsPrincipal(new ClaimsIdentity(
                        new[] { new Claim(ClaimTypes.NameIdentifier, userId.ToString()) },
                        "TestAuth"))
                }
            }
        };

        var result = await controller.GetMembers(workspaceId);

        result.Should().BeOfType<ObjectResult>().Which.StatusCode.Should().Be(StatusCodes.Status403Forbidden);
    }

    [Fact]
    public async Task TC_RBAC_045_ExpiredSprintWithStaleActiveFlag_BlocksTaskUpdate()
    {
        await using var context = CreateContext();
        var userId = Guid.NewGuid();
        var projectId = Guid.NewGuid();
        var statusId = Guid.NewGuid();
        var taskTypeId = Guid.NewGuid();
        var sprintId = Guid.NewGuid();
        var taskId = Guid.NewGuid();
        context.Users.Add(new User { Id = userId, Email = "pm@example.com", FullName = "PM", PasswordHash = "unused", IsActive = true });
        context.Projects.Add(new Project { Id = projectId, Name = "Project", Identifier = "PRJ", WorkspaceId = Guid.NewGuid(), CreatorId = userId });
        context.ProjectMembers.Add(new ProjectMember { ProjectId = projectId, UserId = userId, ProjectRole = "PM", Status = true });
        context.TaskStatuses.Add(new TaskManagement.Domain.Entities.TaskStatus { Id = statusId, ProjectId = projectId, Name = "To Do" });
        context.TaskTypes.Add(new TaskType { Id = taskTypeId, ProjectId = projectId, Name = "Task" });
        context.Sprints.Add(new Sprint
        {
            Id = sprintId,
            ProjectId = projectId,
            Name = "Expired",
            Status = true,
            StartDate = DateTime.UtcNow.AddDays(-20),
            EndDate = DateTime.UtcNow.AddDays(-1)
        });
        context.WorkTasks.Add(new WorkTask
        {
            Id = taskId,
            ProjectId = projectId,
            WorkspaceId = context.Projects.Local.Single().WorkspaceId,
            SprintId = sprintId,
            TaskTypeId = taskTypeId,
            TaskStatusId = statusId,
            Title = "Locked task",
            ReporterId = userId,
            SequenceId = "PRJ-1",
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        });
        await context.SaveChangesAsync();

        var service = new WorkTaskService(context, Mock.Of<IGamificationService>());
        var rowVersion = (await context.WorkTasks.AsNoTracking().SingleAsync()).RowVersion;
        var action = () => service.UpdateAsync(taskId, userId, new UpdateWorkTaskDto
        {
            Title = "Should remain locked",
            RowVersion = rowVersion
        });

        await action.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*Sprint đã đóng*");
    }

    private static OtpService CreateOtpService(IMemoryCache cache)
    {
        return new OtpService(cache, Options.Create(new OtpSecurityOptions()));
    }
}
