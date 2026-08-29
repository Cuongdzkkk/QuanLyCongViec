using System.Globalization;
using Microsoft.EntityFrameworkCore;
using TaskManagement.Application.DTOs.Rewards;
using TaskManagement.Application.Interfaces;
using TaskManagement.Domain.Entities;
using TaskManagement.Infrastructure.Data;

namespace TaskManagement.Infrastructure.Services;

/// <summary>
/// SprintA Reward System V1. Season points are deliberately separate from the
/// career wallet and from every billing/payment aggregate.
/// </summary>
public sealed class RewardSystemService : IRewardSystemService
{
    private static readonly HashSet<string> SeasonTypes = new(StringComparer.OrdinalIgnoreCase)
        { "Sprint", "Month", "EntireProject", "Custom" };
    private static readonly HashSet<string> RewardTypes = new(StringComparer.OrdinalIgnoreCase)
        { "Cash", "Voucher", "Gift", "Privilege", "Custom" };
    private static readonly HashSet<string> ConditionTypes = new(StringComparer.OrdinalIgnoreCase)
        { "Ranking", "PersonalMilestone", "TeamGoal" };
    private static readonly HashSet<string> ConditionMetrics = new(StringComparer.OrdinalIgnoreCase)
        { "SeasonPoints", "FinalizedTaskCount", "OnTimeRate" };
    private static readonly string[] ManagerRoles =
        { "PM", "PO", "SM", "PROJECT_MANAGER", "PROJECT_LEAD", "PROJECT_ADMIN", "ADMIN" };
    private readonly ApplicationDbContext _context;
    private readonly TimeProvider _timeProvider;

    public RewardSystemService(ApplicationDbContext context, TimeProvider? timeProvider = null)
    {
        _context = context;
        _timeProvider = timeProvider ?? TimeProvider.System;
    }

    private DateTimeOffset UtcNow => _timeProvider.GetUtcNow();

    public async Task<IReadOnlyList<RewardSeasonDto>> GetSeasonsAsync(Guid projectId, Guid userId)
    {
        await EnsureMemberAsync(projectId, userId);
        return await _context.RewardSeasons.AsNoTracking()
            .Where(item => item.ProjectId == projectId)
            .OrderByDescending(item => item.StartAt)
            .Select(item => new RewardSeasonDto(item.Id, item.Name, item.Type, item.SprintId, item.StartAt, item.EndAt, item.TimeZone, item.Status, item.AllowSelfApproval))
            .ToListAsync();
    }

