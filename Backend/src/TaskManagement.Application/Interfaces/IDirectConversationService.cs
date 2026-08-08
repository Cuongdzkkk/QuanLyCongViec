using TaskManagement.Application.DTOs.Collaboration;

namespace TaskManagement.Application.Interfaces;

public interface IDirectConversationService
{
    Task<DirectConversationDto> FindOrCreateAsync(Guid userId, Guid participantUserId, CancellationToken cancellationToken = default);
    Task<DirectConversationPageDto> ListAsync(Guid userId, int page, int pageSize, CancellationToken cancellationToken = default);
    Task<DirectMessagePageDto> GetHistoryAsync(Guid conversationId, Guid userId, int page, int pageSize, CancellationToken cancellationToken = default);
    Task<DirectMessageDto> SendAsync(Guid conversationId, Guid userId, string? content, CancellationToken cancellationToken = default);
    Task<DirectMessageDto> SendWithAttachmentsAsync(Guid conversationId, Guid userId, string? content, IReadOnlyList<PendingCollaborationAttachmentDto> attachments, CancellationToken cancellationToken = default);
}

public sealed class DirectConversationNotFoundException : Exception
{
    public DirectConversationNotFoundException() : base("Direct conversation was not found.") { }
}

public sealed class DirectParticipantNotFoundException : Exception
{
    public DirectParticipantNotFoundException() : base("Participant was not found or is outside your collaboration scope.") { }
}
