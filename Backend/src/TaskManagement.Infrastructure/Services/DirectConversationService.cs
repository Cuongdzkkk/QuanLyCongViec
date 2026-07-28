using System.Data;
using Microsoft.EntityFrameworkCore;
using TaskManagement.Application.DTOs.Collaboration;
using TaskManagement.Application.Interfaces;
using TaskManagement.Domain.Entities;
using TaskManagement.Infrastructure.Data;

namespace TaskManagement.Infrastructure.Services;

public sealed class DirectConversationService : IDirectConversationService
{
    public const int MaximumContentLength = 4000;
    public const int MaximumPageSize = 100;
    public const string ConversationOrdering = "lastMessageAt_desc,createdAt_desc,conversationId_desc";
    public const string MessageOrdering = "createdAt_desc,messageId_desc";

    private readonly ApplicationDbContext _context;

    public DirectConversationService(ApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<DirectConversationDto> FindOrCreateAsync(
        Guid userId,
        Guid participantUserId,
        CancellationToken cancellationToken = default)
    {
        if (userId == participantUserId)
            throw new ArgumentException("A direct conversation requires another participant.");

        var (lowId, highId) = CanonicalPair(userId, participantUserId);
        var existing = await FindPairAsync(lowId, highId, userId, cancellationToken);
        if (existing != null) return existing;
        if (await PairExistsAsync(lowId, highId, cancellationToken))
            throw new DirectParticipantNotFoundException();

        var workspaceId = await FindSharedWorkspaceAsync(userId, participantUserId, cancellationToken);
        await using var transaction = _context.Database.IsRelational()
            ? await _context.Database.BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken)
            : null;

        try
        {
            if (_context.Database.ProviderName == "Microsoft.EntityFrameworkCore.SqlServer")
            {
                var pairLock = $"direct-conversation:{lowId:N}:{highId:N}";
                await _context.Database.ExecuteSqlInterpolatedAsync(
                    $"""
                    DECLARE @result int;
                    EXEC @result = sys.sp_getapplock
                        @Resource = {pairLock},
                        @LockMode = 'Exclusive',
                        @LockOwner = 'Transaction',
                        @LockTimeout = 10000;
                    IF @result < 0
                        THROW 51000, 'Could not acquire the direct conversation pair lock.', 1;
                    """,
                    cancellationToken);
            }

            existing = await FindPairAsync(lowId, highId, userId, cancellationToken);
            if (existing != null)
            {
                if (transaction != null) await transaction.CommitAsync(cancellationToken);
                return existing;
            }
            if (await PairExistsAsync(lowId, highId, cancellationToken))
                throw new DirectParticipantNotFoundException();

            // Revalidate inside the transaction so conversation and both participants are atomic.
            workspaceId = await FindSharedWorkspaceAsync(userId, participantUserId, cancellationToken);
            var now = DateTime.UtcNow;
            var conversation = new DirectConversation
            {
                Id = Guid.NewGuid(),
                WorkspaceId = workspaceId,
                UserLowId = lowId,
                UserHighId = highId,
                CreatedAt = now
            };
            conversation.Participants.Add(new DirectConversationParticipant
            {
                ConversationId = conversation.Id,
                UserId = lowId,
                JoinedAt = now
            });
            conversation.Participants.Add(new DirectConversationParticipant
            {
                ConversationId = conversation.Id,
                UserId = highId,
                JoinedAt = now
            });
            _context.DirectConversations.Add(conversation);
            await _context.SaveChangesAsync(cancellationToken);
            if (transaction != null) await transaction.CommitAsync(cancellationToken);

            return await FindPairAsync(lowId, highId, userId, cancellationToken)
                ?? throw new InvalidOperationException("The direct conversation could not be loaded.");
        }
        catch (DbUpdateException)
        {
            if (transaction != null) await transaction.RollbackAsync(cancellationToken);
            _context.ChangeTracker.Clear();
            var concurrent = await FindPairAsync(lowId, highId, userId, cancellationToken);
            if (concurrent != null) return concurrent;
            throw;
        }
    }

