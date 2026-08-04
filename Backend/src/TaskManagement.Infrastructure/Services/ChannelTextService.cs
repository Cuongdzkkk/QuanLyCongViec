using Microsoft.EntityFrameworkCore;
using TaskManagement.Application.Common;
using TaskManagement.Application.DTOs.Collaboration;
using TaskManagement.Application.Interfaces;
using TaskManagement.Domain.Entities;
using TaskManagement.Infrastructure.Data;

namespace TaskManagement.Infrastructure.Services;

public sealed class ChannelTextService : IChannelTextService
{
    public const int MaximumContentLength = 4000;
    public const int MaximumPageSize = 100;

    private readonly ApplicationDbContext _context;
    private readonly IResourceAuthorizationService _authorization;

    public ChannelTextService(
        ApplicationDbContext context,
        IResourceAuthorizationService authorization)
    {
        _context = context;
        _authorization = authorization;
    }

    public async Task<ChannelMessagePageDto> GetHistoryAsync(
        Guid channelId,
        Guid userId,
        int page,
        int pageSize,
        CancellationToken cancellationToken = default)
    {
        ValidatePage(page, pageSize);
        await AuthorizeAsync(channelId, userId, requireSend: false, cancellationToken);

        var query = _context.ChannelMessages
            .AsNoTracking()
            .Where(message => message.CollaborationChannelId == channelId);
        var totalCount = await query.CountAsync(cancellationToken);
        var items = await query
            .OrderByDescending(message => message.SentAt)
            .ThenByDescending(message => message.Id)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(message => new ChannelMessageDto(
                message.Id,
                channelId,
                message.Content,
                new ChannelMessageSenderDto(
                    message.SenderId,
                    message.Sender != null
                        ? message.Sender.FullName ?? message.Sender.Email
                        : "Unknown user",
                    message.Sender != null ? message.Sender.AvatarUrl : null),
                message.SentAt,
                message.Id,
                message.Attachments
                    .OrderBy(attachment => attachment.CreatedAt)
                    .ThenBy(attachment => attachment.Id)
                    .Select(attachment => new CollaborationAttachmentDto(
                        attachment.Id,
                        attachment.OriginalFileName,
                        attachment.ContentType,
                        attachment.SizeBytes))
                    .ToList()))
            .ToListAsync(cancellationToken);

        return new ChannelMessagePageDto(
            items,
            page,
            pageSize,
            totalCount,
            "createdAt_desc,messageId_desc");
    }

    public async Task<ChannelMessageDto> SendAsync(
        Guid channelId,
        Guid userId,
        string? content,
        CancellationToken cancellationToken = default) =>
        await SendWithAttachmentsAsync(
            channelId, userId, content, [], cancellationToken);

    public async Task<ChannelMessageDto> SendWithAttachmentsAsync(
        Guid channelId,
        Guid userId,
        string? content,
        IReadOnlyList<PendingCollaborationAttachmentDto> attachments,
        CancellationToken cancellationToken = default)
    {
        await AuthorizeAsync(channelId, userId, requireSend: true, cancellationToken);
        ValidateAttachments(attachments);
        var normalizedContent = NormalizeContent(content, attachments.Count > 0);

        var message = new ChannelMessage
        {
            Id = Guid.NewGuid(),
            CollaborationChannelId = channelId,
            SenderId = userId,
            Content = normalizedContent,
            SentAt = DateTime.UtcNow
        };
        foreach (var attachment in attachments)
        {
            message.Attachments.Add(new CollaborationMessageAttachment
            {
                Id = attachment.AttachmentId,
                ChannelMessageId = message.Id,
                StorageKey = attachment.StorageKey,
                OriginalFileName = attachment.OriginalFileName,
                ContentType = attachment.ContentType,
                SizeBytes = attachment.SizeBytes,
                UploadedByUserId = userId,
                CreatedAt = message.SentAt
            });
        }
        _context.ChannelMessages.Add(message);
        await _context.SaveChangesAsync(cancellationToken);

        return await _context.ChannelMessages
            .AsNoTracking()
            .Where(item => item.Id == message.Id)
            .Select(item => new ChannelMessageDto(
                item.Id,
                channelId,
                item.Content,
                new ChannelMessageSenderDto(
                    item.SenderId,
                    item.Sender != null
                        ? item.Sender.FullName ?? item.Sender.Email
                        : "Unknown user",
                    item.Sender != null ? item.Sender.AvatarUrl : null),
                item.SentAt,
                item.Id,
                item.Attachments
                    .OrderBy(attachment => attachment.CreatedAt)
                    .ThenBy(attachment => attachment.Id)
                    .Select(attachment => new CollaborationAttachmentDto(
                        attachment.Id,
                        attachment.OriginalFileName,
                        attachment.ContentType,
                        attachment.SizeBytes))
                    .ToList()))
            .SingleAsync(cancellationToken);
    }

