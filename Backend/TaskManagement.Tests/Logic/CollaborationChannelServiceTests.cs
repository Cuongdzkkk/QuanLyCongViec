using System.Security.Claims;
using System.Text.Json;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Moq;
using TaskManagement.API.Controllers;
using TaskManagement.Application.DTOs.Collaboration;
using TaskManagement.Application.Interfaces;
using TaskManagement.Domain.Entities;
using TaskManagement.Infrastructure.Data;
using TaskManagement.Infrastructure.Services;

namespace TaskManagement.Tests.Logic;

public sealed class CollaborationChannelServiceTests
{
    [Fact]
    public async Task ManagerCreatesServerIdAndCreatorMembership()
    {
        await using var context = CreateContext();
        var seed = await DiscoverySeed.InsertAsync(context);
        var service = CreateService(context);

        var result = await service.CreateAsync(
            seed.ProjectAId,
            seed.ManagerId,
            Request("  kênh-phát-triển  ", "  Mô tả tiếng Việt  "),
            "create-vietnamese");

        result.Created.Should().BeTrue();
        result.Channel.ChannelId.Should().NotBeEmpty();
        result.Channel.Name.Should().Be("kênh-phát-triển");
        result.Channel.Description.Should().Be("Mô tả tiếng Việt");
        result.Channel.WorkspaceId.Should().Be(seed.WorkspaceAId);
        result.Channel.ProjectId.Should().Be(seed.ProjectAId);
        result.Channel.Visibility.Should().Be("Private");
        result.Channel.IsMember.Should().BeTrue();
        result.Channel.CanRead.Should().BeTrue();
        result.Channel.CanSend.Should().BeTrue();
        result.Channel.CanManage.Should().BeTrue();

        var channel = await context.CollaborationChannels
            .SingleAsync(item => item.Id == result.Channel.ChannelId);
        channel.CreatedByUserId.Should().Be(seed.ManagerId);
        var membership = await context.CollaborationChannelMembers
            .SingleAsync(item =>
                item.ChannelId == result.Channel.ChannelId &&
                item.UserId == seed.ManagerId);
        membership.IsActive.Should().BeTrue();
        membership.CanSendMessages.Should().BeTrue();
        membership.LeftAt.Should().BeNull();

        (await context.CollaborationChannelMembers.CountAsync(item =>
                item.ChannelId == result.Channel.ChannelId))
            .Should().Be(1);
    }

    [Fact]
    public async Task CreatedChannelIdWorksForSendAndHistoryAcrossContexts()
    {
        var databaseName = Guid.NewGuid().ToString("N");
        Guid channelId;
        DiscoverySeed seed;
        await using (var write = CreateContext(databaseName))
        {
            seed = await DiscoverySeed.InsertAsync(write);
            var created = await CreateService(write).CreateAsync(
                seed.ProjectAId,
                seed.ManagerId,
                Request("persistent"),
                "persistent-create");
            channelId = created.Channel.ChannelId;
            await new ChannelTextService(write, new ResourceAuthorizationService(write))
                .SendAsync(channelId, seed.ManagerId, "persisted text");
        }

        await using var read = CreateContext(databaseName);
        var discovered = await CreateService(read)
            .DiscoverAsync(seed.ProjectAId, seed.ManagerId, 1, 20);
        var history = await new ChannelTextService(read, new ResourceAuthorizationService(read))
            .GetHistoryAsync(channelId, seed.ManagerId, 1, 20);

        discovered.Items.Should().ContainSingle(item => item.ChannelId == channelId);
        history.Items.Should().ContainSingle(item =>
            item.ChannelId == channelId &&
            item.Content == "persisted text");
    }