    public async Task<RewardDashboardDto> GetDashboardAsync(Guid projectId, Guid userId)
    {
        await EnsureMemberAsync(projectId, userId);
        var canManage = await IsManagerAsync(projectId, userId);
        var season = await CurrentSeasonAsync(projectId);
        var careerXp = await _context.RewardPointEvents.AsNoTracking().Where(item => item.UserId == userId && item.Status == "Finalized").SumAsync(item => (int?)item.Xp) ?? 0;
        if (season == null)
        {
            return new RewardDashboardDto(null, careerXp, CalculateLevel(careerXp), 0,
                Array.Empty<RewardLeaderboardEntryDto>(), Array.Empty<RewardPointEventDto>(),
                Array.Empty<RewardGrantDto>(), Array.Empty<RewardGrantDto>(), Array.Empty<RewardDefinitionDto>(), Array.Empty<RewardProgressDto>(), canManage);
        }

        var members = await _context.ProjectMembers.AsNoTracking()
            .Where(item => item.ProjectId == projectId && item.Status && item.LeftAt == null)
            .Include(item => item.User).ToListAsync();
        var events = await _context.RewardPointEvents.AsNoTracking()
            .Where(item => item.SeasonId == season.Id).Include(item => item.User).Include(item => item.WorkTask).ToListAsync();
        var leaderboard = members.Select(member =>
            {
                var userEvents = events.Where(item => item.UserId == member.UserId && item.Status == "Finalized").ToList();
                return new { member.UserId, Name = member.User.FullName ?? member.User.Email, Points = userEvents.Sum(item => item.Points), Tasks = userEvents.Count };
            })
            .OrderByDescending(item => item.Points).ThenBy(item => item.Name)
            .Select((item, index) => new RewardLeaderboardEntryDto(item.UserId, item.Name, item.Points, item.Tasks, index + 1))
            .ToList();

        var grants = await _context.RewardGrants.AsNoTracking()
            .Where(item => item.SeasonId == season.Id).Include(item => item.RecipientUser).Include(item => item.RewardDefinition)
            .OrderByDescending(item => item.EarnedAt).ToListAsync();
        var definitions = await _context.RewardDefinitions.AsNoTracking().Where(item => item.SeasonId == season.Id && item.IsEnabled).ToListAsync();
        var myEvents = events.Where(item => item.UserId == userId && item.Status == "Finalized").ToList();
        var myOnTimeRate = myEvents.Count == 0 ? 0 : myEvents.Count(item => !item.DueDateSnapshot.HasValue || item.CompletedAt.UtcDateTime.Date <= item.DueDateSnapshot.Value.Date) * 100m / myEvents.Count;
        var myRank = leaderboard.FirstOrDefault(item => item.UserId == userId)?.Rank ?? leaderboard.Count + 1;
        var teamOnTimeRate = events.Count == 0 ? 0 : events.Count(item => !item.DueDateSnapshot.HasValue || item.CompletedAt.UtcDateTime.Date <= item.DueDateSnapshot.Value.Date) * 100m / events.Count;
        var progress = definitions.Select(definition =>
        {
            var current = definition.ConditionType.Equals("Ranking", StringComparison.OrdinalIgnoreCase) ? myRank : definition.ConditionType.Equals("TeamGoal", StringComparison.OrdinalIgnoreCase) ? teamOnTimeRate : definition.ConditionMetric.Equals("FinalizedTaskCount", StringComparison.OrdinalIgnoreCase) ? myEvents.Count : definition.ConditionMetric.Equals("OnTimeRate", StringComparison.OrdinalIgnoreCase) ? myOnTimeRate : myEvents.Sum(item => item.Points);
            var goal = definition.ConditionType.Equals("Ranking", StringComparison.OrdinalIgnoreCase) ? definition.RankTo ?? definition.RankFrom ?? 1 : definition.Threshold;
            var percent = definition.ConditionType.Equals("Ranking", StringComparison.OrdinalIgnoreCase) ? (myRank <= goal ? 100 : Math.Max(0, (int)Math.Round(goal / Math.Max(myRank, 1) * 100))) : goal <= 0 ? 100 : Math.Min(100, Math.Max(0, (int)Math.Round(current / goal * 100)));
            return new RewardProgressDto(definition.Id, definition.Name, definition.ConditionType == "Ranking" ? $"Top {goal}" : $"{definition.ConditionMetric} ≥ {goal}", current, goal, percent);
        }).ToList();
        var pending = events.Where(item => item.Status == "Pending" && (canManage || item.UserId == userId)).Select(ToEventDto).ToList();
        return new RewardDashboardDto(ToSeasonDto(season), careerXp, CalculateLevel(careerXp),
            leaderboard.FirstOrDefault(item => item.UserId == userId)?.SeasonPoints ?? 0,
            leaderboard, pending,
            grants.Where(item => (canManage || item.RecipientUserId == userId) && item.Status != "Fulfilled" && item.Status != "Cancelled").Select(ToGrantDto).ToList(),
            grants.Where(item => canManage || item.RecipientUserId == userId).Select(ToGrantDto).ToList(), definitions.Select(ToDefinitionDto).ToList(), progress, canManage)
        {
            MyRank = myRank,
            MyOnTimeRate = myOnTimeRate,
            TeamOnTimeRate = teamOnTimeRate
        };
    }

    public async Task<RewardSeasonDto> CreateSeasonAsync(Guid projectId, Guid userId, CreateRewardSeasonRequest request)
    {
        await EnsureManagerAsync(projectId, userId);
        var type = NormalizeChoice(request.Type, SeasonTypes, "season type");
        if (string.IsNullOrWhiteSpace(request.Name)) throw new ArgumentException("Season name is required.");
        var project = await _context.Projects.Include(item => item.Workspace).FirstOrDefaultAsync(item => item.Id == projectId)
            ?? throw new KeyNotFoundException("Project not found.");
        var window = await ResolveSeasonWindowAsync(project, type, request);

        var season = new RewardSeason
        {
            Id = Guid.NewGuid(), ProjectId = projectId, Name = request.Name.Trim(), Type = type,
            SprintId = request.SprintId, StartAt = window.StartAt, EndAt = window.EndAt,
            TimeZone = window.TimeZone,
            AllowSelfApproval = request.AllowSelfApproval, CreatedBy = userId, CreatedAt = UtcNow
        };
        _context.RewardSeasons.Add(season);
        await _context.SaveChangesAsync();
        return ToSeasonDto(season);
    }

