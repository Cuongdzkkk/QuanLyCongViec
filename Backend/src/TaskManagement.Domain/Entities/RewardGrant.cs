using System;

namespace TaskManagement.Domain.Entities;

public class RewardGrant
{
    public Guid Id { get; set; }
    public Guid ProjectId { get; set; }
    public Guid SeasonId { get; set; }
    public RewardSeason Season { get; set; } = null!;
    public Guid RewardDefinitionId { get; set; }
    public RewardDefinition RewardDefinition { get; set; } = null!;
    public Guid RecipientUserId { get; set; }
    public User RecipientUser { get; set; } = null!;
    public string Status { get; set; } = "PendingFulfillment";
    public bool RequiresManagerResolution { get; set; }
    public string? ManagerNote { get; set; }
    public DateTimeOffset EarnedAt { get; set; }
    public DateTimeOffset? FulfilledAt { get; set; }
    public Guid? FulfilledBy { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
}