    [Fact]
    public async Task DiscoveryBackfillsActiveProjectMembersAndExcludesOtherScopesAndDeletedChannels()
    {
        await using var context = CreateContext();
        var seed = await DiscoverySeed.InsertAsync(context);
        var service = CreateService(context);

        var manager = await service.DiscoverAsync(seed.ProjectAId, seed.ManagerId, 1, 20);
        var member = await service.DiscoverAsync(seed.ProjectAId, seed.MemberId, 1, 20);
        var nonMember = await service.DiscoverAsync(seed.ProjectAId, seed.NonMemberId, 1, 20);

        manager.Items.Select(item => item.ChannelId).Should().Contain(seed.ChannelPrivateId);
        manager.Items.Should().ContainSingle(item =>
            item.Visibility == CollaborationChannelService.PrivateVisibility &&
            item.ChannelId != seed.ChannelPrivateId);
        manager.Items.Single(item => item.ChannelId == seed.ChannelPrivateId)
            .CanManage.Should().BeTrue();
        member.Items.Should().Contain(item => item.ChannelId == seed.ChannelPrivateId);
        member.Items.Should().Contain(item => item.ChannelId != seed.ChannelPrivateId);
        member.Items.Where(item => item.ChannelId == seed.ChannelPrivateId)
            .Single().CanManage.Should().BeFalse();
        member.Items.Where(item => item.ChannelId == seed.ChannelPrivateId)
            .Single().CanSend.Should().BeFalse();
        nonMember.Items.Should().ContainSingle(item => item.ChannelId != seed.ChannelPrivateId);
        manager.Items.Should().NotContain(item =>
            item.ChannelId == seed.ChannelOtherProjectId ||
            item.ChannelId == seed.ChannelOtherWorkspaceId ||
            item.ChannelId == seed.ChannelDeletedId ||
            item.ChannelId == seed.ChannelArchivedId);
    }

    [Fact]
    public async Task PrivateChannelDiscoveryDoesNotBackfillProjectMembers()
    {
        await using var context = CreateContext();
        var seed = await DiscoverySeed.InsertAsync(context);
        var existingMembership = await context.CollaborationChannelMembers
            .SingleAsync(item => item.ChannelId == seed.ChannelPrivateId && item.UserId == seed.MemberId);
        context.CollaborationChannelMembers.Remove(existingMembership);
        await context.SaveChangesAsync();

        var service = CreateService(context);
        (await service.DiscoverAsync(seed.ProjectAId, seed.MemberId, 1, 20))
            .Items.Should().NotContain(item => item.ChannelId == seed.ChannelPrivateId);
        await service.DiscoverAsync(seed.ProjectAId, seed.MemberId, 1, 20);

        (await context.CollaborationChannelMembers.CountAsync(item =>
                item.ChannelId == seed.ChannelPrivateId && item.UserId == seed.MemberId))
            .Should().Be(0);
    }

    [Fact]
    public async Task EnsureProjectDiscussionCreatesOneProjectWideChannelAndIsIdempotent()
    {
        await using var context = CreateContext();
        var seed = await DiscoverySeed.InsertAsync(context);
        var service = CreateService(context);

        var first = await service.EnsureProjectDiscussionAsync(seed.ProjectAId, seed.ManagerId);
        var second = await service.EnsureProjectDiscussionAsync(seed.ProjectAId, seed.MemberId);

        first.Created.Should().BeTrue();
        second.Created.Should().BeFalse();
        second.Channel.ChannelId.Should().Be(first.Channel.ChannelId);
        (await context.CollaborationChannels.CountAsync(item =>
                item.ProjectId == seed.ProjectAId &&
                item.ChannelScope == CollaborationChannelService.ProjectDiscussionScope &&
                !item.IsDeleted && !item.IsArchived))
            .Should().Be(1);
        (await context.CollaborationChannelMembers.CountAsync(item =>
                item.ChannelId == first.Channel.ChannelId))
            .Should().Be(3);
        (await context.CollaborationChannelMembers.AnyAsync(item =>
                item.ChannelId == first.Channel.ChannelId && item.UserId == seed.InactiveId))
            .Should().BeFalse();
    }