    public async Task<RewardSeasonDto> ActivateSeasonAsync(Guid projectId, Guid seasonId, Guid userId)
    {
        await EnsureManagerAsync(projectId, userId);
        var season = await _context.RewardSeasons.FirstOrDefaultAsync(item => item.Id == seasonId && item.ProjectId == projectId)
            ?? throw new KeyNotFoundException("Reward season not found.");
        if (season.EndAt.HasValue && season.EndAt <= season.StartAt) throw new InvalidOperationException("Season dates are invalid.");
        if (await _context.RewardSeasons.AnyAsync(item => item.ProjectId == projectId && item.Status == "Active" && item.Id != seasonId))
            throw new InvalidOperationException("Only one reward season may be active for a project.");
        season.Status = "Active";
        await _context.SaveChangesAsync();
        return ToSeasonDto(season);
    }

    public async Task<RewardSeasonDto> CloseSeasonAsync(Guid projectId, Guid seasonId, Guid userId)
    {
        await EnsureManagerAsync(projectId, userId);
        var season = await _context.RewardSeasons.FirstOrDefaultAsync(item => item.Id == seasonId && item.ProjectId == projectId)
            ?? throw new KeyNotFoundException("Reward season not found.");
        if (await _context.RewardPointEvents.AnyAsync(item => item.SeasonId == seasonId && item.Status == "Pending"))
            throw new InvalidOperationException("Review all pending task events before closing the season.");
        season.Status = "Closed"; season.ClosedAt = UtcNow;
        await _context.SaveChangesAsync();
        await SettleSeasonAsync(projectId, seasonId, userId);
        return ToSeasonDto(season);
    }

    public async Task<RewardDefinitionDto> CreateDefinitionAsync(Guid projectId, Guid seasonId, Guid userId, CreateRewardDefinitionRequest request)
    {
        await EnsureManagerAsync(projectId, userId);
        if (!await _context.RewardSeasons.AnyAsync(item => item.Id == seasonId && item.ProjectId == projectId))
            throw new KeyNotFoundException("Reward season not found.");
        var rewardType = NormalizeChoice(request.RewardType, RewardTypes, "reward type");
        var conditionType = NormalizeChoice(request.ConditionType, ConditionTypes, "condition type");
        var metric = NormalizeChoice(request.ConditionMetric, ConditionMetrics, "condition metric");
        if (string.IsNullOrWhiteSpace(request.Name) || request.Threshold < 0) throw new ArgumentException("Reward name and a non-negative threshold are required.");
        if (conditionType.Equals("Ranking", StringComparison.OrdinalIgnoreCase) && (request.RankFrom is < 1 || request.RankTo is < 1 || (request.RankFrom.HasValue && request.RankTo.HasValue && request.RankTo < request.RankFrom)))
            throw new ArgumentException("Ranking bounds are invalid.");
        var now = UtcNow;
        var definition = new RewardDefinition { Id = Guid.NewGuid(), ProjectId = projectId, SeasonId = seasonId, Name = request.Name.Trim(), Description = request.Description?.Trim(),
            RewardType = rewardType, DisplayValue = request.DisplayValue, Currency = request.Currency?.Trim(), ConditionType = conditionType, ConditionMetric = metric,
            Threshold = request.Threshold, RankFrom = request.RankFrom, RankTo = request.RankTo, RequireActiveMemberAtSettlement = request.RequireActiveMemberAtSettlement,
            CreatedBy = userId, CreatedAt = now, UpdatedAt = now };
        _context.RewardDefinitions.Add(definition); await _context.SaveChangesAsync(); return ToDefinitionDto(definition);
    }

