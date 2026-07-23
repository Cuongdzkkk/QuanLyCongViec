using System.Globalization;
using System.Text;
using Microsoft.EntityFrameworkCore;
using TaskManagement.Application.Common;
using TaskManagement.Application.Interfaces;
using TaskManagement.Domain.Entities;
using TaskManagement.Infrastructure.Data;

namespace TaskManagement.Infrastructure.Services
{
    public class GamificationService : IGamificationService
    {
        private const string RewardType = "TaskBaseReward";
        private const string RollbackType = "TaskRewardRollback";
        private const string AssignmentRewardType = "AssignmentShareReward";
        private const string EarlyBonusType = "EarlyBonusReward";
        private const string AccuracyBonusType = "EstimateAccuracyBonus";
        private const string LatePenaltyType = "LatePenalty";
        private const string CollaborationBonusType = "CollaborationBonus";
        private const string RewardRuleVersion = "task-reward-v1";
        private readonly ApplicationDbContext _context;

        public GamificationService(ApplicationDbContext context)
        {
            _context = context;
        }

        public async Task ApplyStatusChangeRewardsAsync(Guid workTaskId, Guid actorUserId, string? oldStatusName, string? newStatusName)
        {
            var wasDone = IsDone(oldStatusName);
            var isDone = IsDone(newStatusName);

            if (wasDone == isDone)
            {
                return;
            }

            var strategy = _context.Database.CreateExecutionStrategy();
            await strategy.ExecuteAsync(async () =>
            {
                await using var transaction = await _context.Database.BeginTransactionAsync();
                try
                {
                    if ((_context.Database.ProviderName ?? string.Empty).Contains("SqlServer", StringComparison.OrdinalIgnoreCase))
                    {
                        var lockResource = $"reward-ledger:{workTaskId}";
                        await _context.Database.ExecuteSqlInterpolatedAsync(
                            $"EXEC sys.sp_getapplock @Resource={lockResource}, @LockMode='Exclusive', @LockOwner='Transaction', @LockTimeout=10000");
                    }

                    var task = await _context.WorkTasks
                        .AsNoTracking()
                        .Include(t => t.TaskAssignments)
                        .Include(t => t.Project)
                        .FirstOrDefaultAsync(t => t.Id == workTaskId && !t.IsDeleted);

                    if (task == null)
                    {
                        throw new ArgumentException("Task khong ton tai.");
                    }

                    var rewardRules = ProjectManagementHelper.NormalizeRewardRules(
                        ProjectManagementHelper.ReadSection<TaskManagement.Application.DTOs.Project.ProjectRewardRulesDto>(
                            task.Project?.NavigationConfig,
                            "rewardRules"));

                    if (await HasChildTasksAsync(workTaskId))
                    {
                        await transaction.CommitAsync();
                        return;
                    }

                    var targetUserId = task.AssignedUserId ?? task.ReporterId;
    
                    var wallet = await _context.UserWallets
                        .FirstOrDefaultAsync(w => w.UserId == targetUserId);
    
                    if (wallet == null)
                    {
                        wallet = new UserWallet
                        {
                            UserId = targetUserId,
                            TotalPoints = 0,
                            Level = 1
                        };
                        _context.UserWallets.Add(wallet);
                        await _context.SaveChangesAsync();
                    }
    
                    if (!wasDone && isDone)
                    {
                        // Tasks with active assignment shares are rewarded by the assignment flow.
                        // This guard must never run for Done -> non-Done, because that would skip reversal.
                        if (task.TaskAssignments.Any(ta => ta.Status))
                        {
                            await transaction.CommitAsync();
                            return;
                        }

                        var hasUnreversedReward = await _context.PointTransactions.AnyAsync(reward =>
                            reward.UserWalletUserId == targetUserId &&
                            reward.WorkTaskId == workTaskId &&
                            reward.TransactionType == RewardType &&
                            !_context.PointTransactions.Any(reversal => reversal.ReversalOfTransactionId == reward.Id));
                        if (hasUnreversedReward)
                        {
                            await transaction.CommitAsync();
                            return;
                        }

                        var rewardEventId = Guid.NewGuid();
                        var points = CalculateBaseRewardPoints(task, rewardRules);
                        void AddReward(int amount, string type, string reason)
                        {
                            if (amount == 0)
                            {
                                return;
                            }

                            var previousBalance = wallet.TotalPoints;
                            wallet.TotalPoints = Math.Max(0, wallet.TotalPoints + amount);
                            var appliedAmount = wallet.TotalPoints - previousBalance;
                            if (appliedAmount == 0)
                            {
                                return;
                            }

                            wallet.Level = CalculateLevel(wallet.TotalPoints);
                            _context.PointTransactions.Add(new PointTransaction
                            {
                                Id = Guid.NewGuid(),
                                UserWalletUserId = targetUserId,
                                WorkTaskId = workTaskId,
                                Amount = appliedAmount,
                                TransactionType = type,
                                Reason = reason,
                                RewardEventId = rewardEventId,
                                RewardRuleVersion = RewardRuleVersion,
                                IdempotencyKey = $"task-reward:{rewardEventId:N}:{type}",
                                CreatedAt = DateTime.UtcNow
                            });
                        }

                        AddReward(points, RewardType, $"Cong diem hoan thanh task {task.SequenceId ?? task.Id.ToString()}");

                        var earlyBonus = CalculateEarlyBonus(points, task.DueDate, rewardRules);
                        AddReward(earlyBonus, EarlyBonusType, $"Thuong hoan thanh som task {task.SequenceId ?? task.Id.ToString()}");

                        var accuracyBonus = CalculateAccuracyBonus(points, task.TotalEstimatedHours, task.TotalActualHours, rewardRules);
                        AddReward(accuracyBonus, AccuracyBonusType, $"Thuong estimate sat thuc te cho task {task.SequenceId ?? task.Id.ToString()}");

                        var latePenalty = CalculateLatePenalty(points, task.DueDate, rewardRules);
                        AddReward(latePenalty, LatePenaltyType, $"Giam diem do task {task.SequenceId ?? task.Id.ToString()} hoan thanh tre han");
                    }
                    else if (wasDone && !isDone)
                    {
                        var originalBaseReward = await _context.PointTransactions
                            .Where(reward =>
                                reward.UserWalletUserId == targetUserId &&
                                reward.WorkTaskId == workTaskId &&
                                reward.TransactionType == RewardType &&
                                !_context.PointTransactions.Any(reversal => reversal.ReversalOfTransactionId == reward.Id))
                            .OrderByDescending(reward => reward.CreatedAt)
                            .FirstOrDefaultAsync();

                        if (originalBaseReward != null)
                        {
                            var originals = originalBaseReward.RewardEventId.HasValue
                                ? await _context.PointTransactions
                                    .Where(entry => entry.RewardEventId == originalBaseReward.RewardEventId && entry.TransactionType != RollbackType)
                                    .ToListAsync()
                                : new List<PointTransaction> { originalBaseReward };

                            foreach (var original in originals)
                            {
                                var alreadyReversed = await _context.PointTransactions
                                    .AnyAsync(entry => entry.ReversalOfTransactionId == original.Id);
                                if (alreadyReversed)
                                {
                                    continue;
                                }

                                var reversalAmount = -original.Amount;
                                var previousBalance = wallet.TotalPoints;
                                wallet.TotalPoints = Math.Max(0, wallet.TotalPoints + reversalAmount);
                                var appliedReversal = wallet.TotalPoints - previousBalance;
                                _context.PointTransactions.Add(new PointTransaction
                                {
                                    Id = Guid.NewGuid(),
                                    UserWalletUserId = targetUserId,
                                    WorkTaskId = workTaskId,
                                    Amount = appliedReversal,
                                    TransactionType = RollbackType,
                                    Reason = $"Hoan tra diem do Task bi Reject boi user {actorUserId}",
                                    RewardEventId = original.RewardEventId,
                                    RewardRuleVersion = original.RewardRuleVersion ?? RewardRuleVersion,
                                    ReversalOfTransactionId = original.Id,
                                    IdempotencyKey = $"task-reward-reversal:{original.Id:N}",
                                    CreatedAt = DateTime.UtcNow
                                });
                            }

                            wallet.Level = CalculateLevel(wallet.TotalPoints);
                        }
                    }
    
                    await _context.SaveChangesAsync();
                    await transaction.CommitAsync();
                }
                catch
                {
                    await transaction.RollbackAsync();
                    throw;
                }
            });
        }

