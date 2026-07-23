using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using TaskManagement.Application.Common;
using TaskManagement.Application.DTOs.Project;
using TaskManagement.Application.DTOs.WorkTask;
using TaskManagement.Application.Interfaces;
using TaskManagement.Domain.Rules;
using TaskManagement.Infrastructure.Data;

namespace TaskManagement.Infrastructure.Services
{
    public class WorkTaskService : IWorkTaskService
    {
        private readonly ApplicationDbContext _context;
        private readonly IGamificationService _gamificationService;
        private static readonly string[] VisibilityOverrideRoles = { "PM", "PO", "Project Lead", "PROJECT_MANAGER", "PROJECT_LEAD", "Admin" };
        private const string TaskVisibilitySettingGroup = "TaskVisibility";

        public WorkTaskService(ApplicationDbContext context, IGamificationService gamificationService)
        {
            _context = context;
            _gamificationService = gamificationService;
        }

        public async Task<List<WorkTaskResponseDto>> GetByProjectAsync(Guid projectId, Guid userId)
        {
            var membership = await _context.ProjectMembers
                .AsNoTracking()
                .FirstOrDefaultAsync(pm => pm.ProjectId == projectId && pm.UserId == userId && pm.Status);

            if (membership == null)
            {
                throw new UnauthorizedAccessException("Ban khong phai thanh vien active cua du an nay.");
            }

            var executionRules = await LoadExecutionRulesAsync(projectId);
            var normalizedProjectRole = ProjectExecutionRuleHelper.NormalizeProjectRole(membership?.ProjectRole);
            var canSeeAllTasks = executionRules.ManagerAlwaysSeeAllTasks && IsVisibilityOverrideRole(normalizedProjectRole);

            var query = _context.WorkTasks
                .Include(wt => wt.TaskStatus)
                .Include(wt => wt.TaskType)
                .Include(wt => wt.Reporter)
                .Include(wt => wt.AssignedUser)
                .Include(wt => wt.IssueModules)
                .Include(wt => wt.IssueLabels)
                .Include(wt => wt.TaskAssignments)
                    .ThenInclude(ta => ta.User)
                .Where(wt => wt.ProjectId == projectId && !wt.IsDeleted && !wt.IsArchived);

            if (!canSeeAllTasks)
            {
                var accessRecords = await _context.WorkTasks
                    .AsNoTracking()
                    .Where(wt => wt.ProjectId == projectId && !wt.IsDeleted && !wt.IsArchived)
                    .Select(wt => new TaskVisibilityAccessRecord
                    {
                        Id = wt.Id,
                        ReporterId = wt.ReporterId,
                        AssignedUserId = wt.AssignedUserId,
                        AssigneeIds = wt.TaskAssignments
                            .Where(ta => ta.Status)
                            .Select(ta => ta.UserId)
                            .ToList()
                    })
                    .ToListAsync();

                var accessVisibilityMap = await LoadTaskVisibilityMapAsync(accessRecords.Select(item => item.Id));
                var allowedTaskIds = accessRecords
                    .Where(record => CanUserViewTask(
                        record,
                        accessVisibilityMap.TryGetValue(record.Id, out var visibility)
                            ? visibility
                            : new TaskVisibilityDto(),
                        normalizedProjectRole,
                        userId))
                    .Select(record => record.Id)
                    .ToHashSet();

                query = query.Where(wt => allowedTaskIds.Contains(wt.Id));
            }

            var dtos = await query
                .AsNoTracking()
                .OrderByDescending(wt => wt.CreatedAt)
                .Select(wt => new WorkTaskResponseDto
                {
                    Id = wt.Id,
                    Title = wt.Title,
                    Description = wt.Description, // Use projection instead of full loading
                    ProjectId = wt.ProjectId,
                    SprintId = wt.SprintId,
                    TaskTypeId = wt.TaskTypeId,
                    TypeName = wt.TaskType.Name,
                    TaskStatusId = wt.TaskStatusId,
                    StatusName = wt.TaskStatus.Name, // We can normalize this on Frontend/Client if needed, or keeping simple here
                    ReporterId = wt.ReporterId,
                    ReporterName = wt.Reporter.FullName ?? wt.Reporter.Email,
                    AssignedUserId = wt.AssignedUserId,
                    AssigneeName = wt.AssignedUser != null ? wt.AssignedUser.FullName : null,
                    Assignees = wt.TaskAssignments
                        .Where(ta => ta.Status)
                        .Select(ta => new TaskAssigneeDto
                        {
                            UserId = ta.UserId,
                            FullName = ta.User.FullName,
                            Email = ta.User.Email,
                            ProgressPercent = ta.ProgressPercent,
                            ContributionWeight = ta.ContributionWeight,
                            EstimatedHours = ta.EstimatedHours,
                            TotalActualHours = ta.TotalActualHours,
                            IsBlocked = ta.BlockedByUserId.HasValue,
                            BlockedByUserId = ta.BlockedByUserId,
                            BlockReason = ta.BlockReason
                        })
                        .ToList(),
                    ModuleId = wt.IssueModules
                        .OrderBy(im => im.AssignedAt)
                        .Select(im => (Guid?)im.ModuleId)
                        .FirstOrDefault(),
                    LabelIds = wt.IssueLabels
                        .Select(il => il.LabelId)
                        .ToList(),
                    Priority = wt.Priority,
                    StoryPoints = wt.StoryPoints,
                    DueDate = wt.DueDate,
                    PlannedStartDate = wt.PlannedStartDate,
                    PlannedEndDate = wt.PlannedEndDate,
                    TotalEstimatedHours = wt.TotalEstimatedHours,
                    TotalActualHours = wt.TotalActualHours,
                    ParentTaskId = wt.ParentTaskId,
                    CreatedAt = wt.CreatedAt,
                    UpdatedAt = wt.UpdatedAt,
                    RowVersion = wt.RowVersion,
                    SortOrder = wt.SortOrder,
                    SequenceId = wt.SequenceId,
                    IsSubscribed = wt.Subscribers.Any(s => s.UserId == userId)
                })
                .ToListAsync();

            var visibilityMap = await LoadTaskVisibilityMapAsync(dtos.Select(item => item.Id));
            foreach (var dto in dtos)
            {
                var visibility = visibilityMap.TryGetValue(dto.Id, out var config)
                    ? config
                    : new TaskVisibilityDto();
                dto.VisibilityMode = visibility.Mode;
                dto.VisibleToRoles = visibility.Roles;
            }

            // Optional normalization of status name (since it's a DTO logic)
            foreach (var d in dtos) d.StatusName = NormalizeStatusName(d.StatusName);

            // Sort by SortOrder for Kanban drag-drop compatibility
            dtos = dtos.OrderBy(d => d.SortOrder).ToList();

            return dtos;
        }