    private async Task<(DateTimeOffset StartAt, DateTimeOffset? EndAt, string TimeZone)> ResolveSeasonWindowAsync(Project project, string type, CreateRewardSeasonRequest request)
    {
        var timeZoneId = string.IsNullOrWhiteSpace(request.TimeZone) ? project.Workspace?.Timezone : request.TimeZone;
        timeZoneId = string.IsNullOrWhiteSpace(timeZoneId) ? "UTC" : timeZoneId.Trim();
        var timeZone = ResolveTimeZone(timeZoneId);
        var startLocal = request.StartAt.DateTime;
        DateTime? endLocal = request.EndAt?.DateTime;

        if (type.Equals("Month", StringComparison.OrdinalIgnoreCase))
        {
            startLocal = new DateTime(request.StartAt.Year, request.StartAt.Month, 1);
            endLocal = new DateTime(startLocal.Year, startLocal.Month, DateTime.DaysInMonth(startLocal.Year, startLocal.Month), 23, 59, 59, 999).AddTicks(9999);
        }
        else if (type.Equals("Sprint", StringComparison.OrdinalIgnoreCase) && request.SprintId.HasValue)
        {
            var sprint = await _context.Sprints.AsNoTracking().FirstOrDefaultAsync(item => item.Id == request.SprintId && item.ProjectId == project.Id)
                ?? throw new ArgumentException("Sprint does not belong to this project.");
            startLocal = sprint.StartDate.Date;
            endLocal = sprint.EndDate.Date.AddDays(1).AddTicks(-1);
        }
        else if (type.Equals("EntireProject", StringComparison.OrdinalIgnoreCase))
        {
            if (project.StartDate != default) startLocal = project.StartDate.Date;
            endLocal = project.EndDate?.Date.AddDays(1).AddTicks(-1) ?? endLocal;
        }

        if (endLocal.HasValue && endLocal.Value <= startLocal) throw new ArgumentException("Season end must be after start.");
        return (ToUtcInstant(startLocal, timeZone), endLocal.HasValue ? ToUtcInstant(endLocal.Value, timeZone) : null, timeZoneId);
    }

    private static TimeZoneInfo ResolveTimeZone(string timeZoneId)
    {
        var candidates = timeZoneId switch
        {
            "Asia/Saigon" => new[] { "Asia/Saigon", "Asia/Ho_Chi_Minh", "SE Asia Standard Time" },
            "Asia/Ho_Chi_Minh" => new[] { "Asia/Ho_Chi_Minh", "Asia/Saigon", "SE Asia Standard Time" },
            "UTC" => new[] { "UTC", "Etc/UTC" },
            _ => new[] { timeZoneId }
        };
        foreach (var candidate in candidates)
        {
            try { return TimeZoneInfo.FindSystemTimeZoneById(candidate); }
            catch (TimeZoneNotFoundException) { }
            catch (InvalidTimeZoneException) { }
        }
        throw new ArgumentException($"Unknown timezone '{timeZoneId}'.");
    }

    private static DateTimeOffset ToUtcInstant(DateTime local, TimeZoneInfo timeZone)
    {
        var unspecified = DateTime.SpecifyKind(local, DateTimeKind.Unspecified);
        if (timeZone.IsInvalidTime(unspecified)) throw new ArgumentException("Season boundary falls in a missing local time.");
        return new DateTimeOffset(TimeZoneInfo.ConvertTimeToUtc(unspecified, timeZone));
    }

    private static RewardPointEvent CreatePointEvent(WorkTask task, Guid seasonId, Guid userId, int points, DateTimeOffset completedAt, string key) => new()
    {
        Id = Guid.NewGuid(), ProjectId = task.ProjectId, SeasonId = seasonId, WorkTaskId = task.Id, UserId = userId,
        Points = points, Xp = points, Status = "Pending", CompletedAt = completedAt, DueDateSnapshot = task.DueDate,
        CreatedAt = completedAt, IdempotencyKey = key, DifficultySnapshot = DifficultyLabel(task), ScoreSource = task.StoryPoints > 0 ? "StoryPoints" : "EstimateHours"
    };

