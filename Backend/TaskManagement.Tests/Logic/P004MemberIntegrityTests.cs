using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.Extensions.Configuration;
using Moq;
using TaskManagement.Application.DTOs.Project;
using TaskManagement.Application.Interfaces;
using TaskManagement.Domain.Entities;
using TaskManagement.Infrastructure.Data;
using TaskManagement.Infrastructure.Services;

namespace TaskManagement.Tests.Logic;

public sealed class P004MemberIntegrityTests
{
    [Fact]
    public async Task TC_MEM_006_EmailCasingPendingRetryAndExistingMemberAreIdempotent()
    {
        await using var fixture = await MemberFixture.CreateInMemoryAsync();
        var first = await fixture.Service.InviteMemberAsync(
            fixture.ProjectId,
            new ProjectMemberRequestDto { Email = "  New.Member@Example.COM ", Role = "Developer" },
            "Owner");
        var retry = await fixture.Service.InviteMemberAsync(
            fixture.ProjectId,
            new ProjectMemberRequestDto { Email = "new.member@example.com", Role = "Developer" },
            "Owner");

        first.Should().Be(ProjectInvitationOutcome.InvitationCreated);
        retry.Should().Be(ProjectInvitationOutcome.InvitationAlreadyPending);
        (await fixture.Context.Users.CountAsync(user => user.Email == "new.member@example.com")).Should().Be(1);
        (await fixture.Context.ProjectMembers.CountAsync(member => member.ProjectId == fixture.ProjectId)).Should().Be(3);
        (await fixture.Context.RefreshTokens.CountAsync(token => !token.IsRevoked)).Should().Be(1);
        fixture.EmailService.Verify(service => service.SendInviteEmailAsync(
            "new.member@example.com",
            It.IsAny<string>(),
            It.IsAny<string>(),
            It.IsAny<string>(),
            It.IsAny<string>(),
            It.IsAny<string>(),
            It.IsAny<string?>()), Times.Once);

        var invitedUser = await fixture.Context.Users.SingleAsync(user => user.Email == "new.member@example.com");
        var workspaceMembership = await fixture.Context.WorkspaceMembers.SingleAsync(member =>
            member.WorkspaceId == fixture.WorkspaceId && member.UserId == invitedUser.Id);
        workspaceMembership.IsActive.Should().BeFalse("access starts only after invite acceptance");

        var projectMembership = await fixture.Context.ProjectMembers.SingleAsync(member =>
            member.ProjectId == fixture.ProjectId && member.UserId == invitedUser.Id);
        projectMembership.Status = true;
        workspaceMembership.IsActive = true;
        await fixture.Context.SaveChangesAsync();

        var existing = await fixture.Service.InviteMemberAsync(
            fixture.ProjectId,
            new ProjectMemberRequestDto { Email = "NEW.MEMBER@example.com", Role = "Developer" },
            "Owner");
        existing.Should().Be(ProjectInvitationOutcome.AlreadyActiveMember);
        fixture.EmailService.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task TC_MEM_023_RemoveMemberPreservesAssignmentContributionAndRevokesAccess()
    {
        await using var fixture = await MemberFixture.CreateInMemoryAsync();
        var taskId = await fixture.AddAssignedTaskAsync();

        await fixture.Service.RemoveMemberAsync(
            fixture.ProjectId,
            fixture.MemberId,
            fixture.OwnerId,
            "Team change");

        var membership = await fixture.Context.ProjectMembers.SingleAsync(member =>
            member.ProjectId == fixture.ProjectId && member.UserId == fixture.MemberId);
        membership.Status.Should().BeFalse();
        membership.LeftAt.Should().NotBeNull();

        var assignment = await fixture.Context.TaskAssignments
            .Include(item => item.User)
            .SingleAsync(item => item.WorkTaskId == taskId && item.UserId == fixture.MemberId);
        assignment.Status.Should().BeFalse();
        assignment.RemovedAt.Should().NotBeNull();
        assignment.RemovedBy.Should().Be(fixture.OwnerId);
        assignment.RemovalReason.Should().Be("Team change");
        assignment.ProgressPercent.Should().Be(75);
        assignment.ContributionWeight.Should().Be(0.8);
        assignment.EstimatedHours.Should().Be(8);
        assignment.User.FullName.Should().Be("Contributor");

        (await fixture.Service.GetProjectMembersAsync(fixture.ProjectId))
            .Should().NotContain(member => member.UserId == fixture.MemberId);
        (await fixture.Context.AuditLogs.CountAsync(log =>
            log.WorkTaskId == taskId && log.FieldChanged == "ASSIGNEE_REMOVED_FROM_PROJECT"))
            .Should().Be(1);
        (await fixture.Context.WorkTasks.FindAsync(taskId))!.AssignedUserId.Should().BeNull();

        var authorization = await new ResourceAuthorizationService(fixture.Context)
            .AuthorizeProjectAsync(fixture.MemberId, fixture.ProjectId, "project.read");
        authorization.Succeeded.Should().BeFalse("project permission must be revoked immediately");

        var readd = await fixture.Service.InviteMemberAsync(
            fixture.ProjectId,
            new ProjectMemberRequestDto { Email = "CONTRIBUTOR@example.com", Role = "Developer" },
            "Owner");
        readd.Should().Be(ProjectInvitationOutcome.InvitationCreated);

        membership.Status.Should().BeFalse("re-add remains pending until accepted");
        membership.LeftAt.Should().BeNull();
        assignment.Status.Should().BeFalse("historical assignments must not reactivate with membership");
        (await fixture.Context.TaskAssignments.CountAsync(item =>
            item.WorkTaskId == taskId && item.UserId == fixture.MemberId)).Should().Be(1);

        membership.Status = true;
        assignment.Activate();
        await fixture.Context.SaveChangesAsync();
        assignment.Status.Should().BeTrue();
        assignment.RemovedAt.Should().BeNull();
        assignment.RemovedBy.Should().BeNull();
        assignment.RemovalReason.Should().BeNull();
        assignment.ProgressPercent.Should().Be(75, "explicit reassignment reuses the contribution record without contradictory removal state");
    }

    private sealed class MemberFixture : IAsyncDisposable
    {
        private MemberFixture(
            ApplicationDbContext context,
            Mock<IEmailService> emailService,
            ProjectMemberService service,
            Guid workspaceId,
            Guid projectId,
            Guid ownerId,
            Guid memberId)
        {
            Context = context;
            EmailService = emailService;
            Service = service;
            WorkspaceId = workspaceId;
            ProjectId = projectId;
            OwnerId = ownerId;
            MemberId = memberId;
        }

        public ApplicationDbContext Context { get; }
        public Mock<IEmailService> EmailService { get; }
        public ProjectMemberService Service { get; }
        public Guid WorkspaceId { get; }
        public Guid ProjectId { get; }
        public Guid OwnerId { get; }
        public Guid MemberId { get; }

        public static async Task<MemberFixture> CreateInMemoryAsync()
        {
            var options = new DbContextOptionsBuilder<ApplicationDbContext>()
                .UseInMemoryDatabase(Guid.NewGuid().ToString("N"))
                .Options;
            var context = new ApplicationDbContext(options);
            var workspaceId = Guid.NewGuid();
            var projectId = Guid.NewGuid();
            var ownerId = Guid.NewGuid();
            var memberId = Guid.NewGuid();
            context.Users.AddRange(
                new User { Id = ownerId, Email = "owner@example.com", FullName = "Owner", PasswordHash = "unused", IsActive = true },
                new User { Id = memberId, Email = "contributor@example.com", FullName = "Contributor", PasswordHash = "unused", IsActive = true });
            context.Workspaces.Add(new Workspace { Id = workspaceId, OwnerId = ownerId, Name = "Workspace", Slug = $"ws-{workspaceId:N}" });
            context.WorkspaceMembers.AddRange(
                new WorkspaceMember { WorkspaceId = workspaceId, UserId = ownerId, WorkspaceRole = "OWNER", IsActive = true },
                new WorkspaceMember { WorkspaceId = workspaceId, UserId = memberId, WorkspaceRole = "MEMBER", IsActive = true });
            context.Projects.Add(new Project { Id = projectId, WorkspaceId = workspaceId, CreatorId = ownerId, Name = "Project", Identifier = "MEM", Status = true });
            context.ProjectMembers.AddRange(
                new ProjectMember { ProjectId = projectId, UserId = ownerId, ProjectRole = "PM", Status = true, JoinedAt = DateTime.UtcNow },
                new ProjectMember { ProjectId = projectId, UserId = memberId, ProjectRole = "Developer", Status = true, JoinedAt = DateTime.UtcNow });
            context.Roles.AddRange(
                new Role { Id = Guid.NewGuid(), Name = "Developer" },
                new Role { Id = Guid.NewGuid(), Name = "PM" });
            await context.SaveChangesAsync();

            var emailService = new Mock<IEmailService>();
            emailService.Setup(service => service.SendInviteEmailAsync(
                    It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(),
                    It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string?>()))
                .Returns(Task.CompletedTask);
            var configuration = new ConfigurationBuilder()
                .AddInMemoryCollection(new Dictionary<string, string?> { ["Frontend:BaseUrl"] = "http://localhost:5173" })
                .Build();
            return new MemberFixture(
                context,
                emailService,
                new ProjectMemberService(context, emailService.Object, configuration),
                workspaceId,
                projectId,
                ownerId,
                memberId);
        }

        public async Task<Guid> AddAssignedTaskAsync()
        {
            var statusId = Guid.NewGuid();
            var typeId = Guid.NewGuid();
            var taskId = Guid.NewGuid();
            Context.TaskStatuses.Add(new TaskManagement.Domain.Entities.TaskStatus { Id = statusId, ProjectId = ProjectId, Name = "In Progress" });
            Context.TaskTypes.Add(new TaskType { Id = typeId, ProjectId = ProjectId, Name = "Task" });
            Context.WorkTasks.Add(new WorkTask
            {
                Id = taskId,
                ProjectId = ProjectId,
                WorkspaceId = WorkspaceId,
                TaskStatusId = statusId,
                TaskTypeId = typeId,
                ReporterId = OwnerId,
                AssignedUserId = MemberId,
                Title = "Historical contribution",
                SequenceId = "MEM-1",
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            });
            Context.TaskAssignments.Add(new TaskAssignment
            {
                WorkTaskId = taskId,
                UserId = MemberId,
                Status = true,
                ProgressPercent = 75,
                ContributionWeight = 0.8,
                EstimatedHours = 8,
                TotalActualHours = 6
            });
            await Context.SaveChangesAsync();
            return taskId;
        }

        public async ValueTask DisposeAsync() => await Context.DisposeAsync();
    }
}

public sealed class P004SqlServerIntegrationTests
{
    private const string PreviousMigration = "20260716233911_AddFloatingStickiesMvp";

