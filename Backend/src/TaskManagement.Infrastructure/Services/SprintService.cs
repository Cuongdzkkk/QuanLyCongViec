using System.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using TaskManagement.Application.Common;
using TaskManagement.Application.DTOs.Sprint;
using TaskManagement.Application.Interfaces;
using TaskManagement.Infrastructure.Data;
using TaskManagement.Domain.Entities;
using TaskManagement.Domain.Rules;

namespace TaskManagement.Infrastructure.Services
{
    public class SprintService : ISprintService
    {
        private readonly ApplicationDbContext _context;

        public SprintService(ApplicationDbContext context)
        {
            _context = context;
        }

        public async Task<List<SprintResponseDto>> GetByProjectAsync(Guid projectId)
        {
            var sprints = await _context.Sprints
                .AsNoTracking()
                .Where(s => s.ProjectId == projectId && s.Project.Status)
                .OrderBy(s => s.StartDate)
                .ThenBy(s => s.CreatedAt)
                .ThenBy(s => s.Id)
                .Select(s => new SprintResponseDto
                {
                    Id = s.Id,
                    ProjectId = s.ProjectId,
                    WorkspaceId = s.Project.WorkspaceId,
                    Name = s.Name,
                    StartDate = s.StartDate,
                    EndDate = s.EndDate,
                    Status = s.Status,
                    State = s.State == SprintStates.Planned ? "Upcoming" : s.State,
                    StartedAt = s.StartedAt,
                    CompletedAt = s.CompletedAt,
                    TaskCount = s.WorkTasks.Count(wt => !wt.IsDeleted && wt.ParentTaskId == null),
                    CompletedTaskCount = s.WorkTasks.Count(wt => !wt.IsDeleted && wt.ParentTaskId == null && (wt.TaskStatus.Name.Contains("Done") || wt.TaskStatus.Name.Contains("Complete"))),
                    InProgressTaskCount = s.WorkTasks.Count(wt => !wt.IsDeleted && wt.ParentTaskId == null && (wt.TaskStatus.Name.Contains("Progress") || wt.TaskStatus.Name.Contains("Active"))),
                    BacklogTaskCount = s.WorkTasks.Count(wt => !wt.IsDeleted && wt.ParentTaskId == null && (wt.TaskStatus.Name.Contains("Backlog") || wt.TaskStatus.Name.Contains("Todo") || wt.TaskStatus.Name.Contains("To Do"))),
                    IsFavorite = s.IsFavorite,
                    CreatedAt = s.CreatedAt
                })
                .ToListAsync();

            foreach (var sprint in sprints)
            {
                ApplySprintComputedFields(sprint);
            }

            return sprints;
        }

        public async Task<SprintResponseDto?> GetByIdAsync(Guid id)
        {
            var sprint = await _context.Sprints
                .AsNoTracking()
                .Where(s => s.Id == id && s.Project.Status)
                .Select(s => new SprintResponseDto
                {
                    Id = s.Id,
                    ProjectId = s.ProjectId,
                    WorkspaceId = s.Project.WorkspaceId,
                    Name = s.Name,
                    StartDate = s.StartDate,
                    EndDate = s.EndDate,
                    Status = s.Status,
                    State = s.State == SprintStates.Planned ? "Upcoming" : s.State,
                    StartedAt = s.StartedAt,
                    CompletedAt = s.CompletedAt,
                    TaskCount = s.WorkTasks.Count(wt => !wt.IsDeleted && wt.ParentTaskId == null),
                    CompletedTaskCount = s.WorkTasks.Count(wt => !wt.IsDeleted && wt.ParentTaskId == null && (wt.TaskStatus.Name.Contains("Done") || wt.TaskStatus.Name.Contains("Complete"))),
                    InProgressTaskCount = s.WorkTasks.Count(wt => !wt.IsDeleted && wt.ParentTaskId == null && (wt.TaskStatus.Name.Contains("Progress") || wt.TaskStatus.Name.Contains("Active"))),
                    BacklogTaskCount = s.WorkTasks.Count(wt => !wt.IsDeleted && wt.ParentTaskId == null && (wt.TaskStatus.Name.Contains("Backlog") || wt.TaskStatus.Name.Contains("Todo") || wt.TaskStatus.Name.Contains("To Do"))),
                    IsFavorite = s.IsFavorite,
                    CreatedAt = s.CreatedAt
                })
                .FirstOrDefaultAsync();

            if (sprint == null)
            {
                return null;
            }

            ApplySprintComputedFields(sprint);
            return sprint;
        }

