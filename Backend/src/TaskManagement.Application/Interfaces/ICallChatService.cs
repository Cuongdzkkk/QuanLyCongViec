using TaskManagement.Application.DTOs.Collaboration;

namespace TaskManagement.Application.Interfaces;

public interface ICallChatService
{
    Task<IReadOnlyList<CallChatMessageDto>> GetHistoryAsync(
        string roomId,
        Guid callSessionId,
        int limit,
        CancellationToken cancellationToken = default);

    Task<CallChatMessageDto> CreateAsync(
        string roomId,
        Guid callSessionId,
        Guid senderUserId,
        string senderName,
        string content,
        string? clientMessageId,
        CancellationToken cancellationToken = default);
}
