using System;
using System.Collections.Generic;

namespace TaskManagement.Domain.Entities;

public class RewardSeason
{
    public Guid Id { get; set; }
    public Guid ProjectId { get; set; }
    public Project Project { get; set; } = null!;
    public string Name { get; set; } = string.Empty;
    public string Type { get; set; } = "Sprint";
    public Guid? SprintId { get; set; }
    public Sprint? Sprint { get; set; }
    public DateTimeOffset StartAt { get; set; }
    public DateTimeOffset? EndAt { get; set; }
    public string TimeZone { get; set; } = "UTC";
    public string Status { get; set; } = "Draft";
    public bool AllowSelfApproval { get; set; }
    public Guid CreatedBy { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset? ClosedAt { get; set; }

    public ICollection<RewardPointEvent> PointEvents { get; set; } = new List<RewardPointEvent>();
    public ICollection<RewardDefinition> RewardDefinitions { get; set; } = new List<RewardDefinition>();
    public ICollection<RewardGrant> RewardGrants { get; set; } = new List<RewardGrant>();
}