    [Fact]
    public async Task OutsiderAndInactiveUserCannotDiscoverOrCreate()
    {
        await using var context = CreateContext();
        var seed = await DiscoverySeed.InsertAsync(context);
        var service = CreateService(context);

        await service.Invoking(item =>
                item.DiscoverAsync(seed.ProjectAId, seed.OutsiderId, 1, 20))
            .Should().ThrowAsync<CollaborationProjectNotFoundException>();
        await service.Invoking(item =>
                item.DiscoverAsync(seed.ProjectAId, seed.InactiveId, 1, 20))
            .Should().ThrowAsync<CollaborationProjectNotFoundException>();
        await service.Invoking(item =>
                item.CreateAsync(seed.ProjectAId, seed.MemberId, Request("forbidden"), "member-create"))
            .Should().ThrowAsync<CollaborationChannelForbiddenException>();
        await service.Invoking(item =>
                item.CreateAsync(seed.ProjectAId, seed.OutsiderId, Request("forbidden"), "outsider-create"))
            .Should().ThrowAsync<CollaborationChannelForbiddenException>();
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("\u0001unsafe")]
    public async Task InvalidNamesAreRejected(string name)
    {
        await using var context = CreateContext();
        var seed = await DiscoverySeed.InsertAsync(context);

        await CreateService(context).Invoking(item =>
                item.CreateAsync(seed.ProjectAId, seed.ManagerId, Request(name), "invalid-name"))
            .Should().ThrowAsync<ArgumentException>();
    }

    [Fact]
    public async Task OverlongNameAndUnsupportedVisibilityAreRejected()
    {
        await using var context = CreateContext();
        var seed = await DiscoverySeed.InsertAsync(context);
        var service = CreateService(context);

        await service.Invoking(item =>
                item.CreateAsync(
                    seed.ProjectAId,
                    seed.ManagerId,
                    Request(new string('x', 101)),
                    "long-name"))
            .Should().ThrowAsync<ArgumentException>();
        await service.Invoking(item =>
                item.CreateAsync(
                    seed.ProjectAId,
                    seed.ManagerId,
                    Request("public", visibility: "Public"),
                    "public-channel"))
            .Should().ThrowAsync<ArgumentException>();
    }

    [Fact]
    public async Task DuplicateNamesRemainDistinctButIdempotentRetryReturnsSameChannel()
    {
        await using var context = CreateContext();
        var seed = await DiscoverySeed.InsertAsync(context);
        var service = CreateService(context);

        var first = await service.CreateAsync(
            seed.ProjectAId,
            seed.ManagerId,
            Request("same-name"),
            "request-one");
        var retry = await service.CreateAsync(
            seed.ProjectAId,
            seed.ManagerId,
            Request("same-name"),
            "request-one");
        var second = await service.CreateAsync(
            seed.ProjectAId,
            seed.ManagerId,
            Request("same-name"),
            "request-two");

        retry.Created.Should().BeFalse();
        retry.Channel.ChannelId.Should().Be(first.Channel.ChannelId);
        second.Channel.ChannelId.Should().NotBe(first.Channel.ChannelId);
        (await context.CollaborationChannelMembers
                .CountAsync(item =>
                    item.UserId == seed.ManagerId &&
                    (item.ChannelId == first.Channel.ChannelId ||
                     item.ChannelId == second.Channel.ChannelId)))
            .Should().Be(2);
    }

    [Fact]
    public async Task ReusingIdempotencyKeyForDifferentPayloadConflicts()
    {
        await using var context = CreateContext();
        var seed = await DiscoverySeed.InsertAsync(context);
        var service = CreateService(context);
        await service.CreateAsync(
            seed.ProjectAId,
            seed.ManagerId,
            Request("first"),
            "same-request");

        await service.Invoking(item =>
                item.CreateAsync(
                    seed.ProjectAId,
                    seed.ManagerId,
                    Request("different"),
                    "same-request"))
            .Should().ThrowAsync<CollaborationChannelConflictException>();
    }

    [Fact]
    public async Task DiscoveryPaginationIsDeterministicAndHasCorrectTotal()
    {
        await using var context = CreateContext();
        var seed = await DiscoverySeed.InsertAsync(context);
        var timestamp = new DateTime(2026, 7, 28, 10, 0, 0, DateTimeKind.Utc);
        for (var index = 1; index <= 6; index++)
        {
            var channel = DiscoverySeed.Channel(
                index % 2 == 0 ? "alpha" : "beta",
                seed.WorkspaceA,
                seed.ProjectA,
                seed.Manager,
                id: Guid.Parse($"00000000-0000-0000-0000-{index:000000000000}"),
                createdAt: timestamp);
            context.CollaborationChannels.Add(channel);
            context.CollaborationChannelMembers.Add(
                DiscoverySeed.ChannelMember(channel, seed.Manager));
        }
        await context.SaveChangesAsync();
        var service = CreateService(context);

        var pages = new[]
        {
            await service.DiscoverAsync(seed.ProjectAId, seed.ManagerId, 1, 3),
            await service.DiscoverAsync(seed.ProjectAId, seed.ManagerId, 2, 3),
            await service.DiscoverAsync(seed.ProjectAId, seed.ManagerId, 3, 3)
        };
        var ids = pages.SelectMany(page => page.Items).Select(item => item.ChannelId).ToList();

        ids.Should().HaveCount(8);
        ids.Should().OnlyHaveUniqueItems();
        pages.Should().OnlyContain(page => page.TotalCount == 8);
        pages.Should().OnlyContain(page =>
            page.Ordering == CollaborationChannelService.Ordering);
    }