        public async Task ApplyAssignmentProgressRewardsAsync(Guid workTaskId, Guid assigneeUserId, Guid actorUserId, double oldProgressPercent, double newProgressPercent)
        {
            if (oldProgressPercent >= 100 || newProgressPercent < 100)
            {
                return;
            }

            var strategy = _context.Database.CreateExecutionStrategy();
            await strategy.ExecuteAsync(async () =>
            {
                await using var transaction = await _context.Database.BeginTransactionAsync();
                try
                {
                    var task = await _context.WorkTasks
                        .AsNoTracking()
                        .Include(t => t.TaskAssignments)
                        .Include(t => t.Project)
                        .FirstOrDefaultAsync(t => t.Id == workTaskId && !t.IsDeleted);

                    if (task == null)
                    {
                        throw new ArgumentException("Task khong ton tai.");
                    }

                    var rewardRules = ProjectManagementHelper.NormalizeRewardRules(
                        ProjectManagementHelper.ReadSection<TaskManagement.Application.DTOs.Project.ProjectRewardRulesDto>(
                            task.Project?.NavigationConfig,
                            "rewardRules"));

                    if (await HasChildTasksAsync(workTaskId))
                    {
                        await transaction.CommitAsync();
                        return;
                    }

                    var assignment = task.TaskAssignments.FirstOrDefault(ta => ta.UserId == assigneeUserId && ta.Status);
                    if (assignment == null)
                    {
                        return;
                    }

                    var wallet = await GetOrCreateWalletAsync(assigneeUserId);
                    var basePoints = CalculateBaseRewardPoints(task, rewardRules);
                    var activeAssignments = task.TaskAssignments.Where(ta => ta.Status).ToList();
                    var share = CalculateAssignmentShare(task, assignment, activeAssignments);
                    var points = Math.Max(1, (int)Math.Round(basePoints * share, MidpointRounding.AwayFromZero));

                    if (IsOverdue(task.DueDate))
                    {
                        await AddLatePenaltyIfNeededAsync(wallet, assigneeUserId, task, points, "phan viec", rewardRules);
                        await _context.SaveChangesAsync();
                        await transaction.CommitAsync();
                        return;
                    }

                    var alreadyRewarded = await _context.PointTransactions.AnyAsync(pt =>
                        pt.UserWalletUserId == assigneeUserId &&
                        pt.WorkTaskId == workTaskId &&
                        pt.TransactionType == AssignmentRewardType);

                    if (alreadyRewarded)
                    {
                        return;
                    }

                    wallet.TotalPoints += points;
                    wallet.Level = CalculateLevel(wallet.TotalPoints);
                    _context.PointTransactions.Add(new PointTransaction
                    {
                        Id = Guid.NewGuid(),
                        UserWalletUserId = assigneeUserId,
                        WorkTaskId = workTaskId,
                        Amount = points,
                        TransactionType = AssignmentRewardType,
                        Reason = $"Cong diem hoan thanh phan viec task {task.SequenceId ?? task.Id.ToString()}",
                        CreatedAt = DateTime.UtcNow
                    });

                    var earlyBonus = CalculateEarlyBonus(points, task.DueDate, rewardRules);
                    if (earlyBonus > 0)
                    {
                        wallet.TotalPoints += earlyBonus;
                        _context.PointTransactions.Add(new PointTransaction
                        {
                            Id = Guid.NewGuid(),
                            UserWalletUserId = assigneeUserId,
                            WorkTaskId = workTaskId,
                            Amount = earlyBonus,
                            TransactionType = EarlyBonusType,
                            Reason = $"Thuong hoan thanh som phan viec task {task.SequenceId ?? task.Id.ToString()}",
                            CreatedAt = DateTime.UtcNow
                        });
                    }

                    var accuracyBonus = CalculateAccuracyBonus(points, assignment.EstimatedHours, assignment.TotalActualHours, rewardRules);
                    if (accuracyBonus > 0)
                    {
                        wallet.TotalPoints += accuracyBonus;
                        _context.PointTransactions.Add(new PointTransaction
                        {
                            Id = Guid.NewGuid(),
                            UserWalletUserId = assigneeUserId,
                            WorkTaskId = workTaskId,
                            Amount = accuracyBonus,
                            TransactionType = AccuracyBonusType,
                            Reason = $"Thuong estimate sat thuc te cho phan viec task {task.SequenceId ?? task.Id.ToString()}",
                            CreatedAt = DateTime.UtcNow
                        });
                    }

                    var latePenalty = CalculateLatePenalty(points, task.DueDate, rewardRules);
                    if (latePenalty < 0)
                    {
                        wallet.TotalPoints = Math.Max(0, wallet.TotalPoints + latePenalty);
                        _context.PointTransactions.Add(new PointTransaction
                        {
                            Id = Guid.NewGuid(),
                            UserWalletUserId = assigneeUserId,
                            WorkTaskId = workTaskId,
                            Amount = latePenalty,
                            TransactionType = LatePenaltyType,
                            Reason = $"Giam diem do phan viec task {task.SequenceId ?? task.Id.ToString()} hoan thanh tre han",
                            CreatedAt = DateTime.UtcNow
                        });
                    }

                    if (task.DueDate.HasValue || accuracyBonus > 0 || latePenalty < 0)
                    {
                        wallet.Level = CalculateLevel(wallet.TotalPoints);
                    }

                    if (activeAssignments.Count > 1 && rewardRules.CollaborationBonusPoints > 0)
                    {
                        wallet.TotalPoints += rewardRules.CollaborationBonusPoints;
                        wallet.Level = CalculateLevel(wallet.TotalPoints);
                        _context.PointTransactions.Add(new PointTransaction
                        {
                            Id = Guid.NewGuid(),
                            UserWalletUserId = assigneeUserId,
                            WorkTaskId = workTaskId,
                            Amount = rewardRules.CollaborationBonusPoints,
                            TransactionType = CollaborationBonusType,
                            Reason = $"Thuong cong tac nhieu assignee cho task {task.SequenceId ?? task.Id.ToString()}",
                            CreatedAt = DateTime.UtcNow
                        });
                    }

                    await _context.SaveChangesAsync();
                    await transaction.CommitAsync();
                }
                catch
                {
                    await transaction.RollbackAsync();
                    throw;
                }
            });
        }

