using System.Data;
using Microsoft.EntityFrameworkCore;
using TaskManagement.Application.DTOs.Collaboration;
using TaskManagement.Application.Interfaces;
using TaskManagement.Domain.Entities;
using TaskManagement.Infrastructure.Data;

namespace TaskManagement.Infrastructure.Services;

public sealed class CollaborationReadStateService : ICollaborationReadStateService
{
    private readonly ApplicationDbContext _context;
    private readonly IChannelTextService _channelTextService;
    private readonly IDirectConversationService _directConversationService;

    public CollaborationReadStateService(
        ApplicationDbContext context,
        IChannelTextService channelTextService,
        IDirectConversationService directConversationService)
    {
        _context = context;
        _channelTextService = channelTextService;
        _directConversationService = directConversationService;
    }

    public async Task<CollaborationReadStateDto> MarkChannelReadAsync(
        Guid channelId,
        Guid userId,
        Guid messageId,
        CancellationToken cancellationToken = default)
    {
        await _channelTextService.GetHistoryAsync(
            channelId, userId, page: 1, pageSize: 1, cancellationToken);
        if (messageId == Guid.Empty) throw new CollaborationMessageNotFoundException();

        if (!_context.Database.IsRelational())
            return await MarkChannelReadCoreAsync(
                channelId, userId, messageId, useTransaction: false, cancellationToken);

        var strategy = _context.Database.CreateExecutionStrategy();
        return await strategy.ExecuteAsync(() => MarkChannelReadCoreAsync(
            channelId, userId, messageId, useTransaction: true, cancellationToken));
    }

    public async Task<CollaborationReadStateDto> MarkDirectConversationReadAsync(
        Guid conversationId,
        Guid userId,
        Guid messageId,
        CancellationToken cancellationToken = default)
    {
        await _directConversationService.GetHistoryAsync(
            conversationId, userId, page: 1, pageSize: 1, cancellationToken);
        if (messageId == Guid.Empty) throw new CollaborationMessageNotFoundException();

        if (!_context.Database.IsRelational())
            return await MarkDirectReadCoreAsync(
                conversationId, userId, messageId, useTransaction: false, cancellationToken);

        var strategy = _context.Database.CreateExecutionStrategy();
        return await strategy.ExecuteAsync(() => MarkDirectReadCoreAsync(
            conversationId, userId, messageId, useTransaction: true, cancellationToken));
    }

    public async Task<IReadOnlyList<CollaborationReadStateDeliveryDto>>
        GetChannelUnreadUpdatesForMessageAsync(
            Guid messageId,
            CancellationToken cancellationToken = default)
    {
        var message = await _context.ChannelMessages.AsNoTracking()
            .Where(item => item.Id == messageId && item.CollaborationChannelId != null)
            .Select(item => new { ChannelId = item.CollaborationChannelId!.Value, item.SenderId })
            .SingleOrDefaultAsync(cancellationToken)
            ?? throw new CollaborationMessageNotFoundException();
        var recipients = await _context.CollaborationChannelMembers.AsNoTracking()
            .Where(member =>
                member.ChannelId == message.ChannelId &&
                member.UserId != message.SenderId &&
                member.IsActive &&
                member.LeftAt == null &&
                member.User.IsActive &&
                !member.User.IsDeleted)
            .Select(member => member.UserId)
            .ToListAsync(cancellationToken);
        var states = await _context.CollaborationChannelReadStates.AsNoTracking()
            .Where(state =>
                state.ChannelId == message.ChannelId &&
                recipients.Contains(state.UserId))
            .Select(state => new
            {
                state.UserId,
                state.LastReadMessageId,
                state.LastReadAt
            })
            .ToDictionaryAsync(state => state.UserId, cancellationToken);
        var messages = await _context.ChannelMessages.AsNoTracking()
            .Where(item => item.CollaborationChannelId == message.ChannelId)
            .OrderBy(item => item.SentAt)
            .ThenBy(item => item.Id)
            .Select(item => new RecipientMessage(item.Id, item.SenderId))
            .ToListAsync(cancellationToken);
        var updates = new List<CollaborationReadStateDeliveryDto>(recipients.Count);
        foreach (var recipientId in recipients)
        {
            states.TryGetValue(recipientId, out var state);
            var cursorIndex = state?.LastReadMessageId == null
                ? -1
                : messages.FindIndex(item => item.Id == state.LastReadMessageId.Value);
            updates.Add(new(
                recipientId,
                new CollaborationReadStateDto(
                    CollaborationReadResourceTypes.Channel,
                    message.ChannelId,
                    state?.LastReadMessageId,
                    state?.LastReadAt,
                    messages.Skip(cursorIndex + 1)
                        .Count(item => item.SenderId != recipientId))));
        }
        return updates;
    }

