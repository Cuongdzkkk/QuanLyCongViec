using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using TaskManagement.Infrastructure.Data;

namespace TaskManagement.API.Controllers
{
    [ApiController]
    [Route("api/gamification")]
    [Authorize]
    public class GamificationController : ControllerBase
    {
        private readonly ApplicationDbContext _context;

        public GamificationController(ApplicationDbContext context)
        {
            _context = context;
        }

        [HttpGet("me")]
        public async Task<IActionResult> GetMyRewards()
        {
            var userId = GetUserId();
            if (userId == null)
            {
                return Unauthorized(new { message = "Token khong hop le." });
            }

            var wallet = await _context.UserWallets
                .AsNoTracking()
                .Include(item => item.User)
                .FirstOrDefaultAsync(item => item.UserId == userId.Value);

            var transactions = await _context.PointTransactions
                .AsNoTracking()
                .Where(item => item.UserWalletUserId == userId.Value)
                .Include(item => item.WorkTask)
                .OrderByDescending(item => item.CreatedAt)
                .Take(80)
                .Select(item => new
                {
                    item.Id,
                    item.Amount,
                    item.Reason,
                    item.TransactionType,
                    item.WorkTaskId,
                    TaskTitle = item.WorkTask != null ? item.WorkTask.Title : null,
                    TaskSequenceId = item.WorkTask != null ? item.WorkTask.SequenceId : null,
                    item.CreatedAt
                })
                .ToListAsync();

            var transactionTaskIds = transactions
                .Where(item => item.WorkTaskId != null)
                .Select(item => (Guid)item.WorkTaskId!)
                .Distinct()
                .ToHashSet();

            var userTimeLogTaskIds = await _context.TimeLogs
                .AsNoTracking()
                .Where(item => item.UserId == userId.Value)
                .Select(item => item.WorkTaskId)
                .Distinct()
                .ToListAsync();

            var assignedTaskSnapshots = await _context.WorkTasks
                .AsNoTracking()
                .Where(task => !task.IsDeleted && (
                    task.AssignedUserId == userId.Value ||
                    task.TaskAssignments.Any(assignment => assignment.UserId == userId.Value) ||
                    transactionTaskIds.Contains(task.Id) ||
                    userTimeLogTaskIds.Contains(task.Id)))
                .Select(task => new
                {
                    task.Id,
                    task.Title,
                    task.SequenceId,
                    task.Priority,
                    task.StoryPoints,
                    task.TotalEstimatedHours,
                    task.TotalActualHours,
                    task.PlannedStartDate,
                    task.PlannedEndDate,
                    task.DueDate,
                    task.AssignedUserId,
                    task.ReporterId,
                    HasChildren = task.ChildTasks.Any(child => !child.IsDeleted),
                    StatusName = task.TaskStatus.Name,
                    ActiveAssigneeCount = task.TaskAssignments.Count(assignment => assignment.Status),
                    TotalAssigneeCount = task.TaskAssignments.Count(),
                    HasUserRewardTransaction = transactionTaskIds.Contains(task.Id),
                    UserAssignmentEstimatedHours = task.TaskAssignments
                        .Where(assignment => assignment.UserId == userId.Value)
                        .Select(assignment => (double?)assignment.EstimatedHours)
                        .FirstOrDefault(),
                    UserAssignmentActualHours = task.TaskAssignments
                        .Where(assignment => assignment.UserId == userId.Value)
                        .Select(assignment => (double?)assignment.TotalActualHours)
                        .FirstOrDefault(),
                    UserAssignmentProgressPercent = task.TaskAssignments
                        .Where(assignment => assignment.UserId == userId.Value)
                        .Select(assignment => (double?)assignment.ProgressPercent)
                        .FirstOrDefault(),
                    UserAssignmentContributionWeight = task.TaskAssignments
                        .Where(assignment => assignment.UserId == userId.Value)
                        .Select(assignment => (double?)assignment.ContributionWeight)
                        .FirstOrDefault(),
                    UserLoggedHours = task.TimeLogs
                        .Where(log => log.UserId == userId.Value)
                        .Sum(log => (double?)log.Hours) ?? 0
                })
                .ToListAsync();

            var involvedTaskIds = assignedTaskSnapshots.Select(task => (Guid)task.Id).ToHashSet();

            var timeLogSnapshots = await _context.TimeLogs
                .AsNoTracking()
                .Where(item => item.UserId == userId.Value && involvedTaskIds.Contains(item.WorkTaskId))
                .GroupBy(item => item.WorkTaskId)
                .Select(group => new
                {
                    WorkTaskId = group.Key,
                    LoggedHours = group.Sum(item => item.Hours)
                })
                .ToListAsync();

            var points = wallet?.TotalPoints ?? 0;
            var lifetimeExp = await _context.PointTransactions
                .Where(pt => pt.UserWalletUserId == userId.Value && pt.Amount > 0)
                .SumAsync(pt => pt.Amount);
            var career = CalculateCareer(lifetimeExp);
            var summary = BuildSummary(transactions, assignedTaskSnapshots, timeLogSnapshots);

            return Ok(new
            {
                statusCode = 200,
                message = "Success",
                data = new
                {
                    wallet = new
                    {
                        userId = userId.Value,
                        totalPoints = points,
                        level = career.Level,
                        nextLevelAt = career.NextThreshold,
                        userName = wallet?.User.FullName ?? wallet?.User.Email,
                        rankTitle = career.Title
                    },
                    career = new
                    {
                        level = career.Level,
                        title = career.Title,
                        pointsIntoLevel = career.PointsIntoLevel,
                        currentThreshold = career.CurrentThreshold,
                        nextThreshold = career.NextThreshold,
                        progressPercent = career.ProgressPercent
                    },
                    formula = new
                    {
                        expression = "Base points = Difficulty x Standard duration. Final points = Share + Bonus - Penalty",
                        earlyBonusPercent = 10,
                        estimateAccuracyBonusPercent = 5,
                        latePenaltyPercent = 10,
                        contributionRule = "Diem duoc chia theo estimated hours cua assignee. Neu khong co estimate thi fallback sang contribution weight.",
                        actualHoursRule = "Actual hours luon uu tien lay tu time log. Assignment actual hours va task actual hours duoc roll-up tu time log.",
                        policy = new
                        {
                            parentRollup = "Task cha roll-up estimate va actual hours tu sub-work items.",
                            multiAssignee = "Estimate cua task khong bi nhan doi; effort duoc chia theo assignee estimate truoc, roi moi fallback sang contribution weight.",
                            reopenedWork = "Task bi rollback hoac reopen se bi tru diem thong qua giao dich rollback.",
                            carryOver = "Task qua cycle van giu estimate/actual; bonus va penalty duoc xet tai thoi diem hoan thanh."
                        },
                        sample = summary.SampleFormula
                    },
                    summary = new
                    {
                        totalTransactions = transactions.Count,
                        earnedPoints = transactions.Where(item => item.Amount > 0).Sum(item => item.Amount),
                        rollbackPoints = Math.Abs(transactions.Where(item => item.Amount < 0).Sum(item => item.Amount)),
                        basePoints = summary.BasePoints,
                        bonusPoints = summary.BonusPoints,
                        penaltyPoints = summary.PenaltyPoints,
                        completedTasks = summary.CompletedTasks,
                        earlyBonuses = summary.EarlyBonuses,
                        contributionPercent = summary.ContributionPercent,
                        estimatedHours = summary.EstimatedHours,
                        actualHours = summary.ActualHours,
                        loggedHours = summary.LoggedHours
                    },
                    spotlightTasks = summary.SpotlightTasks,
                    transactions,
                    recentAchievements = summary.RecentAchievements
                }
            });
        }

