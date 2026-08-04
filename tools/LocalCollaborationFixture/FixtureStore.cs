using System.Security.Cryptography;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using TaskManagement.Domain.Entities;
using TaskManagement.Infrastructure.Data;

namespace LocalCollaborationFixture;

internal sealed class FixtureStore
{
    private readonly IServiceProvider _services;
    private readonly FixtureIdentity _identity;

    public FixtureStore(IServiceProvider services, FixtureIdentity identity)
    {
        _services = services;
        _identity = identity;
    }

    public async Task ProvisionAsync(CancellationToken cancellationToken = default)
    {
        await using var scope = _services.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        RequireSqlServer(context);
        if (!await context.Database.CanConnectAsync(cancellationToken))
            throw new InvalidOperationException("The approved SQL database is not reachable.");

        var strategy = context.Database.CreateExecutionStrategy();
        await strategy.ExecuteAsync(async () =>
        {
            await using var transaction = await context.Database.BeginTransactionAsync(
                cancellationToken);
            var now = DateTime.UtcNow;

            var userA = await UpsertUserAsync(context, _identity.UserAId, "USER_A", now, cancellationToken);
            var userB = await UpsertUserAsync(context, _identity.UserBId, "USER_B", now, cancellationToken);
            var userC = await UpsertUserAsync(context, _identity.UserCId, "USER_C", now, cancellationToken);
            await context.SaveChangesAsync(cancellationToken);

            var workspace = await context.Workspaces.IgnoreQueryFilters()
                .SingleOrDefaultAsync(item => item.Id == _identity.WorkspaceAId, cancellationToken);
            if (workspace == null)
            {
                workspace = new Workspace
                {
                    Id = _identity.WorkspaceAId,
                    Slug = _identity.Prefix,
                    Name = $"{_identity.Prefix}-WORKSPACE_A",
                    OwnerId = userA.Id,
                    Timezone = "Asia/Ho_Chi_Minh",
                    CreatedAt = now,
                    UpdatedAt = now
                };
                context.Workspaces.Add(workspace);
            }
            else
            {
                RequireOwned(workspace.Name, "workspace");
                workspace.Slug = _identity.Prefix;
                workspace.Name = $"{_identity.Prefix}-WORKSPACE_A";
                workspace.OwnerId = userA.Id;
                workspace.IsDeleted = false;
                workspace.UpdatedAt = now;
            }
            await context.SaveChangesAsync(cancellationToken);

            await UpsertWorkspaceMemberAsync(context, workspace.Id, userA.Id, "OWNER", now, cancellationToken);
            await UpsertWorkspaceMemberAsync(context, workspace.Id, userB.Id, "MEMBER", now, cancellationToken);

            var project = await context.Projects.IgnoreQueryFilters()
                .SingleOrDefaultAsync(item => item.Id == _identity.ProjectAId, cancellationToken);
            if (project == null)
            {
                project = new Project
                {
                    Id = _identity.ProjectAId,
                    WorkspaceId = workspace.Id,
                    CreatorId = userA.Id,
                    Name = $"{_identity.Prefix}-PROJECT_A",
                    Identifier = ProjectIdentifier(_identity.Prefix),
                    Description = $"{_identity.Prefix} local-only collaboration fixture",
                    NetworkType = "Private",
                    StartDate = now.Date,
                    Status = true,
                    CreatedAt = now,
                    UpdatedAt = now
                };
                context.Projects.Add(project);
            }
            else
            {
                RequireOwned(project.Name, "project");
                project.WorkspaceId = workspace.Id;
                project.CreatorId = userA.Id;
                project.Name = $"{_identity.Prefix}-PROJECT_A";
                project.Description = $"{_identity.Prefix} local-only collaboration fixture";
                project.NetworkType = "Private";
                project.Status = true;
                project.IsDeleted = false;
                project.IsArchived = false;
                project.UpdatedAt = now;
            }
            await context.SaveChangesAsync(cancellationToken);

            await UpsertProjectMemberAsync(context, project.Id, userA.Id, "PROJECT_MANAGER", now, cancellationToken);
            await UpsertProjectMemberAsync(context, project.Id, userB.Id, "DEVELOPER", now, cancellationToken);

            var channel = await context.CollaborationChannels.IgnoreQueryFilters()
                .SingleOrDefaultAsync(item => item.Id == _identity.ChannelAId, cancellationToken);
            if (channel == null)
            {
                channel = new CollaborationChannel
                {
                    Id = _identity.ChannelAId,
                    WorkspaceId = workspace.Id,
                    ProjectId = project.Id,
                    CreatedByUserId = userA.Id,
                    Name = $"{_identity.Prefix}-CHANNEL_A",
                    Description = $"{_identity.Prefix} local-only channel",
                    ProvisioningKey = $"{_identity.Prefix}-channel-a",
                    CreatedAt = now,
                    UpdatedAt = now
                };
                context.CollaborationChannels.Add(channel);
            }
            else
            {
                RequireOwned(channel.Name, "channel");
                channel.WorkspaceId = workspace.Id;
                channel.ProjectId = project.Id;
                channel.CreatedByUserId = userA.Id;
                channel.Name = $"{_identity.Prefix}-CHANNEL_A";
                channel.Description = $"{_identity.Prefix} local-only channel";
                channel.ProvisioningKey = $"{_identity.Prefix}-channel-a";
                channel.IsArchived = false;
                channel.IsDeleted = false;
                channel.UpdatedAt = now;
            }
            await context.SaveChangesAsync(cancellationToken);

            await UpsertChannelMemberAsync(context, channel.Id, userA.Id, now, cancellationToken);
            await UpsertChannelMemberAsync(context, channel.Id, userB.Id, now, cancellationToken);

            var pair = FixtureIdentity.Pair(userA.Id, userB.Id);
            var conversation = await context.DirectConversations
                .SingleOrDefaultAsync(item => item.Id == _identity.ConversationAbId, cancellationToken);
            if (conversation == null)
            {
                var pairCollision = await context.DirectConversations.AsNoTracking()
                    .AnyAsync(item => item.UserLowId == pair.Low && item.UserHighId == pair.High, cancellationToken);
                if (pairCollision)
                    throw new FixtureSafetyException("A non-fixture direct conversation already uses fixture user IDs.");
                conversation = new DirectConversation
                {
                    Id = _identity.ConversationAbId,
                    WorkspaceId = workspace.Id,
                    UserLowId = pair.Low,
                    UserHighId = pair.High,
                    CreatedAt = now
                };
                context.DirectConversations.Add(conversation);
            }
            else if (conversation.UserLowId != pair.Low || conversation.UserHighId != pair.High)
            {
                throw new FixtureSafetyException("Fixture conversation ID collides with a non-fixture pair.");
            }
            await context.SaveChangesAsync(cancellationToken);

            await UpsertParticipantAsync(context, conversation.Id, userA.Id, now, cancellationToken);
            await UpsertParticipantAsync(context, conversation.Id, userB.Id, now, cancellationToken);
            await context.SaveChangesAsync(cancellationToken);

            await AssertMatrixAsync(context, cancellationToken);
            await transaction.CommitAsync(cancellationToken);
        });
    }