        public async Task ApplyDueDatePenaltyAsync(Guid workTaskId, Guid actorUserId)
        {
            var task = await _context.WorkTasks
                .Include(t => t.TaskAssignments)
                .Include(t => t.Project)
                .FirstOrDefaultAsync(t => t.Id == workTaskId && !t.IsDeleted);

            if (task == null || !IsOverdue(task.DueDate) || await HasChildTasksAsync(workTaskId))
            {
                return;
            }

            var rewardRules = ProjectManagementHelper.NormalizeRewardRules(
                ProjectManagementHelper.ReadSection<TaskManagement.Application.DTOs.Project.ProjectRewardRulesDto>(
                    task.Project?.NavigationConfig,
                    "rewardRules"));

            var activeAssignments = task.TaskAssignments.Where(assignment => assignment.Status).ToList();
            if (activeAssignments.Any())
            {
                var basePoints = CalculateBaseRewardPoints(task, rewardRules);
                foreach (var assignment in activeAssignments)
                {
                    var wallet = await GetOrCreateWalletAsync(assignment.UserId);
                    var share = CalculateAssignmentShare(task, assignment, activeAssignments);
                    var userPoints = Math.Max(1, (int)Math.Round(basePoints * share, MidpointRounding.AwayFromZero));
                    await AddLatePenaltyIfNeededAsync(wallet, assignment.UserId, task, userPoints, "phan viec", rewardRules);
                }

                await _context.SaveChangesAsync();
                return;
            }

            var targetUserId = task.AssignedUserId ?? task.ReporterId;
            var targetWallet = await GetOrCreateWalletAsync(targetUserId);
            await AddLatePenaltyIfNeededAsync(targetWallet, targetUserId, task, CalculateBaseRewardPoints(task, rewardRules), "task", rewardRules);
            await _context.SaveChangesAsync();
        }

