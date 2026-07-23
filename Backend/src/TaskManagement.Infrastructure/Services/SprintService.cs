using Microsoft.EntityFrameworkCore;
using TaskManagement.Application.DTOs.Sprint;
using TaskManagement.Application.Interfaces;
using TaskManagement.Infrastructure.Data;
using TaskManagement.Domain.Entities;

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
            await SyncProjectSprintStatusesAsync(projectId);

            var sprints = await _context.Sprints
                .Where(s => s.ProjectId == projectId)
                .Select(s => new SprintResponseDto
                {
                    Id = s.Id,
                    ProjectId = s.ProjectId,
                    Name = s.Name,
                    StartDate = s.StartDate,
                    EndDate = s.EndDate,
                    Status = s.Status,
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
            var sprintProjectId = await _context.Sprints
                .Where(s => s.Id == id)
                .Select(s => (Guid?)s.ProjectId)
                .FirstOrDefaultAsync();

            if (sprintProjectId.HasValue)
            {
                await SyncProjectSprintStatusesAsync(sprintProjectId.Value);
            }

            var sprint = await _context.Sprints
                .Where(s => s.Id == id)
                .Select(s => new SprintResponseDto
                {
                    Id = s.Id,
                    ProjectId = s.ProjectId,
                    Name = s.Name,
                    StartDate = s.StartDate,
                    EndDate = s.EndDate,
                    Status = s.Status,
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
                Status = false, // Mới tạo chưa Active, phải gọi Start
                CreatedAt = DateTime.UtcNow
            };

            _context.Sprints.Add(sprint);
            await _context.SaveChangesAsync();
            await SyncProjectSprintStatusesAsync(projectId);

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

            if (dto.EndDate <= dto.StartDate)
                throw new ArgumentException("Ngày kết thúc phải sau ngày bắt đầu.");

            sprint.Name = dto.Name;
            sprint.StartDate = dto.StartDate;
            sprint.EndDate = dto.EndDate;

            await _context.SaveChangesAsync();
            await SyncProjectSprintStatusesAsync(projectId);

            var updated = (await GetByIdAsync(sprint.Id))!;
            ApplySprintComputedFields(updated);
            return updated;
        }

        /// <summary>
        /// 5.3 Sprint Overlap Guard: Chặn 2 Sprint cùng Active trong 1 Project
        /// </summary>
        public async Task<SprintResponseDto> StartAsync(Guid projectId, Guid sprintId)
        {
            var sprint = await _context.Sprints
                .FirstOrDefaultAsync(s => s.Id == sprintId && s.ProjectId == projectId);

            if (sprint == null)
                throw new ArgumentException("Sprint không tồn tại trong dự án này.");

            if (sprint.Status)
                throw new InvalidOperationException("Sprint này đã đang chạy.");

            // === SPRINT OVERLAP GUARD ===
            bool hasActiveSprint = await _context.Sprints
                .AnyAsync(s => s.ProjectId == projectId && s.Status == true);

            if (hasActiveSprint)
                throw new InvalidOperationException("Dự án đang có Sprint đang chạy! Phải đóng Sprint hiện tại trước khi bắt đầu Sprint mới.");

            sprint.Status = true;
            await _context.SaveChangesAsync();
            await SyncProjectSprintStatusesAsync(projectId);

            var started = (await GetByIdAsync(sprint.Id))!;
            ApplySprintComputedFields(started);
            return started;
        }

        /// <summary>
        /// 5.5 Close Sprint + Roll-over Tasks (bọc trong Transaction)
        /// </summary>
        public async Task CloseAsync(Guid sprintId, CloseSprintDto dto, Guid actorUserId)
        {
            var sprint = await _context.Sprints
                .FirstOrDefaultAsync(s => s.Id == sprintId);

            if (sprint == null)
                throw new ArgumentException("Sprint không tồn tại.");

            if (!sprint.Status)
                throw new InvalidOperationException("Sprint này đã đóng.");

            // Validate target sprint nếu có
            if (dto.TargetSprintId.HasValue)
            {
                await SprintScopeValidator.ValidateTargetSprintAsync(
                    _context,
                    sprint.ProjectId,
                    dto.TargetSprintId.Value);

                var targetSprint = await _context.Sprints
                    .FirstOrDefaultAsync(s => s.Id == dto.TargetSprintId.Value && s.ProjectId == sprint.ProjectId);
                if (targetSprint == null)
                    throw new ArgumentException("Sprint đích không tồn tại trong cùng dự án.");
            }

            // === BỌC TRONG TRANSACTION (theo CONTEXT.md) ===
            await using var transaction = await _context.Database.BeginTransactionAsync();
            try
            {
                // Tìm tất cả TaskStatus có Name chứa "Done" hoặc "Hoàn thành" trong project
                var doneStatusIds = await _context.TaskStatuses
                    .Where(ts => ts.ProjectId == sprint.ProjectId &&
                                 (ts.Name.Contains("Done") || ts.Name.Contains("Hoàn thành")))
                    .Select(ts => ts.Id)
                    .ToListAsync();

                // Chuyển các task chưa Done sang sprint mới hoặc backlog (null)
                var unfinishedTasks = await _context.WorkTasks
                    .Where(wt => wt.SprintId == sprintId && !doneStatusIds.Contains(wt.TaskStatusId))
                    .ToListAsync();

                await SprintScopeValidator.EnsureTasksBelongToProjectAsync(
                    _context,
                    sprint.ProjectId,
                    unfinishedTasks.Select(task => (task.Id, task.ProjectId, task.WorkspaceId)));

                foreach (var task in unfinishedTasks)
                {
                    var nextLocation = dto.TargetSprintId?.ToString() ?? "BACKLOG";
                    task.SprintId = dto.TargetSprintId; // null = backlog
                    task.UpdatedAt = DateTime.UtcNow;
                    _context.AuditLogs.Add(new AuditLog
                    {
                        Id = Guid.NewGuid(),
                        WorkTaskId = task.Id,
                        UserId = actorUserId,
                        FieldChanged = "SPRINT_CARRY_OVER",
                        OldValue = sprintId.ToString(),
                        NewValue = nextLocation,
                        CreatedAt = DateTime.UtcNow
                    });
                }

                // Đóng sprint
                sprint.Status = false;
                var today = DateTime.UtcNow.Date;
                if (sprint.EndDate.Date >= today)
                {
                    sprint.EndDate = today.AddDays(-1);
                }

                await _context.SaveChangesAsync();
                await SyncProjectSprintStatusesAsync(sprint.ProjectId);
                await transaction.CommitAsync();
            }
            catch
            {
                await transaction.RollbackAsync();
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
            var today = DateTime.UtcNow.Date;
            var start = sprint.StartDate.Date;
            var end = sprint.EndDate.Date;

            sprint.State = end < today
                ? "Completed"
                : sprint.Status && start <= today && end >= today
                    ? "Active"
                    : "Upcoming";

            sprint.ProgressPercent = sprint.TaskCount == 0
                ? 0
                : (int)Math.Round((double)sprint.CompletedTaskCount * 100 / sprint.TaskCount, MidpointRounding.AwayFromZero);
        }

        private async Task SyncProjectSprintStatusesAsync(Guid projectId)
        {
            var today = DateTime.UtcNow.Date;
            var sprints = await _context.Sprints
                .Where(s => s.ProjectId == projectId)
                .OrderByDescending(s => s.StartDate)
                .ThenByDescending(s => s.CreatedAt)
                .ToListAsync();

            var activeSprint = sprints
                .FirstOrDefault(s => s.StartDate.Date <= today && s.EndDate.Date >= today);

            var hasChanges = false;
            foreach (var sprint in sprints)
            {
                var shouldBeActive = activeSprint != null && sprint.Id == activeSprint.Id;
                if (sprint.Status != shouldBeActive)
                {
                    sprint.Status = shouldBeActive;
                    hasChanges = true;
                }
            }

            if (hasChanges)
            {
                await _context.SaveChangesAsync();
            }
        }
    }
}