    public async Task<IReadOnlyList<CollaborationReadStateDeliveryDto>>
        GetDirectUnreadUpdatesForMessageAsync(
            Guid messageId,
            CancellationToken cancellationToken = default)
    {
        var message = await _context.DirectMessages.AsNoTracking()
            .Where(item => item.Id == messageId && item.ConversationId != null)
            .Select(item => new { ConversationId = item.ConversationId!.Value, item.SenderId })
            .SingleOrDefaultAsync(cancellationToken)
            ?? throw new CollaborationMessageNotFoundException();
        var recipientId = await _context.DirectConversationParticipants.AsNoTracking()
            .Where(participant =>
                participant.ConversationId == message.ConversationId &&
                participant.UserId != message.SenderId)
            .Select(participant => (Guid?)participant.UserId)
            .SingleOrDefaultAsync(cancellationToken);
        if (recipientId == null) return [];

        return
        [
            new CollaborationReadStateDeliveryDto(
                recipientId.Value,
                await GetDirectStateAsync(
                    message.ConversationId, recipientId.Value, cancellationToken))
        ];
    }

    private async Task<CollaborationReadStateDto> MarkChannelReadCoreAsync(
        Guid channelId,
        Guid userId,
        Guid messageId,
        bool useTransaction,
        CancellationToken cancellationToken)
    {
        await using var transaction = useTransaction
            ? await _context.Database.BeginTransactionAsync(
                IsolationLevel.Serializable, cancellationToken)
            : null;
        await AcquireReadLockAsync("channel", channelId, userId, useTransaction, cancellationToken);

        var target = await _context.ChannelMessages.AsNoTracking()
            .Where(message =>
                message.Id == messageId &&
                message.CollaborationChannelId == channelId)
            .Select(message => new MessagePosition(message.Id, message.SentAt))
            .SingleOrDefaultAsync(cancellationToken)
            ?? throw new CollaborationMessageNotFoundException();
        var state = await _context.CollaborationChannelReadStates
            .SingleOrDefaultAsync(item =>
                item.ChannelId == channelId && item.UserId == userId,
                cancellationToken);

        if (state == null)
        {
            state = new CollaborationChannelReadState
            {
                ChannelId = channelId,
                UserId = userId,
                LastReadMessageId = target.Id,
                LastReadAt = DateTime.UtcNow
            };
            _context.CollaborationChannelReadStates.Add(state);
        }
        else
        {
            var current = await GetChannelPositionAsync(
                channelId, state.LastReadMessageId, cancellationToken);
            if (current == null || await IsAfterChannelAsync(
                    channelId, target, current, cancellationToken))
            {
                state.LastReadMessageId = target.Id;
                state.LastReadAt = DateTime.UtcNow;
            }
        }

        await _context.SaveChangesAsync(cancellationToken);
        var result = await GetChannelStateAsync(channelId, userId, cancellationToken);
        if (transaction != null) await transaction.CommitAsync(cancellationToken);
        return result;
    }

