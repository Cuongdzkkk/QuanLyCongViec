using System.Data;
using System.Text;
using Microsoft.EntityFrameworkCore;
using TaskManagement.Application.Common;
using TaskManagement.Application.DTOs.Collaboration;
using TaskManagement.Application.Interfaces;
using TaskManagement.Domain.Entities;
using TaskManagement.Infrastructure.Data;

namespace TaskManagement.Infrastructure.Services;

public sealed class CollaborationChannelService : ICollaborationChannelService
{
    public const int MaximumPageSize = 100;
    public const int MaximumNameLength = 100;
    public const int MaximumDescriptionLength = 500;
    public const int MaximumIdempotencyKeyLength = 100;
    public const string PrivateVisibility = "Private";
    public const string Ordering = "name_asc,createdAt_asc,channelId_asc";

    private readonly ApplicationDbContext _context;
    private readonly IResourceAuthorizationService _authorization;

    public CollaborationChannelService(
        ApplicationDbContext context,
        IResourceAuthorizationService authorization)
    {
        _context = context;
        _authorization = authorization;
    }

    public async Task<CollaborationChannelPageDto> DiscoverAsync(
        Guid projectId,
        Guid userId,
        int page,
        int pageSize,
        CancellationToken cancellationToken = default)
    {
        ValidatePage(page, pageSize);
        var workspaceId = await GetActiveProjectWorkspaceIdAsync(projectId, cancellationToken);
        await AuthorizeReadAsync(workspaceId, projectId, userId);
        var canManage = (await _authorization.AuthorizeProjectAsync(
            userId,
            projectId,
            ResourcePermissionCodes.ProjectWrite)).Succeeded;

        var query = _context.CollaborationChannels
            .AsNoTracking()
            .Where(channel =>
                channel.ProjectId == projectId &&
                channel.WorkspaceId == workspaceId &&
                !channel.IsDeleted &&
                !channel.IsArchived &&
                channel.Members.Any(member =>
                    member.UserId == userId &&
                    member.IsActive &&
                    member.LeftAt == null &&
                    member.User.IsActive &&
                    !member.User.IsDeleted));

        var totalCount = await query.CountAsync(cancellationToken);
        var items = await query
            .OrderBy(channel => channel.Name)
            .ThenBy(channel => channel.CreatedAt)
            .ThenBy(channel => channel.Id)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(channel => new CollaborationChannelDto(
                channel.Id,
                channel.Name,
                channel.Description,
                channel.WorkspaceId,
                channel.ProjectId,
                PrivateVisibility,
                true,
                true,
                channel.Members
                    .Where(member =>
                        member.UserId == userId &&
                        member.IsActive &&
                        member.LeftAt == null)
                    .Select(member => member.CanSendMessages)
                    .Single(),
                canManage,
                channel.CreatedAt,
                channel.UpdatedAt))
            .ToListAsync(cancellationToken);

        return new(items, page, pageSize, totalCount, Ordering);
    }