    public async Task AssertMatrixAsync(CancellationToken cancellationToken = default)
    {
        await using var scope = _services.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        RequireSqlServer(context);
        await AssertMatrixAsync(context, cancellationToken);
    }

    public async Task CleanupAsync(CancellationToken cancellationToken = default)
    {
        await using var scope = _services.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        RequireSqlServer(context);
        var strategy = context.Database.CreateExecutionStrategy();
        await strategy.ExecuteAsync(async () =>
        {
            await using var transaction = await context.Database.BeginTransactionAsync(cancellationToken);
            await VerifyCleanupOwnershipAsync(context, cancellationToken);

            await context.DirectConversationReadStates
                .Where(item => item.ConversationId == _identity.ConversationAbId)
                .ExecuteDeleteAsync(cancellationToken);
            await context.DirectMessages
                .Where(item => item.ConversationId == _identity.ConversationAbId)
                .ExecuteDeleteAsync(cancellationToken);
            await context.DirectConversationParticipants
                .Where(item => item.ConversationId == _identity.ConversationAbId)
                .ExecuteDeleteAsync(cancellationToken);
            await context.DirectConversations
                .Where(item => item.Id == _identity.ConversationAbId)
                .ExecuteDeleteAsync(cancellationToken);

            await context.CollaborationChannelReadStates
                .Where(item => item.ChannelId == _identity.ChannelAId)
                .ExecuteDeleteAsync(cancellationToken);
            await context.ChannelMessages
                .Where(item => item.CollaborationChannelId == _identity.ChannelAId)
                .ExecuteDeleteAsync(cancellationToken);
            await context.CollaborationChannelMembers
                .Where(item => item.ChannelId == _identity.ChannelAId)
                .ExecuteDeleteAsync(cancellationToken);
            await context.CollaborationChannels
                .Where(item => item.Id == _identity.ChannelAId)
                .ExecuteDeleteAsync(cancellationToken);

            await context.ProjectMembers
                .Where(item => item.ProjectId == _identity.ProjectAId)
                .ExecuteDeleteAsync(cancellationToken);
            await context.Projects.IgnoreQueryFilters()
                .Where(item => item.Id == _identity.ProjectAId)
                .ExecuteDeleteAsync(cancellationToken);

            await context.WorkspaceMembers
                .Where(item => item.WorkspaceId == _identity.WorkspaceAId)
                .ExecuteDeleteAsync(cancellationToken);
            await context.Workspaces.IgnoreQueryFilters()
                .Where(item => item.Id == _identity.WorkspaceAId)
                .ExecuteDeleteAsync(cancellationToken);

            await context.RefreshTokens
                .Where(item => _identity.UserIds.Contains(item.UserId))
                .ExecuteDeleteAsync(cancellationToken);
            await context.ExternalLogins
                .Where(item => _identity.UserIds.Contains(item.UserId))
                .ExecuteDeleteAsync(cancellationToken);
            await context.UserRoles
                .Where(item => _identity.UserIds.Contains(item.UserId))
                .ExecuteDeleteAsync(cancellationToken);
            await context.Users.IgnoreQueryFilters()
                .Where(item => _identity.UserIds.Contains(item.Id))
                .ExecuteDeleteAsync(cancellationToken);

            await transaction.CommitAsync(cancellationToken);
        });
    }

