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

public sealed class ProjectMemberDirectAddTests
{
    [Fact]
    public async Task CandidateSearch_OnlyReturnsActiveSameWorkspaceNonMembersAndSupportsNameOrEmailSearch()
    {
        await using var fixture = await Fixture.CreateAsync();

        var candidates = await fixture.Service.GetProjectMemberCandidatesAsync(fixture.ProjectId, "workspace.user", 1, 50);

        candidates.Should().ContainSingle(item => item.UserId == fixture.CandidateId);
        candidates.Should().NotContain(item => item.UserId == fixture.ExistingMemberId);
        candidates.Should().NotContain(item => item.UserId == fixture.OutsideWorkspaceId);
        candidates.Should().NotContain(item => item.UserId == fixture.PendingInviteId);
        candidates.Should().NotContain(item => item.UserId == fixture.InactiveWorkspaceMemberId);
    }

    [Fact]
    public async Task AddExistingMember_ActiveWorkspaceMemberBecomesActiveProjectMemberWithoutInvite()
    {
        await using var fixture = await Fixture.CreateAsync();

        var added = await fixture.Service.AddExistingMemberAsync(fixture.ProjectId,
            new AddExistingProjectMemberRequestDto { UserId = fixture.CandidateId, Role = "Developer" });

        added.UserId.Should().Be(fixture.CandidateId);
        added.ProjectRole.Should().Be("Developer");
        var membership = await fixture.Context.ProjectMembers.SingleAsync(item =>
            item.ProjectId == fixture.ProjectId && item.UserId == fixture.CandidateId);
        membership.Status.Should().BeTrue();
        fixture.EmailService.Verify(service => service.SendInviteEmailAsync(
            It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(),
            It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string?>()), Times.Never);
    }

    [Fact]
    public async Task AddExistingMember_RejectsOutsideWorkspaceDuplicatePendingAndInactiveWorkspaceMember()
    {
        await using var fixture = await Fixture.CreateAsync();

        await FluentActions.Invoking(() => fixture.Service.AddExistingMemberAsync(fixture.ProjectId,
            new AddExistingProjectMemberRequestDto { UserId = fixture.OutsideWorkspaceId, Role = "Developer" }))
            .Should().ThrowAsync<ArgumentException>().WithMessage("*workspace*");

        await FluentActions.Invoking(() => fixture.Service.AddExistingMemberAsync(fixture.ProjectId,
            new AddExistingProjectMemberRequestDto { UserId = fixture.ExistingMemberId, Role = "Developer" }))
            .Should().ThrowAsync<InvalidOperationException>().WithMessage("*already an active*");

        await FluentActions.Invoking(() => fixture.Service.AddExistingMemberAsync(fixture.ProjectId,
            new AddExistingProjectMemberRequestDto { UserId = fixture.PendingInviteId, Role = "Developer" }))
            .Should().ThrowAsync<InvalidOperationException>().WithMessage("*pending invitation*");

        await FluentActions.Invoking(() => fixture.Service.AddExistingMemberAsync(fixture.ProjectId,
            new AddExistingProjectMemberRequestDto { UserId = fixture.InactiveWorkspaceMemberId, Role = "Developer" }))
            .Should().ThrowAsync<ArgumentException>().WithMessage("*active member*");
    }

    [Fact]
    public async Task AddExistingMember_RejectsInvalidRole()
    {
        await using var fixture = await Fixture.CreateAsync();

        await FluentActions.Invoking(() => fixture.Service.AddExistingMemberAsync(fixture.ProjectId,
            new AddExistingProjectMemberRequestDto { UserId = fixture.CandidateId, Role = "NotAProjectRole" }))
            .Should().ThrowAsync<ArgumentException>().WithMessage("*invalid*");
    }

    private sealed class Fixture : IAsyncDisposable
    {
        private Fixture(ApplicationDbContext context, Mock<IEmailService> emailService, ProjectMemberService service)
        {
            Context = context;
            EmailService = emailService;
            Service = service;
        }

        public ApplicationDbContext Context { get; }
        public Mock<IEmailService> EmailService { get; }
        public ProjectMemberService Service { get; private set; }
        public Guid WorkspaceId { get; } = Guid.NewGuid();
        public Guid OtherWorkspaceId { get; } = Guid.NewGuid();
        public Guid ProjectId { get; } = Guid.NewGuid();
        public Guid OwnerId { get; } = Guid.NewGuid();
        public Guid CandidateId { get; } = Guid.NewGuid();
        public Guid ExistingMemberId { get; } = Guid.NewGuid();
        public Guid OutsideWorkspaceId { get; } = Guid.NewGuid();
        public Guid PendingInviteId { get; } = Guid.NewGuid();
        public Guid InactiveWorkspaceMemberId { get; } = Guid.NewGuid();

