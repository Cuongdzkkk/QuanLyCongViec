using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using System.Collections.Generic;
using TaskManagement.API.Hubs;
using TaskManagement.API.Realtime;
using TaskManagement.Application.DTOs.Common;
using TaskManagement.Application.DTOs.Department;
using TaskManagement.Application.Interfaces;

namespace TaskManagement.API.Controllers
{
    [ApiController]
    [Route("api/departments")]
    [Authorize]
    public class DepartmentsController : ControllerBase
    {
        private readonly IDepartmentService _departmentService;
        private readonly TaskManagement.Infrastructure.Data.ApplicationDbContext _context;
        private readonly IResourceAuthorizationService _authorization;
        private readonly IHubContext<KanbanHub> _hub;

        public DepartmentsController(
            IDepartmentService departmentService,
            TaskManagement.Infrastructure.Data.ApplicationDbContext context,
            IHubContext<KanbanHub> hub,
            IResourceAuthorizationService authorization)
        {
            _departmentService = departmentService;
            _context = context;
            _hub = hub;
            _authorization = authorization;
        }

        private async Task PublishDepartmentAsync(Guid id, string action = "upsert", object? data = null)
        {
            var payload = data ?? await _departmentService.GetByIdAsync(id);
            await _hub.PublishAuthenticatedEntityChangedAsync("department", action, id, payload);
        }

        private Task PublishDepartmentDetailAsync(Guid id, string changeType, object? data = null)
        {
            return _hub.PublishAuthenticatedEntityChangedAsync(
                "department-detail",
                changeType,
                id,
                new { departmentId = id, data });
        }

        private async Task AddSiteAuditAsync(Guid entityId, string action, string? oldValue = null, string? newValue = null)
        {
            var userIdClaim = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
            if (!Guid.TryParse(userIdClaim, out var auditUserId))
            {
                await _context.SaveChangesAsync();
                return;
            }

            _context.SiteAuditLogs.Add(new TaskManagement.Domain.Entities.SiteAuditLog
            {
                EntityId = entityId,
                EntityType = "Team",
                Action = action,
                UserId = auditUserId,
                OldValue = oldValue,
                NewValue = newValue,
                CreatedAt = DateTime.UtcNow
            });

            await _context.SaveChangesAsync();
        }

        [HttpGet]
        public async Task<IActionResult> GetAll()
        {
            var departments = await _departmentService.GetAllAsync();
            return Ok(ApiResponse<List<DepartmentResponseDto>>.Success(departments));
        }

        [HttpGet("{id}")]
        public async Task<IActionResult> GetById(Guid id)
        {
            var department = await _departmentService.GetByIdAsync(id);
            if (department == null)
                return NotFound(ApiResponse<object>.Error("Phòng ban không tồn tại.", 404));

            return Ok(ApiResponse<DepartmentResponseDto>.Success(department));
        }

