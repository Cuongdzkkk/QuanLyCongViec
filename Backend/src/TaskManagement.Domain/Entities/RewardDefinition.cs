using System;

namespace TaskManagement.Domain.Entities;

public class RewardDefinition
{
    public Guid Id { get; set; }
    public Guid ProjectId { get; set; }
    public Guid SeasonId { get; set; }
    public RewardSeason Season { get; set; } = null!;
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string RewardType { get; set; } = "Custom";
    public decimal? DisplayValue { get; set; }
    public string? Currency { get; set; }
    public string ConditionType { get; set; } = "PersonalMilestone";
    public string ConditionMetric { get; set; } = "SeasonPoints";
    public decimal Threshold { get; set; }
    public int? RankFrom { get; set; }
    public int? RankTo { get; set; }
    public bool RequireActiveMemberAtSettlement { get; set; } = true;
    public bool IsEnabled { get; set; } = true;
    public Guid CreatedBy { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
}