    [Fact]
    [Trait("Database", "SqlServer")]
    public async Task TC_MEM_006_ConcurrentCanonicalInvitesCreateOneMembershipTokenAndEmail()
    {
        var databaseName = $"TaskManagement_P004_Invite_{Guid.NewGuid():N}";
        var options = SqlOptions(databaseName);
        await using var setup = new ApplicationDbContext(options);
        try
        {
            await setup.Database.MigrateAsync();
            var seed = await SeedInvitationProjectAsync(setup);
            var emailService = new Mock<IEmailService>();
            emailService.Setup(service => service.SendInviteEmailAsync(
                    It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(),
                    It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string?>()))
                .Returns(Task.CompletedTask);
            var configuration = TestConfiguration();

            await using var contextA = new ApplicationDbContext(options);
            await using var contextB = new ApplicationDbContext(options);
            var serviceA = new ProjectMemberService(contextA, emailService.Object, configuration);
            var serviceB = new ProjectMemberService(contextB, emailService.Object, configuration);
            var start = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            var first = Task.Run(async () =>
            {
                await start.Task;
                return await serviceA.InviteMemberAsync(seed.ProjectId,
                    new ProjectMemberRequestDto { Email = " Race.User@Example.COM ", Role = "Developer" }, "Owner");
            });
            var second = Task.Run(async () =>
            {
                await start.Task;
                return await serviceB.InviteMemberAsync(seed.ProjectId,
                    new ProjectMemberRequestDto { Email = "race.user@example.com", Role = "Developer" }, "Owner");
            });

            start.SetResult();
            var outcomes = await Task.WhenAll(first, second);
            outcomes.Should().Contain(ProjectInvitationOutcome.InvitationCreated);
            outcomes.Should().Contain(ProjectInvitationOutcome.InvitationAlreadyPending);

            await using var verification = new ApplicationDbContext(options);
            var user = await verification.Users.SingleAsync(item => item.Email == "race.user@example.com");
            (await verification.ProjectMembers.CountAsync(item =>
                item.ProjectId == seed.ProjectId && item.UserId == user.Id)).Should().Be(1);
            (await verification.RefreshTokens.CountAsync(item =>
                item.UserId == user.Id && !item.IsRevoked)).Should().Be(1);
            emailService.Verify(service => service.SendInviteEmailAsync(
                "race.user@example.com",
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(),
                It.IsAny<string>(), It.IsAny<string?>()), Times.Once);
        }
        finally
        {
            await setup.Database.EnsureDeletedAsync();
        }
    }

