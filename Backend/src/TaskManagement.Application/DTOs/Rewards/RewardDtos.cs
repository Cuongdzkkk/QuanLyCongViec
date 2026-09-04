using System;
using System.Collections.Generic;

namespace TaskManagement.Application.DTOs.Rewards;

public sealed record CreateRewardSeasonRequest(
    string Name,
    string Type,
    Guid? SprintId,
    DateTimeOffset StartAt,
    DateTimeOffset? EndAt,
    string? TimeZone,
    bool AllowSelfApproval = false);

public sealed record CreateRewardDefinitionRequest(
    string Name,
    string? Description,
    string RewardType,
    decimal? DisplayValue,
    string? Currency,
    string ConditionType,
    string ConditionMetric,
    decimal Threshold,
    int? RankFrom,
    int? RankTo,
    string Method = "Ranking",
    int? PointCost = null,
    int? Quantity = null,
    int? ClaimLimit = null,
    DateTimeOffset? StartAt = null,
    DateTimeOffset? EndAt = null,
    bool RequireActiveMemberAtSettlement = true);

public sealed record RewardSeasonDto(
    Guid Id, string Name, string Type, Guid? SprintId, DateTimeOffset StartAt,
    DateTimeOffset? EndAt, string TimeZone, string Status, bool AllowSelfApproval);

public sealed record RewardPointEventDto(
    Guid Id, Guid WorkTaskId, Guid UserId, string UserName, string Status, int Points, int Xp,
    string ScoreSource, string DifficultySnapshot, DateTimeOffset CompletedAt,
    DateTimeOffset? FinalizedAt, string? CancellationReason);

public sealed record RewardDefinitionDto(
    Guid Id, string Name, string? Description, string RewardType, decimal? DisplayValue,
    string? Currency, string ConditionType, string ConditionMetric, decimal Threshold,
    int? RankFrom, int? RankTo, 
    string Method, int? PointCost, int? Quantity, int? ClaimLimit, 
    DateTimeOffset? StartAt, DateTimeOffset? EndAt,
    bool RequireActiveMemberAtSettlement, bool IsEnabled);

public sealed record RewardGrantDto(
    Guid Id, Guid RewardDefinitionId, Guid RecipientUserId, string RecipientName,
    string RewardName, string RewardType, string Status, bool RequiresManagerResolution,
    DateTimeOffset EarnedAt, DateTimeOffset? FulfilledAt);

public sealed record RewardLeaderboardEntryDto(Guid UserId, string UserName, int SeasonPoints, int FinalizedTasks, int Rank);

public sealed record RewardProgressDto(Guid RewardDefinitionId, string Name, string ConditionLabel, decimal CurrentValue, decimal GoalValue, int ProgressPercent);

public sealed record RewardDashboardDto(
    RewardSeasonDto? CurrentSeason,
    int CareerXp,
    int CareerLevel,
    int MySeasonPoints,
    IReadOnlyList<RewardLeaderboardEntryDto> Leaderboard,
    IReadOnlyList<RewardPointEventDto> PendingEvents,
    IReadOnlyList<RewardGrantDto> OpenRewards,
    IReadOnlyList<RewardGrantDto> RewardHistory,
    IReadOnlyList<RewardDefinitionDto> AvailableRewards,
    IReadOnlyList<RewardProgressDto> RewardProgress,
    bool CanManage)
{
    public int MyRank { get; init; }
    public decimal MyOnTimeRate { get; init; }
    public decimal TeamOnTimeRate { get; init; }
}

public sealed record RedeemRewardRequest(Guid RewardDefinitionId);

public sealed record RedeemRewardResponse(
    bool Success, 
    string Message, 
    Guid? GrantId, 
    Guid RewardDefinitionId, 
    string RewardName, 
    int SpentPoints, 
    int RemainingPoints, 
    int? RemainingQuantity);

public sealed record LevelConfigDto(Guid Id, Guid ProjectId, int Level, string Title, int RequiredXpPerLevel, Guid? RewardId);
public sealed record UpdateLevelConfigsRequest(IReadOnlyList<LevelConfigDto> Configs);