    public async Task<ProvisionCollaborationChannelResult> CreateAsync(
        Guid projectId,
        Guid userId,
        CreateCollaborationChannelRequestDto request,
        string? idempotencyKey,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        var name = NormalizeName(request.Name);
        var description = NormalizeDescription(request.Description);
        ValidateVisibility(request.Visibility);
        var normalizedKey = NormalizeIdempotencyKey(idempotencyKey);
        var workspaceId = await GetActiveProjectWorkspaceIdAsync(projectId, cancellationToken);
        await AuthorizeManageAsync(workspaceId, projectId, userId);

        var existing = await FindByProvisioningKeyAsync(
            projectId,
            userId,
            normalizedKey,
            cancellationToken);
        if (existing != null)
            return ExistingResult(existing, name, description);

        await using var transaction = _context.Database.IsRelational()
            ? await _context.Database.BeginTransactionAsync(
                IsolationLevel.Serializable,
                cancellationToken)
            : null;

        try
        {
            // Recheck under the transaction so an archived project cannot receive a new channel.
            workspaceId = await GetActiveProjectWorkspaceIdAsync(
                projectId,
                cancellationToken,
                lockForUpdate: true);
            await AuthorizeManageAsync(workspaceId, projectId, userId);

            var now = DateTime.UtcNow;
            var channel = new CollaborationChannel
            {
                Id = Guid.NewGuid(),
                WorkspaceId = workspaceId,
                ProjectId = projectId,
                CreatedByUserId = userId,
                Name = name,
                Description = description,
                ProvisioningKey = normalizedKey,
                CreatedAt = now,
                UpdatedAt = now
            };
            var membership = new CollaborationChannelMember
            {
                ChannelId = channel.Id,
                UserId = userId,
                JoinedAt = now,
                IsActive = true,
                CanSendMessages = true
            };

            _context.CollaborationChannels.Add(channel);
            _context.CollaborationChannelMembers.Add(membership);
            await _context.SaveChangesAsync(cancellationToken);
            if (transaction != null) await transaction.CommitAsync(cancellationToken);

            return new(ToDto(channel, canManage: true, canSend: true), true);
        }
        catch (DbUpdateException)
        {
            if (transaction != null) await transaction.RollbackAsync(cancellationToken);
            _context.ChangeTracker.Clear();
            var concurrent = await FindByProvisioningKeyAsync(
                projectId,
                userId,
                normalizedKey,
                cancellationToken);
            if (concurrent != null)
                return ExistingResult(concurrent, name, description);
            throw;
        }
    }

    private async Task<Guid> GetActiveProjectWorkspaceIdAsync(
        Guid projectId,
        CancellationToken cancellationToken,
        bool lockForUpdate = false)
    {
        if (lockForUpdate &&
            _context.Database.ProviderName == "Microsoft.EntityFrameworkCore.SqlServer")
        {
            var lockedWorkspaceId = await _context.Database
                .SqlQuery<Guid>(
                    $"""
                    SELECT project_row.[WorkspaceId] AS [Value]
                    FROM [Projects] AS project_row WITH (UPDLOCK, HOLDLOCK)
                    INNER JOIN [Workspaces] AS workspace_row
                        ON project_row.[WorkspaceId] = workspace_row.[Id]
                    WHERE project_row.[Id] = {projectId}
                      AND project_row.[Status] = CAST(1 AS bit)
                      AND project_row.[IsDeleted] = CAST(0 AS bit)
                      AND project_row.[IsArchived] = CAST(0 AS bit)
                      AND workspace_row.[IsDeleted] = CAST(0 AS bit)
                    """)
                .SingleOrDefaultAsync(cancellationToken);
            return lockedWorkspaceId == Guid.Empty
                ? throw new CollaborationProjectNotFoundException()
                : lockedWorkspaceId;
        }

        var workspaceId = await _context.Projects
            .AsNoTracking()
            .Where(project =>
                project.Id == projectId &&
                project.Status &&
                !project.IsDeleted &&
                !project.IsArchived &&
                !project.Workspace.IsDeleted)
            .Select(project => (Guid?)project.WorkspaceId)
            .SingleOrDefaultAsync(cancellationToken);
        return workspaceId ?? throw new CollaborationProjectNotFoundException();
    }

    private async Task AuthorizeReadAsync(Guid workspaceId, Guid projectId, Guid userId)
    {
        var workspace = await _authorization.AuthorizeWorkspaceAsync(
            userId,
            workspaceId,
            ResourcePermissionCodes.WorkspaceRead);
        var project = await _authorization.AuthorizeProjectAsync(
            userId,
            projectId,
            ResourcePermissionCodes.ProjectRead);
        if (!workspace.Succeeded || !project.Succeeded)
            throw new CollaborationProjectNotFoundException();
    }

    private async Task AuthorizeManageAsync(Guid workspaceId, Guid projectId, Guid userId)
    {
        var workspace = await _authorization.AuthorizeWorkspaceAsync(
            userId,
            workspaceId,
            ResourcePermissionCodes.WorkspaceRead);
        var project = await _authorization.AuthorizeProjectAsync(
            userId,
            projectId,
            ResourcePermissionCodes.ProjectWrite);
        if (!workspace.Succeeded || !project.Succeeded)
            throw new CollaborationChannelForbiddenException();
    }

