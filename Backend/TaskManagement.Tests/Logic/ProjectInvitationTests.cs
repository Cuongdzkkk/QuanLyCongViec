using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Options;
using Moq;
using TaskManagement.Application.DTOs.Auth;
using TaskManagement.Application.DTOs.Project;
using TaskManagement.Application.Interfaces;
using TaskManagement.Domain.Entities;
using TaskManagement.Infrastructure.Data;
using TaskManagement.Infrastructure.Services;

namespace TaskManagement.Tests.Logic;

public sealed class ProjectInvitationTests
{
    [Fact]
    public async Task ExistingUserInvitationCreatesNotificationAndTokenLinkedInvitation()
    {
        await using var fixture = await Fixture.CreateAsync();
        var invitation = await fixture.Context.ProjectInvitations.SingleAsync();
        invitation.Status.Should().Be("Pending");
        (await fixture.Context.RefreshTokens.SingleAsync()).ProjectInvitationId.Should().Be(invitation.Id);
        var notification = await fixture.Context.Notifications.SingleAsync();
        notification.RelatedInvitationId.Should().Be(invitation.Id);
        notification.ActionState.Should().Be("Pending");
    }

    [Fact]
    public async Task AnonymousExistingUserTokenValidationDoesNotActivateInvitation()
    {
        await using var fixture = await Fixture.CreateAsync();
        var result = await fixture.CreateAuthService().AcceptInviteTokenAsync(new AcceptInviteTokenRequestDto
        {
            Token = fixture.InviteToken
        });

        result.RequiresLogin.Should().BeTrue();
        (await fixture.Context.ProjectMembers.SingleAsync(item => item.ProjectId == fixture.ProjectId && item.UserId == fixture.InviteeId))
            .Status.Should().BeFalse();
        (await fixture.Context.WorkspaceMembers.SingleAsync(item => item.WorkspaceId == fixture.WorkspaceId && item.UserId == fixture.InviteeId))
            .IsActive.Should().BeFalse();
        (await fixture.Context.ProjectInvitations.SingleAsync()).Status.Should().Be("Pending");
        (await fixture.Context.Notifications.SingleAsync()).ActionState.Should().Be("Pending");
    }

    [Fact]
    public async Task WrongAccountCannotAcceptAndTargetRemainsPending()
    {
        await using var fixture = await Fixture.CreateAsync();
        var invitation = fixture.Invitation;
        var auth = fixture.CreateAuthService();

        await FluentActions.Invoking(() => auth.AcceptProjectInvitationAsync(invitation.Id, fixture.WrongUserId))
            .Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*tài khoản khác*");

        (await fixture.Context.ProjectMembers.SingleAsync(item => item.ProjectId == fixture.ProjectId && item.UserId == fixture.InviteeId))
            .Status.Should().BeFalse();
        (await fixture.Context.ProjectInvitations.FindAsync(invitation.Id))!.Status.Should().Be("Pending");
    }

    [Fact]
    public async Task MatchingUserCanAcceptAndDeclineIsTerminal()
    {
        await using var fixture = await Fixture.CreateAsync();
        var collaboration = new Mock<ICollaborationChannelService>();
        var auth = fixture.CreateAuthService(collaboration.Object);
        var accepted = await auth.AcceptProjectInvitationAsync(fixture.Invitation.Id, fixture.InviteeId);

        accepted.ProjectId.Should().Be(fixture.ProjectId);
        (await fixture.Context.ProjectMembers.SingleAsync(item => item.ProjectId == fixture.ProjectId && item.UserId == fixture.InviteeId))
            .Status.Should().BeTrue();
        (await fixture.Context.ProjectInvitations.FindAsync(fixture.Invitation.Id))!.Status.Should().Be("Accepted");
        (await fixture.Context.Notifications.SingleAsync()).ActionState.Should().Be("Accepted");
        collaboration.Verify(item => item.EnsureProjectMemberAccessAsync(
            fixture.ProjectId,
            fixture.InviteeId,
            true,
            It.IsAny<CancellationToken>()), Times.Once);

        await using var declinedFixture = await Fixture.CreateAsync();
        var declineAuth = declinedFixture.CreateAuthService();
        await declineAuth.DeclineProjectInvitationAsync(declinedFixture.Invitation.Id, declinedFixture.InviteeId);

        (await declinedFixture.Context.ProjectMembers.SingleAsync(item => item.ProjectId == declinedFixture.ProjectId && item.UserId == declinedFixture.InviteeId))
            .Status.Should().BeFalse();
        (await declinedFixture.Context.ProjectInvitations.FindAsync(declinedFixture.Invitation.Id))!.Status.Should().Be("Declined");
        await FluentActions.Invoking(() => declineAuth.AcceptProjectInvitationAsync(declinedFixture.Invitation.Id, declinedFixture.InviteeId))
            .Should().ThrowAsync<InvalidOperationException>();
    }