    [Fact]
    public async Task DiscoveryEnsuresProjectDiscussionWithoutConvertingPrivateChannels()
    {
        await using var context = CreateContext();
        var seed = await DiscoverySeed.InsertAsync(context);
        var before = await context.CollaborationChannels.CountAsync();

        await CreateService(context).DiscoverAsync(seed.ProjectAId, seed.NonMemberId, 1, 20);

        (await context.CollaborationChannels.CountAsync()).Should().Be(before + 1);
        (await context.CollaborationChannels.CountAsync(item =>
                item.ProjectId == seed.ProjectAId &&
                item.ChannelScope == CollaborationChannelService.ProjectDiscussionScope))
            .Should().Be(1);
    }

    [Fact]
    public async Task LostMembershipRemovesDiscoveryReadAndSendAccess()
    {
        await using var context = CreateContext();
        var seed = await DiscoverySeed.InsertAsync(context);
        var discovery = CreateService(context);
        var messages = new ChannelTextService(context, new ResourceAuthorizationService(context));
        (await discovery.DiscoverAsync(seed.ProjectAId, seed.MemberId, 1, 20))
            .Items.Should().Contain(item => item.ChannelId == seed.ChannelPrivateId);
        var member = await context.CollaborationChannelMembers.FindAsync(
            seed.ChannelPrivateId,
            seed.MemberId);
        member!.IsActive = false;
        await context.SaveChangesAsync();

        (await discovery.DiscoverAsync(seed.ProjectAId, seed.MemberId, 1, 20))
            .Items.Should().NotContain(item => item.ChannelId == seed.ChannelPrivateId);
        await messages.Invoking(item =>
                item.GetHistoryAsync(seed.ChannelPrivateId, seed.MemberId, 1, 20))
            .Should().ThrowAsync<ChannelNotFoundException>();
        await messages.Invoking(item =>
                item.SendAsync(seed.ChannelPrivateId, seed.MemberId, "blocked"))
            .Should().ThrowAsync<ChannelNotFoundException>();
    }

    [Fact]
    public async Task ControllerUsesJwtUserAndIgnoresFakeCreatorFields()
    {
        var currentUserId = Guid.NewGuid();
        var fakeUserId = Guid.NewGuid();
        var projectId = Guid.NewGuid();
        var request = JsonSerializer.Deserialize<CreateCollaborationChannelRequestDto>(
            $$"""{"name":"general","creatorId":"{{fakeUserId}}","userId":"{{fakeUserId}}"}""",
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true })!;
        var channel = new CollaborationChannelDto(
            Guid.NewGuid(),
            "general",
            null,
            Guid.NewGuid(),
            projectId,
            "Private",
            true,
            true,
            true,
            true,
            DateTime.UtcNow,
            DateTime.UtcNow);
        var service = new Mock<ICollaborationChannelService>();
        service.Setup(item => item.CreateAsync(
                projectId,
                currentUserId,
                request,
                "controller-request",
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ProvisionCollaborationChannelResult(channel, true));
        var controller = Controller(service.Object, currentUserId);

        var response = await controller.Create(
            projectId,
            request,
            "controller-request");

        response.Should().BeOfType<ObjectResult>()
            .Which.StatusCode.Should().Be(StatusCodes.Status201Created);
        service.VerifyAll();
        typeof(CreateCollaborationChannelRequestDto).GetProperty("CreatorId").Should().BeNull();
        typeof(CreateCollaborationChannelRequestDto).GetProperty("UserId").Should().BeNull();
    }

