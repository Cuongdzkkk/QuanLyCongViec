namespace TaskManagement.Domain.Entities;

public class PaymentOrder
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    public string PlanCode { get; set; } = string.Empty;
    public decimal AmountVnd { get; set; }
    public string Status { get; set; } = "Pending";
    public string TransferCode { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
    public DateTime? PaidAt { get; set; }
    public Guid? ApprovedByUserId { get; set; }
    public string? AdminNote { get; set; }

    public User User { get; set; } = null!;
    public User? ApprovedByUser { get; set; }
}