        private async Task<UserWallet> GetOrCreateWalletAsync(Guid userId)
        {
            var wallet = await _context.UserWallets.FirstOrDefaultAsync(w => w.UserId == userId);
            if (wallet != null)
            {
                return wallet;
            }

            wallet = new UserWallet
            {
                UserId = userId,
                TotalPoints = 0,
                Level = 1
            };
            _context.UserWallets.Add(wallet);
            await _context.SaveChangesAsync();
            return wallet;
        }

        private async Task<bool> HasChildTasksAsync(Guid workTaskId)
        {
            return await _context.WorkTasks
                .AsNoTracking()
                .AnyAsync(task => task.ParentTaskId == workTaskId && !task.IsDeleted);
        }

        private async Task AddLatePenaltyIfNeededAsync(UserWallet wallet, Guid userId, TaskManagement.Domain.Entities.WorkTask task, int basePoints, string scopeLabel, TaskManagement.Application.DTOs.Project.ProjectRewardRulesDto rewardRules)
        {
            var alreadyPenalized = await _context.PointTransactions.AnyAsync(pt =>
                pt.UserWalletUserId == userId &&
                pt.WorkTaskId == task.Id &&
                pt.TransactionType == LatePenaltyType);

            if (alreadyPenalized)
            {
                return;
            }

            var latePenalty = await CalculateLatePenaltyAsync(userId, task.Id, basePoints, task.DueDate, rewardRules);
            if (latePenalty >= 0)
            {
                return;
            }

            wallet.TotalPoints = Math.Max(0, wallet.TotalPoints + latePenalty);
            wallet.Level = CalculateLevel(wallet.TotalPoints);
            _context.PointTransactions.Add(new PointTransaction
            {
                Id = Guid.NewGuid(),
                UserWalletUserId = userId,
                WorkTaskId = task.Id,
                Amount = latePenalty,
                TransactionType = LatePenaltyType,
                Reason = $"Giam diem do {scopeLabel} {task.SequenceId ?? task.Id.ToString()} da qua han",
                CreatedAt = DateTime.UtcNow
            });
        }