    [Fact]
    public void ResponseDtoDoesNotExposeMembershipOrSecurityInternals()
    {
        typeof(CollaborationChannelDto).GetProperties().Select(property => property.Name)
            .Should().BeEquivalentTo([
                "ChannelId",
                "Name",
                "Description",
                "WorkspaceId",
                "ProjectId",
                "Visibility",
                "IsMember",
                "CanRead",
                "CanSend",
                "CanManage",
                "CreatedAt",
                "UpdatedAt",
                "UnreadCount",
                "LastReadMessageId"
            ]);
    }

    private static CollaborationChannelsController Controller(
        ICollaborationChannelService service,
        Guid userId) =>
        new(service)
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext
                {
                    User = new ClaimsPrincipal(new ClaimsIdentity(
                        [new Claim(ClaimTypes.NameIdentifier, userId.ToString())],
                        "test"))
                }
            }
        };

    private static CreateCollaborationChannelRequestDto Request(
        string name,
        string? description = null,
        string visibility = "Private") =>
        new()
        {
            Name = name,
            Description = description,
            Visibility = visibility
        };

    private static CollaborationChannelService CreateService(ApplicationDbContext context) =>
        new(context, new ResourceAuthorizationService(context));

    private static ApplicationDbContext CreateContext(string? databaseName = null)
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(databaseName ?? Guid.NewGuid().ToString("N"))
            .Options;
        return new ApplicationDbContext(options);
    }
}

internal sealed class DiscoverySeed
{
    public required User Manager { get; init; }
    public required User Member { get; init; }
    public required User NonMember { get; init; }
    public required User Outsider { get; init; }
    public required User Inactive { get; init; }
    public required Workspace WorkspaceA { get; init; }
    public required Workspace WorkspaceB { get; init; }
    public required Project ProjectA { get; init; }
    public required Project ProjectB { get; init; }
    public required Project ProjectOtherWorkspace { get; init; }
    public required CollaborationChannel ChannelPrivate { get; init; }
    public required CollaborationChannel ChannelOtherProject { get; init; }
    public required CollaborationChannel ChannelOtherWorkspace { get; init; }
    public required CollaborationChannel ChannelDeleted { get; init; }
    public required CollaborationChannel ChannelArchived { get; init; }

    public Guid ManagerId => Manager.Id;
    public Guid MemberId => Member.Id;
    public Guid NonMemberId => NonMember.Id;
    public Guid OutsiderId => Outsider.Id;
    public Guid InactiveId => Inactive.Id;
    public Guid WorkspaceAId => WorkspaceA.Id;
    public Guid ProjectAId => ProjectA.Id;
    public Guid ChannelPrivateId => ChannelPrivate.Id;
    public Guid ChannelOtherProjectId => ChannelOtherProject.Id;
    public Guid ChannelOtherWorkspaceId => ChannelOtherWorkspace.Id;
    public Guid ChannelDeletedId => ChannelDeleted.Id;
    public Guid ChannelArchivedId => ChannelArchived.Id;