        public async Task<WorkTaskResponseDto> CreateAsync(Guid reporterId, CreateWorkTaskDto request)
        {
            if (reporterId == Guid.Empty)
            {
                throw new UnauthorizedAccessException("Authenticated reporter context is required.");
            }

            var isActiveReporter = await _context.Users
                .AsNoTracking()
                .AnyAsync(user => user.Id == reporterId && user.IsActive && !user.IsDeleted);
            if (!isActiveReporter)
            {
                throw new UnauthorizedAccessException("Authenticated reporter is not active.");
            }

            var hasActiveResourceMembership = await _context.ProjectMembers
                .AsNoTracking()
                .AnyAsync(member => member.ProjectId == request.ProjectId &&
                                    member.UserId == reporterId &&
                                    member.Status &&
                                    member.LeftAt == null &&
                                    member.Project.Status &&
                                    !member.Project.IsDeleted &&
                                    !member.Project.Workspace.IsDeleted &&
                                    member.Project.Workspace.Members.Any(workspaceMember =>
                                        workspaceMember.UserId == reporterId && workspaceMember.IsActive));
            if (!hasActiveResourceMembership)
            {
                throw new UnauthorizedAccessException("Reporter must be an active member of the project and workspace.");
            }

            // 1. Kiểm tra user là member của project
            if (reporterId != Guid.Empty)
            {
                var isMember = await _context.ProjectMembers
                    .AnyAsync(pm => pm.ProjectId == request.ProjectId && pm.UserId == reporterId && pm.Status);
                if (!isMember)
                {
                    throw new UnauthorizedAccessException("Bạn không phải thành viên của dự án này. Không thể tạo công việc.");
                }
            }

            // 2. Resolve TaskStatus
            var taskStatus = await ResolveTaskStatusAsync(request.StatusName, request.ProjectId);

            // 3. Resolve TaskType
            var resolvedTaskTypeId = await ResolveTaskTypeIdAsync(request.TaskTypeId, request.TypeName, request.ProjectId);

            // 4. Fallback reporter
            // 5. Generate SortOrder (LexoRank-style: max current + 65536)
            double maxSort = 0;
            var existingMax = await _context.WorkTasks
                .Where(wt => wt.ProjectId == request.ProjectId && !wt.IsDeleted)
                .MaxAsync(wt => (double?)wt.SortOrder);
            maxSort = (existingMax ?? 0) + 65536;

            // 6. Generate SequenceId (e.g., CUN-42)
            var project = await _context.Projects.FirstAsync(p => p.Id == request.ProjectId);
            var executionRules = LoadExecutionRules(project.NavigationConfig);
            project.IssueSequence += 1;
            string sequenceId = $"{project.Identifier}-{project.IssueSequence}";

            var requestedAssigneeIds = (request.AssigneeIds ?? new List<Guid>()).Distinct().ToList();
            if (!requestedAssigneeIds.Any() && request.AssignedUserId.HasValue)
            {
                requestedAssigneeIds.Add(request.AssignedUserId.Value);
            }

            if (requestedAssigneeIds.Any())
            {
                var validAssigneeCount = await _context.ProjectMembers
                    .CountAsync(pm => pm.ProjectId == request.ProjectId && pm.Status && requestedAssigneeIds.Contains(pm.UserId));
                if (validAssigneeCount != requestedAssigneeIds.Count)
                {
                    throw new InvalidOperationException("Assignee khong thuoc du an nay.");
                }
            }

            if (request.SprintId.HasValue)
            {
                var sprint = await _context.Sprints
                    .FirstOrDefaultAsync(s => s.Id == request.SprintId.Value && s.ProjectId == request.ProjectId);
                if (sprint == null)
                {
                    throw new InvalidOperationException("Cycle khong thuoc du an nay.");
                }
                if (SprintStatePolicy.IsTaskMutationLocked(sprint, DateTime.UtcNow))
                {
                    throw new InvalidOperationException("Khong the them Task vao mot Sprint da dong.");
                }
            }

            if (request.ModuleId.HasValue)
            {
                var moduleExists = await _context.Modules
                    .AnyAsync(m => m.Id == request.ModuleId.Value && m.ProjectId == request.ProjectId);
                if (!moduleExists)
                {
                    throw new InvalidOperationException("Module khong thuoc du an nay.");
                }
            }

            if (request.ParentTaskId.HasValue)
            {
                var parentExists = await _context.WorkTasks
                    .AnyAsync(wt => wt.Id == request.ParentTaskId.Value && wt.ProjectId == request.ProjectId && !wt.IsDeleted);
                if (!parentExists)
                {
                    throw new InvalidOperationException("Parent task khong thuoc du an nay.");
                }
            }

            var requestedLabelIds = (request.LabelIds ?? new List<Guid>()).Distinct().ToList();
            if (requestedLabelIds.Any())
            {
                var validLabelCount = await _context.Labels
                    .CountAsync(label => requestedLabelIds.Contains(label.Id)
                        && (label.ProjectId == request.ProjectId || (label.ProjectId == null && label.WorkspaceId == project.WorkspaceId)));
                if (validLabelCount != requestedLabelIds.Count)
                {
                    throw new InvalidOperationException("Label khong thuoc du an nay.");
                }
            }

            var normalizedVisibility = await ResolveTaskVisibilityAsync(
                request.VisibilityMode,
                request.VisibleToRoles,
                request.ProjectId,
                reporterId,
                executionRules);

            if (request.TotalEstimatedHours <= 0)
            {
                var primaryRole = await ResolvePrimaryProjectRoleAsync(request.ProjectId, requestedAssigneeIds, reporterId);
                request.TotalEstimatedHours = ProjectExecutionRuleHelper.CalculateSuggestedEstimateHours(
                    executionRules,
                    primaryRole,
                    request.StoryPoints,
                    request.Priority,
                    requestedAssigneeIds.Count,
                    0,
                    request.Title);
            }

            // 7. Create entity
            var workTask = new TaskManagement.Domain.Entities.WorkTask
            {
                Id = Guid.NewGuid(),
                ProjectId = request.ProjectId,
                WorkspaceId = project.WorkspaceId,
                SprintId = request.SprintId,
                ParentTaskId = request.ParentTaskId,
                TaskTypeId = resolvedTaskTypeId,
                TaskStatusId = taskStatus.Id,
                Title = request.Title,
                Description = request.Description,
                Priority = request.Priority,
                StoryPoints = request.StoryPoints,
                PlannedStartDate = request.PlannedStartDate,
                PlannedEndDate = request.PlannedEndDate,
                ReporterId = reporterId,
                AssignedUserId = request.AssignedUserId,
                DueDate = request.DueDate,
                TotalEstimatedHours = request.TotalEstimatedHours,
                SortOrder = maxSort,
                SequenceId = sequenceId,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };

            _context.WorkTasks.Add(workTask);
            await _context.SaveChangesAsync();

            if (requestedAssigneeIds.Any())
            {
                workTask.AssignedUserId = requestedAssigneeIds[0];
                foreach (var assigneeId in requestedAssigneeIds)
                {
                    _context.TaskAssignments.Add(new Domain.Entities.TaskAssignment
                    {
                        WorkTaskId = workTask.Id,
                        UserId = assigneeId,
                        Status = true
                    });
                }
            }

            if (request.ModuleId.HasValue)
            {
                _context.IssueModules.Add(new Domain.Entities.IssueModule
                {
                    WorkTaskId = workTask.Id,
                    ModuleId = request.ModuleId.Value,
                    AssignedAt = DateTime.UtcNow
                });
            }

            foreach (var labelId in requestedLabelIds)
            {
                _context.IssueLabels.Add(new Domain.Entities.IssueLabel
                {
                    WorkTaskId = workTask.Id,
                    LabelId = labelId,
                    AssignedAt = DateTime.UtcNow
                });
            }

            if (requestedAssigneeIds.Any() || request.ModuleId.HasValue || requestedLabelIds.Any())
            {
                await _context.SaveChangesAsync();
            }

            await SaveTaskVisibilityAsync(workTask.Id, normalizedVisibility);

            // Reload with navigation properties
            var created = await _context.WorkTasks
                .Include(wt => wt.TaskStatus)
                .Include(wt => wt.TaskType)
                .Include(wt => wt.Reporter)
                .Include(wt => wt.AssignedUser)
                .Include(wt => wt.IssueModules)
                .Include(wt => wt.IssueLabels)
                .Include(wt => wt.TaskAssignments)
                    .ThenInclude(ta => ta.User)
                .FirstAsync(wt => wt.Id == workTask.Id);

            return await MapToDtoAsync(created);
        }

