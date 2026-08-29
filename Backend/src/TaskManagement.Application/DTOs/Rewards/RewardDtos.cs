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
    int? RankFrom, int? RankTo, bool RequireActiveMemberAtSettlement, bool IsEnabled);

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