        [HttpGet("leaderboard")]
        public async Task<IActionResult> GetLeaderboard()
        {
            var leaders = await _context.UserWallets
                .AsNoTracking()
                .Include(item => item.User)
                .OrderByDescending(item => item.TotalPoints)
                .ThenBy(item => item.User.FullName)
                .Take(20)
                .ToListAsync();

            var payload = leaders.Select(item =>
            {
                var career = CalculateCareer(item.TotalPoints);
                return new
                {
                    item.UserId,
                    UserName = item.User.FullName ?? item.User.Email,
                    item.TotalPoints,
                    item.Level,
                    careerLevel = career.Level,
                    careerTitle = career.Title,
                    nextThreshold = career.NextThreshold
                };
            });

            return Ok(new { statusCode = 200, message = "Success", data = payload });
        }

        private Guid? GetUserId()
        {
            var claim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            return Guid.TryParse(claim, out var id) ? id : null;
        }

        private static (int Level, string Title, int CurrentThreshold, int NextThreshold, int PointsIntoLevel, int ProgressPercent) CalculateCareer(int points)
        {
            var level = 1;
            var currentThreshold = 0;
            var nextThreshold = PointsForCareerLevel(level + 1);

            while (points >= nextThreshold)
            {
                level++;
                currentThreshold = nextThreshold;
                nextThreshold = PointsForCareerLevel(level + 1);
            }

            var span = Math.Max(1, nextThreshold - currentThreshold);
            var pointsIntoLevel = Math.Max(0, points - currentThreshold);
            var progress = Math.Min(100, (int)Math.Round((double)pointsIntoLevel / span * 100, MidpointRounding.AwayFromZero));

            return (level, CareerTitle(level), currentThreshold, nextThreshold, pointsIntoLevel, progress);
        }

