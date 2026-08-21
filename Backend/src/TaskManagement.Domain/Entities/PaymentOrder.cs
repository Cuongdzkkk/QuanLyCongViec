namespace TaskManagement.Domain.Entities;

public class PaymentOrder
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    public string PlanCode { get; set; } = string.Empty;
    public string PlanNameSnapshot { get; set; } = string.Empty;
    public int IncludedAiCreditsSnapshot { get; set; }
    public decimal AmountVnd { get; set; }
    public string Currency { get; set; } = "VND";
    public string Provider { get; set; } = "manual_bank_transfer";
    public string Status { get; set; } = "Pending";
    public string TransferCode { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
    public DateTime? ExpiresAt { get; set; }
    public DateTime? PaidAt { get; set; }
    public Guid? ApprovedByUserId { get; set; }
    public string? AdminNote { get; set; }

    public User User { get; set; } = null!;
    public User? ApprovedByUser { get; set; }
    public ICollection<PaymentTransaction> Transactions { get; set; } = new List<PaymentTransaction>();
}