        public async Task<SprintResponseDto> CreateAsync(Guid projectId, CreateSprintDto dto)
        {
            // Validate project tồn tại
            var projectExists = await _context.Projects.AnyAsync(p => p.Id == projectId);
            if (!projectExists)
                throw new ArgumentException("Dự án không tồn tại.");

            if (dto.EndDate <= dto.StartDate)
                throw new ArgumentException("Ngày kết thúc phải sau ngày bắt đầu.");

            var sprint = new Sprint
            {
                Id = Guid.NewGuid(),
                ProjectId = projectId,
                Name = dto.Name,
                StartDate = dto.StartDate,
                EndDate = dto.EndDate,
                Status = false,
                State = SprintStates.Planned,
                CreatedAt = DateTime.UtcNow
            };

            _context.Sprints.Add(sprint);
            await _context.SaveChangesAsync();

            var created = (await GetByIdAsync(sprint.Id))!;
            ApplySprintComputedFields(created);
            return created;
        }

        public async Task<SprintResponseDto> UpdateAsync(Guid projectId, Guid sprintId, UpdateSprintDto dto)
        {
            var sprint = await _context.Sprints
                .FirstOrDefaultAsync(s => s.Id == sprintId && s.ProjectId == projectId);

            if (sprint == null)
                throw new ArgumentException("Sprint không tồn tại trong dự án này.");

            if (SprintStatePolicy.ResolveState(sprint) != SprintStates.Planned)
                throw new ArgumentException("Chỉ có thể chỉnh sửa sprint sắp tới.");

            if (dto.EndDate <= dto.StartDate)
                throw new ArgumentException("Ngày kết thúc phải sau ngày bắt đầu.");

            sprint.Name = dto.Name;
            sprint.StartDate = dto.StartDate;
            sprint.EndDate = dto.EndDate;

            await _context.SaveChangesAsync();

            var updated = (await GetByIdAsync(sprint.Id))!;
            ApplySprintComputedFields(updated);
            return updated;
        }

        public Task<SprintResponseDto> StartAsync(Guid projectId, Guid sprintId)
        {
            if (!_context.Database.IsRelational())
            {
                return StartCoreAsync(projectId, sprintId);
            }

            return _context.Database.CreateExecutionStrategy()
                .ExecuteAsync(() => StartCoreAsync(projectId, sprintId));
        }

        private async Task<SprintResponseDto> StartCoreAsync(Guid projectId, Guid sprintId)
        {
            await using var transaction = await BeginProjectTransitionAsync(projectId);
            try
            {
                var sprint = await _context.Sprints
                    .Include(item => item.Project)
                    .SingleOrDefaultAsync(item =>
                        item.Id == sprintId &&
                        item.ProjectId == projectId &&
                        item.Project.Status);

                if (sprint == null)
                {
                    throw new KeyNotFoundException("Cycle does not exist in this active project.");
                }

                var currentState = SprintStatePolicy.ResolveState(sprint);
                if (currentState == SprintStates.Active)
                {
                    if (sprint.State != SprintStates.Active)
                    {
                        sprint.State = SprintStates.Active;
                        sprint.StartedAt ??= DateTime.UtcNow;
                        await _context.SaveChangesAsync();
                    }
                    await CommitAsync(transaction);
                    return await GetRequiredByIdAsync(sprint.Id);
                }

                if (currentState != SprintStates.Planned)
                {
                    throw new SprintTransitionException(
                        "CYCLE_ALREADY_COMPLETED",
                        "Completed cycle cannot be started again.");
                }

                if (sprint.EndDate <= sprint.StartDate || sprint.EndDate.Date < DateTime.UtcNow.Date)
                {
                    throw new SprintTransitionException(
                        "CYCLE_DATES_INVALID",
                        "Cycle dates are not valid for starting.");
                }

                var hasActiveSprint = await _context.Sprints
                    .AnyAsync(item =>
                        item.ProjectId == projectId &&
                        item.Id != sprintId &&
                        (item.State == SprintStates.Active ||
                         (item.State == SprintStates.Planned && item.Status)));
                if (hasActiveSprint)
                {
                    throw new SprintTransitionException(
                        "ACTIVE_CYCLE_EXISTS",
                        "Project already has an active sprint. Close it before starting another sprint.");
                }

                sprint.State = SprintStates.Active;
                sprint.Status = true;
                sprint.StartedAt ??= DateTime.UtcNow;
                sprint.CompletedAt = null;
                await _context.SaveChangesAsync();
                await CommitAsync(transaction);
                return await GetRequiredByIdAsync(sprint.Id);
            }
            catch (DbUpdateException ex) when (IsActiveCycleConstraintViolation(ex))
            {
                await RollbackAsync(transaction);
                throw new SprintTransitionException(
                    "ACTIVE_CYCLE_EXISTS",
                    "Project already has an active sprint. Close it before starting another sprint.");
            }
            catch
            {
                await RollbackAsync(transaction);
                throw;
            }
        }

