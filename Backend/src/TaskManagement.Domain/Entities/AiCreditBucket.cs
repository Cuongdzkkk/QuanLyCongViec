namespace TaskManagement.Domain.Entities;

/// <summary>Immutable grant metadata plus the mutable remaining balance for a paid credit grant.</summary>
public sealed class AiCreditBucket
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    public string PlanCode { get; set; } = string.Empty;
    public int GrantedCredits { get; set; }
    public int RemainingCredits { get; set; }
    public DateTime ValidFrom { get; set; }
    public DateTime ExpiresAt { get; set; }
    public string SourceType { get; set; } = "PaymentOrder";
    public Guid? SourcePaymentOrderId { get; set; }
    public string? SourceReference { get; set; }
    public DateTime CreatedAt { get; set; }
    public byte[] RowVersion { get; set; } = Array.Empty<byte>();

    public User User { get; set; } = null!;
    public PaymentOrder? SourcePaymentOrder { get; set; }
    public ICollection<AiCreditReservationAllocation> ReservationAllocations { get; set; } = new List<AiCreditReservationAllocation>();
}