        public async Task<WorkTaskResponseDto> UpdateAsync(Guid taskId, Guid userId, UpdateWorkTaskDto dto)
        {
            if (dto.RowVersion == null || dto.RowVersion.Length == 0)
                throw new ArgumentException("RowVersion is required for task updates.");

            var taskToUpdate = await _context.WorkTasks
                .Include(wt => wt.TaskStatus)
                .Include(wt => wt.Sprint)
                .Include(wt => wt.TaskType)
                .Include(wt => wt.Reporter)
                .Include(wt => wt.AssignedUser)
                .Include(wt => wt.TaskAssignments)
                .FirstOrDefaultAsync(wt => wt.Id == taskId && !wt.IsDeleted);

            if (taskToUpdate == null) throw new ArgumentException("Tác vụ không tồn tại.");

            // 1. RBAC Check
            var membership = await _context.ProjectMembers
                .FirstOrDefaultAsync(pm => pm.ProjectId == taskToUpdate.ProjectId && pm.UserId == userId && pm.Status);

            if (membership == null)
                throw new UnauthorizedAccessException("Bạn không phải thành viên của dự án này.");

            bool isManager = IsManagerRole(ProjectExecutionRuleHelper.NormalizeProjectRole(membership.ProjectRole));
            if (!isManager && taskToUpdate.ReporterId != userId && taskToUpdate.AssignedUserId != userId && !taskToUpdate.TaskAssignments.Any(ta => ta.UserId == userId))
            {
                 throw new UnauthorizedAccessException("Bạn không có quyền sửa đổi tác vụ này.");
            }

            if (dto.AssignedUserId.HasValue)
            {
                var assigneeIsMember = await _context.ProjectMembers
                    .AnyAsync(pm => pm.ProjectId == taskToUpdate.ProjectId && pm.UserId == dto.AssignedUserId.Value && pm.Status);
                if (!assigneeIsMember)
                {
                    throw new InvalidOperationException("Assignee khong thuoc du an nay.");
                }
            }

            if (dto.SprintId.HasValue)
            {
                var targetSprint = await _context.Sprints
                    .FirstOrDefaultAsync(s => s.Id == dto.SprintId.Value && s.ProjectId == taskToUpdate.ProjectId);
                if (targetSprint == null)
                {
                    throw new InvalidOperationException("Cycle khong thuoc du an nay.");
                }
                if (SprintStatePolicy.IsTaskMutationLocked(targetSprint, DateTime.UtcNow))
                {
                    throw new InvalidOperationException("Khong the chuyen Task vao mot Sprint da dong.");
                }
            }

            if (dto.TaskTypeId != Guid.Empty)
            {
                var taskTypeExists = await _context.TaskTypes
                    .AnyAsync(tt => tt.Id == dto.TaskTypeId && tt.ProjectId == taskToUpdate.ProjectId);
                if (!taskTypeExists)
                {
                    throw new InvalidOperationException("Task type khong thuoc du an nay.");
                }
            }

            // 2. Sprint Lock
            if (SprintStatePolicy.IsTaskMutationLocked(taskToUpdate.Sprint, DateTime.UtcNow))
            {
                throw new InvalidOperationException("Không thể chỉnh sửa Task của một Sprint đã đóng.");
            }

            // Update fields
            taskToUpdate.Title = dto.Title;
            taskToUpdate.Description = dto.Description;
            taskToUpdate.Priority = dto.Priority ?? taskToUpdate.Priority;
            taskToUpdate.StoryPoints = dto.StoryPoints ?? taskToUpdate.StoryPoints;
            taskToUpdate.AssignedUserId = dto.AssignedUserId;
            taskToUpdate.PlannedStartDate = dto.PlannedStartDate;
            taskToUpdate.PlannedEndDate = dto.PlannedEndDate;
            taskToUpdate.DueDate = dto.DueDate;
            taskToUpdate.SprintId = dto.SprintId;
            taskToUpdate.TaskTypeId = dto.TaskTypeId != Guid.Empty ? dto.TaskTypeId : taskToUpdate.TaskTypeId;
            if (dto.TotalEstimatedHours.HasValue)
            {
                taskToUpdate.TotalEstimatedHours = Math.Max(0, dto.TotalEstimatedHours.Value);
            }
            
            taskToUpdate.UpdatedAt = DateTime.UtcNow;


            _context.Entry(taskToUpdate).Property(nameof(taskToUpdate.RowVersion)).OriginalValue = dto.RowVersion;

            await _context.SaveChangesAsync();
            if (dto.VisibilityMode != null || dto.VisibleToRoles != null)
            {
                var executionRules = await LoadExecutionRulesAsync(taskToUpdate.ProjectId);
                var visibility = await ResolveTaskVisibilityAsync(
                    dto.VisibilityMode,
                    dto.VisibleToRoles,
                    taskToUpdate.ProjectId,
                    userId,
                    executionRules);
                await SaveTaskVisibilityAsync(taskToUpdate.Id, visibility);
            }

            return await MapToDtoAsync(taskToUpdate);
        }

        public async Task UpdateTaskStatusAsync(Guid taskId, Guid userId, UpdateTaskStatusRequestDto request)
        {
            if (request.RowVersion == null || request.RowVersion.Length == 0)
                throw new ArgumentException("RowVersion is required for task status updates.");

            var taskToUpdate = await _context.WorkTasks
                .Include(wt => wt.TaskStatus)
                .Include(wt => wt.Sprint)
                .Include(wt => wt.TaskAssignments)
                .FirstOrDefaultAsync(wt => wt.Id == taskId && !wt.IsDeleted);

            if (taskToUpdate == null) throw new ArgumentException("Tác vụ không tồn tại.");

            var membership = await _context.ProjectMembers
                .FirstOrDefaultAsync(pm => pm.ProjectId == taskToUpdate.ProjectId && pm.UserId == userId && pm.Status);
            if (membership == null)
                throw new UnauthorizedAccessException("Ban khong phai thanh vien cua du an nay.");

            var canUpdateTask = IsManagerRole(ProjectExecutionRuleHelper.NormalizeProjectRole(membership.ProjectRole))
                || taskToUpdate.ReporterId == userId
                || taskToUpdate.AssignedUserId == userId
                || taskToUpdate.TaskAssignments.Any(ta => ta.UserId == userId && ta.Status);
            if (!canUpdateTask)
                throw new UnauthorizedAccessException("Ban khong co quyen sua tac vu nay.");

            // Sprint Lock
            if (SprintStatePolicy.IsTaskMutationLocked(taskToUpdate.Sprint, DateTime.UtcNow))
                throw new InvalidOperationException("Không thể chỉnh sửa Task của một Sprint đã đóng.");

            var oldStatus = taskToUpdate.TaskStatus;
            var oldStatusName = oldStatus.Name;

            // Resolve new status
            TaskManagement.Domain.Entities.TaskStatus? newStatus = null;
            if (request.TaskStatusId != Guid.Empty)
            {
                newStatus = await _context.TaskStatuses.FirstOrDefaultAsync(ts => ts.Id == request.TaskStatusId && ts.ProjectId == taskToUpdate.ProjectId);
            }
            else if (!string.IsNullOrEmpty(request.StatusName))
            {
                newStatus = await ResolveTaskStatusAsync(request.StatusName, taskToUpdate.ProjectId);
            }

            if (newStatus == null) throw new ArgumentException("Trạng thái chuyển đến không tồn tại.");
            if (oldStatus.Id == newStatus.Id) return;

            // Allow all status transitions — guardrails are dependency/subtask checks only.

            bool isMovingToActiveOrDone = newStatus.Name.Contains("In Progress", StringComparison.OrdinalIgnoreCase) ||
                                          newStatus.Name.Contains("Done", StringComparison.OrdinalIgnoreCase);

            // Task Dependencies check
            if (isMovingToActiveOrDone)
            {
                var blockers = await _context.TaskDependencies
                    .Include(td => td.PredecessorTask)
                    .ThenInclude(pt => pt.TaskStatus)
                    .Where(td => td.SuccessorTaskId == taskId && td.DependencyType == 1)
                    .Select(td => td.PredecessorTask)
                    .ToListAsync();

                foreach (var blocker in blockers)
                {
                    bool isBlockerDone = blocker.TaskStatus.Name.Contains("Done", StringComparison.OrdinalIgnoreCase);
                    if (!isBlockerDone)
                        throw new InvalidOperationException($"Tác vụ phụ thuộc '{blocker.Title}' chưa hoàn thành.");
                }
            }

            // Parent-Subtask Constraint
            bool isMovingToDone = newStatus.Name.Contains("Done", StringComparison.OrdinalIgnoreCase);
            if (isMovingToDone)
            {
                var subtaskStatuses = await _context.WorkTasks
                    .Include(wt => wt.TaskStatus)
                    .Where(wt => wt.ParentTaskId == taskId && !wt.IsDeleted)
                    .Select(wt => wt.TaskStatus.Name)
                    .ToListAsync();

                bool hasUnfinishedSubtasks = subtaskStatuses
                    .Any(statusName => !statusName.Contains("Done", StringComparison.OrdinalIgnoreCase));
                if (hasUnfinishedSubtasks)
                    throw new InvalidOperationException("Không thể hoàn thành tác vụ cha khi vẫn còn subtask chưa hoàn thành.");
            }

            taskToUpdate.TaskStatusId = newStatus.Id;
            taskToUpdate.UpdatedAt = DateTime.UtcNow;

            _context.Entry(taskToUpdate).Property(nameof(taskToUpdate.RowVersion)).OriginalValue = request.RowVersion;

            try
            {
                await _context.SaveChangesAsync();
                await _gamificationService.ApplyStatusChangeRewardsAsync(taskId, userId, oldStatusName, newStatus.Name);
            }
            catch (DbUpdateConcurrencyException)
            {
                throw;
            }
        }