    [Fact]
    [Trait("Database", "SqlServer")]
    public async Task PreserveAssignmentHistoryMigration_UpDownAndReapplyPasses()
    {
        var databaseName = $"TaskManagement_P004_Migration_{Guid.NewGuid():N}";
        var options = SqlOptions(databaseName);
        await using var context = new ApplicationDbContext(options);
        try
        {
            var migrator = context.GetService<IMigrator>();
            await migrator.MigrateAsync();
            (await HasColumnAsync(context, "TaskAssignments", "RemovedAt")).Should().BeTrue();
            (await HasColumnAsync(context, "TaskAssignments", "RemovedBy")).Should().BeTrue();
            (await HasColumnAsync(context, "TaskAssignments", "RemovalReason")).Should().BeTrue();

            await migrator.MigrateAsync(PreviousMigration);
            (await HasColumnAsync(context, "TaskAssignments", "RemovedAt")).Should().BeFalse();

            await migrator.MigrateAsync();
            (await HasColumnAsync(context, "TaskAssignments", "RemovedAt")).Should().BeTrue();
        }
        finally
        {
            await context.Database.EnsureDeletedAsync();
        }
    }

    private static DbContextOptions<ApplicationDbContext> SqlOptions(string databaseName) =>
        new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseSqlServer($"Server=KHOI\\SQLEXPRESS;Database={databaseName};Trusted_Connection=True;TrustServerCertificate=True;Encrypt=False;Connect Timeout=30")
            .Options;