        [HttpGet("{id}/full")]
        public async Task<IActionResult> GetFullById(Guid id)
        {
            var department = await _departmentService.GetByIdAsync(id);
            if (department == null)
                return NotFound(ApiResponse<object>.Error("Phòng ban không tồn tại.", 404));

            var members = await _context.DepartmentMembers
                .Where(dm => dm.DepartmentId == id)
                .Select(dm => new { 
                    id = dm.UserId, 
                    name = dm.User.FullName ?? dm.User.Email, 
                    fullName = dm.User.FullName ?? dm.User.Email,
                    email = dm.User.Email,
                    avatarUrl = dm.User.AvatarUrl,
                    avatar = dm.User.AvatarUrl,
                    role = "Member"
                })
                .ToListAsync();

            var linkedGoalIds = await _context.TeamGoals
                .Where(tg => tg.DepartmentId == id)
                .Select(tg => tg.GoalId)
                .ToListAsync();

            var goals = await _context.Goals
                .Where(g => (g.DepartmentId == id || linkedGoalIds.Contains(g.Id)) && !g.IsArchived)
                .Select(g => new { id = g.Id, title = g.Title, status = g.Status, progress = g.Progress, updatedAt = g.UpdatedAt, ownerId = g.OwnerId, ownerName = g.Owner.FullName ?? g.Owner.Email, ownerAvatarUrl = g.Owner.AvatarUrl })
                .ToListAsync();

            var projects = await _context.ProjectDepartmentRoles
                .Where(pdr => pdr.DepartmentId == id)
                .Select(pdr => new
                {
                    id = pdr.ProjectId,
                    name = pdr.Project.Name,
                    title = pdr.Project.Name,
                    status = pdr.Project.Status ? "Active" : "Archived",
                    roleName = pdr.RoleName,
                    updatedAt = pdr.Project.UpdatedAt
                })
                .ToListAsync();

            var linkedProjectIds = projects.Select(p => p.id).ToList();
            var activityTasks = await _context.WorkTasks
                .AsNoTracking()
                .Include(task => task.Project)
                .Include(task => task.TaskStatus)
                .Include(task => task.AssignedUser)
                .Include(task => task.Reporter)
                .Where(task => linkedProjectIds.Contains(task.ProjectId) && !task.IsDeleted && !task.IsArchived)
                .OrderByDescending(task => task.UpdatedAt)
                .Take(50)
                .Select(task => new
                {
                    id = task.Id,
                    projectId = task.ProjectId,
                    title = task.Title,
                    sequenceId = task.SequenceId,
                    projectName = task.Project.Name,
                    status = task.TaskStatus.Name,
                    priority = task.Priority,
                    updatedAt = task.UpdatedAt,
                    createdAt = task.CreatedAt,
                    assignedUser = task.AssignedUser == null ? null : new
                    {
                        id = task.AssignedUser.Id,
                        fullName = task.AssignedUser.FullName ?? task.AssignedUser.Email,
                        name = task.AssignedUser.FullName ?? task.AssignedUser.Email,
                        email = task.AssignedUser.Email,
                        avatarUrl = task.AssignedUser.AvatarUrl
                    },
                    reporter = task.Reporter == null ? null : new
                    {
                        id = task.Reporter.Id,
                        fullName = task.Reporter.FullName ?? task.Reporter.Email,
                        name = task.Reporter.FullName ?? task.Reporter.Email,
                        email = task.Reporter.Email,
                        avatarUrl = task.Reporter.AvatarUrl
                    },
                    assignees = task.TaskAssignments
                        .Where(assignment => assignment.Status)
                        .Select(assignment => new
                        {
                            userId = assignment.UserId,
                            fullName = assignment.User.FullName ?? assignment.User.Email,
                            email = assignment.User.Email,
                            avatarUrl = assignment.User.AvatarUrl,
                            progressPercent = assignment.ProgressPercent
                        })
                        .ToList()
                })
                .ToListAsync();

            var parent = department.ParentId.HasValue ? await _context.Departments
                .Where(d => d.Id == department.ParentId.Value)
                .Select(d => new { id = d.Id, name = d.Name })
                .FirstOrDefaultAsync() : null;

            var manager = department.ManagerId.HasValue ? await _context.Users
                .Where(u => u.Id == department.ManagerId.Value)
                .Select(u => new
                {
                    id = u.Id,
                    name = u.FullName ?? u.Email,
                    email = u.Email,
                    avatarUrl = u.AvatarUrl
                })
                .FirstOrDefaultAsync() : null;

            var children = await _context.Departments
                .Where(d => d.ParentId == id && !d.IsDeleted)
                .Select(d => new { id = d.Id, name = d.Name })
                .ToListAsync();

            var callerIdValue = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
            var canReadKudos = Guid.TryParse(callerIdValue, out var callerId) &&
                (await _authorization.AuthorizeDepartmentAsync(callerId, id)).Succeeded;
            var kudos = canReadKudos
                ? (await _context.Kudos
                .Include(k => k.Sender)
                .Where(k => k.DepartmentId == id)
                .OrderByDescending(k => k.CreatedAt)
                .Select(k => new {
                    id = k.Id,
                    message = k.Message,
                    sender = k.Sender.FullName ?? k.Sender.Email,
                    senderId = k.SenderId,
                    senderEmail = k.Sender.Email,
                    senderAvatarUrl = k.Sender.AvatarUrl,
                    icon = k.Icon ?? "🌟",
                    createdAt = k.CreatedAt
                })
                .ToListAsync()).Cast<object>().ToList()
                : new List<object>();

            return Ok(new {
                statusCode = 200,
                data = new {
                    id = department.Id,
                    name = department.Name,
                    isArchived = !department.IsActive,
                    description = department.Description ?? "Phòng ban / Đội nhóm trong hệ thống",
                    coverImage = department.CoverImage ?? "https://images.unsplash.com/photo-1550751827-4bd374c3f58b?w=1200&q=80",
                    members = members,
                    manager = manager,
                    hierarchy = new { parent = parent, children = children },
                    goals = goals,
                    projects = projects,
                    activityTasks = activityTasks,
                    kudos = kudos
                }
            });
        }