    public async Task HandleTaskStatusChangeAsync(Guid workTaskId, Guid actorUserId, string? oldStatusName, string? newStatusName)
    {
        var wasDone = IsDone(oldStatusName); var isDone = IsDone(newStatusName);
        if (wasDone == isDone) return;
        var task = await _context.WorkTasks.AsNoTracking().Include(item => item.TaskAssignments)
            .FirstOrDefaultAsync(item => item.Id == workTaskId && !item.IsDeleted);
        if (task == null || await _context.WorkTasks.AnyAsync(item => item.ParentTaskId == workTaskId && !item.IsDeleted)) return;
        if (isDone)
        {
            var completedAt = UtcNow;
            var season = await _context.RewardSeasons.FirstOrDefaultAsync(item => item.ProjectId == task.ProjectId && item.Status == "Active" &&
                item.StartAt <= completedAt && (!item.EndAt.HasValue || completedAt <= item.EndAt.Value));
            if (season == null) return;
            var members = await _context.ProjectMembers.AsNoTracking().Where(item => item.ProjectId == task.ProjectId && item.Status && item.LeftAt == null).Select(item => item.UserId).ToListAsync();
            var assignees = task.TaskAssignments.Where(item => item.Status).Select(item => item.UserId).Where(members.Contains).Distinct().ToList();
            if (assignees.Count == 0 && task.AssignedUserId.HasValue && members.Contains(task.AssignedUserId.Value)) assignees.Add(task.AssignedUserId.Value);
            if (assignees.Count == 0 && members.Contains(task.ReporterId)) assignees.Add(task.ReporterId);
            var activeAssignments = task.TaskAssignments.Where(item => item.Status && assignees.Contains(item.UserId)).ToList();
            var total = CalculateTaskScore(task);
            foreach (var userId in assignees)
            {
                var assignment = activeAssignments.FirstOrDefault(item => item.UserId == userId);
                var share = activeAssignments.Count <= 1 ? 1d : CalculateShare(task, assignment, activeAssignments);
                var points = Math.Max(1, (int)Math.Round(total * share, MidpointRounding.AwayFromZero));
                var key = $"reward-task:{workTaskId:N}:{userId:N}";
                var pointEvent = await _context.RewardPointEvents.FirstOrDefaultAsync(item => item.WorkTaskId == workTaskId && item.UserId == userId);
                if (pointEvent == null)
                {
                    _context.RewardPointEvents.Add(CreatePointEvent(task, season.Id, userId, points, completedAt, key));
                }
                else if (pointEvent.Status == "Cancelled")
                {
                    // The unique task/member key represents one logical award. A corrected
                    // completion rearms that award instead of creating a second ledger row.
                    pointEvent.ProjectId = task.ProjectId;
                    pointEvent.SeasonId = season.Id;
                    pointEvent.Points = points;
                    pointEvent.Xp = points;
                    pointEvent.Status = "Pending";
                    pointEvent.CompletedAt = completedAt;
                    pointEvent.DueDateSnapshot = task.DueDate;
                    pointEvent.FinalizedAt = null;
                    pointEvent.FinalizedBy = null;
                    pointEvent.CancelledAt = null;
                    pointEvent.CancelledBy = null;
                    pointEvent.CancellationReason = null;
                    pointEvent.DifficultySnapshot = DifficultyLabel(task);
                    pointEvent.ScoreSource = task.StoryPoints > 0 ? "StoryPoints" : "EstimateHours";
                }
            }
            try
            {
                await _context.SaveChangesAsync();
            }
            catch (DbUpdateException exception) when (IsUniqueConstraintViolation(exception))
            {
                // A concurrent completion may have won the unique logical-award key.
                // Treat that race as an idempotent retry rather than surfacing a duplicate-award error.
                foreach (var entry in _context.ChangeTracker.Entries<RewardPointEvent>().Where(item => item.State == EntityState.Added))
                    entry.State = EntityState.Detached;
            }
        }
        else
        {
            var events = await _context.RewardPointEvents.Where(item => item.WorkTaskId == workTaskId && item.Status != "Cancelled").ToListAsync();
            foreach (var pointEvent in events)
            {
                pointEvent.Status = "Cancelled"; pointEvent.CancelledAt = UtcNow; pointEvent.CancelledBy = actorUserId;
                pointEvent.CancellationReason = "Task rejected or reopened.";
            }
            await _context.SaveChangesAsync();
        }
    }