        public async Task<IEnumerable<WorkTaskResponseDto>> GetTasksByProjectIdAsync(Guid projectId)
        {
            var tasks = await _context.WorkTasks
                .AsNoTracking()
                .Include(wt => wt.TaskStatus)
                .Include(wt => wt.TaskType)
                .Include(wt => wt.Reporter)
                .Include(wt => wt.AssignedUser)
                .Include(wt => wt.Project)
                .Where(wt => wt.ProjectId == projectId && !wt.IsDeleted && !wt.IsArchived)
                .OrderByDescending(wt => wt.CreatedAt)
                .Select(wt => new WorkTaskResponseDto
                {
                    Id = wt.Id,
                    ProjectId = wt.ProjectId,
                    Title = wt.Title,
                    Description = wt.Description,
                    Priority = wt.Priority,
                    StoryPoints = wt.StoryPoints,
                    StatusName = wt.TaskStatus.Name,
                    TaskStatusId = wt.TaskStatusId,
                    TaskTypeName = wt.TaskType.Name,
                    AssigneeName = wt.AssignedUser != null ? wt.AssignedUser.FullName : null,
                    AssignedUserId = wt.AssignedUserId,
                    ReporterName = wt.Reporter.FullName,
                    ReporterId = wt.ReporterId,
                    PlannedStartDate = wt.PlannedStartDate,
                    PlannedEndDate = wt.PlannedEndDate,
                    DueDate = wt.DueDate,
                    TotalEstimatedHours = wt.TotalEstimatedHours,
                    TotalActualHours = wt.TotalActualHours,
                    ParentTaskId = wt.ParentTaskId,
                    RowVersion = wt.RowVersion,
                    CreatedAt = wt.CreatedAt,
                    UpdatedAt = wt.UpdatedAt,
                    ProjectName = wt.Project.Name,
                    SortOrder = wt.SortOrder,
                    SequenceId = wt.SequenceId
                })
                .ToListAsync();

            return tasks;
        }

        public async Task<IEnumerable<WorkTaskResponseDto>> GetMyTasksAsync(Guid userId)
        {
            var tasks = await _context.WorkTasks
                .AsNoTracking()
                .Include(wt => wt.TaskStatus)
                .Include(wt => wt.TaskType)
                .Include(wt => wt.Reporter)
                .Include(wt => wt.AssignedUser)
                .Include(wt => wt.TaskAssignments)
                    .ThenInclude(ta => ta.User)
                .Include(wt => wt.Project)
                .Where(wt => (wt.AssignedUserId == userId || wt.TaskAssignments.Any(ta => ta.UserId == userId && ta.Status) || wt.Subscribers.Any(s => s.UserId == userId)) && !wt.IsDeleted && !wt.IsArchived)
                .OrderByDescending(wt => wt.UpdatedAt)
                .Select(wt => new WorkTaskResponseDto
                {
                    Id = wt.Id,
                    ProjectId = wt.ProjectId,
                    Title = wt.Title,
                    Description = wt.Description,
                    Priority = wt.Priority,
                    StoryPoints = wt.StoryPoints,
                    StatusName = wt.TaskStatus.Name,
                    TaskStatusId = wt.TaskStatusId,
                    TaskTypeName = wt.TaskType.Name,
                    AssigneeName = wt.AssignedUser != null ? wt.AssignedUser.FullName : null,
                    AssignedUserId = wt.AssignedUserId,
                    Assignees = wt.TaskAssignments
                        .Where(ta => ta.Status)
                        .Select(ta => new TaskAssigneeDto
                        {
                            UserId = ta.UserId,
                            FullName = ta.User.FullName,
                            Email = ta.User.Email,
                            ProgressPercent = ta.ProgressPercent,
                            ContributionWeight = ta.ContributionWeight,
                            EstimatedHours = ta.EstimatedHours,
                            TotalActualHours = ta.TotalActualHours,
                            IsBlocked = ta.BlockedByUserId.HasValue,
                            BlockedByUserId = ta.BlockedByUserId,
                            BlockReason = ta.BlockReason
                        })
                        .ToList(),
                    ReporterName = wt.Reporter.FullName,
                    ReporterId = wt.ReporterId,
                    PlannedStartDate = wt.PlannedStartDate,
                    PlannedEndDate = wt.PlannedEndDate,
                    DueDate = wt.DueDate,
                    TotalEstimatedHours = wt.TotalEstimatedHours,
                    TotalActualHours = wt.TotalActualHours,
                    ParentTaskId = wt.ParentTaskId,
                    RowVersion = wt.RowVersion,
                    CreatedAt = wt.CreatedAt,
                    UpdatedAt = wt.UpdatedAt,
                    ProjectName = wt.Project.Name,
                    SortOrder = wt.SortOrder,
                    SequenceId = wt.SequenceId,
                    IsSubscribed = wt.Subscribers.Any(s => s.UserId == userId)
                })
                .ToListAsync();

            return tasks;
        }

        // ==================== PRIVATE HELPERS ====================

        /// <summary>
        /// Normalize DB status name → standard frontend group name.
        /// Frontend expects: "TO DO", "IN PROGRESS", "DONE"
        /// </summary>
        private static string NormalizeStatusName(string? dbStatusName)
        {
            if (string.IsNullOrEmpty(dbStatusName)) return "BACKLOG";

            var upper = dbStatusName.ToUpper().Replace(" ", "");

            if (upper.Contains("CANCEL") || upper.Contains("HUY"))
                return "CANCELLED";
            if (upper.Contains("DONE") || upper.Contains("HOANTHANH") || upper.Contains("COMPLETE"))
                return "DONE";
            if (upper.Contains("INREVIEW") || upper.Contains("REVIEW") || upper.Contains("KIEMTRA"))
                return "IN REVIEW";
            if (upper.Contains("INPROGRESS") || upper.Contains("DANGLAM") || upper.Contains("ACTIVE"))
                return "IN PROGRESS";
            if (upper.Contains("TODO") || upper.Contains("CANLAM"))
                return "TO DO";
            if (upper.Contains("BACKLOG"))
                return "BACKLOG";

            // For seed data like "Status 1", "Status 2", etc. → default to BACKLOG
            return "BACKLOG";
        }

        private WorkTaskResponseDto MapToDto(TaskManagement.Domain.Entities.WorkTask wt)
        {
            return new WorkTaskResponseDto
            {
                Id = wt.Id,
                Title = wt.Title,
                Description = wt.Description,
                ProjectId = wt.ProjectId,
                SprintId = wt.SprintId,
                TaskTypeId = wt.TaskTypeId,
                TypeName = wt.TaskType?.Name ?? "",
                TaskStatusId = wt.TaskStatusId,
                StatusName = NormalizeStatusName(wt.TaskStatus?.Name),
                ReporterId = wt.ReporterId,
                ReporterName = wt.Reporter != null ? wt.Reporter.FullName : "",
                AssignedUserId = wt.AssignedUserId,
                AssigneeName = wt.AssignedUser != null ? wt.AssignedUser.FullName : null,
                Assignees = wt.TaskAssignments
                    .Where(ta => ta.Status)
                    .Select(ta => new TaskAssigneeDto
                    {
                        UserId = ta.UserId,
                        FullName = ta.User.FullName,
                        Email = ta.User.Email,
                        ProgressPercent = ta.ProgressPercent,
                        ContributionWeight = ta.ContributionWeight,
                        EstimatedHours = ta.EstimatedHours,
                        TotalActualHours = ta.TotalActualHours,
                        IsBlocked = ta.BlockedByUserId.HasValue,
                        BlockedByUserId = ta.BlockedByUserId,
                        BlockReason = ta.BlockReason
                    })
                    .ToList(),
                ModuleId = wt.IssueModules
                    .OrderBy(im => im.AssignedAt)
                    .Select(im => (Guid?)im.ModuleId)
                    .FirstOrDefault(),
                LabelIds = wt.IssueLabels
                    .Select(il => il.LabelId)
                    .ToList(),
                Priority = wt.Priority,
                StoryPoints = wt.StoryPoints,
                DueDate = wt.DueDate,
                PlannedStartDate = wt.PlannedStartDate,
                PlannedEndDate = wt.PlannedEndDate,
                TotalEstimatedHours = wt.TotalEstimatedHours,
                TotalActualHours = wt.TotalActualHours,
                ParentTaskId = wt.ParentTaskId,
                CreatedAt = wt.CreatedAt,
                UpdatedAt = wt.UpdatedAt,
                RowVersion = wt.RowVersion,
                SortOrder = wt.SortOrder,
                SequenceId = wt.SequenceId,
                IsSubscribed = false
            };
        }