        [HttpPost]
        public async Task<IActionResult> Create([FromBody] CreateDepartmentDto dto)
        {
            try
            {
                var result = await _departmentService.CreateAsync(dto);
                var memberIds = dto.MemberIds
                    .Where(id => id != Guid.Empty)
                    .Distinct()
                    .ToList();

                var userIdClaim = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
                if (Guid.TryParse(userIdClaim, out var userId) && !memberIds.Contains(userId))
                    memberIds.Add(userId);

                if (memberIds.Count > 0)
                    await _departmentService.AddMembersAsync(result.Id, memberIds);

                result = await _departmentService.GetByIdAsync(result.Id) ?? result;

                if (Guid.TryParse(userIdClaim, out var auditUserId))
                {
                    _context.SiteAuditLogs.Add(new TaskManagement.Domain.Entities.SiteAuditLog
                    {
                        EntityId = result.Id,
                        EntityType = "Team",
                        Action = "Create",
                        UserId = auditUserId,
                        NewValue = result.Name,
                        CreatedAt = DateTime.UtcNow
                    });
                    await _context.SaveChangesAsync();
                }

                await PublishDepartmentAsync(result.Id, data: result);
                return CreatedAtAction(nameof(GetById), new { id = result.Id },
                    ApiResponse<DepartmentResponseDto>.Created(result));
            }
            catch (ArgumentException ex)
            {
                return BadRequest(ApiResponse<object>.Error(ex.Message));
            }
        }

        [HttpPut("{id}")]
        public async Task<IActionResult> Update(Guid id, [FromBody] UpdateDepartmentDto dto)
        {
            try
            {
                var before = await _departmentService.GetByIdAsync(id);
                var result = await _departmentService.UpdateAsync(id, dto);
                await AddSiteAuditAsync(id, "Update", before?.Name, result.Name);
                await PublishDepartmentAsync(id, data: result);
                return Ok(ApiResponse<DepartmentResponseDto>.Success(result, "Cập nhật thành công."));
            }
            catch (ArgumentException ex)
            {
                return BadRequest(ApiResponse<object>.Error(ex.Message));
            }
        }

        /// <summary>
        /// 5.1 Archive: Vô hiệu hóa tạm thời (ẩn khỏi dropdown, vẫn xem lịch sử)
        /// </summary>
        [HttpPut("{id}/archive")]
        public async Task<IActionResult> Archive(Guid id)
        {
            try
            {
                await _departmentService.ArchiveAsync(id);
                await AddSiteAuditAsync(id, "Archive");
                await PublishDepartmentAsync(id);
                return Ok(ApiResponse<object>.Success(null!, "Phòng ban đã được vô hiệu hóa."));
            }
            catch (ArgumentException ex)
            {
                return BadRequest(ApiResponse<object>.Error(ex.Message));
            }
        }

        /// <summary>
        /// Khôi phục phòng ban đã bị Archive
        /// </summary>
        [HttpPut("{id}/restore")]
        public async Task<IActionResult> Restore(Guid id)
        {
            try
            {
                await _departmentService.RestoreAsync(id);
                await AddSiteAuditAsync(id, "Restore");
                await PublishDepartmentAsync(id);
                return Ok(ApiResponse<object>.Success(null!, "Phòng ban đã được khôi phục."));
            }
            catch (ArgumentException ex)
            {
                return BadRequest(ApiResponse<object>.Error(ex.Message));
            }
        }