    public async Task<RewardPointEventDto> ReviewPointEventAsync(Guid projectId, Guid eventId, Guid reviewerId, bool approve, string? reason)
    {
        await EnsureManagerAsync(projectId, reviewerId);
        var pointEvent = await _context.RewardPointEvents.Include(item => item.Season).FirstOrDefaultAsync(item => item.Id == eventId && item.ProjectId == projectId)
            ?? throw new KeyNotFoundException("Reward point event not found.");
        if (pointEvent.Status != "Pending") return ToEventDto(pointEvent);
        if (!approve)
        {
            pointEvent.Status = "Cancelled"; pointEvent.CancelledAt = UtcNow; pointEvent.CancelledBy = reviewerId;
            pointEvent.CancellationReason = string.IsNullOrWhiteSpace(reason) ? "Rejected by manager." : reason.Trim();
        }
        else
        {
            if (pointEvent.UserId == reviewerId && !pointEvent.Season.AllowSelfApproval) throw new UnauthorizedAccessException("Self-approval is disabled for this season.");
            pointEvent.Status = "Finalized"; pointEvent.FinalizedAt = UtcNow; pointEvent.FinalizedBy = reviewerId;
        }
        try
        {
            await _context.SaveChangesAsync();
        }
        catch (DbUpdateException exception) when (IsUniqueConstraintViolation(exception))
        {
            foreach (var entry in _context.ChangeTracker.Entries<RewardGrant>().Where(item => item.State == EntityState.Added))
                entry.State = EntityState.Detached;
        }
        return ToEventDto(pointEvent);
    }

    public async Task SettleSeasonAsync(Guid projectId, Guid seasonId, Guid userId)
    {
        await EnsureManagerAsync(projectId, userId);
        var season = await _context.RewardSeasons.FirstOrDefaultAsync(item => item.Id == seasonId && item.ProjectId == projectId)
            ?? throw new KeyNotFoundException("Reward season not found.");
        if (season.Status != "Closed") throw new InvalidOperationException("Only closed seasons can be settled.");
        var definitions = await _context.RewardDefinitions.Where(item => item.SeasonId == seasonId && item.IsEnabled).ToListAsync();
        var members = await _context.ProjectMembers.AsNoTracking().Where(item => item.ProjectId == projectId).Include(item => item.User).ToListAsync();
        var activeMemberIds = members.Where(item => item.Status && item.LeftAt == null).Select(item => item.UserId).ToHashSet();
        var events = await _context.RewardPointEvents.AsNoTracking().Where(item => item.SeasonId == seasonId && item.Status == "Finalized").ToListAsync();
        var scores = members.Select(member => new { member.UserId, Points = events.Where(item => item.UserId == member.UserId).Sum(item => item.Points), Tasks = events.Count(item => item.UserId == member.UserId) }).OrderByDescending(item => item.Points).ThenBy(item => item.UserId).ToList();
        foreach (var definition in definitions)
        {
            IEnumerable<Guid> recipients;
            var requiresResolution = false;
            if (definition.ConditionType.Equals("Ranking", StringComparison.OrdinalIgnoreCase))
            {
                var from = Math.Max(1, definition.RankFrom ?? 1); var to = Math.Max(from, definition.RankTo ?? from);
                requiresResolution = to < scores.Count && scores[to - 1].Points == scores[to].Points;
                var cutoff = to <= scores.Count ? scores[to - 1].Points : 0;
                recipients = scores.Where((item, index) => index + 1 >= from && (index + 1 <= to || (requiresResolution && item.Points == cutoff)))
                    .Where(item => !definition.RequireActiveMemberAtSettlement || activeMemberIds.Contains(item.UserId)).Select(item => item.UserId);
            }
            else if (definition.ConditionType.Equals("TeamGoal", StringComparison.OrdinalIgnoreCase))
            {
                var onTime = events.Count == 0 ? 0 : events.Count(item => !item.DueDateSnapshot.HasValue || item.CompletedAt.UtcDateTime.Date <= item.DueDateSnapshot.Value.Date) * 100m / events.Count;
                recipients = onTime >= definition.Threshold ? members.Where(item => !definition.RequireActiveMemberAtSettlement || activeMemberIds.Contains(item.UserId)).Select(item => item.UserId) : Enumerable.Empty<Guid>();
            }
            else
            {
                recipients = scores.Where(item =>
                {
                    var userEvents = events.Where(pointEvent => pointEvent.UserId == item.UserId).ToList();
                    var value = definition.ConditionMetric.Equals("FinalizedTaskCount", StringComparison.OrdinalIgnoreCase)
                        ? item.Tasks
                        : definition.ConditionMetric.Equals("OnTimeRate", StringComparison.OrdinalIgnoreCase)
                            ? (userEvents.Count == 0 ? 0 : userEvents.Count(pointEvent => !pointEvent.DueDateSnapshot.HasValue || pointEvent.CompletedAt.UtcDateTime.Date <= pointEvent.DueDateSnapshot.Value.Date) * 100m / userEvents.Count)
                            : item.Points;
                    return value >= definition.Threshold && (!definition.RequireActiveMemberAtSettlement || activeMemberIds.Contains(item.UserId));
                }).Select(item => item.UserId);
            }
            foreach (var recipient in recipients.Distinct())
                if (!await _context.RewardGrants.AnyAsync(item => item.RewardDefinitionId == definition.Id && item.RecipientUserId == recipient))
                    _context.RewardGrants.Add(new RewardGrant { Id = Guid.NewGuid(), ProjectId = projectId, SeasonId = seasonId, RewardDefinitionId = definition.Id,
                        RecipientUserId = recipient, Status = "PendingFulfillment", RequiresManagerResolution = requiresResolution, EarnedAt = UtcNow, CreatedAt = UtcNow });
        }
        await _context.SaveChangesAsync();
    }