    private async Task<CollaborationReadStateDto> MarkDirectReadCoreAsync(
        Guid conversationId,
        Guid userId,
        Guid messageId,
        bool useTransaction,
        CancellationToken cancellationToken)
    {
        await using var transaction = useTransaction
            ? await _context.Database.BeginTransactionAsync(
                IsolationLevel.Serializable, cancellationToken)
            : null;
        await AcquireReadLockAsync("dm", conversationId, userId, useTransaction, cancellationToken);

        var target = await _context.DirectMessages.AsNoTracking()
            .Where(message =>
                message.Id == messageId &&
                message.ConversationId == conversationId)
            .Select(message => new MessagePosition(message.Id, message.SentAt))
            .SingleOrDefaultAsync(cancellationToken)
            ?? throw new CollaborationMessageNotFoundException();
        var state = await _context.DirectConversationReadStates
            .SingleOrDefaultAsync(item =>
                item.ConversationId == conversationId && item.UserId == userId,
                cancellationToken);

        if (state == null)
        {
            state = new DirectConversationReadState
            {
                ConversationId = conversationId,
                UserId = userId,
                LastReadMessageId = target.Id,
                LastReadAt = DateTime.UtcNow
            };
            _context.DirectConversationReadStates.Add(state);
        }
        else
        {
            var current = await GetDirectPositionAsync(
                conversationId, state.LastReadMessageId, cancellationToken);
            if (current == null || await IsAfterDirectAsync(
                    conversationId, target, current, cancellationToken))
            {
                state.LastReadMessageId = target.Id;
                state.LastReadAt = DateTime.UtcNow;
            }
        }

        await _context.SaveChangesAsync(cancellationToken);
        var result = await GetDirectStateAsync(conversationId, userId, cancellationToken);
        if (transaction != null) await transaction.CommitAsync(cancellationToken);
        return result;
    }

    private async Task<CollaborationReadStateDto> GetChannelStateAsync(
        Guid channelId,
        Guid userId,
        CancellationToken cancellationToken)
    {
        var state = await _context.CollaborationChannelReadStates.AsNoTracking()
            .Where(item => item.ChannelId == channelId && item.UserId == userId)
            .Select(item => new
            {
                item.LastReadMessageId,
                item.LastReadAt,
                CursorSentAt = item.LastReadMessage != null
                    ? (DateTime?)item.LastReadMessage.SentAt
                    : null
            })
            .SingleOrDefaultAsync(cancellationToken);
        var unreadCount = await CountChannelUnreadAsync(
            channelId, userId, state?.LastReadMessageId, state?.CursorSentAt, cancellationToken);
        return new(
            CollaborationReadResourceTypes.Channel,
            channelId,
            state?.LastReadMessageId,
            state?.LastReadAt,
            unreadCount);
    }

    private async Task<CollaborationReadStateDto> GetDirectStateAsync(
        Guid conversationId,
        Guid userId,
        CancellationToken cancellationToken)
    {
        var state = await _context.DirectConversationReadStates.AsNoTracking()
            .Where(item => item.ConversationId == conversationId && item.UserId == userId)
            .Select(item => new
            {
                item.LastReadMessageId,
                item.LastReadAt,
                CursorSentAt = item.LastReadMessage != null
                    ? (DateTime?)item.LastReadMessage.SentAt
                    : null
            })
            .SingleOrDefaultAsync(cancellationToken);
        var unreadCount = await CountDirectUnreadAsync(
            conversationId, userId, state?.LastReadMessageId, state?.CursorSentAt, cancellationToken);
        return new(
            CollaborationReadResourceTypes.DirectConversation,
            conversationId,
            state?.LastReadMessageId,
            state?.LastReadAt,
            unreadCount);
    }

    private async Task<int> CountChannelUnreadAsync(
        Guid channelId,
        Guid userId,
        Guid? cursorId,
        DateTime? cursorSentAt,
        CancellationToken cancellationToken)
    {
        var messages = _context.ChannelMessages.AsNoTracking().Where(message =>
            message.CollaborationChannelId == channelId && message.SenderId != userId);
        if (cursorId == null || cursorSentAt == null)
            return await messages.CountAsync(cancellationToken);

        var newerCount = await messages.CountAsync(
            message => message.SentAt > cursorSentAt.Value,
            cancellationToken);
        var sameTime = await messages
            .Where(message => message.SentAt == cursorSentAt.Value)
            .OrderBy(message => message.Id)
            .Select(message => message.Id)
            .ToListAsync(cancellationToken);
        return newerCount + CountAfterCursor(sameTime, cursorId.Value);
    }