        private async Task<WorkTaskResponseDto> MapToDtoAsync(TaskManagement.Domain.Entities.WorkTask wt)
        {
            var dto = MapToDto(wt);
            var visibility = await LoadTaskVisibilityAsync(wt.Id);
            dto.VisibilityMode = visibility.Mode;
            dto.VisibleToRoles = visibility.Roles;
            return dto;
        }

        public async Task<List<WorkTaskResponseDto>> SearchTasksAsync(
            Guid userId,
            string? query,
            string? status,
            Guid? assigneeId,
            int? priority,
            Guid? projectId = null,
            string? scope = "all")
        {
            // TÌM CÁC PROJECT MÀ USER CÓ QUYỀN
            var userProjectIds = await _context.ProjectMembers
                .Where(pm => pm.UserId == userId && pm.Status)
                .Select(pm => pm.ProjectId)
                .ToListAsync();

            var dbQuery = _context.WorkTasks
                .Include(wt => wt.TaskStatus)
                .Include(wt => wt.TaskType)
                .Include(wt => wt.Reporter)
                .Include(wt => wt.AssignedUser)
                .Include(wt => wt.TaskAssignments)
                    .ThenInclude(ta => ta.User)
                .Include(wt => wt.IssueModules)
                .Include(wt => wt.IssueLabels)
                .Include(wt => wt.Project)
                .Where(wt => (userProjectIds.Contains(wt.ProjectId) || wt.Subscribers.Any(s => s.UserId == userId)) && !wt.IsDeleted && !wt.IsArchived);

            if (projectId.HasValue && projectId.Value != Guid.Empty)
            {
                dbQuery = dbQuery.Where(wt => wt.ProjectId == projectId.Value);
            }

            // Filtering
            if (!string.IsNullOrEmpty(query))
            {
                string lowerQuery = query.ToLower();
                dbQuery = dbQuery.Where(wt => 
                    wt.Title.ToLower().Contains(lowerQuery) || 
                    (wt.Description != null && wt.Description.ToLower().Contains(lowerQuery)));
            }

            if (!string.IsNullOrEmpty(status))
            {
                string lowerStatus = status.ToLower();
                dbQuery = dbQuery.Where(wt => wt.TaskStatus.Name.ToLower() == lowerStatus);
            }

            if (assigneeId.HasValue)
            {
                dbQuery = dbQuery.Where(wt => wt.AssignedUserId == assigneeId.Value || wt.TaskAssignments.Any(ta => ta.UserId == assigneeId.Value && ta.Status));
            }

            if (priority.HasValue)
            {
                dbQuery = dbQuery.Where(wt => wt.Priority == priority.Value);
            }

            var accessRecords = await dbQuery
                .AsNoTracking()
                .Select(wt => new
                {
                    wt.Id,
                    wt.ProjectId,
                    wt.ReporterId,
                    wt.AssignedUserId,
                    AssigneeIds = wt.TaskAssignments
                        .Where(ta => ta.Status)
                        .Select(ta => ta.UserId)
                        .ToList()
                })
                .ToListAsync();

            var visibilityMap = await LoadTaskVisibilityMapAsync(accessRecords.Select(item => item.Id));
            var projectIds = accessRecords.Select(item => item.ProjectId).Distinct().ToList();
            var membershipRoles = await _context.ProjectMembers
                .AsNoTracking()
                .Where(item => item.UserId == userId && item.Status && projectIds.Contains(item.ProjectId))
                .ToDictionaryAsync(
                    item => item.ProjectId,
                    item => ProjectExecutionRuleHelper.NormalizeProjectRole(item.ProjectRole));

            var projectRuleLookup = await _context.Projects
                .AsNoTracking()
                .Where(project => projectIds.Contains(project.Id) && !project.IsDeleted)
                .ToDictionaryAsync(
                    project => project.Id,
                    project => LoadExecutionRules(project.NavigationConfig));

            var allowedTaskIds = accessRecords
                .Where(record =>
                {
                    var rules = projectRuleLookup.TryGetValue(record.ProjectId, out var config)
                        ? config
                        : ProjectExecutionRuleHelper.NormalizeExecutionRules(null);
                    var normalizedProjectRole = membershipRoles.TryGetValue(record.ProjectId, out var role)
                        ? role
                        : string.Empty;
                    if (rules.ManagerAlwaysSeeAllTasks && IsVisibilityOverrideRole(normalizedProjectRole))
                    {
                        return true;
                    }

                    return CanUserViewTask(
                        new TaskVisibilityAccessRecord
                        {
                            Id = record.Id,
                            ReporterId = record.ReporterId,
                            AssignedUserId = record.AssignedUserId,
                            AssigneeIds = record.AssigneeIds
                        },
                        visibilityMap.TryGetValue(record.Id, out var visibility)
                            ? visibility
                            : new TaskVisibilityDto(),
                        normalizedProjectRole,
                        userId);
                })
                .Select(record => record.Id)
                .ToHashSet();

            var results = await dbQuery
                .Where(wt => allowedTaskIds.Contains(wt.Id))
                .OrderByDescending(wt => wt.CreatedAt)
                .Select(wt => new WorkTaskResponseDto
                {
                    Id = wt.Id,
                    Title = wt.Title,
                    Description = wt.Description,
                    ProjectId = wt.ProjectId,
                    SprintId = wt.SprintId,
                    TaskTypeId = wt.TaskTypeId,
                    TypeName = wt.TaskType.Name,
                    TaskStatusId = wt.TaskStatusId,
                    StatusName = wt.TaskStatus.Name,
                    ReporterId = wt.ReporterId,
                    ReporterName = wt.Reporter.FullName ?? wt.Reporter.Email,
                    AssignedUserId = wt.AssignedUserId,
                    AssigneeName = wt.AssignedUser != null ? wt.AssignedUser.FullName : null,
                    Assignees = wt.TaskAssignments
                        .Where(ta => ta.Status)
                        .Select(ta => new TaskAssigneeDto
                        {
                            UserId = ta.UserId,
                            FullName = ta.User.FullName,
                            Email = ta.User.Email,
                            ProgressPercent = ta.ProgressPercent,
                            ContributionWeight = ta.ContributionWeight,
                            EstimatedHours = ta.EstimatedHours,
                            TotalActualHours = ta.TotalActualHours,
                            IsBlocked = ta.BlockedByUserId.HasValue,
                            BlockedByUserId = ta.BlockedByUserId,
                            BlockReason = ta.BlockReason
                        })
                        .ToList(),
                    ModuleId = wt.IssueModules
                        .OrderBy(im => im.AssignedAt)
                        .Select(im => (Guid?)im.ModuleId)
                        .FirstOrDefault(),
                    LabelIds = wt.IssueLabels
                        .Select(il => il.LabelId)
                        .ToList(),
                    Priority = wt.Priority,
                    StoryPoints = wt.StoryPoints,
                    DueDate = wt.DueDate,
                    PlannedStartDate = wt.PlannedStartDate,
                    PlannedEndDate = wt.PlannedEndDate,
                    TotalEstimatedHours = wt.TotalEstimatedHours,
                    TotalActualHours = wt.TotalActualHours,
                    ParentTaskId = wt.ParentTaskId,
                    RowVersion = wt.RowVersion,
                    CreatedAt = wt.CreatedAt,
                    UpdatedAt = wt.UpdatedAt,
                    ProjectName = wt.Project.Name,
                    SortOrder = wt.SortOrder,
                    SequenceId = wt.SequenceId,
                    IsSubscribed = wt.Subscribers.Any(s => s.UserId == userId)
                })
                .ToListAsync();

            foreach (var result in results)
            {
                var visibility = visibilityMap.TryGetValue(result.Id, out var config)
                    ? config
                    : new TaskVisibilityDto();
                result.VisibilityMode = visibility.Mode;
                result.VisibleToRoles = visibility.Roles;
            }

            foreach (var r in results) r.StatusName = NormalizeStatusName(r.StatusName);

            return results;
        }