    public async Task AssertCleanAsync(CancellationToken cancellationToken = default)
    {
        await using var scope = _services.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        RequireSqlServer(context);
        var remaining =
            await context.Users.IgnoreQueryFilters().CountAsync(item => _identity.UserIds.Contains(item.Id), cancellationToken) +
            await context.Workspaces.IgnoreQueryFilters().CountAsync(item => item.Id == _identity.WorkspaceAId, cancellationToken) +
            await context.Projects.IgnoreQueryFilters().CountAsync(item => item.Id == _identity.ProjectAId, cancellationToken) +
            await context.CollaborationChannels.IgnoreQueryFilters().CountAsync(item => item.Id == _identity.ChannelAId, cancellationToken) +
            await context.DirectConversations.CountAsync(item => item.Id == _identity.ConversationAbId, cancellationToken) +
            await context.CollaborationChannelReadStates.CountAsync(item => item.ChannelId == _identity.ChannelAId, cancellationToken) +
            await context.DirectConversationReadStates.CountAsync(item => item.ConversationId == _identity.ConversationAbId, cancellationToken);
        if (remaining != 0)
            throw new InvalidOperationException($"Cleanup left {remaining} fixture root rows.");
    }

    private static void RequireSqlServer(ApplicationDbContext context)
    {
        if (context.Database.ProviderName != "Microsoft.EntityFrameworkCore.SqlServer")
            throw new FixtureSafetyException("The fixture runtime is not using the SQL Server provider.");
    }