    private Task<CollaborationChannel?> FindByProvisioningKeyAsync(
        Guid projectId,
        Guid userId,
        string provisioningKey,
        CancellationToken cancellationToken) =>
        _context.CollaborationChannels
            .Include(channel => channel.Members)
            .AsNoTracking()
            .SingleOrDefaultAsync(channel =>
                channel.ProjectId == projectId &&
                channel.CreatedByUserId == userId &&
                channel.ProvisioningKey == provisioningKey,
                cancellationToken);

    private static ProvisionCollaborationChannelResult ExistingResult(
        CollaborationChannel channel,
        string requestedName,
        string? requestedDescription)
    {
        var membership = channel.Members.SingleOrDefault(member =>
            member.UserId == channel.CreatedByUserId &&
            member.IsActive &&
            member.LeftAt == null);
        if (channel.IsDeleted ||
            channel.IsArchived ||
            membership == null ||
            !string.Equals(channel.Name, requestedName, StringComparison.Ordinal) ||
            !string.Equals(channel.Description, requestedDescription, StringComparison.Ordinal))
        {
            throw new CollaborationChannelConflictException(
                "The idempotency key is already associated with another channel request.");
        }

        return new(ToDto(
            channel,
            canManage: true,
            canSend: membership.CanSendMessages), false);
    }

    private static CollaborationChannelDto ToDto(
        CollaborationChannel channel,
        bool canManage,
        bool canSend) =>
        new(
            channel.Id,
            channel.Name,
            channel.Description,
            channel.WorkspaceId,
            channel.ProjectId,
            PrivateVisibility,
            true,
            true,
            canSend,
            canManage,
            channel.CreatedAt,
            channel.UpdatedAt);

    private static string NormalizeName(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            throw new ArgumentException("Channel name is required.", nameof(value));
        var normalized = value.Trim().Normalize(NormalizationForm.FormC);
        if (normalized.Length > MaximumNameLength)
            throw new ArgumentException(
                $"Channel name cannot exceed {MaximumNameLength} characters.",
                nameof(value));
        if (normalized.Any(char.IsControl))
            throw new ArgumentException("Channel name cannot contain control characters.", nameof(value));
        return normalized;
    }

    private static string? NormalizeDescription(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return null;
        var normalized = value.Trim().Normalize(NormalizationForm.FormC);
        if (normalized.Length > MaximumDescriptionLength)
            throw new ArgumentException(
                $"Channel description cannot exceed {MaximumDescriptionLength} characters.",
                nameof(value));
        if (normalized.Any(character => char.IsControl(character) && character is not '\r' and not '\n' and not '\t'))
            throw new ArgumentException(
                "Channel description contains an unsupported control character.",
                nameof(value));
        return normalized;
    }

    private static void ValidateVisibility(string? visibility)
    {
        if (!string.Equals(visibility?.Trim(), PrivateVisibility, StringComparison.OrdinalIgnoreCase))
            throw new ArgumentException(
                "Only Private collaboration channels are supported.",
                nameof(visibility));
    }

    private static string NormalizeIdempotencyKey(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            throw new ArgumentException("Idempotency-Key header is required.", nameof(value));
        var normalized = value.Trim();
        if (normalized.Length > MaximumIdempotencyKeyLength ||
            normalized.Any(character =>
                !(char.IsAsciiLetterOrDigit(character) || character is '-' or '_' or '.' or ':')))
        {
            throw new ArgumentException(
                "Idempotency-Key must contain 1-100 ASCII letters, digits, '-', '_', '.', or ':'.",
                nameof(value));
        }
        return normalized;
    }

    private static void ValidatePage(int page, int pageSize)
    {
        if (page < 1) throw new ArgumentOutOfRangeException(nameof(page), "Page must be at least 1.");
        if (pageSize is < 1 or > MaximumPageSize)
            throw new ArgumentOutOfRangeException(
                nameof(pageSize),
                $"Page size must be between 1 and {MaximumPageSize}.");
    }
}