        public Task<SprintResponseDto> CloseAsync(
            Guid projectId,
            Guid sprintId,
            CloseSprintDto dto,
            Guid actorUserId)
        {
            if (!_context.Database.IsRelational())
            {
                return CloseCoreAsync(projectId, sprintId, dto, actorUserId);
            }

            return _context.Database.CreateExecutionStrategy()
                .ExecuteAsync(() => CloseCoreAsync(projectId, sprintId, dto, actorUserId));
        }

        private async Task<SprintResponseDto> CloseCoreAsync(
            Guid projectId,
            Guid sprintId,
            CloseSprintDto dto,
            Guid actorUserId)
        {
            await using var transaction = await BeginProjectTransitionAsync(projectId);
            try
            {
                var sprint = await _context.Sprints
                    .Include(item => item.Project)
                    .SingleOrDefaultAsync(item =>
                        item.Id == sprintId &&
                        item.ProjectId == projectId &&
                        item.Project.Status);
                if (sprint == null)
                {
                    throw new KeyNotFoundException("Cycle does not exist in this active project.");
                }

                var currentState = SprintStatePolicy.ResolveState(sprint);
                if (currentState == SprintStates.Completed)
                {
                    await CommitAsync(transaction);
                    return await GetRequiredByIdAsync(sprint.Id);
                }

                if (currentState != SprintStates.Active)
                {
                    throw new SprintTransitionException(
                        "CYCLE_NOT_ACTIVE",
                        "Only an active cycle can be completed.");
                }

                if (dto.TargetSprintId == sprintId)
                {
                    throw new SprintTransitionException(
                        "INVALID_TARGET_CYCLE",
                        "Carry-over target must be a different planned cycle.");
                }

                if (dto.TargetSprintId.HasValue)
                {
                    await SprintScopeValidator.ValidateTargetSprintAsync(
                        _context,
                        projectId,
                        dto.TargetSprintId.Value);
                    var targetState = await _context.Sprints
                        .Where(item =>
                            item.Id == dto.TargetSprintId.Value &&
                            item.ProjectId == projectId)
                        .Select(item => item.State)
                        .SingleOrDefaultAsync();
                    if (targetState != SprintStates.Planned)
                    {
                        throw new SprintTransitionException(
                            "INVALID_TARGET_CYCLE",
                            "Carry-over target must be a planned cycle in the same project.");
                    }
                }

                var doneStatusIds = await _context.TaskStatuses
                    .Where(ts => ts.ProjectId == projectId &&
                        (ts.Name.Contains("Done") ||
                         ts.Name.Contains("Complete") ||
                         ts.Name.Contains("Hoàn thành")))
                    .Select(ts => ts.Id)
                    .ToListAsync();

                var unfinishedTasks = await _context.WorkTasks
                    .Where(wt =>
                        wt.ProjectId == projectId &&
                        wt.SprintId == sprintId &&
                        !doneStatusIds.Contains(wt.TaskStatusId))
                    .ToListAsync();

                await SprintScopeValidator.EnsureTasksBelongToProjectAsync(
                    _context,
                    projectId,
                    unfinishedTasks.Select(task => (task.Id, task.ProjectId, task.WorkspaceId)));

                var now = DateTime.UtcNow;
                foreach (var task in unfinishedTasks)
                {
                    var nextLocation = dto.TargetSprintId?.ToString() ?? "BACKLOG";
                    task.SprintId = dto.TargetSprintId;
                    task.UpdatedAt = now;
                    _context.AuditLogs.Add(new AuditLog
                    {
                        Id = Guid.NewGuid(),
                        WorkTaskId = task.Id,
                        UserId = actorUserId,
                        FieldChanged = "SPRINT_CARRY_OVER",
                        OldValue = sprintId.ToString(),
                        NewValue = nextLocation,
                        CreatedAt = now
                    });
                }

                sprint.State = SprintStates.Completed;
                sprint.Status = false;
                sprint.CompletedAt = now;
                await _context.SaveChangesAsync();
                await CommitAsync(transaction);
                return await GetRequiredByIdAsync(sprint.Id);
            }
            catch
            {
                await RollbackAsync(transaction);
                throw;
            }
        }

