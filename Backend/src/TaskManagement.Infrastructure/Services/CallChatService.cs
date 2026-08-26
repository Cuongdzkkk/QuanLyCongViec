using Microsoft.EntityFrameworkCore;
using TaskManagement.Application.DTOs.Collaboration;
using TaskManagement.Application.Interfaces;
using TaskManagement.Domain.Entities;
using TaskManagement.Infrastructure.Data;

namespace TaskManagement.Infrastructure.Services;

public sealed class CallChatService : ICallChatService
{
    private readonly ApplicationDbContext _context;

    public CallChatService(ApplicationDbContext context) => _context = context;

    public async Task<IReadOnlyList<CallChatMessageDto>> GetHistoryAsync(
        string roomId,
        Guid callSessionId,
        int limit,
        CancellationToken cancellationToken = default)
    {
        var messages = await _context.Set<CallChatMessage>()
            .AsNoTracking()
            .Where(message => message.RoomId == roomId && message.CallSessionId == callSessionId)
            .OrderByDescending(message => message.CreatedAt)
            .ThenByDescending(message => message.Id)
            .Take(Math.Clamp(limit, 1, 100))
            .Join(
                _context.Users.AsNoTracking(),
                message => message.SenderUserId,
                user => user.Id,
                (message, user) => new CallChatMessageDto(
                    message.Id,
                    message.CallSessionId,
                    message.RoomId,
                    message.SenderUserId,
                    string.IsNullOrWhiteSpace(user.FullName) ? user.Email : user.FullName,
                    message.Content,
                    message.CreatedAt))
            .ToListAsync(cancellationToken);

        messages.Reverse();
        return messages;
    }

    public async Task<CallChatMessageDto> CreateAsync(
        string roomId,
        Guid callSessionId,
        Guid senderUserId,
        string senderName,
        string content,
        string? clientMessageId,
        CancellationToken cancellationToken = default)
    {
        var message = new CallChatMessage
        {
            Id = Guid.NewGuid(),
            CallSessionId = callSessionId,
            RoomId = roomId,
            SenderUserId = senderUserId,
            Content = content.Trim(),
            CreatedAt = DateTime.UtcNow
        };
        _context.Set<CallChatMessage>().Add(message);
        await _context.SaveChangesAsync(cancellationToken);
        return new CallChatMessageDto(
            message.Id,
            message.CallSessionId,
            message.RoomId,
            message.SenderUserId,
            senderName,
            message.Content,
            message.CreatedAt,
            string.IsNullOrWhiteSpace(clientMessageId) ? null : clientMessageId.Trim());
    }
}
