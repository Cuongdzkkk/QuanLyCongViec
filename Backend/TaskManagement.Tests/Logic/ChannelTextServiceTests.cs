using System.Security.Claims;
using System.Text.Json;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Moq;
using TaskManagement.API.Controllers;
using TaskManagement.API.Services;
using TaskManagement.Application.DTOs.Collaboration;
using TaskManagement.Application.Interfaces;
using TaskManagement.Domain.Entities;
using TaskManagement.Infrastructure.Data;
using TaskManagement.Infrastructure.Services;

namespace TaskManagement.Tests.Logic;

public sealed class ChannelTextServiceTests
{
    [Fact]
    public async Task MemberCanSendAndSenderComesFromAuthenticatedUser()
    {
        await using var context = CreateContext();
        var seed = await ChannelSeed.InsertAsync(context);
        var service = CreateService(context);

        var result = await service.SendAsync(seed.ChannelAId, seed.UserAId, "  Xin chào SprintA  ");

        result.Content.Should().Be("Xin chào SprintA");
        result.Sender.UserId.Should().Be(seed.UserAId);
        var stored = await context.ChannelMessages.SingleAsync(message => message.Id == result.MessageId);
        stored.SenderId.Should().Be(seed.UserAId);
        stored.CollaborationChannelId.Should().Be(seed.ChannelAId);
        stored.LegacyDepartmentId.Should().BeNull();
    }

    [Fact]
    public async Task NewContextCanReadPersistedMessageAndMemberBMayRead()
    {
        var databaseName = $"channel-persistence-{Guid.NewGuid():N}";
        Guid channelId;
        Guid userAId;
        Guid userBId;
        await using (var write = CreateContext(databaseName))
        {
            var seed = await ChannelSeed.InsertAsync(write);
            channelId = seed.ChannelAId;
            userAId = seed.UserAId;
            userBId = seed.UserBId;
            await CreateService(write).SendAsync(channelId, userAId, "persisted");
        }

        await using var read = CreateContext(databaseName);
        var page = await CreateService(read).GetHistoryAsync(channelId, userBId, 1, 20);

        page.TotalCount.Should().Be(1);
        page.Items.Single().Content.Should().Be("persisted");
    }

    [Fact]
    public async Task OutsiderCannotReadOrSend()
    {
        await using var context = CreateContext();
        var seed = await ChannelSeed.InsertAsync(context);
        var service = CreateService(context);

        await service.Invoking(item => item.GetHistoryAsync(seed.ChannelAId, seed.OutsiderId, 1, 20))
            .Should().ThrowAsync<ChannelNotFoundException>();
        await service.Invoking(item => item.SendAsync(seed.ChannelAId, seed.OutsiderId, "blocked"))
            .Should().ThrowAsync<ChannelNotFoundException>();
    }

    [Fact]
    public async Task InactiveUserAndReadOnlyMemberCannotSend()
    {
        await using var context = CreateContext();
        var seed = await ChannelSeed.InsertAsync(context);
        var service = CreateService(context);

        await service.Invoking(item => item.SendAsync(seed.ChannelAId, seed.InactiveUserId, "blocked"))
            .Should().ThrowAsync<ChannelNotFoundException>();
        await service.Invoking(item => item.SendAsync(seed.ChannelAId, seed.UserBId, "blocked"))
            .Should().ThrowAsync<ChannelSendForbiddenException>();
    }

    [Fact]
    public async Task DeletedOrCrossWorkspaceChannelIsNotVisible()
    {
        await using var context = CreateContext();
        var seed = await ChannelSeed.InsertAsync(context);
        var service = CreateService(context);

        await service.Invoking(item => item.GetHistoryAsync(seed.DeletedChannelId, seed.UserAId, 1, 20))
            .Should().ThrowAsync<ChannelNotFoundException>();
        await service.Invoking(item => item.SendAsync(seed.DeletedChannelId, seed.UserAId, "blocked"))
            .Should().ThrowAsync<ChannelNotFoundException>();
        await service.Invoking(item => item.GetHistoryAsync(seed.ChannelBId, seed.UserAId, 1, 20))
            .Should().ThrowAsync<ChannelNotFoundException>();
    }

