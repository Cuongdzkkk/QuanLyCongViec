using System.Security.Claims;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Moq;
using TaskManagement.API.Controllers;
using TaskManagement.Application.DTOs.AI;
using TaskManagement.Application.Interfaces;
using TaskManagement.Domain.Entities;
using TaskManagement.Infrastructure.Data;
using TaskManagement.Infrastructure.Services;

namespace TaskManagement.Tests.Logic;

public sealed class AiConversationAuthorizationTests
{
    [Fact]
    public async Task ActiveMemberWithoutWorkspaceManageCanCreateOwnConversation()
    {
        await using var context = CreateContext(out var workspaceId, out var ownerId, out var memberId);
        context.WorkspaceMembers.Add(new WorkspaceMember
        {
            WorkspaceId = workspaceId,
            UserId = memberId,
            WorkspaceRole = "MEMBER",
            IsActive = true
        });
        await context.SaveChangesAsync();

        var controller = CreateController(context, memberId);
        var result = await controller.CreateConversation(new AiController.AiConversationCreateRequest
        {
            WorkspaceId = workspaceId,
            Title = "Member conversation"
        });

        result.Should().BeOfType<OkObjectResult>();
        (await context.AiConversations.SingleAsync()).Should().Match<AiConversation>(conversation =>
            conversation.UserId == memberId && conversation.WorkspaceId == workspaceId);
        (await context.Workspaces.SingleAsync(workspace => workspace.Id == workspaceId)).OwnerId.Should().Be(ownerId);
    }

    [Theory]
    [InlineData(true, false)]
    [InlineData(false, true)]
    public async Task NonMemberOrInactiveMemberGets403(bool addMembership, bool membershipActive)
    {
        await using var context = CreateContext(out var workspaceId, out _, out var memberId);
        if (addMembership)
        {
            context.WorkspaceMembers.Add(new WorkspaceMember
            {
                WorkspaceId = workspaceId,
                UserId = memberId,
                WorkspaceRole = "MEMBER",
                IsActive = membershipActive
            });
            await context.SaveChangesAsync();
        }

        var controller = CreateController(context, memberId);
        var result = await controller.CreateConversation(new AiController.AiConversationCreateRequest
        {
            WorkspaceId = workspaceId,
            Title = "Denied conversation"
        });

        result.Should().BeOfType<ObjectResult>().Which.StatusCode.Should().Be(StatusCodes.Status403Forbidden);
        (await context.AiConversations.CountAsync()).Should().Be(0);
    }

    [Fact]
    public async Task OtherUsersConversationIsNotReadable()
    {
        await using var context = CreateContext(out var workspaceId, out _, out var memberId);
        var otherUserId = Guid.NewGuid();
        context.Users.Add(new User
        {
            Id = otherUserId,
            Email = "other@example.com",
            FullName = "Other User",
            PasswordHash = "unused"
        });
        context.AiConversations.Add(new AiConversation
        {
            Id = Guid.NewGuid(),
            UserId = otherUserId,
            WorkspaceId = workspaceId,
            Title = "Private conversation",
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        });
        await context.SaveChangesAsync();

        var conversationId = await context.AiConversations.Select(conversation => conversation.Id).SingleAsync();
        var result = await CreateController(context, memberId).GetConversation(conversationId);

        result.Should().BeOfType<NotFoundObjectResult>();
    }

    private static AiController CreateController(ApplicationDbContext context, Guid userId)
    {
        var controller = new AiController(
            Mock.Of<IAiService>(),
            Mock.Of<IAiCreditUsageService>(),
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

        return controller;
    }

    private static ApplicationDbContext CreateContext(out Guid workspaceId, out Guid ownerId, out Guid memberId)
    {
        workspaceId = Guid.NewGuid();
        ownerId = Guid.NewGuid();
        memberId = Guid.NewGuid();
        var context = new ApplicationDbContext(new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options);

        context.Users.AddRange(
            new User { Id = ownerId, Email = "owner@example.com", FullName = "Owner", PasswordHash = "unused" },
            new User { Id = memberId, Email = "member@example.com", FullName = "Member", PasswordHash = "unused" });
        context.Workspaces.Add(new Workspace
        {
            Id = workspaceId,
            OwnerId = ownerId,
            Name = "Workspace",
            Slug = "workspace",
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        });
        context.WorkspaceMembers.Add(new WorkspaceMember
        {
            WorkspaceId = workspaceId,
            UserId = ownerId,
            WorkspaceRole = "OWNER",
            IsActive = true
        });
        context.SaveChanges();
        return context;
    }
}