        private static bool IsDone(string? statusName)
        {
            if (string.IsNullOrWhiteSpace(statusName))
            {
                return false;
            }

            var normalized = NormalizeStatus(statusName);
            return normalized.Contains("DONE") ||
                   normalized.Contains("COMPLETE") ||
                   normalized.Contains("HOAN THANH") ||
                   normalized.Contains("HOÀN THÀNH");
        }

        private static string NormalizeStatus(string statusName)
        {
            var decomposed = statusName.Trim().Normalize(NormalizationForm.FormD);
            var builder = new StringBuilder(decomposed.Length);

            foreach (var character in decomposed)
            {
                if (CharUnicodeInfo.GetUnicodeCategory(character) != UnicodeCategory.NonSpacingMark)
                {
                    builder.Append(character);
                }
            }

            return builder.ToString().Normalize(NormalizationForm.FormC).ToUpperInvariant();
        }

        private static double CalculateAssignmentShare(
            TaskManagement.Domain.Entities.WorkTask task,
            TaskAssignment assignment,
            List<TaskAssignment> activeAssignments)
        {
            var totalAssignmentEstimate = activeAssignments.Sum(ta => Math.Max(0, ta.EstimatedHours));
            if (totalAssignmentEstimate > 0)
            {
                return Math.Max(0, assignment.EstimatedHours) / totalAssignmentEstimate;
            }

            var taskEstimate = Math.Max(0, task.TotalEstimatedHours);
            if (taskEstimate > 0 && assignment.EstimatedHours > 0)
            {
                return Math.Min(1, assignment.EstimatedHours / taskEstimate);
            }

            var totalWeight = activeAssignments.Sum(ta => Math.Max(ta.ContributionWeight, 0));
            var userWeight = Math.Max(assignment.ContributionWeight, 0);
            if (totalWeight > 0)
            {
                return userWeight / totalWeight;
            }

            return 1.0 / Math.Max(activeAssignments.Count, 1);
        }

