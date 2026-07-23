namespace TaskManagement.Domain.Entities;

public class AiActionExecution
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    public Guid WorkspaceId { get; set; }
    public Guid? ProjectId { get; set; }
    public Guid? ConversationId { get; set; }
    public string ActionType { get; set; } = string.Empty;
    public string IdempotencyKey { get; set; } = string.Empty;
    public string PayloadJson { get; set; } = "{}";
    public string PayloadHash { get; set; } = string.Empty;
    public string PreviewJson { get; set; } = "{}";
    public string? ResultJson { get; set; }
    public string State { get; set; } = "DRAFT";
    public string? ErrorCode { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
    public DateTime ExpiresAt { get; set; }
    public DateTime? ConfirmedAt { get; set; }
    public DateTime? ExecutedAt { get; set; }
    public byte[] RowVersion { get; set; } = Array.Empty<byte>();
}
