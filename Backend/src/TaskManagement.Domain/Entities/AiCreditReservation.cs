namespace TaskManagement.Domain.Entities;

public sealed class AiCreditReservation
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    public string IdempotencyKey { get; set; } = string.Empty;
    public int RequestedCredits { get; set; }
    public int ReservedCredits { get; set; }
    public int FinalizedCredits { get; set; }
    // Compatibility columns retained from PaymentP0Foundation.
    public int Credits { get; set; }
    public string Status { get; set; } = "Reserved";
    public DateTime CreatedAt { get; set; }
    public DateTime ExpiresAt { get; set; }
    public DateTime? FinalizedAt { get; set; }
    public DateTime? ReleasedAt { get; set; }
    public DateTime? CompletedAt { get; set; }

    public User User { get; set; } = null!;
    public ICollection<AiCreditReservationAllocation> Allocations { get; set; } = new List<AiCreditReservationAllocation>();
}
