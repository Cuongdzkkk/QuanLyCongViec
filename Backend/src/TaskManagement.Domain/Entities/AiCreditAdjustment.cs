namespace TaskManagement.Domain.Entities;

public class AiCreditAdjustment
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    public int Amount { get; set; }
    public string AdjustmentType { get; set; } = "Credit";
    public string Reason { get; set; } = string.Empty;
    public Guid CreatedByUserId { get; set; }
    public DateTime EffectivePeriodStart { get; set; }
    public DateTime EffectivePeriodEnd { get; set; }
    public DateTime CreatedAt { get; set; }

    public User User { get; set; } = null!;
    public User CreatedByUser { get; set; } = null!;
}
