namespace TaskManagement.Domain.Entities;

public sealed class PaymentWebhookEvent
{
    public Guid Id { get; set; }
    public string Provider { get; set; } = string.Empty;
    public string ProviderEventId { get; set; } = string.Empty;
    public string EventType { get; set; } = string.Empty;
    public string RawPayload { get; set; } = string.Empty;
    public string Status { get; set; } = "Received";
    public DateTime ReceivedAt { get; set; }
    public DateTime? ProcessedAt { get; set; }
}
