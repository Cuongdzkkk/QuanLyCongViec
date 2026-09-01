using System.Security.Claims;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Moq;
using System.Text.Json;
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

    [Fact]
    public async Task ActiveMemberWithoutWorkspaceManageCanListOwnConversations()
    {
        await using var context = CreateContext(out var workspaceId, out _, out var memberId);
        AddActiveMember(context, workspaceId, memberId);
        var ownConversationId = Guid.NewGuid();
        var otherConversationId = Guid.NewGuid();
        context.AiConversations.AddRange(
            new AiConversation
            {
                Id = ownConversationId,
                UserId = memberId,
                WorkspaceId = workspaceId,
                Title = "Own conversation",
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            },
            new AiConversation
            {
                Id = otherConversationId,
                UserId = Guid.NewGuid(),
                WorkspaceId = workspaceId,
                Title = "Other conversation",
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            });
        await context.SaveChangesAsync();

        var result = await CreateController(context, memberId).GetConversations(workspaceId);

        var payload = JsonSerializer.Serialize(result.Should().BeOfType<OkObjectResult>().Which.Value);
        payload.Should().Contain(ownConversationId.ToString());
        payload.Should().NotContain(otherConversationId.ToString());
    }

    [Fact]
    public async Task NonMemberCannotListConversationsAndGets403()
    {
        await using var context = CreateContext(out var workspaceId, out _, out var memberId);

        var result = await CreateController(context, memberId).GetConversations(workspaceId);

        result.Should().BeOfType<ObjectResult>().Which.StatusCode.Should().Be(StatusCodes.Status403Forbidden);
    }

    [Fact]
    public async Task InactiveMemberCannotListConversationsAndGets403()
    {
        await using var context = CreateContext(out var workspaceId, out _, out var memberId);
        AddInactiveMember(context, workspaceId, memberId);
        await context.SaveChangesAsync();

        var result = await CreateController(context, memberId).GetConversations(workspaceId);

        result.Should().BeOfType<ObjectResult>().Which.StatusCode.Should().Be(StatusCodes.Status403Forbidden);
    }

    [Fact]
    public async Task ActiveMemberWithoutWorkspaceManageCanSaveOwnConversation()
    {
        await using var context = CreateContext(out var workspaceId, out _, out var memberId);
        AddActiveMember(context, workspaceId, memberId);
        var conversation = new AiConversation
        {
            Id = Guid.NewGuid(),
            UserId = memberId,
            WorkspaceId = workspaceId,
            Title = "Before save",
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
        context.AiConversations.Add(conversation);
        await context.SaveChangesAsync();

        var result = await CreateController(context, memberId).SaveConversation(
            conversation.Id,
            new AiController.AiConversationSaveRequest
            {
                Title = "After save",
                Messages = JsonSerializer.Deserialize<JsonElement>("[{\"role\":\"user\",\"content\":\"saved\"}]")
            });

        result.Should().BeOfType<OkObjectResult>();
        var saved = await context.AiConversations.SingleAsync(item => item.Id == conversation.Id);
        saved.Title.Should().Be("After save");
        saved.MessagesJson.Should().Contain("saved");
    }

    [Fact]
    public async Task NonMemberCannotSaveConversationAndGets403()
    {
        await using var context = CreateContext(out var workspaceId, out _, out var memberId);
        var conversation = new AiConversation
        {
            Id = Guid.NewGuid(),
            UserId = memberId,
            WorkspaceId = workspaceId,
            Title = "Private conversation",
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
        context.AiConversations.Add(conversation);
        await context.SaveChangesAsync();

        var result = await CreateController(context, memberId).SaveConversation(
            conversation.Id,
            new AiController.AiConversationSaveRequest
            {
                Messages = JsonSerializer.Deserialize<JsonElement>("[]")
            });

        result.Should().BeOfType<ObjectResult>().Which.StatusCode.Should().Be(StatusCodes.Status403Forbidden);
    }

    [Fact]
    public async Task InactiveMemberCannotSaveConversationAndGets403()
    {
        await using var context = CreateContext(out var workspaceId, out _, out var memberId);
        AddInactiveMember(context, workspaceId, memberId);
        var conversation = AddConversation(context, workspaceId, memberId, "Inactive conversation");
        await context.SaveChangesAsync();

        var result = await CreateController(context, memberId).SaveConversation(
            conversation.Id,
            new AiController.AiConversationSaveRequest
            {
                Messages = JsonSerializer.Deserialize<JsonElement>("[]")
            });

        result.Should().BeOfType<ObjectResult>().Which.StatusCode.Should().Be(StatusCodes.Status403Forbidden);
    }

    [Fact]
    public async Task OtherUsersConversationCannotBeSaved()
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
        var conversation = new AiConversation
        {
            Id = Guid.NewGuid(),
            UserId = otherUserId,
            WorkspaceId = workspaceId,
            Title = "Private conversation",
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
        context.AiConversations.Add(conversation);
        await context.SaveChangesAsync();

        var result = await CreateController(context, memberId).SaveConversation(
            conversation.Id,
            new AiController.AiConversationSaveRequest
            {
                Messages = JsonSerializer.Deserialize<JsonElement>("[]")
            });

        result.Should().BeOfType<NotFoundObjectResult>();
        (await context.AiConversations.SingleAsync(item => item.Id == conversation.Id)).Title.Should().Be("Private conversation");
    }

    [Fact]
    public async Task ActiveMemberWithoutWorkspaceManageCanReadOwnConversation()
    {
        await using var context = CreateContext(out var workspaceId, out _, out var memberId);
        AddActiveMember(context, workspaceId, memberId);
        var conversation = new AiConversation
        {
            Id = Guid.NewGuid(),
            UserId = memberId,
            WorkspaceId = workspaceId,
            Title = "Readable conversation",
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
        context.AiConversations.Add(conversation);
        await context.SaveChangesAsync();

        var result = await CreateController(context, memberId).GetConversation(conversation.Id);

        result.Should().BeOfType<OkObjectResult>();
    }

    [Fact]
    public async Task ActiveMemberWithoutWorkspaceManageCanRenameOwnConversation()
    {
        await using var context = CreateContext(out var workspaceId, out _, out var memberId);
        AddActiveMember(context, workspaceId, memberId);
        var conversation = AddConversation(context, workspaceId, memberId, "Before rename");
        await context.SaveChangesAsync();

        var result = await CreateController(context, memberId).RenameConversation(
            conversation.Id,
            new AiController.AiConversationRenameRequest { Title = "After rename" });

        result.Should().BeOfType<OkObjectResult>();
        (await context.AiConversations.SingleAsync(item => item.Id == conversation.Id)).Title.Should().Be("After rename");
    }

    [Fact]
    public async Task ActiveMemberWithoutWorkspaceManageCanDeleteOwnConversation()
    {
        await using var context = CreateContext(out var workspaceId, out _, out var memberId);
        AddActiveMember(context, workspaceId, memberId);
        var conversation = AddConversation(context, workspaceId, memberId, "To delete");
        await context.SaveChangesAsync();

        var result = await CreateController(context, memberId).DeleteConversation(conversation.Id);

        result.Should().BeOfType<OkObjectResult>();
        (await context.AiConversations.CountAsync()).Should().Be(0);
    }

    [Fact]
    public async Task ActiveMemberCanSendContextMessageWithWorkspaceReadAccess()
    {
        await using var context = CreateContext(out var workspaceId, out _, out var memberId);
        AddActiveMember(context, workspaceId, memberId);
        await context.SaveChangesAsync();
        var aiService = new Mock<IAiService>();
        aiService
            .Setup(service => service.ContextChatAsync(memberId, It.IsAny<AiContextChatRequestDto>()))
            .ReturnsAsync(new AiContextChatResponseDto { Answer = "Read-only response" });

        var result = await CreateController(context, memberId, aiService.Object).ContextChat(new AiContextChatRequestDto
        {
            WorkspaceId = workspaceId,
            Message = "Summarize this workspace"
        });

        result.Should().BeOfType<OkObjectResult>();
        aiService.Verify(service => service.ContextChatAsync(memberId, It.IsAny<AiContextChatRequestDto>()), Times.Once);
    }

    [Fact]
    public async Task NonMemberCannotSendContextMessageAndGets403()
    {
        await using var context = CreateContext(out var workspaceId, out _, out var memberId);
        var aiService = new Mock<IAiService>();

        var result = await CreateController(context, memberId, aiService.Object).ContextChat(new AiContextChatRequestDto
        {
            WorkspaceId = workspaceId,
            Message = "Read this workspace"
        });

        result.Should().BeOfType<ObjectResult>().Which.StatusCode.Should().Be(StatusCodes.Status403Forbidden);
        aiService.Verify(service => service.ContextChatAsync(It.IsAny<Guid>(), It.IsAny<AiContextChatRequestDto>()), Times.Never);
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

    private static AiController CreateController(ApplicationDbContext context, Guid userId, IAiService? aiService = null)
    {
        var controller = new AiController(
            aiService ?? Mock.Of<IAiService>(),
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

    private static void AddActiveMember(ApplicationDbContext context, Guid workspaceId, Guid userId)
    {
        context.WorkspaceMembers.Add(new WorkspaceMember
        {
            WorkspaceId = workspaceId,
            UserId = userId,
            WorkspaceRole = "MEMBER",
            IsActive = true
        });
    }

    private static void AddInactiveMember(ApplicationDbContext context, Guid workspaceId, Guid userId)
    {
        context.WorkspaceMembers.Add(new WorkspaceMember
        {
            WorkspaceId = workspaceId,
            UserId = userId,
            WorkspaceRole = "MEMBER",
            IsActive = false
        });
    }

    private static AiConversation AddConversation(ApplicationDbContext context, Guid workspaceId, Guid userId, string title)
    {
        var conversation = new AiConversation
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            WorkspaceId = workspaceId,
            Title = title,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
        context.AiConversations.Add(conversation);
        return conversation;
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