        /// <summary>
        /// 5.1 Soft Delete: Đánh dấu xóa (Global Query Filter sẽ tự ẩn)
        /// </summary>
        [HttpDelete("{id}")]
        public async Task<IActionResult> SoftDelete(Guid id)
        {
            try
            {
                await _departmentService.SoftDeleteAsync(id);
                await AddSiteAuditAsync(id, "Delete");
                await PublishDepartmentAsync(id, "deleted");
                return Ok(ApiResponse<object>.Success(null!, "Phòng ban đã được xóa."));
            }
            catch (ArgumentException ex)
            {
                return BadRequest(ApiResponse<object>.Error(ex.Message));
            }
        }

        [HttpPost("{id}/members")]
        public async Task<IActionResult> AddMembers(Guid id, [FromBody] List<Guid> userIds)
        {
            try
            {
                await _departmentService.AddMembersAsync(id, userIds);
                var userIdClaim = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
                if (Guid.TryParse(userIdClaim, out var auditUserId))
                {
                    _context.SiteAuditLogs.Add(new TaskManagement.Domain.Entities.SiteAuditLog
                    {
                        EntityId = id,
                        EntityType = "Team",
                        Action = "AddMember",
                        UserId = auditUserId,
                        NewValue = string.Join(",", userIds),
                        CreatedAt = DateTime.UtcNow
                    });
                    await _context.SaveChangesAsync();
                }
                await PublishDepartmentDetailAsync(id, "members-changed", new { userIds });
                return Ok(ApiResponse<object>.Success(null!, "Thêm thành viên thành công."));
            }
            catch (ArgumentException ex)
            {
                return BadRequest(ApiResponse<object>.Error(ex.Message));
            }
        }

        [HttpDelete("{id}/members/{userId}")]
        public async Task<IActionResult> RemoveMember(Guid id, Guid userId)
        {
            try
            {
                await _departmentService.RemoveMemberAsync(id, userId);
                await AddSiteAuditAsync(id, "RemoveMember", userId.ToString());
                await PublishDepartmentDetailAsync(id, "members-changed", new { userId });
                return Ok(ApiResponse<object>.Success(null!, "Xóa thành viên thành công."));
            }
            catch (ArgumentException ex)
            {
                return BadRequest(ApiResponse<object>.Error(ex.Message));
            }
        }

        [HttpPut("{id}/hierarchy")]
        public async Task<IActionResult> UpdateHierarchy(Guid id, [FromBody] Guid? parentId)
        {
            try
            {
                if (parentId.HasValue)
                {
                    if (parentId.Value == id)
                        return BadRequest(ApiResponse<object>.Error("Team khong the la cha cua chinh no."));

                    var cursor = parentId.Value;
                    while (true)
                    {
                        var parent = await _context.Departments
                            .AsNoTracking()
                            .Where(d => d.Id == cursor && !d.IsDeleted)
                            .Select(d => new { d.Id, d.ParentId })
                            .FirstOrDefaultAsync();

                        if (parent == null || !parent.ParentId.HasValue) break;
                        if (parent.ParentId.Value == id)
                            return BadRequest(ApiResponse<object>.Error("Khong the tao vong lap phan cap team."));

                        cursor = parent.ParentId.Value;
                    }
                }

                await _departmentService.UpdateHierarchyAsync(id, parentId);
                await PublishDepartmentAsync(id);
                await PublishDepartmentDetailAsync(id, "hierarchy-changed", new { parentId });
                return Ok(ApiResponse<object>.Success(null!, "Cập nhật sơ đồ phân cấp thành công."));
            }
            catch (ArgumentException ex)
            {
                return BadRequest(ApiResponse<object>.Error(ex.Message));
            }
        }