    private static IConfiguration TestConfiguration() => new ConfigurationBuilder()
        .AddInMemoryCollection(new Dictionary<string, string?> { ["Frontend:BaseUrl"] = "http://localhost:5173" })
        .Build();

    private static async Task<(Guid ProjectId, Guid WorkspaceId)> SeedInvitationProjectAsync(ApplicationDbContext context)
    {
        var ownerId = Guid.NewGuid();
        var workspaceId = Guid.NewGuid();
        var projectId = Guid.NewGuid();
        context.Users.Add(new User { Id = ownerId, Email = $"owner-{ownerId:N}@example.com", PasswordHash = "unused", IsActive = true });
        context.Workspaces.Add(new Workspace { Id = workspaceId, OwnerId = ownerId, Name = "Workspace", Slug = $"ws-{workspaceId:N}" });
        context.WorkspaceMembers.Add(new WorkspaceMember { WorkspaceId = workspaceId, UserId = ownerId, WorkspaceRole = "OWNER", IsActive = true });
        context.Projects.Add(new Project { Id = projectId, WorkspaceId = workspaceId, CreatorId = ownerId, Name = "Project", Identifier = "INV", Status = true });
        context.ProjectMembers.Add(new ProjectMember { ProjectId = projectId, UserId = ownerId, ProjectRole = "PM", Status = true, JoinedAt = DateTime.UtcNow });
        context.Roles.Add(new Role { Id = Guid.NewGuid(), Name = "Developer" });
        await context.SaveChangesAsync();
        return (projectId, workspaceId);
    }

    private static async Task<bool> HasColumnAsync(
        ApplicationDbContext context,
        string tableName,
        string columnName)
    {
        await context.Database.OpenConnectionAsync();
        try
        {
            await using var command = context.Database.GetDbConnection().CreateCommand();
            command.CommandText = "SELECT COUNT(*) FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME = @table AND COLUMN_NAME = @column";
            var table = command.CreateParameter();
            table.ParameterName = "@table";
            table.Value = tableName;
            command.Parameters.Add(table);
            var column = command.CreateParameter();
            column.ParameterName = "@column";
            column.Value = columnName;
            command.Parameters.Add(column);
            return Convert.ToInt32(await command.ExecuteScalarAsync()) == 1;
        }
        finally
        {
            await context.Database.CloseConnectionAsync();
        }
    }
}