    [Fact]
    public async Task PlaceholderUserSetupActivatesMembershipWithoutCreatingNotification()
    {
        await using var fixture = await Fixture.CreateAsync(placeholder: true);
        var result = await fixture.CreateAuthService().AcceptInviteTokenAsync(new AcceptInviteTokenRequestDto
        {
            Token = fixture.InviteToken,
            FullName = "New Invitee",
            Password = "Password!123"
        });

        result.RequiresLogin.Should().BeFalse();
        result.Response.Should().NotBeNull();
        (await fixture.Context.ProjectMembers.SingleAsync(item => item.ProjectId == fixture.ProjectId && item.UserId == fixture.InviteeId))
            .Status.Should().BeTrue();
        (await fixture.Context.WorkspaceMembers.SingleAsync(item => item.WorkspaceId == fixture.WorkspaceId && item.UserId == fixture.InviteeId))
            .IsActive.Should().BeTrue();
        (await fixture.Context.ProjectInvitations.SingleAsync()).Status.Should().Be("Accepted");
        (await fixture.Context.Notifications.CountAsync()).Should().Be(0);
    }

    [Fact]
    public async Task RealtimeFailureAfterAcceptCommitDoesNotFailAcceptance()
    {
        await using var fixture = await Fixture.CreateAsync();
        var notifier = new Mock<ISignalRClientNotifier>();
        notifier.Setup(item => item.SendNotificationUpdatedAsync(It.IsAny<Guid>(), It.IsAny<Notification>()))
            .ThrowsAsync(new InvalidOperationException("SignalR unavailable"));

        var result = await fixture.CreateAuthService(notifier: notifier.Object)
            .AcceptProjectInvitationAsync(fixture.Invitation.Id, fixture.InviteeId);

        result.ProjectId.Should().Be(fixture.ProjectId);
        (await fixture.Context.ProjectInvitations.SingleAsync()).Status.Should().Be("Accepted");
        (await fixture.Context.ProjectMembers.SingleAsync(item => item.ProjectId == fixture.ProjectId && item.UserId == fixture.InviteeId))
            .Status.Should().BeTrue();
        notifier.Verify(item => item.SendNotificationUpdatedAsync(fixture.InviteeId, It.IsAny<Notification>()), Times.Once);
    }

    [Fact]
    public async Task RealtimeFailureAfterDeclineCommitDoesNotFailDecline()
    {
        await using var fixture = await Fixture.CreateAsync();
        var notifier = new Mock<ISignalRClientNotifier>();
        notifier.Setup(item => item.SendNotificationUpdatedAsync(It.IsAny<Guid>(), It.IsAny<Notification>()))
            .ThrowsAsync(new InvalidOperationException("SignalR unavailable"));

        await fixture.CreateAuthService(notifier: notifier.Object)
            .DeclineProjectInvitationAsync(fixture.Invitation.Id, fixture.InviteeId);

        (await fixture.Context.ProjectInvitations.SingleAsync()).Status.Should().Be("Declined");
        (await fixture.Context.ProjectMembers.SingleAsync(item => item.ProjectId == fixture.ProjectId && item.UserId == fixture.InviteeId))
            .Status.Should().BeFalse();
        notifier.Verify(item => item.SendNotificationUpdatedAsync(fixture.InviteeId, It.IsAny<Notification>()), Times.Once);
    }

    private sealed class Fixture : IAsyncDisposable
    {
        private Fixture(ApplicationDbContext context, ProjectMemberService memberService, Guid workspaceId, Guid projectId,
            Guid ownerId, Guid inviteeId, Guid wrongUserId, ProjectInvitation invitation, string inviteeEmail, string inviteToken)
        {
            Context = context;
            MemberService = memberService;
            WorkspaceId = workspaceId;
            ProjectId = projectId;
            OwnerId = ownerId;
            InviteeId = inviteeId;
            WrongUserId = wrongUserId;
            Invitation = invitation;
            InviteeEmail = inviteeEmail;
            InviteToken = inviteToken;
        }