        private async Task<ProjectExecutionRulesDto> LoadExecutionRulesAsync(Guid projectId)
        {
            var rawNavigationConfig = await _context.Projects
                .AsNoTracking()
                .Where(project => project.Id == projectId && !project.IsDeleted)
                .Select(project => project.NavigationConfig)
                .FirstOrDefaultAsync();

            return LoadExecutionRules(rawNavigationConfig);
        }

        private static ProjectExecutionRulesDto LoadExecutionRules(string? rawNavigationConfig)
        {
            if (string.IsNullOrWhiteSpace(rawNavigationConfig))
            {
                return ProjectExecutionRuleHelper.NormalizeExecutionRules(null);
            }

            try
            {
                using var document = JsonDocument.Parse(rawNavigationConfig);
                if (document.RootElement.ValueKind != JsonValueKind.Object ||
                    !document.RootElement.TryGetProperty("executionRules", out var executionRulesElement))
                {
                    return ProjectExecutionRuleHelper.NormalizeExecutionRules(null);
                }

                return ProjectExecutionRuleHelper.NormalizeExecutionRules(
                    executionRulesElement.Deserialize<ProjectExecutionRulesDto>());
            }
            catch
            {
                return ProjectExecutionRuleHelper.NormalizeExecutionRules(null);
            }
        }

        private static bool IsManagerRole(string? normalizedProjectRole)
        {
            return ResourcePermissionPolicy.ProjectRoleHasPermission(
                normalizedProjectRole,
                ResourcePermissionCodes.ProjectWrite);
        }

        private static bool IsVisibilityOverrideRole(string? normalizedProjectRole)
        {
            return !string.IsNullOrWhiteSpace(normalizedProjectRole)
                && VisibilityOverrideRoles.Any(role =>
                    ProjectExecutionRuleHelper.NormalizeProjectRole(role) == normalizedProjectRole);
        }

        private sealed class TaskVisibilityAccessRecord
        {
            public Guid Id { get; set; }
            public Guid ReporterId { get; set; }
            public Guid? AssignedUserId { get; set; }
            public List<Guid> AssigneeIds { get; set; } = new();
        }

        private static bool CanUserViewTask(TaskVisibilityAccessRecord task, TaskVisibilityDto visibility, string normalizedProjectRole, Guid userId)
        {
            var visibilityMode = ProjectExecutionRuleHelper.NormalizeVisibilityMode(visibility.Mode);

            if (visibilityMode == "project")
            {
                return true;
            }

            if (visibilityMode == "assigned")
            {
                return task.AssignedUserId == userId || task.AssigneeIds.Contains(userId);
            }

            if (visibilityMode == "role")
            {
                return visibility.Roles
                    .Select(ProjectExecutionRuleHelper.NormalizeProjectRole)
                    .Contains(normalizedProjectRole, StringComparer.OrdinalIgnoreCase);
            }

            return true;
        }

        private static bool CanUserViewTask(WorkTaskResponseDto task, string normalizedProjectRole, Guid userId)
        {
            return CanUserViewTask(
                new TaskVisibilityAccessRecord
                {
                    Id = task.Id,
                    ReporterId = task.ReporterId,
                    AssignedUserId = task.AssignedUserId,
                    AssigneeIds = task.Assignees.Select(assignee => assignee.UserId).Distinct().ToList()
                },
                new TaskVisibilityDto
                {
                    Mode = task.VisibilityMode,
                    Roles = task.VisibleToRoles
                },
                normalizedProjectRole,
                userId);
        }

        private async Task<Dictionary<Guid, TaskVisibilityDto>> LoadTaskVisibilityMapAsync(IEnumerable<Guid> taskIds)
        {
            var ids = taskIds
                .Where(id => id != Guid.Empty)
                .Distinct()
                .ToList();

            if (!ids.Any())
            {
                return new Dictionary<Guid, TaskVisibilityDto>();
            }

            var keyMap = ids.ToDictionary(id => id.ToString(), id => id, StringComparer.OrdinalIgnoreCase);
            var rawSettings = await _context.SystemSettings
                .AsNoTracking()
                .Where(setting => setting.SettingGroup == TaskVisibilitySettingGroup && keyMap.Keys.Contains(setting.Key))
                .ToListAsync();

            var result = new Dictionary<Guid, TaskVisibilityDto>();
            foreach (var setting in rawSettings)
            {
                if (!keyMap.TryGetValue(setting.Key, out var taskId))
                {
                    continue;
                }

                result[taskId] = ProjectExecutionRuleHelper.ParseTaskVisibility(setting.Value);
            }

            return result;
        }

        private async Task<TaskVisibilityDto> LoadTaskVisibilityAsync(Guid taskId)
        {
            var setting = await _context.SystemSettings
                .AsNoTracking()
                .FirstOrDefaultAsync(item => item.SettingGroup == TaskVisibilitySettingGroup && item.Key == taskId.ToString());

            return setting == null
                ? ProjectExecutionRuleHelper.NormalizeTaskVisibility(new TaskVisibilityDto())
                : ProjectExecutionRuleHelper.ParseTaskVisibility(setting.Value);
        }

        private async Task SaveTaskVisibilityAsync(Guid taskId, TaskVisibilityDto visibility)
        {
            var normalized = ProjectExecutionRuleHelper.NormalizeTaskVisibility(visibility);
            var setting = await _context.SystemSettings
                .FirstOrDefaultAsync(item => item.SettingGroup == TaskVisibilitySettingGroup && item.Key == taskId.ToString());

            if (setting == null)
            {
                setting = new Domain.Entities.SystemSetting
                {
                    Id = Guid.NewGuid(),
                    SettingGroup = TaskVisibilitySettingGroup,
                    Key = taskId.ToString(),
                    Description = "Task visibility configuration"
                };
                _context.SystemSettings.Add(setting);
            }

            setting.Value = ProjectExecutionRuleHelper.BuildTaskVisibilityPayload(normalized);
            setting.LastModifiedAt = DateTime.UtcNow;
            await _context.SaveChangesAsync();
        }

        private async Task<TaskVisibilityDto> ResolveTaskVisibilityAsync(
            string? requestedMode,
            IEnumerable<string>? requestedRoles,
            Guid projectId,
            Guid actorUserId,
            ProjectExecutionRulesDto executionRules)
        {
            var normalizedMode = ProjectExecutionRuleHelper.NormalizeVisibilityMode(
                string.IsNullOrWhiteSpace(requestedMode)
                    ? executionRules.DefaultTaskVisibilityMode
                    : requestedMode);

            var normalizedRoles = (requestedRoles ?? Array.Empty<string>())
                .Select(ProjectExecutionRuleHelper.NormalizeProjectRole)
                .Where(role => !string.IsNullOrWhiteSpace(role))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();

            if (normalizedMode == "role" && !normalizedRoles.Any())
            {
                var actorRole = await _context.ProjectMembers
                    .AsNoTracking()
                    .Where(item => item.ProjectId == projectId && item.UserId == actorUserId && item.Status)
                    .Select(item => item.ProjectRole)
                    .FirstOrDefaultAsync();

                var normalizedActorRole = ProjectExecutionRuleHelper.NormalizeProjectRole(actorRole);
                if (!string.IsNullOrWhiteSpace(normalizedActorRole))
                {
                    normalizedRoles.Add(normalizedActorRole);
                }
            }

            return ProjectExecutionRuleHelper.NormalizeTaskVisibility(new TaskVisibilityDto
            {
                Mode = normalizedMode,
                Roles = normalizedRoles
            });
        }

