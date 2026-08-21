namespace TaskManagement.Domain.Entities;

public sealed class AiCreditReservation
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    public int Credits { get; set; }
    public string IdempotencyKey { get; set; } = string.Empty;
    public string Status { get; set; } = "Reserved";
    public DateTime ExpiresAt { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? CompletedAt { get; set; }
    public User User { get; set; } = null!;
}