        private static int PointsForCareerLevel(int level)
        {
            var normalizedLevel = Math.Max(1, level);
            return 250 * normalizedLevel * (normalizedLevel + 1);
        }

        private static string CareerTitle(int level)
        {
            if (level <= 1) return "Contributor";
            if (level == 2) return "Specialist";
            if (level == 3) return "Senior Specialist";
            if (level == 4) return "Lead";
            if (level == 5) return "Principal";
            return "Director";
        }

        private static RewardSummary BuildSummary(
            IEnumerable<dynamic> transactions,
            IEnumerable<dynamic> tasks,
            IEnumerable<dynamic> timeLogs)
        {
            var taskList = tasks.ToList();
            var completedTaskList = taskList
                .Where(IsCompletedForRewardSummary)
                .ToList();
            var timeLogLookup = timeLogs.ToDictionary(item => (Guid)item.WorkTaskId, item => (double)item.LoggedHours);

            var completedTasks = completedTaskList.Count;
            var earlyBonuses = transactions.Count(item => $"{item.TransactionType ?? string.Empty}".Contains("EarlyBonus", StringComparison.OrdinalIgnoreCase));
            var basePoints = transactions
                .Where(item =>
                    string.Equals($"{item.TransactionType ?? string.Empty}", "TaskBaseReward", StringComparison.OrdinalIgnoreCase) ||
                    string.Equals($"{item.TransactionType ?? string.Empty}", "AssignmentShareReward", StringComparison.OrdinalIgnoreCase))
                .Sum(item => (int)item.Amount);
            var bonusPoints = transactions
                .Where(item =>
                    $"{item.TransactionType ?? string.Empty}".Contains("Bonus", StringComparison.OrdinalIgnoreCase))
                .Sum(item => (int)item.Amount);
            var penaltyPoints = Math.Abs(transactions
                .Where(item =>
                    $"{item.TransactionType ?? string.Empty}".Contains("Penalty", StringComparison.OrdinalIgnoreCase) ||
                    $"{item.TransactionType ?? string.Empty}".Contains("Rollback", StringComparison.OrdinalIgnoreCase))
                .Sum(item => (int)item.Amount));
            var totalContributionWeight = completedTaskList
                .Select(task => (double)Math.Max(0.0, ReadDouble(task.UserAssignmentContributionWeight)))
                .DefaultIfEmpty(0.0)
                .Sum();
            var contributionPercent = completedTaskList.Count > 0
                ? Math.Round((double)(totalContributionWeight / Math.Max(completedTaskList.Count, 1) * 100), 1)
                : 0;
            var loggedHours = completedTaskList.Select(task => (double)Math.Max(0.0, GetUserLoggedHours(task, timeLogLookup))).DefaultIfEmpty(0.0).Sum();
            var estimatedHours = completedTaskList.Select(task => (double)GetUserEstimatedHours(task)).DefaultIfEmpty(0.0).Sum();
            var actualHours = completedTaskList.Select(task => (double)Math.Max(GetUserActualHours(task), GetUserLoggedHours(task, timeLogLookup))).DefaultIfEmpty(0.0).Sum();

            var spotlightTasks = completedTaskList
                .OrderByDescending(task => CalculateTaskFormula(task.StoryPoints, task.TotalEstimatedHours, task.PlannedStartDate, task.PlannedEndDate, task.DueDate))
                .Take(4)
                .Select(task =>
                {
                    var basePointsForTask = CalculateTaskFormula(task.StoryPoints, task.TotalEstimatedHours, task.PlannedStartDate, task.PlannedEndDate, task.DueDate);
                    var estimatedTaskHours = GetUserEstimatedHours(task);
                    var actualTaskHours = Math.Max(GetUserActualHours(task), GetUserLoggedHours(task, timeLogLookup));
                    var share = CalculateAssignedTaskSharePercent(task);
                    var overdueTask = IsOverdue(task.DueDate);
                    var bonusPointsForTask = overdueTask
                        ? 0
                        : CalculateEarlyBonusPoints(basePointsForTask, task.DueDate, $"{task.StatusName ?? string.Empty}")
                            + CalculateAccuracyBonusPoints(basePointsForTask, estimatedTaskHours, actualTaskHours);
                    var penaltyPointsForTask = CalculateLatePenaltyPoints(basePointsForTask, task.DueDate, $"{task.StatusName ?? string.Empty}");
                    var sharedBasePoints = Math.Max(1, (int)Math.Round(basePointsForTask * (share / 100.0), MidpointRounding.AwayFromZero));
                    var sharedBonusPoints = Math.Max(0, (int)Math.Round(bonusPointsForTask * (share / 100.0), MidpointRounding.AwayFromZero));
                    var sharedPenaltyPoints = Math.Max(0, (int)Math.Round(penaltyPointsForTask * (share / 100.0), MidpointRounding.AwayFromZero));
                    var finalPoints = overdueTask ? -sharedPenaltyPoints : Math.Max(0, sharedBasePoints + sharedBonusPoints - sharedPenaltyPoints);
                    var efficiency = CalculateEfficiency(estimatedTaskHours, actualTaskHours);
                    var qualityModifier = CalculateQualityModifier(task.DueDate, $"{task.StatusName ?? string.Empty}");
                    return new
                    {
                        task.Id,
                        task.Title,
                        task.SequenceId,
                        task.Priority,
                        storyPoints = task.StoryPoints,
                        estimatedDays = EstimateDays(task.PlannedStartDate, task.PlannedEndDate, task.DueDate),
                        basePoints = basePointsForTask,
                        sharedBasePoints,
                        contributionShare = share,
                        progressPercent = task.UserAssignmentProgressPercent != null ? (int)Math.Round((double)task.UserAssignmentProgressPercent, MidpointRounding.AwayFromZero) : ($"{task.StatusName ?? string.Empty}".ToUpperInvariant().Contains("DONE") ? 100 : 0),
                        estimatedHours = Math.Round(estimatedTaskHours, 1),
                        actualHours = Math.Round(actualTaskHours, 1),
                        efficiency = Math.Round(efficiency, 2),
                        qualityModifier = Math.Round(qualityModifier, 2),
                        bonusPoints = sharedBonusPoints,
                        penaltyPoints = sharedPenaltyPoints,
                        finalPoints,
                        fairPoints = finalPoints
                    };
                })
                .ToList();

            var recentAchievements = transactions
                .Where(item => item.Amount > 0)
                .Take(3)
                .Select(item => new
                {
                    item.Id,
                    title = item.TaskSequenceId ?? item.TaskTitle ?? "Work item",
                    item.Amount,
                    createdAt = item.CreatedAt,
                    item.Reason
                })
                .ToList();

            var sampleTask = spotlightTasks.FirstOrDefault();

            return new RewardSummary
            {
                CompletedTasks = completedTasks,
                EarlyBonuses = earlyBonuses,
                BasePoints = basePoints,
                BonusPoints = bonusPoints,
                PenaltyPoints = penaltyPoints,
                ContributionPercent = contributionPercent,
                EstimatedHours = Math.Round(estimatedHours, 1),
                ActualHours = Math.Round(actualHours, 1),
                LoggedHours = Math.Round(loggedHours, 1),
                SpotlightTasks = spotlightTasks,
                RecentAchievements = recentAchievements,
                SampleFormula = sampleTask == null
                    ? new
                    {
                        value = 3,
                        impact = 1,
                        days = 2,
                        difficulty = 3,
                        duration = 2,
                        share = 100,
                        bonus = 0,
                        penalty = 0,
                        total = 6,
                        note = "Task mau se xuat hien sau khi co du lieu that."
                    }
                    : new
                    {
                        value = Math.Max(1, (int)Math.Round((double)sampleTask.storyPoints, MidpointRounding.AwayFromZero)),
                        impact = 1,
                        days = sampleTask.estimatedDays,
                        difficulty = Math.Max(1, (int)Math.Round((double)sampleTask.storyPoints, MidpointRounding.AwayFromZero)),
                        duration = sampleTask.estimatedDays,
                        share = sampleTask.contributionShare,
                        bonus = sampleTask.bonusPoints,
                        penalty = sampleTask.penaltyPoints,
                        total = sampleTask.finalPoints,
                        note = $"Estimate {sampleTask.estimatedHours}h / Actual {sampleTask.actualHours}h / Shared base {sampleTask.sharedBasePoints} pts."
                    }
            };
        }