        private async Task<string?> ResolvePrimaryProjectRoleAsync(Guid projectId, IReadOnlyCollection<Guid> assigneeIds, Guid fallbackUserId)
        {
            var candidateUserIds = assigneeIds.Any()
                ? assigneeIds.ToList()
                : new List<Guid> { fallbackUserId };

            foreach (var candidateUserId in candidateUserIds)
            {
                var role = await _context.ProjectMembers
                    .AsNoTracking()
                    .Where(item => item.ProjectId == projectId && item.UserId == candidateUserId && item.Status)
                    .Select(item => item.ProjectRole)
                    .FirstOrDefaultAsync();

                if (!string.IsNullOrWhiteSpace(role))
                {
                    return role;
                }
            }

            return null;
        }

        public async Task ArchiveAsync(Guid id)
        {
            var task = await _context.WorkTasks.FirstOrDefaultAsync(wt => wt.Id == id);
            if (task == null) throw new ArgumentException("Tac vu khong ton tai.");

            task.IsArchived = true;
            task.UpdatedAt = DateTime.UtcNow;
            await _context.SaveChangesAsync();
        }

        public async Task RestoreAsync(Guid id)
        {
            var task = await _context.WorkTasks.FirstOrDefaultAsync(wt => wt.Id == id);
            if (task == null) throw new ArgumentException("Tac vu khong ton tai.");

            task.IsArchived = false;
            task.UpdatedAt = DateTime.UtcNow;
            await _context.SaveChangesAsync();
        }

        public async Task<bool> ToggleSubscriptionAsync(Guid taskId, Guid userId)
        {
            var existing = await _context.TaskSubscribers
                .FirstOrDefaultAsync(ts => ts.WorkTaskId == taskId && ts.UserId == userId);

            if (existing != null)
            {
                _context.TaskSubscribers.Remove(existing);
                await _context.SaveChangesAsync();
                return false;
            }

            _context.TaskSubscribers.Add(new TaskManagement.Domain.Entities.TaskSubscriber
            {
                WorkTaskId = taskId,
                UserId = userId,
                SubscribedAt = DateTime.UtcNow
            });
            await _context.SaveChangesAsync();
            return true;
        }

        private async Task<TaskManagement.Domain.Entities.TaskStatus> ResolveTaskStatusAsync(string? statusName, Guid projectId)
        {
            TaskManagement.Domain.Entities.TaskStatus? taskStatus = null;

            // Normalize the requested status name to one of the 3 standard names
            string normalizedName = NormalizeStatusName(statusName);

            // Try to find an existing status in this project that matches the normalized name
            var projectStatuses = await _context.TaskStatuses
                .Where(ts => ts.ProjectId == projectId)
                .ToListAsync();

            taskStatus = projectStatuses.FirstOrDefault(ts =>
                NormalizeStatusName(ts.Name) == normalizedName);

            if (taskStatus != null)
                return taskStatus;

            // No matching status found in this project - create one
            int position = normalizedName switch
            {
                "BACKLOG" => 0,
                "TO DO" => 1,
                "IN PROGRESS" => 2,
                "IN REVIEW" => 3,
                "DONE" => 4,
                "CANCELLED" => 5,
                _ => 0
            };

            taskStatus = new TaskManagement.Domain.Entities.TaskStatus
            {
                Id = Guid.NewGuid(),
                ProjectId = projectId,
                Name = normalizedName,
                Position = position
            };
            _context.TaskStatuses.Add(taskStatus);
            await _context.SaveChangesAsync();

            return taskStatus;
        }

        private async Task<Guid> ResolveTaskTypeIdAsync(Guid? taskTypeId, string? typeName, Guid projectId)
        {
            if (taskTypeId.HasValue && taskTypeId.Value != Guid.Empty)
            {
                return taskTypeId.Value;
            }

            TaskManagement.Domain.Entities.TaskType? taskType = null;

            if (!string.IsNullOrEmpty(typeName))
            {
                taskType = await _context.TaskTypes
                    .FirstOrDefaultAsync(tt => tt.Name.ToUpper() == typeName.ToUpper());
            }
            if (taskType == null)
            {
                taskType = await _context.TaskTypes
                    .FirstOrDefaultAsync(tt => tt.ProjectId == projectId);
            }
            if (taskType == null)
            {
                taskType = new TaskManagement.Domain.Entities.TaskType
                {
                    Id = Guid.NewGuid(),
                    Name = string.IsNullOrEmpty(typeName) ? "Task" : typeName,
                    ProjectId = projectId,
                    ColorCode = "#3b82f6"
                };
                _context.TaskTypes.Add(taskType);
                await _context.SaveChangesAsync();
            }

            return taskType.Id;
        }

        public async Task<IEnumerable<TaskManagement.Application.DTOs.WorkTask.ContingencyPlanDto>> GetContingencyPlansAsync(Guid taskId)
        {
            var plans = await _context.ContingencyPlans
                .Include(p => p.ContingencyPlanTasks)
                    .ThenInclude(t => t.Assignee)
                .Where(p => p.WorkTaskId == taskId && !p.IsDeleted)
                .OrderBy(p => p.CreatedAt)
                .ToListAsync();

            return plans.Select(p => new TaskManagement.Application.DTOs.WorkTask.ContingencyPlanDto
            {
                Id = p.Id,
                WorkTaskId = p.WorkTaskId,
                Name = p.Name,
                RiskLevel = p.RiskLevel,
                RiskDescription = p.RiskDescription,
                Notes = p.Notes,
                ContingencyTasks = p.ContingencyPlanTasks.Select(cpt => new TaskManagement.Application.DTOs.WorkTask.ContingencyTaskDto
                {
                    Id = cpt.Id,
                    Title = cpt.Title,
                    Description = cpt.Description,
                    StatusName = cpt.StatusName,
                    Priority = cpt.Priority,
                    AssigneeId = cpt.AssigneeId,
                    AssigneeName = cpt.Assignee?.FullName,
                    IsActivated = cpt.IsActivated
                }).ToList(),
                CreatedAt = p.CreatedAt,
                UpdatedAt = p.UpdatedAt
            });
        }

        public async Task<TaskManagement.Application.DTOs.WorkTask.ContingencyPlanDto?> GetContingencyPlanByIdAsync(Guid taskId, Guid planId)
        {
            var plans = await GetContingencyPlansAsync(taskId);
            return plans.FirstOrDefault(p => p.Id == planId);
        }

        public async Task<TaskManagement.Application.DTOs.WorkTask.ContingencyPlanDto> CreateContingencyPlanAsync(Guid taskId, Guid userId, TaskManagement.Application.DTOs.WorkTask.CreateContingencyPlanDto dto)
        {
            var taskExists = await _context.WorkTasks.AnyAsync(t => t.Id == taskId && !t.IsDeleted);
            if (!taskExists) throw new Exception("Task not found");

            var plan = new TaskManagement.Domain.Entities.ContingencyPlan
            {
                Id = Guid.NewGuid(),
                WorkTaskId = taskId,
                Name = dto.Name,
                RiskLevel = dto.RiskLevel,
                RiskDescription = dto.RiskDescription,
                Notes = dto.Notes,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow,
                CreatedBy = userId,
                UpdatedBy = userId
            };
            
            _context.ContingencyPlans.Add(plan);
            
            var auditLog = new TaskManagement.Domain.Entities.AuditLog
            {
                Id = Guid.NewGuid(),
                WorkTaskId = taskId,
                UserId = userId,
                FieldChanged = "ContingencyPlan_Created",
                NewValue = plan.Name,
                CreatedAt = DateTime.UtcNow
            };
            _context.AuditLogs.Add(auditLog);

            await _context.SaveChangesAsync();
            var result = await GetContingencyPlanByIdAsync(taskId, plan.Id);
            if (result == null) throw new Exception("Failed to retrieve created contingency plan");
            return result;
        }

