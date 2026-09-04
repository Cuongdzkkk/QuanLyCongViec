using System.Security.Claims;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using Moq;
using TaskManagement.API.Controllers;
using TaskManagement.API.Hubs;
using TaskManagement.Infrastructure.Data;
using TaskManagement.Infrastructure.Services;

namespace TaskManagement.Tests.Logic;

public sealed class GoalsAuthorizationSweepTests
{
    [Fact]
    public async Task GetById_DoesNotReturnGoalFromAnotherWorkspace()
    {
        await using var context = CreateContext();
        var userA = Guid.NewGuid();
        var userB = Guid.NewGuid();
        var workspaceA = Guid.NewGuid();
        var workspaceB = Guid.NewGuid();
        var goalB = Guid.NewGuid();
        Seed(context, userA, userB, workspaceA, workspaceB, goalB);

        var controller = CreateController(context, userA);

        var result = await controller.GetById(workspaceA, goalB);

        result.Should().BeOfType<NotFoundResult>();
    }

    [Fact]
    public async Task AddRisk_DoesNotMutateGoalFromAnotherWorkspace()
    {
        await using var context = CreateContext();
        var userA = Guid.NewGuid();
        var userB = Guid.NewGuid();
        var workspaceA = Guid.NewGuid();
        var workspaceB = Guid.NewGuid();
        var goalB = Guid.NewGuid();
        Seed(context, userA, userB, workspaceA, workspaceB, goalB);

        var controller = CreateController(context, userA);

        var result = await controller.AddRisk(workspaceA, goalB, new { text = "unauthorized" });

        result.Should().BeOfType<NotFoundResult>();
        (await context.GoalRisks.CountAsync()).Should().Be(0);
    }

    private static ApplicationDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new ApplicationDbContext(options);
    }

    private static GoalsController CreateController(ApplicationDbContext context, Guid userId)
    {
        var httpContext = new DefaultHttpContext
        {
            User = new ClaimsPrincipal(new ClaimsIdentity(
                [new Claim(ClaimTypes.NameIdentifier, userId.ToString())],
                "TestAuth"))
        };
        var accessor = new HttpContextAccessor { HttpContext = httpContext };
        var controller = new GoalsController(
            new GoalService(context, accessor),
            Mock.Of<IHubContext<KanbanHub>>(),
            context,
            new ResourceAuthorizationService(context));
        controller.ControllerContext = new ControllerContext { HttpContext = httpContext };
        return controller;
    }

    private static void Seed(
        ApplicationDbContext context,
        Guid userA,
        Guid userB,
        Guid workspaceA,
        Guid workspaceB,
        Guid goalB)
    {
        var now = DateTime.UtcNow;
        var userAEntity = NewUser(userA, "user-a");
        var userBEntity = NewUser(userB, "user-b");
        var workspaceAEntity = new Domain.Entities.Workspace
        {
            Id = workspaceA,
            Name = "Workspace A",
            Slug = "workspace-a",
            OwnerId = userA,
            Owner = userAEntity,
            CreatedAt = now,
            UpdatedAt = now
        };
        var workspaceBEntity = new Domain.Entities.Workspace
        {
            Id = workspaceB,
            Name = "Workspace B",
            Slug = "workspace-b",
            OwnerId = userB,
            Owner = userBEntity,
            CreatedAt = now,
            UpdatedAt = now
        };
        context.Users.AddRange(userAEntity, userBEntity);
        context.Workspaces.AddRange(workspaceAEntity, workspaceBEntity);
        context.WorkspaceMembers.AddRange(
            new Domain.Entities.WorkspaceMember
            {
                WorkspaceId = workspaceA,
                UserId = userA,
                Workspace = workspaceAEntity,
                User = userAEntity,
                WorkspaceRole = "MEMBER",
                IsActive = true,
                JoinedAt = now
            },
            new Domain.Entities.WorkspaceMember
            {
                WorkspaceId = workspaceB,
                UserId = userB,
                Workspace = workspaceBEntity,
                User = userBEntity,
                WorkspaceRole = "MEMBER",
                IsActive = true,
                JoinedAt = now
            });
        context.Goals.Add(new Domain.Entities.Goal
        {
            Id = goalB,
            WorkspaceId = workspaceB,
            OwnerId = userB,
            Workspace = workspaceBEntity,
            Owner = userBEntity,
            Title = "Private goal B",
            CreatedAt = now,
            UpdatedAt = now
        });
        context.SaveChanges();
    }

    private static Domain.Entities.User NewUser(Guid id, string name) => new()
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