    public async Task<RewardGrantDto> ResolveGrantAsync(Guid projectId, Guid grantId, Guid userId, bool award, string? note)
    {
        await EnsureManagerAsync(projectId, userId);
        var grant = await _context.RewardGrants.Include(item => item.RecipientUser).Include(item => item.RewardDefinition)
            .FirstOrDefaultAsync(item => item.Id == grantId && item.ProjectId == projectId) ?? throw new KeyNotFoundException("Reward grant not found.");
        if (!grant.RequiresManagerResolution) return ToGrantDto(grant);
        grant.RequiresManagerResolution = false;
        grant.ManagerNote = string.IsNullOrWhiteSpace(note) ? "Tie resolved by manager." : note.Trim();
        grant.Status = award ? "PendingFulfillment" : "Cancelled";
        await _context.SaveChangesAsync();
        return ToGrantDto(grant);
    }

    public async Task<RewardGrantDto> FulfillGrantAsync(Guid projectId, Guid grantId, Guid userId)
    {
        await EnsureManagerAsync(projectId, userId);
        var grant = await _context.RewardGrants.Include(item => item.RecipientUser).Include(item => item.RewardDefinition)
            .FirstOrDefaultAsync(item => item.Id == grantId && item.ProjectId == projectId) ?? throw new KeyNotFoundException("Reward grant not found.");
        if (grant.RequiresManagerResolution) throw new InvalidOperationException("Resolve the tied ranking before fulfillment.");
        if (grant.Status == "Fulfilled") return ToGrantDto(grant);
        if (grant.Status is not ("Earned" or "PendingFulfillment")) throw new InvalidOperationException("Grant is not fulfillable.");
        grant.Status = "Fulfilled"; grant.FulfilledAt = UtcNow; grant.FulfilledBy = userId;
        await _context.SaveChangesAsync();
        return ToGrantDto(grant);
    }

    private async Task<RewardSeason?> CurrentSeasonAsync(Guid projectId)
    {
        var now = UtcNow;
        var active = await _context.RewardSeasons.AsNoTracking()
            .Where(item => item.ProjectId == projectId && item.Status == "Active" && item.StartAt <= now && (!item.EndAt.HasValue || now <= item.EndAt.Value))
            .OrderByDescending(item => item.StartAt).FirstOrDefaultAsync();
        return active ?? await _context.RewardSeasons.AsNoTracking()
            .Where(item => item.ProjectId == projectId && item.Status == "Closed")
            .OrderByDescending(item => item.StartAt).FirstOrDefaultAsync();
    }

    private async Task EnsureMemberAsync(Guid projectId, Guid userId)
    {
        if (!await _context.ProjectMembers.AnyAsync(item => item.ProjectId == projectId && item.UserId == userId && item.Status && item.LeftAt == null))
            throw new UnauthorizedAccessException("Active project membership is required.");
    }

    private async Task EnsureManagerAsync(Guid projectId, Guid userId)
    {
        if (!await IsManagerAsync(projectId, userId)) throw new UnauthorizedAccessException("Project manager permission is required.");
    }