        public ApplicationDbContext Context { get; }
        public ProjectMemberService MemberService { get; }
        public Guid WorkspaceId { get; }
        public Guid ProjectId { get; }
        public Guid OwnerId { get; }
        public Guid InviteeId { get; }
        public Guid WrongUserId { get; }
        public ProjectInvitation Invitation { get; }
        public string InviteeEmail { get; }
        public string InviteToken { get; }

        public static async Task<Fixture> CreateAsync(bool placeholder = false)
        {
            var context = new ApplicationDbContext(new DbContextOptionsBuilder<ApplicationDbContext>()
                .UseInMemoryDatabase(Guid.NewGuid().ToString("N"))
                .Options);
            var workspaceId = Guid.NewGuid();
            var projectId = Guid.NewGuid();
            var ownerId = Guid.NewGuid();
            var inviteeId = Guid.NewGuid();
            var wrongUserId = Guid.NewGuid();
            var inviteeEmail = "invitee@example.com";
            context.Users.AddRange(
                new User { Id = ownerId, Email = "owner@example.com", FullName = "Owner", PasswordHash = "hash", IsActive = true },
                new User { Id = inviteeId, Email = inviteeEmail, FullName = "Invitee", PasswordHash = placeholder ? string.Empty : "hash", IsActive = !placeholder },
                new User { Id = wrongUserId, Email = "wrong@example.com", FullName = "Wrong", PasswordHash = "hash", IsActive = true });
            context.Workspaces.Add(new Workspace { Id = workspaceId, OwnerId = ownerId, Name = "Workspace", Slug = $"ws-{workspaceId:N}" });
            context.Projects.Add(new Project { Id = projectId, WorkspaceId = workspaceId, CreatorId = ownerId, Name = "Project", Identifier = "INV", Status = true });
            context.WorkspaceMembers.AddRange(
                new WorkspaceMember { WorkspaceId = workspaceId, UserId = ownerId, WorkspaceRole = "OWNER", IsActive = true },
                new WorkspaceMember { WorkspaceId = workspaceId, UserId = inviteeId, WorkspaceRole = "MEMBER", IsActive = false });
            context.ProjectMembers.Add(new ProjectMember { ProjectId = projectId, UserId = ownerId, ProjectRole = "PM", Status = true, JoinedAt = DateTime.UtcNow });
            context.Roles.Add(new Role { Id = Guid.NewGuid(), Name = "Developer" });
            await context.SaveChangesAsync();

            var inviteUrl = string.Empty;
            var email = new Mock<IEmailService>();
            email.Setup(item => item.SendInviteEmailAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string?>()))
                .Callback<string, string, string, string, string, string, string?>((_, _, _, _, _, url, _) => inviteUrl = url)
                .Returns(Task.CompletedTask);
            var service = new ProjectMemberService(context, email.Object, new ConfigurationBuilder().Build());
            await service.InviteMemberAsync(projectId, new ProjectMemberRequestDto { Email = inviteeEmail, Role = "DEV" }, "Owner", ownerId);
            var invitation = await context.ProjectInvitations.SingleAsync();
            var tokenStart = inviteUrl.IndexOf("token=", StringComparison.Ordinal) + "token=".Length;
            var inviteToken = Uri.UnescapeDataString(inviteUrl[tokenStart..]);
            return new Fixture(context, service, workspaceId, projectId, ownerId, inviteeId, wrongUserId, invitation, inviteeEmail, inviteToken);
        }

        public AuthService CreateAuthService(
            ICollaborationChannelService? collaboration = null,
            ISignalRClientNotifier? notifier = null)
        {
            var jwt = new Mock<IJwtService>();
            jwt.Setup(item => item.GenerateAccessToken(It.IsAny<User>(), It.IsAny<IList<string>>())).Returns("access-token");
            jwt.Setup(item => item.GenerateRefreshToken()).Returns("refresh-token");
            return new AuthService(
                Context,
                jwt.Object,
                new ConfigurationBuilder().Build(),
                new OtpService(new Microsoft.Extensions.Caching.Memory.MemoryCache(new Microsoft.Extensions.Caching.Memory.MemoryCacheOptions()), Options.Create(new TaskManagement.Application.Configuration.OtpSecurityOptions())),
                new Mock<IEmailService>().Object,
                collaborationChannelService: collaboration,
                clientNotifier: notifier);
        }

        public ValueTask DisposeAsync() => Context.DisposeAsync();
    }
}