    private async Task<int> CountDirectUnreadAsync(
        Guid conversationId,
        Guid userId,
        Guid? cursorId,
        DateTime? cursorSentAt,
        CancellationToken cancellationToken)
    {
        var messages = _context.DirectMessages.AsNoTracking().Where(message =>
            message.ConversationId == conversationId && message.SenderId != userId);
        if (cursorId == null || cursorSentAt == null)
            return await messages.CountAsync(cancellationToken);

        var newerCount = await messages.CountAsync(
            message => message.SentAt > cursorSentAt.Value,
            cancellationToken);
        var sameTime = await messages
            .Where(message => message.SentAt == cursorSentAt.Value)
            .OrderBy(message => message.Id)
            .Select(message => message.Id)
            .ToListAsync(cancellationToken);
        return newerCount + CountAfterCursor(sameTime, cursorId.Value);
    }

    private static int CountAfterCursor(IReadOnlyList<Guid> orderedIds, Guid cursorId)
    {
        var cursorIndex = -1;
        for (var index = 0; index < orderedIds.Count; index++)
        {
            if (orderedIds[index] == cursorId)
            {
                cursorIndex = index;
                break;
            }
        }
        return cursorIndex < 0 ? orderedIds.Count : orderedIds.Count - cursorIndex - 1;
    }

    private Task<MessagePosition?> GetChannelPositionAsync(
        Guid channelId,
        Guid? messageId,
        CancellationToken cancellationToken) =>
        _context.ChannelMessages.AsNoTracking()
            .Where(message =>
                message.Id == messageId &&
                message.CollaborationChannelId == channelId)
            .Select(message => new MessagePosition(message.Id, message.SentAt))
            .SingleOrDefaultAsync(cancellationToken);

    private Task<MessagePosition?> GetDirectPositionAsync(
        Guid conversationId,
        Guid? messageId,
        CancellationToken cancellationToken) =>
        _context.DirectMessages.AsNoTracking()
            .Where(message =>
                message.Id == messageId &&
                message.ConversationId == conversationId)
            .Select(message => new MessagePosition(message.Id, message.SentAt))
            .SingleOrDefaultAsync(cancellationToken);

    private async Task<bool> IsAfterChannelAsync(
        Guid channelId,
        MessagePosition candidate,
        MessagePosition cursor,
        CancellationToken cancellationToken)
    {
        if (candidate.SentAt != cursor.SentAt) return candidate.SentAt > cursor.SentAt;
        var orderedIds = await _context.ChannelMessages.AsNoTracking()
            .Where(message =>
                message.CollaborationChannelId == channelId &&
                message.SentAt == cursor.SentAt)
            .OrderBy(message => message.Id)
            .Select(message => message.Id)
            .ToListAsync(cancellationToken);
        return orderedIds.IndexOf(candidate.Id) > orderedIds.IndexOf(cursor.Id);
    }

    private async Task<bool> IsAfterDirectAsync(
        Guid conversationId,
        MessagePosition candidate,
        MessagePosition cursor,
        CancellationToken cancellationToken)
    {
        if (candidate.SentAt != cursor.SentAt) return candidate.SentAt > cursor.SentAt;
        var orderedIds = await _context.DirectMessages.AsNoTracking()
            .Where(message =>
                message.ConversationId == conversationId &&
                message.SentAt == cursor.SentAt)
            .OrderBy(message => message.Id)
            .Select(message => message.Id)
            .ToListAsync(cancellationToken);
        return orderedIds.IndexOf(candidate.Id) > orderedIds.IndexOf(cursor.Id);
    }

    private async Task AcquireReadLockAsync(
        string resourceType,
        Guid resourceId,
        Guid userId,
        bool useTransaction,
        CancellationToken cancellationToken)
    {
        if (!useTransaction ||
            _context.Database.ProviderName != "Microsoft.EntityFrameworkCore.SqlServer") return;
        var lockName = $"collaboration-read:{resourceType}:{resourceId:N}:{userId:N}";
        await _context.Database.ExecuteSqlInterpolatedAsync(
            $"""
            DECLARE @result int;
            EXEC @result = sys.sp_getapplock
                @Resource = {lockName},
                @LockMode = 'Exclusive',
                @LockOwner = 'Transaction',
                @LockTimeout = 10000;
            IF @result < 0
                THROW 51000, 'Could not acquire the collaboration read-state lock.', 1;
            """,
            cancellationToken);
    }

    private sealed record MessagePosition(Guid Id, DateTime SentAt);
    private sealed record RecipientMessage(Guid Id, Guid SenderId);
}