        public async Task<TaskManagement.Application.DTOs.WorkTask.ContingencyPlanDto> UpdateContingencyPlanAsync(Guid taskId, Guid planId, Guid userId, TaskManagement.Application.DTOs.WorkTask.UpdateContingencyPlanDto dto)
        {
            var plan = await _context.ContingencyPlans.FirstOrDefaultAsync(p => p.Id == planId && p.WorkTaskId == taskId && !p.IsDeleted);
            if (plan == null) throw new Exception("Contingency plan not found");
            
            plan.Name = dto.Name;
            plan.RiskLevel = dto.RiskLevel;
            plan.RiskDescription = dto.RiskDescription;
            plan.Notes = dto.Notes;
            plan.UpdatedAt = DateTime.UtcNow;
            plan.UpdatedBy = userId;
            
            var auditLog = new TaskManagement.Domain.Entities.AuditLog
            {
                Id = Guid.NewGuid(),
                WorkTaskId = taskId,
                UserId = userId,
                FieldChanged = "ContingencyPlan_Updated",
                NewValue = plan.Name,
                CreatedAt = DateTime.UtcNow
            };
            _context.AuditLogs.Add(auditLog);

            await _context.SaveChangesAsync();
            var result = await GetContingencyPlanByIdAsync(taskId, plan.Id);
            if (result == null) throw new Exception("Failed to retrieve updated contingency plan");
            return result;
        }

        public async Task DeleteContingencyPlanAsync(Guid taskId, Guid planId, Guid userId)
        {
            var plan = await _context.ContingencyPlans.FirstOrDefaultAsync(p => p.Id == planId && p.WorkTaskId == taskId && !p.IsDeleted);
            if (plan == null) throw new Exception("Contingency plan not found");

            plan.IsDeleted = true;
            plan.UpdatedAt = DateTime.UtcNow;
            plan.UpdatedBy = userId;

            await _context.SaveChangesAsync();
        }

        public async Task CreateContingencyTaskToPlanAsync(Guid taskId, Guid planId, TaskManagement.Application.DTOs.WorkTask.CreateContingencyTaskDto dto, Guid userId)
        {
            var planExists = await _context.ContingencyPlans.AnyAsync(p => p.Id == planId && p.WorkTaskId == taskId && !p.IsDeleted);
            if (!planExists) throw new Exception("Contingency plan not found");
            
            _context.ContingencyPlanTasks.Add(new TaskManagement.Domain.Entities.ContingencyPlanTask
            {
                Id = Guid.NewGuid(),
                ContingencyPlanId = planId,
                Title = dto.Title,
                Description = dto.Description,
                Priority = dto.Priority,
                AssigneeId = dto.AssigneeId,
                StatusName = "Đang chờ xử lý",
                CreatedAt = DateTime.UtcNow
            });
            
            await _context.SaveChangesAsync();
        }

        public async Task AddContingencyTaskToPlanAsync(Guid taskId, Guid planId, Guid fallbackTaskId, Guid userId)
        {
            var planExists = await _context.ContingencyPlans.AnyAsync(p => p.Id == planId && p.WorkTaskId == taskId && !p.IsDeleted);
            if (!planExists) throw new Exception("Contingency plan not found");
            
            var taskExists = await _context.WorkTasks.AnyAsync(t => t.Id == fallbackTaskId && !t.IsDeleted);
            if (!taskExists) throw new Exception("Fallback task not found");
            
            var linkExists = await _context.ContingencyPlanTasks.AnyAsync(l => l.ContingencyPlanId == planId && l.WorkTaskId == fallbackTaskId);
            if (linkExists) throw new Exception("Task is already linked to this plan");
            
            _context.ContingencyPlanTasks.Add(new TaskManagement.Domain.Entities.ContingencyPlanTask
            {
                Id = Guid.NewGuid(),
                ContingencyPlanId = planId,
                WorkTaskId = fallbackTaskId,
                CreatedAt = DateTime.UtcNow
            });
            
            await _context.SaveChangesAsync();
        }

        public async Task RemoveContingencyTaskFromPlanAsync(Guid taskId, Guid planId, Guid contingencyTaskId, Guid userId)
        {
            var planExists = await _context.ContingencyPlans.AnyAsync(p => p.Id == planId && p.WorkTaskId == taskId && !p.IsDeleted);
            if (!planExists) throw new Exception("Contingency plan not found");
            
            var link = await _context.ContingencyPlanTasks.FirstOrDefaultAsync(l => l.Id == contingencyTaskId && l.ContingencyPlanId == planId);
            if (link == null) throw new Exception("Contingency task not found");
            
            _context.ContingencyPlanTasks.Remove(link);
            await _context.SaveChangesAsync();
        }

        public async Task ActivateContingencyTaskAsync(Guid taskId, Guid planId, Guid contingencyTaskId, Guid userId)
        {
            var plan = await _context.ContingencyPlans.FirstOrDefaultAsync(p => p.Id == planId && p.WorkTaskId == taskId && !p.IsDeleted);
            if (plan == null) throw new Exception("Contingency plan not found");
            
            var link = await _context.ContingencyPlanTasks.FirstOrDefaultAsync(l => l.Id == contingencyTaskId && l.ContingencyPlanId == planId);
            if (link == null) throw new Exception("Contingency task not found");
            
            if (link.IsActivated) throw new Exception("Contingency task is already activated");

            link.IsActivated = true;
            link.StatusName = "IN PROGRESS";
            link.ActivatedById = userId;
            link.ActivatedAt = DateTime.UtcNow;

            var auditLog = new TaskManagement.Domain.Entities.AuditLog
            {
                Id = Guid.NewGuid(),
                WorkTaskId = taskId,
                UserId = userId,
                FieldChanged = "ContingencyTask_Activated",
                NewValue = contingencyTaskId.ToString(),
                CreatedAt = DateTime.UtcNow
            };
            _context.AuditLogs.Add(auditLog);

            // Add issue/blocked logic to main task
            var mainTask = await _context.WorkTasks.FirstOrDefaultAsync(t => t.Id == taskId && !t.IsDeleted);
            if (mainTask != null)
            {
                _context.AuditLogs.Add(new TaskManagement.Domain.Entities.AuditLog
                {
                    Id = Guid.NewGuid(),
                    WorkTaskId = mainTask.Id,
                    UserId = userId,
                    FieldChanged = "System_Issue_Reported",
                    NewValue = "Contingency Triggered",
                    CreatedAt = DateTime.UtcNow
                });
                
                // Create a real WorkTask for this contingency task
                var taskStatus = await _context.TaskStatuses.FirstOrDefaultAsync(ts => ts.Name == "IN PROGRESS" && ts.ProjectId == mainTask.ProjectId);
                if (taskStatus == null) taskStatus = await _context.TaskStatuses.FirstOrDefaultAsync(ts => ts.ProjectId == mainTask.ProjectId);
                
                var taskType = await _context.TaskTypes.FirstOrDefaultAsync(tt => tt.ProjectId == mainTask.ProjectId);
                
                double maxSort = 0;
                var existingMax = await _context.WorkTasks
                    .Where(wt => wt.ProjectId == mainTask.ProjectId && !wt.IsDeleted)
                    .MaxAsync(wt => (double?)wt.SortOrder);
                if (existingMax.HasValue) maxSort = existingMax.Value + 65536;
                else maxSort = 65536;

                var newWorkTask = new TaskManagement.Domain.Entities.WorkTask
                {
                    Id = Guid.NewGuid(),
                    ProjectId = mainTask.ProjectId,
                    Title = "[DỰ PHÒNG] " + link.Title,
                    Description = link.Description,
                    TaskStatusId = taskStatus?.Id ?? Guid.Empty,
                    TaskTypeId = taskType?.Id ?? Guid.Empty,
                    Priority = link.Priority,
                    ReporterId = userId,
                    ParentTaskId = null,
                    SortOrder = maxSort,
                    CreatedAt = DateTime.UtcNow,
                    UpdatedAt = DateTime.UtcNow
                };
                
                if (link.AssigneeId.HasValue)
                {
                    var assignment = new TaskManagement.Domain.Entities.TaskAssignment
                    {
                        WorkTaskId = newWorkTask.Id,
                        UserId = link.AssigneeId.Value,
                        Status = true
                    };
                    _context.TaskAssignments.Add(assignment);
                }

                _context.WorkTasks.Add(newWorkTask);
            }

            await _context.SaveChangesAsync();
        }
    }
}