        private static int CalculateEarlyBonusPoints(int basePoints, DateTime? dueDate, string statusName)
        {
            var normalized = statusName.ToUpperInvariant();
            var isDone = normalized.Contains("DONE") || normalized.Contains("COMPLETE");
            if (!isDone || !dueDate.HasValue || DateTime.Now.Date > dueDate.Value.Date)
            {
                return 0;
            }

            return Math.Max(1, (int)Math.Round(basePoints * 0.1, MidpointRounding.AwayFromZero));
        }

        private static int CalculateAccuracyBonusPoints(int basePoints, double estimatedHours, double actualHours)
        {
            if (estimatedHours <= 0 || actualHours <= 0)
            {
                return 0;
            }

            var ratio = actualHours / Math.Max(estimatedHours, 0.5);
            if (ratio < 0.85 || ratio > 1.15)
            {
                return 0;
            }

            return Math.Max(1, (int)Math.Round(basePoints * 0.05, MidpointRounding.AwayFromZero));
        }

        private static int CalculateLatePenaltyPoints(int basePoints, DateTime? dueDate, string statusName)
        {
            var normalized = statusName.ToUpperInvariant();
            var isDone = normalized.Contains("DONE") || normalized.Contains("COMPLETE");
            if (!isDone || !IsOverdue(dueDate))
            {
                return 0;
            }

            return Math.Max(1, (int)Math.Round(basePoints * 0.1, MidpointRounding.AwayFromZero));
        }

