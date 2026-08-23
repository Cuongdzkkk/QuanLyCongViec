namespace TaskManagement.Domain.Entities;

public sealed class PaymentTransaction
{
    public Guid Id { get; set; }
    public Guid PaymentOrderId { get; set; }
    public string Provider { get; set; } = string.Empty;
    public string ProviderTransactionId { get; set; } = string.Empty;
    public decimal Amount { get; set; }
    public string Currency { get; set; } = "VND";
    public string Status { get; set; } = "Paid";
    public DateTime PaidAt { get; set; }
    public string? ProviderReference { get; set; }
    public int? IncludedAiCredits { get; set; }
    public DateTime? SubscriptionPeriodStart { get; set; }
    public DateTime? SubscriptionPeriodEnd { get; set; }
    public DateTime CreatedAt { get; set; }

    public PaymentOrder PaymentOrder { get; set; } = null!;
}
