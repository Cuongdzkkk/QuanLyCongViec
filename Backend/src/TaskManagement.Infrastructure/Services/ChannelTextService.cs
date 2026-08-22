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

        var query = BuildMessageQuery(channelId);
        var totalCount = await query.CountAsync(cancellationToken);
        var messages = await query
            .OrderByDescending(message => message.SentAt)
            .ThenByDescending(message => message.Id)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);
        var items = messages.Select(message => MapMessage(message, channelId, userId)).ToList();

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
        (await SendWithMentionsAsync(
            channelId, userId, content, [], [], cancellationToken)).Message;

    public async Task<ChannelMessageDto> SendWithAttachmentsAsync(
        Guid channelId,
        Guid userId,
        string? content,
        IReadOnlyList<PendingCollaborationAttachmentDto> attachments,
        CancellationToken cancellationToken = default)
        => (await SendWithMentionsAsync(
            channelId, userId, content, [], attachments, cancellationToken)).Message;

    public async Task<SendChannelMessageResult> SendWithMentionsAsync(
        Guid channelId,
        Guid userId,
        string? content,
        IReadOnlyList<ChannelMessageMentionRequestDto> mentions,
        IReadOnlyList<PendingCollaborationAttachmentDto> attachments,
        CancellationToken cancellationToken = default,
        Guid? replyToMessageId = null)
    {
        var channel = await AuthorizeAsync(channelId, userId, requireSend: true, cancellationToken);
        ValidateAttachments(attachments);
        var normalizedContent = NormalizeContent(content, attachments.Count > 0);
        var normalizedMentions = await ValidateMentionsAsync(
            channelId, userId, normalizedContent, mentions, cancellationToken);
        if (replyToMessageId.HasValue)
        {
            var replyExists = await _context.ChannelMessages.AsNoTracking().AnyAsync(
                message => message.Id == replyToMessageId.Value &&
                    message.CollaborationChannelId == channelId,
                cancellationToken);
            if (!replyExists)
                throw new ArgumentException("The referenced message is not available in this channel.", nameof(replyToMessageId));
        }
        var actor = await _context.Users.AsNoTracking()
            .Where(item => item.Id == userId)
            .Select(item => new ChannelMessageSenderDto(
                item.Id,
                item.FullName ?? item.Email,
                item.AvatarUrl))
            .SingleAsync(cancellationToken);

        var message = new ChannelMessage
        {
            Id = Guid.NewGuid(),
            CollaborationChannelId = channelId,
            SenderId = userId,
            Content = normalizedContent,
            SentAt = DateTime.UtcNow,
            ReplyToMessageId = replyToMessageId
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
        foreach (var mention in normalizedMentions)
        {
            message.Mentions.Add(new ChannelMessageMention
            {
                Id = Guid.NewGuid(),
                ChannelMessageId = message.Id,
                MentionedUserId = mention.UserId,
                StartIndex = mention.StartIndex,
                Length = mention.Length,
                DisplayText = normalizedContent.Substring(mention.StartIndex, mention.Length),
                CreatedAt = message.SentAt
            });
        }

        var preview = CreatePreview(normalizedContent);
        var notificationEvents = normalizedMentions.Select(mention =>
        {
            var notificationId = Guid.NewGuid();
            _context.Notifications.Add(new Notification
            {
                Id = notificationId,
                UserId = mention.UserId,
                Title = $"Mention in #{channel.Name}",
                Content = $"{actor.DisplayName} mentioned you: {preview}",
                NotificationType = "collaboration_channel_mention",
                RelatedProjectId = channel.ProjectId,
                CollaborationChannelId = channelId,
                ChannelMessageId = message.Id,
                TriggeredByUserId = userId,
                LinkUrl = $"/chat?projectId={channel.ProjectId:D}&channelId={channelId:D}&messageId={message.Id:D}",
                CreatedAt = message.SentAt,
                IsRead = false
            });
            return new CollaborationMentionDelivery(
                mention.UserId,
                new CollaborationMentionCreatedEventDto(
                    notificationId,
                    channelId,
                    message.Id,
                    actor,
                    preview,
                    message.SentAt));
        }).ToList();
        _context.ChannelMessages.Add(message);
        await _context.SaveChangesAsync(cancellationToken);

        var persistedEntity = await BuildMessageQuery(channelId)
            .SingleAsync(item => item.Id == message.Id, cancellationToken);
        var persisted = MapMessage(persistedEntity, channelId, userId);

        return new SendChannelMessageResult(persisted, notificationEvents);
    }

    public async Task<ChannelMessagePageDto> SearchAsync(
        Guid channelId,
        Guid userId,
        string query,
        int page,
        int pageSize,
        CancellationToken cancellationToken = default)
    {
        ValidatePage(page, pageSize);
        await AuthorizeAsync(channelId, userId, requireSend: false, cancellationToken);
        var normalizedQuery = query?.Trim() ?? string.Empty;
        if (normalizedQuery.Length is < 1 or > 200)
            throw new ArgumentException("Search query must contain between 1 and 200 characters.", nameof(query));

        var search = BuildMessageQuery(channelId)
            .Where(message => message.Content.Contains(normalizedQuery));
        var totalCount = await search.CountAsync(cancellationToken);
        var messages = await search
            .OrderByDescending(message => message.SentAt)
            .ThenByDescending(message => message.Id)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);
        return new ChannelMessagePageDto(
            messages.Select(message => MapMessage(message, channelId, userId)).ToList(),
            page,
            pageSize,
            totalCount,
            "createdAt_desc,messageId_desc");
    }

    public async Task<ChannelMessageReactionChangeDto> AddReactionAsync(
        Guid channelId,
        Guid messageId,
        Guid userId,
        string emoji,
        CancellationToken cancellationToken = default)
    {
        await AuthorizeMessageAsync(channelId, messageId, userId, requireManage: false, cancellationToken);
        var normalizedEmoji = NormalizeEmoji(emoji);
        var existing = await _context.CollaborationMessageReactions.SingleOrDefaultAsync(
            reaction => reaction.ChannelMessageId == messageId &&
                reaction.UserId == userId && reaction.Emoji == normalizedEmoji,
            cancellationToken);
        if (existing == null)
        {
            _context.CollaborationMessageReactions.Add(new CollaborationMessageReaction
            {
                Id = Guid.NewGuid(),
                ChannelMessageId = messageId,
                UserId = userId,
                Emoji = normalizedEmoji,
                CreatedAt = DateTime.UtcNow
            });
            await _context.SaveChangesAsync(cancellationToken);
        }
        return await BuildReactionChangeAsync(channelId, messageId, userId, normalizedEmoji, true, cancellationToken);
    }

    public async Task<ChannelMessageReactionChangeDto> RemoveReactionAsync(
        Guid channelId,
        Guid messageId,
        Guid userId,
        string emoji,
        CancellationToken cancellationToken = default)
    {
        await AuthorizeMessageAsync(channelId, messageId, userId, requireManage: false, cancellationToken);
        var normalizedEmoji = NormalizeEmoji(emoji);
        var existing = await _context.CollaborationMessageReactions.SingleOrDefaultAsync(
            reaction => reaction.ChannelMessageId == messageId &&
                reaction.UserId == userId && reaction.Emoji == normalizedEmoji,
            cancellationToken);
        if (existing != null)
        {
            _context.CollaborationMessageReactions.Remove(existing);
            await _context.SaveChangesAsync(cancellationToken);
        }
        return await BuildReactionChangeAsync(channelId, messageId, userId, normalizedEmoji, false, cancellationToken);
    }

    public async Task<IReadOnlyList<ChannelPinnedMessageDto>> GetPinsAsync(
        Guid channelId,
        Guid userId,
        CancellationToken cancellationToken = default)
    {
        await AuthorizeAsync(channelId, userId, requireSend: false, cancellationToken);
        var pins = await _context.CollaborationMessagePins
            .AsNoTracking()
            .Where(pin => pin.ChannelMessage.CollaborationChannelId == channelId)
            .Include(pin => pin.PinnedByUser)
            .Include(pin => pin.ChannelMessage).ThenInclude(message => message.Sender)
            .Include(pin => pin.ChannelMessage).ThenInclude(message => message.ReplyToMessage).ThenInclude(message => message!.Sender)
            .Include(pin => pin.ChannelMessage).ThenInclude(message => message.Attachments)
            .Include(pin => pin.ChannelMessage).ThenInclude(message => message.Mentions)
            .Include(pin => pin.ChannelMessage).ThenInclude(message => message.Reactions)
            .OrderByDescending(pin => pin.PinnedAt)
            .ThenByDescending(pin => pin.Id)
            .ToListAsync(cancellationToken);
        return pins.Select(pin => new ChannelPinnedMessageDto(
            MapMessage(pin.ChannelMessage, channelId, userId),
            ToSender(pin.PinnedByUserId, pin.PinnedByUser),
            pin.PinnedAt)).ToList();
    }

    public async Task<ChannelMessagePinChangeDto> PinAsync(
        Guid channelId,
        Guid messageId,
        Guid userId,
        CancellationToken cancellationToken = default)
    {
        await AuthorizeMessageAsync(channelId, messageId, userId, requireManage: true, cancellationToken);
        var existing = await _context.CollaborationMessagePins.SingleOrDefaultAsync(
            pin => pin.ChannelMessageId == messageId, cancellationToken);
        if (existing == null)
        {
            existing = new CollaborationMessagePin
            {
                Id = Guid.NewGuid(),
                ChannelMessageId = messageId,
                PinnedByUserId = userId,
                PinnedAt = DateTime.UtcNow
            };
            _context.CollaborationMessagePins.Add(existing);
            await _context.SaveChangesAsync(cancellationToken);
        }
        return new ChannelMessagePinChangeDto(channelId, messageId, existing.PinnedByUserId, existing.PinnedAt, true);
    }

    public async Task<ChannelMessagePinChangeDto> UnpinAsync(
        Guid channelId,
        Guid messageId,
        Guid userId,
        CancellationToken cancellationToken = default)
    {
        await AuthorizeMessageAsync(channelId, messageId, userId, requireManage: true, cancellationToken);
        var existing = await _context.CollaborationMessagePins.SingleOrDefaultAsync(
            pin => pin.ChannelMessageId == messageId, cancellationToken);
        if (existing != null)
        {
            _context.CollaborationMessagePins.Remove(existing);
            await _context.SaveChangesAsync(cancellationToken);
        }
        return new ChannelMessagePinChangeDto(channelId, messageId, null, null, false);
    }

    public async Task<IReadOnlyList<ChannelMemberSuggestionDto>> SearchMembersAsync(
        Guid channelId,
        Guid userId,
        string? query,
        int limit,
        CancellationToken cancellationToken = default)
    {
        if (limit is < 1 or > 20)
            throw new ArgumentOutOfRangeException(nameof(limit), "Limit must be between 1 and 20.");
        await AuthorizeAsync(channelId, userId, requireSend: false, cancellationToken);
        var normalizedQuery = query?.Trim() ?? string.Empty;
        if (normalizedQuery.Length > 100)
            throw new ArgumentException("Member query cannot exceed 100 characters.", nameof(query));

        return await _context.CollaborationChannelMembers.AsNoTracking()
            .Where(member =>
                member.ChannelId == channelId &&
                member.IsActive &&
                member.LeftAt == null &&
                member.User.IsActive &&
                !member.User.IsDeleted &&
                (normalizedQuery == string.Empty || member.User.FullName.Contains(normalizedQuery)))
            .OrderBy(member => member.User.FullName)
            .ThenBy(member => member.UserId)
            .Take(limit)
            .Select(member => new ChannelMemberSuggestionDto(
                member.UserId,
                member.User.FullName,
                member.User.AvatarUrl))
            .ToListAsync(cancellationToken);
    }

    private IQueryable<ChannelMessage> BuildMessageQuery(Guid channelId) =>
        _context.ChannelMessages
            .AsNoTracking()
            .Where(message => message.CollaborationChannelId == channelId)
            .Include(message => message.Sender)
            .Include(message => message.Attachments)
            .Include(message => message.Mentions)
            .Include(message => message.ReplyToMessage).ThenInclude(message => message!.Sender)
            .Include(message => message.Reactions)
            .Include(message => message.Pin);

    private static ChannelMessageDto MapMessage(ChannelMessage message, Guid channelId, Guid currentUserId)
    {
        var reply = message.ReplyToMessage == null
            ? message.ReplyToMessageId.HasValue
                ? new ChannelMessageQuoteDto(
                    message.ReplyToMessageId.Value,
                    string.Empty,
                    new ChannelMessageSenderDto(Guid.Empty, "Unavailable user", null),
                    message.SentAt,
                    false)
                : null
            : new ChannelMessageQuoteDto(
                message.ReplyToMessage.Id,
                message.ReplyToMessage.Content,
                ToSender(message.ReplyToMessage.SenderId, message.ReplyToMessage.Sender),
                message.ReplyToMessage.SentAt);
        var reactions = message.Reactions
            .GroupBy(reaction => reaction.Emoji, StringComparer.Ordinal)
            .Select(group => new ChannelMessageReactionDto(
                group.Key,
                group.Count(),
                group.Any(reaction => reaction.UserId == currentUserId)))
            .OrderBy(reaction => reaction.Emoji, StringComparer.Ordinal)
            .ToList();
        return new ChannelMessageDto(
            message.Id,
            channelId,
            message.Content,
            ToSender(message.SenderId, message.Sender),
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
                .ToList(),
            message.Mentions
                .OrderBy(mention => mention.StartIndex)
                .Select(mention => new ChannelMessageMentionDto(
                    mention.MentionedUserId,
                    mention.DisplayText,
                    mention.StartIndex,
                    mention.Length))
                .ToList(),
            reply,
            reactions,
            message.Pin != null);
    }

    private static ChannelMessageSenderDto ToSender(Guid userId, User? user) =>
        new(userId, user?.FullName ?? user?.Email ?? "Unknown user", user?.AvatarUrl);

    private async Task AuthorizeMessageAsync(
        Guid channelId,
        Guid messageId,
        Guid userId,
        bool requireManage,
        CancellationToken cancellationToken)
    {
        var channel = await AuthorizeAsync(channelId, userId, requireSend: false, cancellationToken);
        var messageExists = await _context.ChannelMessages.AsNoTracking().AnyAsync(
            message => message.Id == messageId && message.CollaborationChannelId == channelId,
            cancellationToken);
        if (!messageExists) throw new ChannelNotFoundException();
        if (!requireManage) return;
        var projectAccess = await _authorization.AuthorizeProjectAsync(
            userId, channel.ProjectId, ResourcePermissionCodes.ProjectWrite);
        if (!projectAccess.Succeeded) throw new ChannelManageForbiddenException();
    }

    private async Task<ChannelMessageReactionChangeDto> BuildReactionChangeAsync(
        Guid channelId,
        Guid messageId,
        Guid userId,
        string emoji,
        bool isActive,
        CancellationToken cancellationToken)
    {
        var reactions = await _context.CollaborationMessageReactions.AsNoTracking()
            .Where(reaction => reaction.ChannelMessageId == messageId)
            .ToListAsync(cancellationToken);
        var summary = reactions
            .GroupBy(reaction => reaction.Emoji, StringComparer.Ordinal)
            .Select(group => new ChannelMessageReactionDto(
                group.Key,
                group.Count(),
                group.Any(reaction => reaction.UserId == userId)))
            .OrderBy(reaction => reaction.Emoji, StringComparer.Ordinal)
            .ToList();
        return new ChannelMessageReactionChangeDto(
            channelId, messageId, userId, emoji, isActive, summary);
    }

    private static string NormalizeEmoji(string emoji)
    {
        if (string.IsNullOrWhiteSpace(emoji))
            throw new ArgumentException("Reaction emoji is required.", nameof(emoji));
        var normalized = emoji.Trim();
        if (normalized.Length > 32 || normalized.Any(char.IsControl))
            throw new ArgumentException("Reaction emoji is invalid.", nameof(emoji));
        return normalized;
    }

    private async Task<AuthorizedChannel> AuthorizeAsync(
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
            .Select(item => new { item.WorkspaceId, item.ProjectId, item.Name })
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
        return new AuthorizedChannel(channel.ProjectId, channel.WorkspaceId, channel.Name);
    }

    private async Task<IReadOnlyList<ChannelMessageMentionRequestDto>> ValidateMentionsAsync(
        Guid channelId,
        Guid senderId,
        string content,
        IReadOnlyList<ChannelMessageMentionRequestDto> mentions,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(mentions);
        var deduplicated = mentions
            .Where(item => item.UserId != senderId)
            .GroupBy(item => item.UserId)
            .Select(group => group.First())
            .ToList();
        if (deduplicated.Count > 20)
            throw new ArgumentException("A message can mention at most 20 users.", nameof(mentions));
        if (deduplicated.Any(item =>
                item.UserId == Guid.Empty ||
                item.StartIndex < 0 ||
                item.Length is < 2 or > 200 ||
                item.StartIndex > content.Length - item.Length ||
                content[item.StartIndex] != '@'))
            throw new ArgumentException("Mention metadata is invalid.", nameof(mentions));

        var userIds = deduplicated.Select(item => item.UserId).ToList();
        var authorizedMembers = await _context.CollaborationChannelMembers.AsNoTracking()
            .Where(member =>
                member.ChannelId == channelId &&
                userIds.Contains(member.UserId) &&
                member.IsActive &&
                member.LeftAt == null &&
                member.User.IsActive &&
                !member.User.IsDeleted)
            .Select(member => new { member.UserId, member.User.FullName })
            .ToListAsync(cancellationToken);
        if (authorizedMembers.Count != userIds.Count)
            throw new ChannelMentionForbiddenException();
        var displayNames = authorizedMembers.ToDictionary(item => item.UserId, item => item.FullName);
        if (deduplicated.Any(item =>
                content.Substring(item.StartIndex, item.Length) != $"@{displayNames[item.UserId]}"))
            throw new ArgumentException("Mention text does not match the selected channel member.", nameof(mentions));
        var ordered = deduplicated.OrderBy(item => item.StartIndex).ToList();
        if (ordered.Zip(ordered.Skip(1), (left, right) =>
                left.StartIndex + left.Length > right.StartIndex).Any(overlaps => overlaps))
            throw new ArgumentException("Mention spans cannot overlap.", nameof(mentions));
        return deduplicated;
    }

    private static string CreatePreview(string content)
    {
        var preview = string.Join(' ', content.Split(
            [' ', '\t', '\r', '\n'],
            StringSplitOptions.RemoveEmptyEntries));
        return preview.Length <= 160 ? preview : $"{preview[..157]}...";
    }

    private sealed record AuthorizedChannel(Guid ProjectId, Guid WorkspaceId, string Name);

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