        private static double CalculateEfficiency(double estimatedHours, double actualHours)
        {
            var safeEstimate = Math.Max(1, estimatedHours);
            if (actualHours <= 0)
            {
                return 1;
            }

            var ratio = safeEstimate / Math.Max(actualHours, 0.5);
            return Math.Clamp(ratio, 0.6, 1.25);
        }

        private static double CalculateQualityModifier(DateTime? dueDate, string statusName)
        {
            var normalized = statusName.ToUpperInvariant();
            var isDone = normalized.Contains("DONE") || normalized.Contains("COMPLETE");
            var overdue = IsOverdue(dueDate);

            if (!isDone)
            {
                return overdue ? 0.75 : 0.9;
            }

            return overdue ? 0.9 : 1.1;
        }

        private static int CalculateTaskFormula(double storyPoints, double totalEstimatedHours, DateTime? plannedStartDate, DateTime? plannedEndDate, DateTime? dueDate)
        {
            var value = Math.Max(1, (int)Math.Round(storyPoints <= 0 ? Math.Max(1, totalEstimatedHours / 4.0) : storyPoints, MidpointRounding.AwayFromZero));
            var days = EstimateDays(plannedStartDate, plannedEndDate, dueDate);
            if (days <= 1 && totalEstimatedHours > 0 && !plannedStartDate.HasValue && !plannedEndDate.HasValue)
            {
                days = Math.Max(1, (int)Math.Ceiling(totalEstimatedHours / 8.0));
            }

            return value * days;
        }

        private static bool IsOverdue(DateTime? dueDate)
        {
            return dueDate.HasValue && DateTime.Now.Date > dueDate.Value.Date;
        }

        private static bool IsCompletedForRewardSummary(dynamic task)
        {
            var statusName = $"{task.StatusName ?? string.Empty}".ToUpperInvariant();
            if (statusName.Contains("DONE") || statusName.Contains("COMPLETE"))
            {
                return true;
            }

            if (ReadDouble(task.UserAssignmentProgressPercent) >= 100)
            {
                return true;
            }

            return task.HasUserRewardTransaction == true;
        }