        private static int CalculateBaseRewardPoints(TaskManagement.Domain.Entities.WorkTask task, TaskManagement.Application.DTOs.Project.ProjectRewardRulesDto rewardRules)
        {
            var difficulty = Math.Max(1, (int)Math.Round(task.StoryPoints <= 0 ? Math.Max(1, task.TotalEstimatedHours / 4.0) : task.StoryPoints, MidpointRounding.AwayFromZero));
            var standardDuration = CalculateStandardDurationDays(task.TotalEstimatedHours, task.PlannedStartDate, task.PlannedEndDate, task.DueDate);
            return Math.Max(1, (int)Math.Round(difficulty * standardDuration * rewardRules.BasePointMultiplier, MidpointRounding.AwayFromZero));
        }

        private static int CalculateStandardDurationDays(double estimatedHours, DateTime? plannedStartDate, DateTime? plannedEndDate, DateTime? dueDate)
        {
            var endDate = plannedEndDate ?? dueDate;
            if (plannedStartDate.HasValue && endDate.HasValue)
            {
                return Math.Max(1, (endDate.Value.Date - plannedStartDate.Value.Date).Days + 1);
            }

            if (estimatedHours > 0)
            {
                return Math.Max(1, (int)Math.Ceiling(estimatedHours / 8.0));
            }

            return 1;
        }

        private static int CalculateEarlyBonus(int basePoints, DateTime? dueDate, TaskManagement.Application.DTOs.Project.ProjectRewardRulesDto rewardRules)
        {
            if (dueDate.HasValue && DateTime.Now.Date <= dueDate.Value.Date)
            {
                return Math.Max(1, (int)Math.Round(basePoints * (rewardRules.EarlyBonusPercent / 100d), MidpointRounding.AwayFromZero));
            }

            return 0;
        }

        private static int CalculateAccuracyBonus(int basePoints, double estimatedHours, double actualHours, TaskManagement.Application.DTOs.Project.ProjectRewardRulesDto rewardRules)
        {
            if (estimatedHours <= 0 || actualHours <= 0)
            {
                return 0;
            }

            var ratio = actualHours / Math.Max(estimatedHours, 0.5);
            if (ratio >= 0.85 && ratio <= 1.15)
            {
                return Math.Max(1, (int)Math.Round(basePoints * (rewardRules.AccuracyBonusPercent / 100d), MidpointRounding.AwayFromZero));
            }

            return 0;
        }

        private static int CalculateLatePenalty(int basePoints, DateTime? dueDate, TaskManagement.Application.DTOs.Project.ProjectRewardRulesDto rewardRules)
        {
            if (IsOverdue(dueDate))
            {
                return -Math.Max(1, (int)Math.Round(basePoints * (rewardRules.LatePenaltyPercent / 100d), MidpointRounding.AwayFromZero));
            }

            return 0;
        }

        private static bool IsOverdue(DateTime? dueDate)
        {
            return dueDate.HasValue && DateTime.Now.Date > dueDate.Value.Date;
        }

        private async Task<int> CalculateLatePenaltyAsync(Guid userId, Guid workTaskId, int basePoints, DateTime? dueDate, TaskManagement.Application.DTOs.Project.ProjectRewardRulesDto rewardRules)
        {
            if (!IsOverdue(dueDate))
            {
                return 0;
            }

            var minimumPenalty = Math.Max(1, (int)Math.Round(basePoints * (rewardRules.LatePenaltyPercent / 100d), MidpointRounding.AwayFromZero));
            var existingPositiveRewards = await _context.PointTransactions
                .Where(pt =>
                    pt.UserWalletUserId == userId &&
                    pt.WorkTaskId == workTaskId &&
                    pt.Amount > 0 &&
                    pt.TransactionType != LatePenaltyType)
                .SumAsync(pt => (int?)pt.Amount) ?? 0;

            return -Math.Max(minimumPenalty, existingPositiveRewards + minimumPenalty);
        }

        private static int CalculateLevel(int totalPoints)
        {
            var level = 1;
            var nextThreshold = PointsForCareerLevel(level + 1);

            while (totalPoints >= nextThreshold)
            {
                level++;
                nextThreshold = PointsForCareerLevel(level + 1);
            }

            return level;
        }

        private static int PointsForCareerLevel(int level)
        {
            var normalizedLevel = Math.Max(1, level);
            return 250 * normalizedLevel * (normalizedLevel + 1);
        }
    }
}