    public async Task<DirectConversationPageDto> ListAsync(
        Guid userId,
        int page,
        int pageSize,
        CancellationToken cancellationToken = default)
    {
        ValidatePage(page, pageSize);
        await EnsureActiveUserAsync(userId, cancellationToken);

        var query = VisibleConversations(userId);
        var totalCount = await query.CountAsync(cancellationToken);
        var items = await query
            .OrderByDescending(conversation => conversation.LastMessageAt)
            .ThenByDescending(conversation => conversation.CreatedAt)
            .ThenByDescending(conversation => conversation.Id)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(conversation => new DirectConversationDto(
                conversation.Id,
                new DirectParticipantDto(
                    conversation.UserLowId == userId ? conversation.UserHighId : conversation.UserLowId,
                    conversation.UserLowId == userId
                        ? conversation.UserHigh.FullName
                        : conversation.UserLow.FullName,
                    conversation.UserLowId == userId
                        ? conversation.UserHigh.AvatarUrl
                        : conversation.UserLow.AvatarUrl),
                conversation.Messages
                    .OrderByDescending(message => message.SentAt)
                    .ThenByDescending(message => message.Id)
                    .Select(message => message.Content)
                    .FirstOrDefault(),
                conversation.LastMessageAt,
                conversation.CreatedAt))
            .ToListAsync(cancellationToken);

        return new(items, page, pageSize, totalCount, ConversationOrdering);
    }

    public async Task<DirectMessagePageDto> GetHistoryAsync(
        Guid conversationId,
        Guid userId,
        int page,
        int pageSize,
        CancellationToken cancellationToken = default)
    {
        ValidatePage(page, pageSize);
        await AuthorizeConversationAsync(conversationId, userId, cancellationToken);
        var query = _context.DirectMessages.AsNoTracking()
            .Where(message => message.ConversationId == conversationId);
        var totalCount = await query.CountAsync(cancellationToken);
        var items = await query
            .OrderByDescending(message => message.SentAt)
            .ThenByDescending(message => message.Id)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(message => new DirectMessageDto(
                message.Id,
                conversationId,
                message.Content,
                new DirectMessageSenderDto(
                    message.SenderId,
                    message.Sender != null ? message.Sender.FullName : "Unknown user",
                    message.Sender != null ? message.Sender.AvatarUrl : null),
                message.SentAt))
            .ToListAsync(cancellationToken);
        return new(items, page, pageSize, totalCount, MessageOrdering);
    }

    public async Task<DirectMessageDto> SendAsync(
        Guid conversationId,
        Guid userId,
        string? content,
        CancellationToken cancellationToken = default)
    {
        var conversation = await AuthorizeConversationAsync(conversationId, userId, cancellationToken);
        var normalizedContent = NormalizeContent(content);
        var recipientId = conversation.UserLowId == userId
            ? conversation.UserHighId
            : conversation.UserLowId;
        var sentAt = DateTime.UtcNow;
        await using var transaction = _context.Database.IsRelational()
            ? await _context.Database.BeginTransactionAsync(
                IsolationLevel.ReadCommitted,
                cancellationToken)
            : null;
        var message = new DirectMessage
        {
            Id = Guid.NewGuid(),
            ConversationId = conversationId,
            SenderId = userId,
            ReceiverId = recipientId,
            Content = normalizedContent,
            SentAt = sentAt
        };
        _context.DirectMessages.Add(message);
        await _context.SaveChangesAsync(cancellationToken);

        if (_context.Database.IsRelational())
        {
            await _context.DirectConversations
                .Where(item => item.Id == conversationId &&
                    (item.LastMessageAt == null || item.LastMessageAt < sentAt))
                .ExecuteUpdateAsync(setters => setters.SetProperty(
                    item => item.LastMessageAt,
                    sentAt), cancellationToken);
        }
        else
        {
            var trackedConversation = await _context.DirectConversations
                .SingleAsync(item => item.Id == conversationId, cancellationToken);
            trackedConversation.LastMessageAt =
                trackedConversation.LastMessageAt == null || trackedConversation.LastMessageAt < sentAt
                    ? sentAt
                    : trackedConversation.LastMessageAt;
            await _context.SaveChangesAsync(cancellationToken);
        }
        if (transaction != null) await transaction.CommitAsync(cancellationToken);

        return await _context.DirectMessages.AsNoTracking()
            .Where(item => item.Id == message.Id)
            .Select(item => new DirectMessageDto(
                item.Id,
                conversationId,
                item.Content,
                new DirectMessageSenderDto(
                    item.SenderId,
                    item.Sender != null ? item.Sender.FullName : "Unknown user",
                    item.Sender != null ? item.Sender.AvatarUrl : null),
                item.SentAt))
            .SingleAsync(cancellationToken);
    }