    public static async Task<DiscoverySeed> InsertAsync(ApplicationDbContext context)
    {
        var manager = User("manager");
        var member = User("member");
        var nonMember = User("non-member");
        var outsider = User("outsider");
        var inactive = User("inactive", active: false);
        var workspaceA = Workspace("workspace-a", manager);
        var workspaceB = Workspace("workspace-b", outsider);
        var projectA = Project("Project A", workspaceA, manager);
        var projectB = Project("Project B", workspaceA, manager);
        var projectOtherWorkspace = Project("Project Other", workspaceB, outsider);
        var channelPrivate = Channel("private", workspaceA, projectA, manager);
        var channelOtherProject = Channel("other-project", workspaceA, projectB, manager);
        var channelOtherWorkspace = Channel(
            "other-workspace",
            workspaceB,
            projectOtherWorkspace,
            outsider);
        var channelDeleted = Channel("deleted", workspaceA, projectA, manager, deleted: true);
        var channelArchived = Channel("archived", workspaceA, projectA, manager, archived: true);

        context.AddRange(
            manager,
            member,
            nonMember,
            outsider,
            inactive,
            workspaceA,
            workspaceB,
            projectA,
            projectB,
            projectOtherWorkspace);
        context.WorkspaceMembers.AddRange(
            WorkspaceMember(workspaceA, manager),
            WorkspaceMember(workspaceA, member),
            WorkspaceMember(workspaceA, nonMember),
            WorkspaceMember(workspaceA, inactive),
            WorkspaceMember(workspaceB, outsider));
        context.ProjectMembers.AddRange(
            ProjectMember(projectA, manager, "PM"),
            ProjectMember(projectA, member, "DEVELOPER"),
            ProjectMember(projectA, nonMember, "DEVELOPER"),
            ProjectMember(projectA, inactive, "PM"),
            ProjectMember(projectB, manager, "PM"),
            ProjectMember(projectOtherWorkspace, outsider, "PM"));
        context.CollaborationChannels.AddRange(
            channelPrivate,
            channelOtherProject,
            channelOtherWorkspace,
            channelDeleted,
            channelArchived);
        context.CollaborationChannelMembers.AddRange(
            ChannelMember(channelPrivate, manager),
            ChannelMember(channelPrivate, member, canSend: false),
            ChannelMember(channelOtherProject, manager),
            ChannelMember(channelOtherWorkspace, outsider),
            ChannelMember(channelDeleted, manager),
            ChannelMember(channelArchived, manager));
        await context.SaveChangesAsync();

        return new()
        {
            Manager = manager,
            Member = member,
            NonMember = nonMember,
            Outsider = outsider,
            Inactive = inactive,
            WorkspaceA = workspaceA,
            WorkspaceB = workspaceB,
            ProjectA = projectA,
            ProjectB = projectB,
            ProjectOtherWorkspace = projectOtherWorkspace,
            ChannelPrivate = channelPrivate,
            ChannelOtherProject = channelOtherProject,
            ChannelOtherWorkspace = channelOtherWorkspace,
            ChannelDeleted = channelDeleted,
            ChannelArchived = channelArchived
        };
    }

    public static User User(string key, bool active = true) => new()
    {
        Id = Guid.NewGuid(),
        Email = $"{key}-{Guid.NewGuid():N}@sprinta.test",
        FullName = $"User {key}",
        PasswordHash = "test-only",
        IsActive = active,
        IsDeleted = false,
        CreatedAt = DateTime.UtcNow
    };

    public static Workspace Workspace(string slug, User owner) => new()
    {
        Id = Guid.NewGuid(),
        Slug = $"{slug}-{Guid.NewGuid():N}",
        Name = slug,
        Owner = owner,
        OwnerId = owner.Id,
        CreatedAt = DateTime.UtcNow,
        UpdatedAt = DateTime.UtcNow
    };

    public static Project Project(string name, Workspace workspace, User creator) => new()
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

    public static CollaborationChannel Channel(
        string name,
        Workspace workspace,
        Project project,
        User creator,
        bool deleted = false,
        bool archived = false,
        Guid? id = null,
        DateTime? createdAt = null) =>
        new()
        {
            Id = id ?? Guid.NewGuid(),
            Workspace = workspace,
            WorkspaceId = workspace.Id,
            Project = project,
            ProjectId = project.Id,
            CreatedByUser = creator,
            CreatedByUserId = creator.Id,
            Name = name,
            CreatedAt = createdAt ?? DateTime.UtcNow,
            UpdatedAt = createdAt ?? DateTime.UtcNow,
            IsDeleted = deleted,
            IsArchived = archived
        };

    public static WorkspaceMember WorkspaceMember(Workspace workspace, User user) => new()
    {
        Workspace = workspace,
        WorkspaceId = workspace.Id,
        User = user,
        UserId = user.Id,
        WorkspaceRole = "MEMBER",
        JoinedAt = DateTime.UtcNow,
        IsActive = true
    };

    public static ProjectMember ProjectMember(
        Project project,
        User user,
        string role) =>
        new()
        {
            Project = project,
            ProjectId = project.Id,
            User = user,
            UserId = user.Id,
            ProjectRole = role,
            JoinedAt = DateTime.UtcNow,
            Status = true
        };

    public static CollaborationChannelMember ChannelMember(
        CollaborationChannel channel,
        User user,
        bool canSend = true) =>
        new()
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
