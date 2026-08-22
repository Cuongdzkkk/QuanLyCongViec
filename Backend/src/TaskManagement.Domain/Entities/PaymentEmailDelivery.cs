namespace TaskManagement.Domain.Entities;

public sealed class PaymentEmailDelivery
{
    public Guid Id { get; set; }
    public Guid PaymentOrderId { get; set; }
    public Guid UserId { get; set; }
    public string RecipientEmail { get; set; } = string.Empty;
    public string Kind { get; set; } = "CustomerPaymentReceipt";
    public bool IsAutomatic { get; set; }
    public int Attempt { get; set; }
    public string Status { get; set; } = "Requested";
    public string? ProviderMessageId { get; set; }
    public string? FailureReason { get; set; }
    public DateTime RequestedAt { get; set; }
    public DateTime? SentAt { get; set; }
    public DateTime? FailedAt { get; set; }

    public PaymentOrder PaymentOrder { get; set; } = null!;
    public User User { get; set; } = null!;
}