    private async Task<bool> IsManagerAsync(Guid projectId, Guid userId)
    {
        var project = await _context.Projects.AsNoTracking().FirstOrDefaultAsync(item => item.Id == projectId) ?? throw new KeyNotFoundException("Project not found.");
        if (project.CreatorId == userId) return true;
        var role = await _context.ProjectMembers.Where(item => item.ProjectId == projectId && item.UserId == userId && item.Status && item.LeftAt == null).Select(item => item.ProjectRole).FirstOrDefaultAsync();
        return role != null && ManagerRoles.Contains(role.Trim(), StringComparer.OrdinalIgnoreCase);
    }

    private static string NormalizeChoice(string value, HashSet<string> choices, string label)
    {
        var choice = choices.FirstOrDefault(item => item.Equals(value?.Trim(), StringComparison.OrdinalIgnoreCase));
        return choice ?? throw new ArgumentException($"Unsupported {label}.");
    }

    private static bool IsDone(string? value) => !string.IsNullOrWhiteSpace(value) && (value.Contains("done", StringComparison.OrdinalIgnoreCase) || value.Contains("complete", StringComparison.OrdinalIgnoreCase) || value.Contains("hoàn thành", StringComparison.OrdinalIgnoreCase));
    private static bool IsUniqueConstraintViolation(DbUpdateException exception)
    {
        var message = exception.ToString();
        return message.Contains("2601", StringComparison.Ordinal) || message.Contains("2627", StringComparison.Ordinal) || message.Contains("UNIQUE KEY", StringComparison.OrdinalIgnoreCase);
    }
    private static int CalculateTaskScore(WorkTask task)
    {
        if (task.StoryPoints > 0) return Math.Max(50, (int)Math.Round(task.StoryPoints * 50, MidpointRounding.AwayFromZero));
        var hours = task.TotalEstimatedHours;
        return hours <= 4 ? 50 : hours <= 16 ? 100 : hours <= 40 ? 200 : 350;
    }
    private static string DifficultyLabel(WorkTask task) => task.StoryPoints > 0 ? $"SP:{task.StoryPoints.ToString(CultureInfo.InvariantCulture)}" : task.TotalEstimatedHours <= 4 ? "S" : task.TotalEstimatedHours <= 16 ? "M" : task.TotalEstimatedHours <= 40 ? "L" : "XL";
    private static double CalculateShare(WorkTask task, TaskAssignment? assignment, List<TaskAssignment> active)
    {
        if (assignment == null) return 1d / Math.Max(active.Count, 1);
        var totalHours = active.Sum(item => Math.Max(0, item.EstimatedHours));
        if (totalHours > 0) return Math.Max(0, assignment.EstimatedHours) / totalHours;
        var totalWeight = active.Sum(item => Math.Max(0, item.ContributionWeight));
        return totalWeight > 0 ? Math.Max(0, assignment.ContributionWeight) / totalWeight : 1d / Math.Max(active.Count, 1);
    }
    private static int CalculateLevel(int points) { var level = 1; while (points >= 250 * (level + 1) * (level + 2)) level++; return level; }
    private static RewardSeasonDto ToSeasonDto(RewardSeason item) => new(item.Id, item.Name, item.Type, item.SprintId, item.StartAt, item.EndAt, item.TimeZone, item.Status, item.AllowSelfApproval);
    private static RewardPointEventDto ToEventDto(RewardPointEvent item) => new(item.Id, item.WorkTaskId, item.UserId, item.User?.FullName ?? item.User?.Email ?? "Member", item.Status, item.Points, item.Xp, item.ScoreSource, item.DifficultySnapshot, item.CompletedAt, item.FinalizedAt, item.CancellationReason);
    private static RewardGrantDto ToGrantDto(RewardGrant item) => new(item.Id, item.RewardDefinitionId, item.RecipientUserId, item.RecipientUser?.FullName ?? item.RecipientUser?.Email ?? "", item.RewardDefinition?.Name ?? "", item.RewardDefinition?.RewardType ?? "Custom", item.Status, item.RequiresManagerResolution, item.EarnedAt, item.FulfilledAt);
    private static RewardDefinitionDto ToDefinitionDto(RewardDefinition item) => new(item.Id, item.Name, item.Description, item.RewardType, item.DisplayValue, item.Currency, item.ConditionType, item.ConditionMetric, item.Threshold, item.RankFrom, item.RankTo, item.RequireActiveMemberAtSettlement, item.IsEnabled);
}