    [Fact]
    public async Task HistoryIsIsolatedByChannel()
    {
        await using var context = CreateContext();
        var seed = await ChannelSeed.InsertAsync(context);
        context.ChannelMessages.AddRange(
            Message(seed.ChannelAId, seed.UserAId, "A"),
            Message(seed.ChannelBId, seed.OutsiderId, "B"));
        await context.SaveChangesAsync();

        var page = await CreateService(context).GetHistoryAsync(seed.ChannelAId, seed.UserAId, 1, 20);

        page.Items.Select(item => item.Content).Should().Equal("A");
        page.TotalCount.Should().Be(1);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   \r\n  ")]
    public async Task EmptyContentIsRejected(string? content)
    {
        await using var context = CreateContext();
        var seed = await ChannelSeed.InsertAsync(context);

        await CreateService(context).Invoking(item => item.SendAsync(seed.ChannelAId, seed.UserAId, content))
            .Should().ThrowAsync<ArgumentException>();
        (await context.ChannelMessages.CountAsync()).Should().Be(0);
    }

    [Fact]
    public async Task OverLimitContentIsRejected()
    {
        await using var context = CreateContext();
        var seed = await ChannelSeed.InsertAsync(context);

        await CreateService(context).Invoking(item =>
                item.SendAsync(seed.ChannelAId, seed.UserAId, new string('x', 4001)))
            .Should().ThrowAsync<ArgumentException>();
    }

    [Fact]
    public async Task UnicodeAndHtmlAreStoredAsPlainText()
    {
        await using var context = CreateContext();
        var seed = await ChannelSeed.InsertAsync(context);
        const string content = "Tiếng Việt\n<script>alert('x')</script>";

        var result = await CreateService(context).SendAsync(seed.ChannelAId, seed.UserAId, content);

        result.Content.Should().Be(content);
        // The API returns JSON text; the frontend must continue rendering it with text interpolation.
        JsonSerializer.Serialize(result).Should().Contain("\\u003Cscript\\u003E");
    }

    [Fact]
    public async Task PaginationHasStableTieBreakerWithoutLossOrDuplicates()
    {
        await using var context = CreateContext();
        var seed = await ChannelSeed.InsertAsync(context);
        var timestamp = new DateTime(2026, 7, 28, 9, 0, 0, DateTimeKind.Utc);
        for (var index = 1; index <= 7; index++)
        {
            context.ChannelMessages.Add(new ChannelMessage
            {
                Id = Guid.Parse($"00000000-0000-0000-0000-{index:000000000000}"),
                CollaborationChannelId = seed.ChannelAId,
                SenderId = seed.UserAId,
                Content = $"message-{index}",
                SentAt = timestamp
            });
        }
        await context.SaveChangesAsync();
        var service = CreateService(context);

        var first = await service.GetHistoryAsync(seed.ChannelAId, seed.UserAId, 1, 3);
        var second = await service.GetHistoryAsync(seed.ChannelAId, seed.UserAId, 2, 3);
        var third = await service.GetHistoryAsync(seed.ChannelAId, seed.UserAId, 3, 3);
        var ids = first.Items.Concat(second.Items).Concat(third.Items).Select(item => item.MessageId).ToList();

        ids.Should().HaveCount(7);
        ids.Should().OnlyHaveUniqueItems();
        first.TotalCount.Should().Be(7);
        first.Ordering.Should().Be("createdAt_desc,messageId_desc");
    }

    [Fact]
    public async Task PermissionChangeIsAppliedOnNextRequest()
    {
        await using var context = CreateContext();
        var seed = await ChannelSeed.InsertAsync(context);
        var service = CreateService(context);
        (await service.GetHistoryAsync(seed.ChannelAId, seed.UserBId, 1, 20)).Should().NotBeNull();
        var member = await context.CollaborationChannelMembers.FindAsync(seed.ChannelAId, seed.UserBId);
        member!.IsActive = false;
        await context.SaveChangesAsync();

        await service.Invoking(item => item.GetHistoryAsync(seed.ChannelAId, seed.UserBId, 1, 20))
            .Should().ThrowAsync<ChannelNotFoundException>();
    }

    [Fact]
    public async Task ControllerUsesJwtUserAndRequestCannotDeclareSender()
    {
        var userAId = Guid.NewGuid();
        var userBId = Guid.NewGuid();
        var channelId = Guid.NewGuid();
        var request = JsonSerializer.Deserialize<SendChannelMessageRequestDto>(
            $$"""{"content":"hello","senderId":"{{userBId}}"}""",
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true })!;
        var service = new Mock<IChannelTextService>();
        var readStateService = new Mock<ICollaborationReadStateService>();
        var publisher = new Mock<ICollaborationRealtimePublisher>();
        ChannelMessageDto? persistedMessage = null;
        service.Setup(item => item.SendAsync(channelId, userAId, "hello", It.IsAny<CancellationToken>()))
            .ReturnsAsync(persistedMessage = new ChannelMessageDto(
                Guid.NewGuid(),
                channelId,
                "hello",
                new ChannelMessageSenderDto(userAId, "User A", null),
                DateTime.UtcNow,
                Guid.NewGuid()));
        publisher.Setup(item => item.PublishChannelMessageCreatedAsync(
                persistedMessage,
                It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        readStateService.Setup(item => item.GetChannelUnreadUpdatesForMessageAsync(
                persistedMessage.MessageId,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync([]);
        var controller = new ChannelMessagesController(
            service.Object, readStateService.Object, publisher.Object,
            Mock.Of<ICollaborationAttachmentStorage>())
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext
                {
                    User = new ClaimsPrincipal(new ClaimsIdentity(
                        [new Claim(ClaimTypes.NameIdentifier, userAId.ToString())],
                        "test"))
                }
            }
        };

        var response = await controller.Send(channelId, request);

        response.Should().BeOfType<ObjectResult>()
            .Which.StatusCode.Should().Be(StatusCodes.Status201Created);
        service.VerifyAll();
        readStateService.VerifyAll();
        publisher.VerifyAll();
        typeof(SendChannelMessageRequestDto).GetProperty("SenderId").Should().BeNull();
    }

    [Fact]
    public void ResponseDtoDoesNotExposeSensitiveUserFields()
    {
        typeof(ChannelMessageSenderDto).GetProperties().Select(property => property.Name)
            .Should().BeEquivalentTo(["UserId", "DisplayName", "AvatarUrl"]);
    }

    private static ChannelTextService CreateService(ApplicationDbContext context) =>
        new(context, new ResourceAuthorizationService(context));

    private static ApplicationDbContext CreateContext(string? databaseName = null)
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(databaseName ?? Guid.NewGuid().ToString("N"))
            .Options;
        return new ApplicationDbContext(options);
    }

    private static ChannelMessage Message(Guid channelId, Guid senderId, string content) => new()
    {
        Id = Guid.NewGuid(),
        CollaborationChannelId = channelId,
        SenderId = senderId,
        Content = content,
        SentAt = DateTime.UtcNow
    };
}

internal sealed record ChannelSeed(
    Guid UserAId,
    Guid UserBId,
    Guid OutsiderId,
    Guid InactiveUserId,
    Guid ChannelAId,
    Guid ChannelBId,
    Guid DeletedChannelId)
{
    public static async Task<ChannelSeed> InsertAsync(ApplicationDbContext context)
    {
        var userA = User("a");
        var userB = User("b");
        var outsider = User("outsider");
        var inactive = User("inactive", active: false);
        var workspaceA = Workspace("workspace-a", userA);
        var workspaceB = Workspace("workspace-b", outsider);
        var projectA = Project("Project A", workspaceA, userA);
        var projectB = Project("Project B", workspaceB, outsider);
        var channelA = Channel("general-a", workspaceA, projectA, userA);
        var channelB = Channel("general-b", workspaceB, projectB, outsider);
        var deleted = Channel("deleted", workspaceA, projectA, userA, deleted: true);

        context.AddRange(userA, userB, outsider, inactive, workspaceA, workspaceB, projectA, projectB);
        context.WorkspaceMembers.AddRange(
            WorkspaceMember(workspaceA, userA),
            WorkspaceMember(workspaceA, userB),
            WorkspaceMember(workspaceA, inactive),
            WorkspaceMember(workspaceB, outsider));
        context.ProjectMembers.AddRange(
            ProjectMember(projectA, userA),
            ProjectMember(projectA, userB),
            ProjectMember(projectA, inactive),
            ProjectMember(projectB, outsider));
        context.CollaborationChannels.AddRange(channelA, channelB, deleted);
        context.CollaborationChannelMembers.AddRange(
            ChannelMember(channelA, userA, canSend: true),
            ChannelMember(channelA, userB, canSend: false),
            ChannelMember(channelA, inactive, canSend: true),
            ChannelMember(channelB, outsider, canSend: true),
            ChannelMember(deleted, userA, canSend: true));
        await context.SaveChangesAsync();

        return new(
            userA.Id,
            userB.Id,
            outsider.Id,
            inactive.Id,
            channelA.Id,
            channelB.Id,
            deleted.Id);
    }

    private static User User(string key, bool active = true) => new()
    {
        Id = Guid.NewGuid(),
        Email = $"{key}-{Guid.NewGuid():N}@sprinta.test",
        FullName = $"User {key}",
        PasswordHash = "test-only",
        IsActive = active,
        IsDeleted = false,
        CreatedAt = DateTime.UtcNow
    };

    private static Workspace Workspace(string slug, User owner) => new()
    {
        Id = Guid.NewGuid(),
        Slug = $"{slug}-{Guid.NewGuid():N}",
        Name = slug,
        Owner = owner,
        OwnerId = owner.Id,
        CreatedAt = DateTime.UtcNow,
        UpdatedAt = DateTime.UtcNow
    };

    private static Project Project(string name, Workspace workspace, User creator) => new()
    {
        Id = Guid.NewGuid(),
        Name = name,
        Identifier = $"P{Guid.NewGuid():N}"[..8],
        Workspace = workspace,
        WorkspaceId = workspace.Id,
        Creator = creator,
        CreatorId = creator.Id,
        StartDate = DateTime.UtcNow.Date,
        CreatedAt = DateTime.UtcNow,
        UpdatedAt = DateTime.UtcNow,
        Status = true
    };

    private static CollaborationChannel Channel(
        string name,
        Workspace workspace,
        Project project,
        User creator,
        bool deleted = false) => new()
    {
        Id = Guid.NewGuid(),
        Workspace = workspace,
        WorkspaceId = workspace.Id,
        Project = project,
        ProjectId = project.Id,
        CreatedByUser = creator,
        CreatedByUserId = creator.Id,
        Name = name,
        CreatedAt = DateTime.UtcNow,
        UpdatedAt = DateTime.UtcNow,
        IsDeleted = deleted
    };

    private static WorkspaceMember WorkspaceMember(Workspace workspace, User user) => new()
    {
        Workspace = workspace,
        WorkspaceId = workspace.Id,
        User = user,
        UserId = user.Id,
        WorkspaceRole = "MEMBER",
        JoinedAt = DateTime.UtcNow,
        IsActive = true
    };

    private static ProjectMember ProjectMember(Project project, User user) => new()
    {
        Project = project,
        ProjectId = project.Id,
        User = user,
        UserId = user.Id,
        ProjectRole = "DEVELOPER",
        JoinedAt = DateTime.UtcNow,
        Status = true
    };

    private static CollaborationChannelMember ChannelMember(
        CollaborationChannel channel,
        User user,
        bool canSend) => new()
    {
        Channel = channel,
        ChannelId = channel.Id,
        User = user,
        UserId = user.Id,
        JoinedAt = DateTime.UtcNow,
        IsActive = true,
        CanSendMessages = canSend
    };
}
