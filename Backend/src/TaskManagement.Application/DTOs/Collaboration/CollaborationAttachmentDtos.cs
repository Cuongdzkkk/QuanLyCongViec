namespace TaskManagement.Application.DTOs.Collaboration;

public sealed record CollaborationAttachmentDto(
    Guid AttachmentId,
    string OriginalFileName,
    string ContentType,
    long SizeBytes)
{
    public string DownloadUrl => $"/api/collaboration-attachments/{AttachmentId:D}/content";
}

public sealed record PendingCollaborationAttachmentDto(
    Guid AttachmentId,
    string StorageKey,
    string OriginalFileName,
    string ContentType,
    long SizeBytes);
