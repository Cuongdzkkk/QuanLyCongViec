using TaskManagement.Application.AI;

namespace TaskManagement.Application.Interfaces
{
    public interface IAiAttachmentService
    {
        Task<AiAttachmentDto> UploadAsync(
            Guid userId,
            Guid workspaceId,
            Guid conversationId,
            string fileName,
            string mimeType,
            Stream content,
            long fileSize,
            CancellationToken cancellationToken = default);

        Task<AiAttachmentDto?> GetAsync(Guid userId, Guid attachmentId, CancellationToken cancellationToken = default);
        Task<AiAttachmentContentDto?> GetContentAsync(Guid userId, Guid attachmentId, CancellationToken cancellationToken = default);
        Task DeleteAsync(Guid userId, Guid attachmentId, CancellationToken cancellationToken = default);
        Task<AiAttachmentChatResponseDto> ChatAsync(
            Guid userId,
            Guid workspaceId,
            Guid conversationId,
            IReadOnlyCollection<Guid> attachmentIds,
            string message,
            CancellationToken cancellationToken = default);
    }

    public sealed class AiAttachmentDto
    {
        public Guid Id { get; set; }
        public Guid WorkspaceId { get; set; }
        public Guid ConversationId { get; set; }
        public string FileName { get; set; } = string.Empty;
        public string MimeType { get; set; } = string.Empty;
        public string Kind { get; set; } = string.Empty;
        public string Status { get; set; } = string.Empty;
        public string? ErrorMessage { get; set; }
        public long FileSize { get; set; }
        public int? Width { get; set; }
        public int? Height { get; set; }
        public int ChunkCount { get; set; }
        public string ContentUrl { get; set; } = string.Empty;
        public DateTime CreatedAt { get; set; }
    }

    public sealed class AiAttachmentContentDto
    {
        public string FileName { get; set; } = string.Empty;
        public string MimeType { get; set; } = string.Empty;
        public byte[] Bytes { get; set; } = Array.Empty<byte>();
    }

    public sealed class AiAttachmentChatResponseDto
    {
        public string Answer { get; set; } = string.Empty;
        public List<AiAttachmentCitationDto> Citations { get; set; } = new();
        public List<AiSuggestedActionDto> Actions { get; set; } = new();
    }

    public sealed class AiAttachmentCitationDto
    {
        public Guid AttachmentId { get; set; }
        public string SourceId { get; set; } = string.Empty;
        public string FileName { get; set; } = string.Empty;
        public string Locator { get; set; } = string.Empty;
        public string Excerpt { get; set; } = string.Empty;
    }

    public sealed class AiAttachmentRagSourceDto
    {
        public Guid AttachmentId { get; set; }
        public string SourceId { get; set; } = string.Empty;
        public string FileName { get; set; } = string.Empty;
        public string Locator { get; set; } = string.Empty;
        public string Content { get; set; } = string.Empty;
    }

    public sealed class AiAttachmentImageInputDto
    {
        public Guid AttachmentId { get; set; }
        public string SourceId { get; set; } = string.Empty;
        public string FileName { get; set; } = string.Empty;
        public string MimeType { get; set; } = string.Empty;
        public byte[] Bytes { get; set; } = Array.Empty<byte>();
    }
}