    private async Task AuthorizeAsync(
        Guid channelId,
        Guid userId,
        bool requireSend,
        CancellationToken cancellationToken)
    {
        var channel = await _context.CollaborationChannels
            .AsNoTracking()
            .Where(item =>
                item.Id == channelId &&
                !item.IsDeleted &&
                !item.IsArchived &&
                !item.Workspace.IsDeleted &&
                item.Project.Status &&
                !item.Project.IsDeleted &&
                !item.Project.IsArchived &&
                item.Project.WorkspaceId == item.WorkspaceId)
            .Select(item => new { item.WorkspaceId, item.ProjectId })
            .SingleOrDefaultAsync(cancellationToken);
        if (channel == null) throw new ChannelNotFoundException();

        var workspaceAccess = await _authorization.AuthorizeWorkspaceAsync(
            userId,
            channel.WorkspaceId,
            ResourcePermissionCodes.WorkspaceRead);
        var projectAccess = await _authorization.AuthorizeProjectAsync(
            userId,
            channel.ProjectId,
            ResourcePermissionCodes.ProjectRead);
        if (!workspaceAccess.Succeeded || !projectAccess.Succeeded)
            throw new ChannelNotFoundException();

        var membership = await _context.CollaborationChannelMembers
            .AsNoTracking()
            .Where(member =>
                member.ChannelId == channelId &&
                member.UserId == userId &&
                member.IsActive &&
                member.LeftAt == null &&
                member.User.IsActive &&
                !member.User.IsDeleted)
            .Select(member => new { member.CanSendMessages })
            .SingleOrDefaultAsync(cancellationToken);
        if (membership == null) throw new ChannelNotFoundException();
        if (requireSend && !membership.CanSendMessages)
            throw new ChannelSendForbiddenException();
    }

    private static string NormalizeContent(string? content, bool hasAttachments)
    {
        if (string.IsNullOrWhiteSpace(content))
        {
            if (hasAttachments) return string.Empty;
            throw new ArgumentException("Message content is required.", nameof(content));
        }

        var normalized = content
            .Replace("\r\n", "\n", StringComparison.Ordinal)
            .Replace('\r', '\n')
            .Trim();
        if (normalized.Length > MaximumContentLength)
            throw new ArgumentException(
                $"Message content cannot exceed {MaximumContentLength} characters.",
                nameof(content));
        return normalized;
    }

    private static void ValidateAttachments(IReadOnlyList<PendingCollaborationAttachmentDto> attachments)
    {
        ArgumentNullException.ThrowIfNull(attachments);
        if (attachments.Count > 5)
            throw new ArgumentException("A message can contain at most 5 attachments.", nameof(attachments));
        if (attachments.Any(item =>
                item.AttachmentId == Guid.Empty ||
                string.IsNullOrWhiteSpace(item.StorageKey) ||
                item.StorageKey != Path.GetFileName(item.StorageKey) ||
                string.IsNullOrWhiteSpace(item.OriginalFileName) ||
                string.IsNullOrWhiteSpace(item.ContentType) ||
                item.SizeBytes is <= 0 or > 10 * 1024 * 1024))
            throw new ArgumentException("Attachment metadata is invalid.", nameof(attachments));
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