    private IQueryable<DirectConversation> VisibleConversations(Guid userId) =>
        _context.DirectConversations.AsNoTracking().Where(conversation =>
            conversation.Participants.Any(participant => participant.UserId == userId) &&
            conversation.UserLow.IsActive && !conversation.UserLow.IsDeleted &&
            conversation.UserHigh.IsActive && !conversation.UserHigh.IsDeleted &&
            !conversation.Workspace.IsDeleted &&
            conversation.Workspace.Members.Any(member =>
                member.UserId == conversation.UserLowId && member.IsActive) &&
            conversation.Workspace.Members.Any(member =>
                member.UserId == conversation.UserHighId && member.IsActive));

    private async Task<DirectConversation> AuthorizeConversationAsync(
        Guid conversationId,
        Guid userId,
        CancellationToken cancellationToken)
    {
        await EnsureActiveUserAsync(userId, cancellationToken);
        return await VisibleConversations(userId)
            .SingleOrDefaultAsync(conversation => conversation.Id == conversationId, cancellationToken)
            ?? throw new DirectConversationNotFoundException();
    }

    private async Task<DirectConversationDto?> FindPairAsync(
        Guid lowId,
        Guid highId,
        Guid currentUserId,
        CancellationToken cancellationToken) =>
        await VisibleConversations(currentUserId)
            .Where(conversation => conversation.UserLowId == lowId && conversation.UserHighId == highId)
            .Select(conversation => new DirectConversationDto(
                conversation.Id,
                new DirectParticipantDto(
                    currentUserId == lowId ? highId : lowId,
                    currentUserId == lowId ? conversation.UserHigh.FullName : conversation.UserLow.FullName,
                    currentUserId == lowId ? conversation.UserHigh.AvatarUrl : conversation.UserLow.AvatarUrl),
                conversation.Messages
                    .OrderByDescending(message => message.SentAt)
                    .ThenByDescending(message => message.Id)
                    .Select(message => message.Content)
                    .FirstOrDefault(),
                conversation.LastMessageAt,
                conversation.CreatedAt))
            .SingleOrDefaultAsync(cancellationToken);

    private Task<bool> PairExistsAsync(
        Guid lowId,
        Guid highId,
        CancellationToken cancellationToken) =>
        _context.DirectConversations.AsNoTracking().AnyAsync(
            conversation =>
                conversation.UserLowId == lowId &&
                conversation.UserHighId == highId,
            cancellationToken);

    private async Task<Guid> FindSharedWorkspaceAsync(
        Guid userId,
        Guid participantUserId,
        CancellationToken cancellationToken)
    {
        await EnsureActiveUserAsync(userId, cancellationToken);
        var participantIsActive = await _context.Users.AsNoTracking()
            .AnyAsync(user =>
                user.Id == participantUserId && user.IsActive && !user.IsDeleted,
                cancellationToken);
        if (!participantIsActive) throw new DirectParticipantNotFoundException();

        var workspaceId = await _context.WorkspaceMembers.AsNoTracking()
            .Where(member =>
                member.UserId == userId &&
                member.IsActive &&
                !member.Workspace.IsDeleted &&
                member.Workspace.Members.Any(other =>
                    other.UserId == participantUserId && other.IsActive))
            .OrderBy(member => member.WorkspaceId)
            .Select(member => (Guid?)member.WorkspaceId)
            .FirstOrDefaultAsync(cancellationToken);
        return workspaceId ?? throw new DirectParticipantNotFoundException();
    }

    private async Task EnsureActiveUserAsync(Guid userId, CancellationToken cancellationToken)
    {
        if (!await _context.Users.AsNoTracking().AnyAsync(
                user => user.Id == userId && user.IsActive && !user.IsDeleted,
                cancellationToken))
            throw new DirectParticipantNotFoundException();
    }

    private static (Guid LowId, Guid HighId) CanonicalPair(Guid first, Guid second) =>
        first.CompareTo(second) < 0 ? (first, second) : (second, first);

    private static string NormalizeContent(string? content)
    {
        if (string.IsNullOrWhiteSpace(content))
            throw new ArgumentException("Message content is required.", nameof(content));
        var normalized = content.Replace("\r\n", "\n", StringComparison.Ordinal)
            .Replace('\r', '\n')
            .Trim();
        if (normalized.Length > MaximumContentLength)
            throw new ArgumentException(
                $"Message content cannot exceed {MaximumContentLength} characters.",
                nameof(content));
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
