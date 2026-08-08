using Microsoft.EntityFrameworkCore;
using TaskManagement.Application.Interfaces;
using TaskManagement.Infrastructure.Data;

namespace TaskManagement.Infrastructure.Services;

public sealed class CollaborationRealtimeAuthorizationService
    : ICollaborationRealtimeAuthorizationService
{
    private readonly ApplicationDbContext _context;
    private readonly IChannelTextService _channelTextService;
    private readonly IDirectConversationService _directConversationService;

    public CollaborationRealtimeAuthorizationService(
        ApplicationDbContext context,
        IChannelTextService channelTextService,
        IDirectConversationService directConversationService)
    {
        _context = context;
        _channelTextService = channelTextService;
        _directConversationService = directConversationService;
    }

    public async Task AuthorizeChannelJoinAsync(
        Guid channelId,
        Guid userId,
        CancellationToken cancellationToken = default)
    {
        await EnsureActiveUserAsync(userId, cancellationToken);
        await _channelTextService.GetHistoryAsync(
            channelId,
            userId,
            page: 1,
            pageSize: 1,
            cancellationToken);
    }

    public async Task AuthorizeDirectConversationJoinAsync(
        Guid conversationId,
        Guid userId,
        CancellationToken cancellationToken = default)
    {
        await EnsureActiveUserAsync(userId, cancellationToken);
        await _directConversationService.GetHistoryAsync(
            conversationId,
            userId,
            page: 1,
            pageSize: 1,
            cancellationToken);
    }

    private async Task EnsureActiveUserAsync(
        Guid userId,
        CancellationToken cancellationToken)
    {
        var active = await _context.Users
            .AsNoTracking()
            .AnyAsync(
                user => user.Id == userId && user.IsActive && !user.IsDeleted,
                cancellationToken);
        if (!active) throw new CollaborationRealtimeUserInactiveException();
    }
}