        public static async Task<Fixture> CreateAsync()
        {
            var fixture = new Fixture(
                new ApplicationDbContext(new DbContextOptionsBuilder<ApplicationDbContext>()
                    .UseInMemoryDatabase(Guid.NewGuid().ToString("N"))
                    .Options),
                new Mock<IEmailService>(),
                null!);

            var now = DateTime.UtcNow;
            fixture.Context.Users.AddRange(
                new User { Id = fixture.OwnerId, Email = "owner@example.com", FullName = "Owner", IsActive = true },
                new User { Id = fixture.CandidateId, Email = "workspace.user@example.com", FullName = "Workspace User", IsActive = true },
                new User { Id = fixture.ExistingMemberId, Email = "existing@example.com", FullName = "Existing", IsActive = true },
                new User { Id = fixture.OutsideWorkspaceId, Email = "outside@example.com", FullName = "Outside", IsActive = true },
                new User { Id = fixture.PendingInviteId, Email = "pending@example.com", FullName = "Pending", IsActive = true },
                new User { Id = fixture.InactiveWorkspaceMemberId, Email = "inactive@example.com", FullName = "Inactive", IsActive = true });
            fixture.Context.Workspaces.AddRange(
                new Workspace { Id = fixture.WorkspaceId, OwnerId = fixture.OwnerId, Name = "Workspace", Slug = $"workspace-{fixture.WorkspaceId:N}" },
                new Workspace { Id = fixture.OtherWorkspaceId, OwnerId = fixture.OwnerId, Name = "Other", Slug = $"other-{fixture.OtherWorkspaceId:N}" });
            fixture.Context.WorkspaceMembers.AddRange(
                new WorkspaceMember { WorkspaceId = fixture.WorkspaceId, UserId = fixture.OwnerId, WorkspaceRole = "OWNER", IsActive = true },
                new WorkspaceMember { WorkspaceId = fixture.WorkspaceId, UserId = fixture.CandidateId, WorkspaceRole = "MEMBER", IsActive = true },
                new WorkspaceMember { WorkspaceId = fixture.WorkspaceId, UserId = fixture.ExistingMemberId, WorkspaceRole = "MEMBER", IsActive = true },
                new WorkspaceMember { WorkspaceId = fixture.OtherWorkspaceId, UserId = fixture.OutsideWorkspaceId, WorkspaceRole = "MEMBER", IsActive = true },
                new WorkspaceMember { WorkspaceId = fixture.WorkspaceId, UserId = fixture.PendingInviteId, WorkspaceRole = "MEMBER", IsActive = true },
                new WorkspaceMember { WorkspaceId = fixture.WorkspaceId, UserId = fixture.InactiveWorkspaceMemberId, WorkspaceRole = "MEMBER", IsActive = false });
            fixture.Context.Projects.Add(new Project
            {
                Id = fixture.ProjectId,
                WorkspaceId = fixture.WorkspaceId,
                CreatorId = fixture.OwnerId,
                Name = "Project",
                Identifier = "MEM1",
                Status = true
            });
            fixture.Context.ProjectMembers.AddRange(
                new ProjectMember { ProjectId = fixture.ProjectId, UserId = fixture.OwnerId, ProjectRole = "PM", Status = true, JoinedAt = now },
                new ProjectMember { ProjectId = fixture.ProjectId, UserId = fixture.ExistingMemberId, ProjectRole = "Developer", Status = true, JoinedAt = now },
                new ProjectMember { ProjectId = fixture.ProjectId, UserId = fixture.PendingInviteId, ProjectRole = "Developer", Status = false, JoinedAt = now });
            fixture.Context.Roles.AddRange(
                new Role { Id = Guid.NewGuid(), Name = "Developer" },
                new Role { Id = Guid.NewGuid(), Name = "PM" });
            await fixture.Context.SaveChangesAsync();

            fixture.EmailService.Setup(service => service.SendInviteEmailAsync(
                    It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(),
                    It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string?>()))
                .Returns(Task.CompletedTask);
            var configuration = new ConfigurationBuilder()
                .AddInMemoryCollection(new Dictionary<string, string?> { ["Frontend:BaseUrl"] = "http://localhost:5173" })
                .Build();
            fixture.Service = new ProjectMemberService(fixture.Context, fixture.EmailService.Object, configuration);
            return fixture;
        }

        public async ValueTask DisposeAsync() => await Context.DisposeAsync();
    }
}