    private async Task<User> UpsertUserAsync(
        ApplicationDbContext context,
        Guid id,
        string label,
        DateTime now,
        CancellationToken cancellationToken)
    {
        var expectedEmail = _identity.Email(label);
        var user = await context.Users.IgnoreQueryFilters()
            .SingleOrDefaultAsync(item => item.Id == id, cancellationToken);
        if (user == null)
        {
            user = new User { Id = id, CreatedAt = now };
            context.Users.Add(user);
        }
        else if (!user.Email.StartsWith(_identity.Prefix, StringComparison.Ordinal))
        {
            throw new FixtureSafetyException($"Fixture {label} ID collides with a non-fixture user.");
        }

        user.Email = expectedEmail;
        user.FullName = $"{_identity.Prefix}-{label}";
        user.PasswordHash = Convert.ToBase64String(RandomNumberGenerator.GetBytes(48));
        user.IsActive = true;
        user.IsDeleted = false;
        user.UpdatedAt = now;
        user.RefreshToken = null;
        user.RefreshTokenExpiryTime = null;
        return user;
    }

    private static async Task UpsertWorkspaceMemberAsync(
        ApplicationDbContext context,
        Guid workspaceId,
        Guid userId,
        string role,
        DateTime now,
        CancellationToken cancellationToken)
    {
        var member = await context.WorkspaceMembers.SingleOrDefaultAsync(
            item => item.WorkspaceId == workspaceId && item.UserId == userId,
            cancellationToken);
        if (member == null)
        {
            context.WorkspaceMembers.Add(new WorkspaceMember
            {
                WorkspaceId = workspaceId,
                UserId = userId,
                WorkspaceRole = role,
                JoinedAt = now,
                IsActive = true
            });
            return;
        }
        member.WorkspaceRole = role;
        member.IsActive = true;
    }

    private static async Task UpsertProjectMemberAsync(
        ApplicationDbContext context,
        Guid projectId,
        Guid userId,
        string role,
        DateTime now,
        CancellationToken cancellationToken)
    {
        var member = await context.ProjectMembers.SingleOrDefaultAsync(
            item => item.ProjectId == projectId && item.UserId == userId,
            cancellationToken);
        if (member == null)
        {
            context.ProjectMembers.Add(new ProjectMember
            {
                ProjectId = projectId,
                UserId = userId,
                ProjectRole = role,
                JoinedAt = now,
                Status = true
            });
            return;
        }
        member.ProjectRole = role;
        member.Status = true;
        member.LeftAt = null;
    }

    private static async Task UpsertChannelMemberAsync(
        ApplicationDbContext context,
        Guid channelId,
        Guid userId,
        DateTime now,
        CancellationToken cancellationToken)
    {
        var member = await context.CollaborationChannelMembers.SingleOrDefaultAsync(
            item => item.ChannelId == channelId && item.UserId == userId,
            cancellationToken);
        if (member == null)
        {
            context.CollaborationChannelMembers.Add(new CollaborationChannelMember
            {
                ChannelId = channelId,
                UserId = userId,
                JoinedAt = now,
                IsActive = true,
                CanSendMessages = true
            });
            return;
        }
        member.IsActive = true;
        member.CanSendMessages = true;
        member.LeftAt = null;
    }

    private static async Task UpsertParticipantAsync(
        ApplicationDbContext context,
        Guid conversationId,
        Guid userId,
        DateTime now,
        CancellationToken cancellationToken)
    {
        if (await context.DirectConversationParticipants.AnyAsync(
                item => item.ConversationId == conversationId && item.UserId == userId,
                cancellationToken)) return;
        context.DirectConversationParticipants.Add(new DirectConversationParticipant
        {
            ConversationId = conversationId,
            UserId = userId,
            JoinedAt = now
        });
    }

