namespace TaskManagement.Domain.Entities;

public sealed class AiCreditReservationAllocation
{
    public Guid Id { get; set; }
    public Guid ReservationId { get; set; }
    public Guid CreditBucketId { get; set; }
    public int AllocatedCredits { get; set; }
    public int ConsumedCredits { get; set; }
    public DateTime CreatedAt { get; set; }

    public AiCreditReservation Reservation { get; set; } = null!;
    public AiCreditBucket CreditBucket { get; set; } = null!;
}