        [HttpPut("{id}/manager/{userId}")]
        public async Task<IActionResult> UpdateManager(Guid id, Guid userId)
        {
            var department = await _context.Departments.FirstOrDefaultAsync(d => d.Id == id);
            if (department == null)
                return NotFound(ApiResponse<object>.Error("Team không tồn tại.", 404));

            var isMember = await _context.DepartmentMembers.AnyAsync(dm => dm.DepartmentId == id && dm.UserId == userId);
            if (!isMember)
                return BadRequest(ApiResponse<object>.Error("Người quản lý phải là thành viên của team."));

            department.ManagerId = userId;
            var userIdClaim = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
            if (Guid.TryParse(userIdClaim, out var auditUserId))
            {
                _context.SiteAuditLogs.Add(new TaskManagement.Domain.Entities.SiteAuditLog
                {
                    EntityId = id,
                    EntityType = "Team",
                    Action = "ChangeManager",
                    UserId = auditUserId,
                    NewValue = userId.ToString(),
                    CreatedAt = DateTime.UtcNow
                });
            }
            await _context.SaveChangesAsync();
            await PublishDepartmentAsync(id);
            await PublishDepartmentDetailAsync(id, "manager-changed", new { userId });
            return Ok(ApiResponse<object>.Success(new { department.Id, department.ManagerId }));
        }

        [HttpPost("{id}/goals/{goalId}")]
        public async Task<IActionResult> LinkGoal(Guid id, Guid goalId)
        {
            if (!await CanManageTeamLinksAsync(id)) return Forbid();
            var departmentExists = await _context.Departments.AnyAsync(d => d.Id == id);
            if (!departmentExists)
                return NotFound(ApiResponse<object>.Error("Team không tồn tại.", 404));

            var goal = await _context.Goals.FirstOrDefaultAsync(g => g.Id == goalId && !g.IsArchived);
            if (goal == null)
                return NotFound(ApiResponse<object>.Error("Mục tiêu không tồn tại.", 404));

            var userIdClaim = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
            var currentUserId = Guid.TryParse(userIdClaim, out var parsedUserId) ? parsedUserId : Guid.Empty;

            var exists = await _context.TeamGoals.AnyAsync(tg => tg.DepartmentId == id && tg.GoalId == goalId);
            if (!exists)
            {
                _context.TeamGoals.Add(new TaskManagement.Domain.Entities.TeamGoal
                {
                    DepartmentId = id,
                    GoalId = goalId,
                    CreatedByUserId = currentUserId == Guid.Empty ? goal.OwnerId : currentUserId,
                    CreatedAt = DateTime.UtcNow
                });
            }

            if (!goal.DepartmentId.HasValue)
                goal.DepartmentId = id;
            goal.UpdatedAt = DateTime.UtcNow;
            if (currentUserId != Guid.Empty)
            {
                _context.SiteAuditLogs.Add(new TaskManagement.Domain.Entities.SiteAuditLog
                {
                    EntityId = id,
                    EntityType = "Team",
                    Action = "LinkGoal",
                    UserId = currentUserId,
                    NewValue = goalId.ToString(),
                    CreatedAt = DateTime.UtcNow
                });
            }
            await _context.SaveChangesAsync();
            await PublishDepartmentDetailAsync(id, "goals-changed", new { goalId });
            return Ok(ApiResponse<object>.Success(new { goal.Id, goal.DepartmentId }));
        }

        [HttpDelete("{id}/goals/{goalId}")]
        public async Task<IActionResult> UnlinkGoal(Guid id, Guid goalId)
        {
            if (!await CanManageTeamLinksAsync(id)) return Forbid();
            var goal = await _context.Goals.FirstOrDefaultAsync(g => g.Id == goalId);
            var links = await _context.TeamGoals
                .Where(tg => tg.DepartmentId == id && tg.GoalId == goalId)
                .ToListAsync();

            if (goal == null && links.Count == 0)
                return NoContent();

            if (links.Count > 0)
                _context.TeamGoals.RemoveRange(links);

            if (goal != null && goal.DepartmentId == id)
            {
                goal.DepartmentId = null;
                goal.UpdatedAt = DateTime.UtcNow;
            }

            var userIdClaim = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
            if (Guid.TryParse(userIdClaim, out var currentUserId))
            {
                _context.SiteAuditLogs.Add(new TaskManagement.Domain.Entities.SiteAuditLog
                {
                    EntityId = id,
                    EntityType = "Team",
                    Action = "UnlinkGoal",
                    UserId = currentUserId,
                    OldValue = goalId.ToString(),
                    CreatedAt = DateTime.UtcNow
                });
            }
            await _context.SaveChangesAsync();
            await PublishDepartmentDetailAsync(id, "goals-changed", new { goalId });
            return NoContent();
        }