        private static int CalculateContributionSharePercent(double assignmentEstimateHours, double taskEstimateHours, double contributionWeight)
        {
            if (taskEstimateHours > 0 && assignmentEstimateHours > 0)
            {
                return Math.Max(5, Math.Min(100, (int)Math.Round((assignmentEstimateHours / taskEstimateHours) * 100, MidpointRounding.AwayFromZero)));
            }

            if (contributionWeight > 0)
            {
                return Math.Max(5, Math.Min(100, (int)Math.Round(contributionWeight * 100, MidpointRounding.AwayFromZero)));
            }

            return 100;
        }

        private static double GetUserEstimatedHours(dynamic task)
        {
            var assignmentEstimate = Math.Max(0.0, ReadDouble(task.UserAssignmentEstimatedHours));
            if (assignmentEstimate > 0)
            {
                return assignmentEstimate;
            }

            var totalEstimate = Math.Max(0.0, ReadDouble(task.TotalEstimatedHours));
            var assigneeCount = Math.Max(1, ReadInt(task.ActiveAssigneeCount) > 0 ? ReadInt(task.ActiveAssigneeCount) : ReadInt(task.TotalAssigneeCount));
            return totalEstimate > 0 ? Math.Round(totalEstimate / assigneeCount, 1) : 0;
        }

        private static double GetUserActualHours(dynamic task)
        {
            var assignmentActual = Math.Max(0.0, ReadDouble(task.UserAssignmentActualHours));
            if (assignmentActual > 0)
            {
                return assignmentActual;
            }

            return Math.Max(0.0, ReadDouble(task.UserLoggedHours));
        }

        private static double GetUserLoggedHours(dynamic task, Dictionary<Guid, double> timeLogLookup)
        {
            return timeLogLookup.TryGetValue((Guid)task.Id, out var loggedHours)
                ? Math.Max(0.0, loggedHours)
                : Math.Max(0.0, ReadDouble(task.UserLoggedHours));
        }

        private static int CalculateAssignedTaskSharePercent(dynamic task)
        {
            var assignmentEstimate = Math.Max(0.0, ReadDouble(task.UserAssignmentEstimatedHours));
            var taskEstimate = Math.Max(0.0, ReadDouble(task.TotalEstimatedHours));
            var contributionWeight = Math.Max(0.0, ReadDouble(task.UserAssignmentContributionWeight));
            var assigneeCount = Math.Max(1, ReadInt(task.ActiveAssigneeCount) > 0 ? ReadInt(task.ActiveAssigneeCount) : ReadInt(task.TotalAssigneeCount));

            if (taskEstimate > 0 && assignmentEstimate > 0)
            {
                return Math.Max(5, Math.Min(100, (int)Math.Round((assignmentEstimate / taskEstimate) * 100, MidpointRounding.AwayFromZero)));
            }

            if (contributionWeight > 0)
            {
                return Math.Max(5, Math.Min(100, (int)Math.Round(contributionWeight * 100, MidpointRounding.AwayFromZero)));
            }

            return Math.Max(5, Math.Min(100, (int)Math.Round(100.0 / assigneeCount, MidpointRounding.AwayFromZero)));
        }

        private static double ReadDouble(object? value)
        {
            if (value == null)
            {
                return 0;
            }

            return Convert.ToDouble(value);
        }

        private static int ReadInt(object? value)
        {
            if (value == null)
            {
                return 0;
            }

            return Convert.ToInt32(value);
        }

        private static int EstimateDays(DateTime? plannedStartDate, DateTime? plannedEndDate, DateTime? dueDate)
        {
            var endDate = plannedEndDate ?? dueDate;
            if (plannedStartDate.HasValue && endDate.HasValue)
            {
                return Math.Max(1, (endDate.Value.Date - plannedStartDate.Value.Date).Days + 1);
            }

            return 1;
        }

        private sealed class RewardSummary
        {
            public int CompletedTasks { get; init; }
            public int EarlyBonuses { get; init; }
            public int BasePoints { get; init; }
            public int BonusPoints { get; init; }
            public int PenaltyPoints { get; init; }
            public double ContributionPercent { get; init; }
            public double EstimatedHours { get; init; }
            public double ActualHours { get; init; }
            public double LoggedHours { get; init; }
            public object SampleFormula { get; init; } = null!;
            public IEnumerable<object> SpotlightTasks { get; init; } = Array.Empty<object>();
            public IEnumerable<object> RecentAchievements { get; init; } = Array.Empty<object>();
        }
    }
}