        /// <summary>
        /// 6.1 Burndown Chart Data points
        /// </summary>
        public async Task<List<BurndownDataDto>> GetBurndownChartAsync(Guid sprintId)
        {
            var sprint = await _context.Sprints.FirstOrDefaultAsync(s => s.Id == sprintId);
            if (sprint == null) throw new ArgumentException("Sprint không tồn tại.");

            var result = new List<BurndownDataDto>();
            if (sprint.EndDate <= sprint.StartDate) return result;

            // Lấy tất cả Tasks của Sprint
            var tasks = await _context.WorkTasks
                .Include(t => t.TaskStatus)
                .Where(t => t.SprintId == sprintId && !t.IsDeleted && t.ParentTaskId == null)
                .ToListAsync();

            int totalPoints = (int)tasks.Sum(t => t.StoryPoints); // Nếu bằng 0 thì cũng được, hoặc (int)Math.Max(1, t.StoryPoints)
            var hasStoryPoints = totalPoints > 0;
            if (!hasStoryPoints)
            {
                totalPoints = tasks.Count;
            }

            int totalDays = (sprint.EndDate.Date - sprint.StartDate.Date).Days;
            if (totalDays <= 0) totalDays = 1;
            double idealBurnRate = (double)totalPoints / totalDays;

            // Xây danh sách Done Tasks để mapping Remaining
            // Coi như Task.UpdatedAt chính là thời điểm Done
            var doneTasks = tasks
                .Where(t => t.TaskStatus != null &&
                            (t.TaskStatus.Name.Contains("DONE", StringComparison.OrdinalIgnoreCase) ||
                             t.TaskStatus.Name.Contains("COMPLETE", StringComparison.OrdinalIgnoreCase)))
                .ToList();

            for (int i = 0; i <= totalDays; i++)
            {
                var currentDate = sprint.StartDate.Date.AddDays(i);
                
                // Ideal points drops linearly
                int currentIdeal = (int)Math.Max(0, totalPoints - (idealBurnRate * i));

                // Tính điểm còn lại thực tế
                // Remaining = Total - Các Task đã Done TRƯỚC HOẶC TRONG ngày currentDate
                int pointsDoneBeforeCurrent = (int)doneTasks
                    .Where(t => t.UpdatedAt.Date <= currentDate)
                    .Sum(t => hasStoryPoints ? Math.Max(t.StoryPoints, 0) : 1);

                int remaining = Math.Max(0, totalPoints - pointsDoneBeforeCurrent);

                // Nếu ngày tương lai so với hiện tại, thì Remaining = Điểm ngày hôm qua (chưa Burn được thêm)
                if (currentDate > DateTime.UtcNow.Date)
                {
                    // Chỉ vẽ remaining path line đến hôm nay (các giá trị tương lai để nguyên bằng ngày hôm nay, hoặc null nhưng để int thì dùng remaining cũ)
                }

                result.Add(new BurndownDataDto
                {
                    Date = currentDate.ToString("dd/MM"),
                    IdealPoints = currentIdeal,
                    RemainingPoints = remaining
                });
            }

            return result;
        }

        private static void ApplySprintComputedFields(SprintResponseDto sprint)
        {
            sprint.ProgressPercent = sprint.TaskCount == 0
                ? 0
                : (int)Math.Round((double)sprint.CompletedTaskCount * 100 / sprint.TaskCount, MidpointRounding.AwayFromZero);
        }

        private async Task<IDbContextTransaction?> BeginProjectTransitionAsync(Guid projectId)
        {
            if (!_context.Database.IsRelational())
            {
                return null;
            }

            var transaction = await _context.Database.BeginTransactionAsync(IsolationLevel.Serializable);
            if (_context.Database.ProviderName?.Contains("SqlServer", StringComparison.OrdinalIgnoreCase) == true)
            {
                var lockResource = $"cycle-transition-project:{projectId:N}";
                await _context.Database.ExecuteSqlInterpolatedAsync(
                    $"EXEC sys.sp_getapplock @Resource={lockResource}, @LockMode='Exclusive', @LockOwner='Transaction', @LockTimeout=10000;");
            }

            return transaction;
        }

        private static async Task CommitAsync(IDbContextTransaction? transaction)
        {
            if (transaction != null)
            {
                await transaction.CommitAsync();
            }
        }

        private static async Task RollbackAsync(IDbContextTransaction? transaction)
        {
            if (transaction != null)
            {
                await transaction.RollbackAsync();
            }
        }

        private async Task<SprintResponseDto> GetRequiredByIdAsync(Guid sprintId) =>
            await GetByIdAsync(sprintId) ??
            throw new KeyNotFoundException("Cycle no longer exists.");

        private static bool IsActiveCycleConstraintViolation(DbUpdateException exception)
        {
            var message = exception.InnerException?.Message ?? exception.Message;
            return message.Contains("UX_Sprints_Project_Active", StringComparison.OrdinalIgnoreCase) ||
                message.Contains("duplicate", StringComparison.OrdinalIgnoreCase) ||
                message.Contains("unique", StringComparison.OrdinalIgnoreCase);
        }
    }
}