    private async Task AssertMatrixAsync(
        ApplicationDbContext context,
        CancellationToken cancellationToken)
    {
        var activeWorkspaceUsers = await context.WorkspaceMembers.AsNoTracking()
            .Where(item => item.WorkspaceId == _identity.WorkspaceAId && item.IsActive)
            .Select(item => item.UserId)
            .OrderBy(item => item)
            .ToListAsync(cancellationToken);
        RequireSet(activeWorkspaceUsers, _identity.UserAId, _identity.UserBId);

        var activeProjectUsers = await context.ProjectMembers.AsNoTracking()
            .Where(item => item.ProjectId == _identity.ProjectAId && item.Status)
            .Select(item => item.UserId)
            .OrderBy(item => item)
            .ToListAsync(cancellationToken);
        RequireSet(activeProjectUsers, _identity.UserAId, _identity.UserBId);

        var activeChannelUsers = await context.CollaborationChannelMembers.AsNoTracking()
            .Where(item => item.ChannelId == _identity.ChannelAId && item.IsActive && item.LeftAt == null)
            .Select(item => item.UserId)
            .OrderBy(item => item)
            .ToListAsync(cancellationToken);
        RequireSet(activeChannelUsers, _identity.UserAId, _identity.UserBId);

        var participants = await context.DirectConversationParticipants.AsNoTracking()
            .Where(item => item.ConversationId == _identity.ConversationAbId)
            .Select(item => item.UserId)
            .OrderBy(item => item)
            .ToListAsync(cancellationToken);
        RequireSet(participants, _identity.UserAId, _identity.UserBId);

        if (activeWorkspaceUsers.Contains(_identity.UserCId) ||
            activeProjectUsers.Contains(_identity.UserCId) ||
            activeChannelUsers.Contains(_identity.UserCId) ||
            participants.Contains(_identity.UserCId))
            throw new InvalidOperationException("USER_C leaked into the protected fixture graph.");
    }

    private async Task VerifyCleanupOwnershipAsync(
        ApplicationDbContext context,
        CancellationToken cancellationToken)
    {
        var workspaceName = await context.Workspaces.IgnoreQueryFilters()
            .Where(item => item.Id == _identity.WorkspaceAId)
            .Select(item => item.Name)
            .SingleOrDefaultAsync(cancellationToken);
        if (workspaceName != null) RequireOwned(workspaceName, "workspace");

        var projectName = await context.Projects.IgnoreQueryFilters()
            .Where(item => item.Id == _identity.ProjectAId)
            .Select(item => item.Name)
            .SingleOrDefaultAsync(cancellationToken);
        if (projectName != null) RequireOwned(projectName, "project");

        var channelName = await context.CollaborationChannels.IgnoreQueryFilters()
            .Where(item => item.Id == _identity.ChannelAId)
            .Select(item => item.Name)
            .SingleOrDefaultAsync(cancellationToken);
        if (channelName != null) RequireOwned(channelName, "channel");

        var foreignUsers = await context.Users.IgnoreQueryFilters()
            .Where(item => _identity.UserIds.Contains(item.Id) && !item.Email.StartsWith(_identity.Prefix))
            .CountAsync(cancellationToken);
        if (foreignUsers != 0)
            throw new FixtureSafetyException("Cleanup refused because a fixture user ID is not prefix-owned.");
    }

    private void RequireOwned(string value, string category)
    {
        if (!value.StartsWith(_identity.Prefix, StringComparison.Ordinal))
            throw new FixtureSafetyException($"Fixture {category} ID collides with non-prefix data.");
    }

    private static void RequireSet(IReadOnlyCollection<Guid> actual, params Guid[] expected)
    {
        if (actual.Count != expected.Length || expected.Any(item => !actual.Contains(item)))
            throw new InvalidOperationException("Fixture membership matrix is not exact.");
    }

    private static string ProjectIdentifier(string prefix)
    {
        var hash = Convert.ToHexString(SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(prefix)));
        return $"E2E{hash[..7]}";
    }
}