        [HttpPost("{id}/projects/{projectId}")]
        public async Task<IActionResult> LinkProject(Guid id, Guid projectId)
        {
            if (!await CanManageTeamLinksAsync(id)) return Forbid();
            var departmentExists = await _context.Departments.AnyAsync(d => d.Id == id);
            if (!departmentExists)
                return NotFound(ApiResponse<object>.Error("Team không tồn tại.", 404));

            var projectExists = await _context.Projects.AnyAsync(p => p.Id == projectId && !p.IsDeleted);
            if (!projectExists)
                return NotFound(ApiResponse<object>.Error("Dự án không tồn tại.", 404));

            const string roleName = "Team";
            var exists = await _context.ProjectDepartmentRoles.AnyAsync(pdr =>
                pdr.DepartmentId == id && pdr.ProjectId == projectId && pdr.RoleName == roleName);

            if (!exists)
            {
                _context.ProjectDepartmentRoles.Add(new TaskManagement.Domain.Entities.ProjectDepartmentRole
                {
                    DepartmentId = id,
                    ProjectId = projectId,
                    RoleName = roleName,
                    AssignedAt = DateTime.UtcNow
                });
            }

            await AddSiteAuditAsync(id, "LinkProject", null, projectId.ToString());
            await PublishDepartmentDetailAsync(id, "projects-changed", new { projectId });

            return Ok(ApiResponse<object>.Success(new { departmentId = id, projectId, roleName }));
        }

        [HttpDelete("{id}/projects/{projectId}")]
        public async Task<IActionResult> UnlinkProject(Guid id, Guid projectId)
        {
            if (!await CanManageTeamLinksAsync(id)) return Forbid();
            var links = await _context.ProjectDepartmentRoles
                .Where(pdr => pdr.DepartmentId == id && pdr.ProjectId == projectId)
                .ToListAsync();

            if (links.Count > 0)
            {
                _context.ProjectDepartmentRoles.RemoveRange(links);
            }

            // A team link grants its members access to the project. Removing the
            // link must also revoke that access and clear their task assignments.
            var teamMemberIds = await _context.DepartmentMembers
                .Where(member => member.DepartmentId == id)
                .Select(member => member.UserId)
                .ToListAsync();
            var projectMembers = await _context.ProjectMembers
                .Where(member => member.ProjectId == projectId && teamMemberIds.Contains(member.UserId))
                .ToListAsync();
            foreach (var member in projectMembers)
            {
                member.Status = false;
                member.LeftAt = DateTime.UtcNow;
            }

            var assignedTasks = await _context.WorkTasks
                .Include(task => task.TaskAssignments)
                .Where(task => task.ProjectId == projectId && !task.IsDeleted)
                .ToListAsync();
            foreach (var task in assignedTasks)
            {
                foreach (var assignment in task.TaskAssignments.Where(assignment => teamMemberIds.Contains(assignment.UserId)))
                {
                    assignment.Status = false;
                    assignment.RemovedAt = DateTime.UtcNow;
                }
                if (task.AssignedUserId.HasValue && teamMemberIds.Contains(task.AssignedUserId.Value))
                {
                    task.AssignedUserId = null;
                    task.UpdatedAt = DateTime.UtcNow;
                }
            }

            await AddSiteAuditAsync(id, "UnlinkProject", projectId.ToString());
            await PublishDepartmentDetailAsync(id, "projects-changed", new { projectId });

            return NoContent();
        }

        private async Task<bool> CanManageTeamLinksAsync(Guid departmentId)
        {
            if (User.IsInRole("Admin") || User.IsInRole("SystemAdmin")) return true;
            var claim = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
            return Guid.TryParse(claim, out var userId) && await _context.Departments
                .AnyAsync(department => department.Id == departmentId && department.ManagerId == userId && !department.IsDeleted);
        }
    }
}
