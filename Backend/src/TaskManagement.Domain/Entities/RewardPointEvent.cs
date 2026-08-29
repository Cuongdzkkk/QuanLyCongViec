using System;

namespace TaskManagement.Domain.Entities;

public class RewardPointEvent
{
    public Guid Id { get; set; }
    public Guid ProjectId { get; set; }
    public Guid SeasonId { get; set; }
    public RewardSeason Season { get; set; } = null!;
    public Guid WorkTaskId { get; set; }
    public WorkTask WorkTask { get; set; } = null!;
    public Guid UserId { get; set; }
    public User User { get; set; } = null!;
    public int Points { get; set; }
    public int Xp { get; set; }
    public string Status { get; set; } = "Pending";
    public string EventType { get; set; } = "TaskCompletion";
    public string ScoreSource { get; set; } = "StoryPoints";
    public string DifficultySnapshot { get; set; } = "M";
    public DateTimeOffset CompletedAt { get; set; }
    public DateTime? DueDateSnapshot { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset? FinalizedAt { get; set; }
    public Guid? FinalizedBy { get; set; }
    public DateTimeOffset? CancelledAt { get; set; }
    public Guid? CancelledBy { get; set; }
    public string? CancellationReason { get; set; }
    public string IdempotencyKey { get; set; } = string.Empty;
}
