using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.SignalR;
using System.Security.Claims;
using TaskManagement.Application.Common;
using TaskManagement.Application.Interfaces;
using TaskManagement.Infrastructure.Data;
using TaskManagement.Domain.Entities;
using TaskManagement.API.Hubs;
using TaskManagement.API.Realtime;

namespace TaskManagement.API.Controllers
{
    [ApiController]
    [Route("api/workspaces")]
    [Authorize]
    public class WorkspacesController : ControllerBase
    {
        private readonly ApplicationDbContext _context;
        private readonly IResourceAuthorizationService _authorizationService;
        private readonly IHubContext<KanbanHub>? _hub;

        public WorkspacesController(
            ApplicationDbContext context,
            IResourceAuthorizationService authorizationService,
            IHubContext<KanbanHub>? hub = null)
        {
            _context = context;
            _authorizationService = authorizationService;
            _hub = hub;
        }

        /// <summary>
        /// Lấy tất cả workspace mà user sở hữu, được mời trực tiếp hoặc được cấp quyền qua team
        /// </summary>
        [HttpGet]
        public async Task<IActionResult> GetMyWorkspaces()
        {
            var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (!Guid.TryParse(userId, out Guid parsedUserId))
                return Unauthorized(new { statusCode = 401, message = "Vui lòng đăng nhập." });

            var workspaces = await _context.Workspaces
                .AsNoTracking()
                .Where(workspace =>
                    !workspace.IsDeleted &&
                    (workspace.OwnerId == parsedUserId ||
                     workspace.Members.Any(member => member.UserId == parsedUserId && member.IsActive) ||
                     workspace.TeamAccesses.Any(access =>
                         access.Department.IsActive &&
                         !access.Department.IsDeleted &&
                         access.Department.DepartmentMembers.Any(member =>
                             member.UserId == parsedUserId &&
                             member.LeftAt == null &&
                             member.User.IsActive &&
                             !member.User.IsDeleted))))
                .Select(workspace => new
                {
                    workspace.Id,
                    workspace.Name,
                    workspace.Slug,
                    workspace.Logo,
                    workspace.Timezone,
                    WorkspaceRole = workspace.OwnerId == parsedUserId
                        ? "OWNER"
                        : workspace.Members
                            .Where(member => member.UserId == parsedUserId && member.IsActive)
                            .Select(member => member.WorkspaceRole)
                            .FirstOrDefault() ?? "MEMBER",
                    AccessSource = workspace.OwnerId == parsedUserId
                        ? "OWNER"
                        : workspace.Members.Any(member => member.UserId == parsedUserId && member.IsActive)
                            ? "DIRECT"
                            : "TEAM",
                    workspace.OwnerId,
                    OwnerName = workspace.Owner.FullName,
                    OwnerEmail = workspace.Owner.Email,
                    OwnerAvatarUrl = workspace.Owner.AvatarUrl,
                    MemberCount = workspace.Members.Count(member => member.IsActive),
                    ProjectCount = workspace.Projects.Count(project => !project.IsDeleted),
                    workspace.CreatedAt,
                    workspace.UpdatedAt
                })
                .OrderByDescending(workspace => workspace.UpdatedAt)
                .ToListAsync();

            return Ok(new { statusCode = 200, message = "Success", data = workspaces });
        }

        /// <summary>
        /// Tạo workspace mới
        /// </summary>
        [HttpPost]
        public async Task<IActionResult> Create([FromBody] CreateWorkspaceRequest request)
        {
            var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (!Guid.TryParse(userId, out Guid parsedUserId))
                return Unauthorized(new { statusCode = 401, message = "Vui lòng đăng nhập." });

            // Validate slug uniqueness
            var slugExists = await _context.Workspaces.AnyAsync(w => w.Slug == request.Slug);
            if (slugExists)
                return BadRequest(new { statusCode = 400, message = "Slug đã tồn tại. Vui lòng chọn tên khác." });

            var workspace = new Workspace
            {
                Id = Guid.NewGuid(),
                Name = request.Name,
                Slug = request.Slug.ToLower().Trim(),
                OwnerId = parsedUserId,
                Timezone = request.Timezone ?? "Asia/Ho_Chi_Minh",
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };

            _context.Workspaces.Add(workspace);

            // Auto-add creator as OWNER
            _context.WorkspaceMembers.Add(new WorkspaceMember
            {
                WorkspaceId = workspace.Id,
                UserId = parsedUserId,
                WorkspaceRole = "OWNER",
                JoinedAt = DateTime.UtcNow,
                IsActive = true
            });

            await _context.SaveChangesAsync();

            return CreatedAtAction(nameof(GetMyWorkspaces), null,
                new { statusCode = 201, message = "Tạo workspace thành công.", data = new { workspace.Id, workspace.Name, workspace.Slug } });
        }

        /// <summary>
        /// Lấy thông tin workspace theo slug
        /// </summary>
        [HttpGet("{slug}")]
        public async Task<IActionResult> GetBySlug(string slug)
        {
            var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (!Guid.TryParse(userId, out Guid parsedUserId))
                return Unauthorized(new { statusCode = 401, message = "Vui lòng đăng nhập." });

            var workspace = await _context.Workspaces
                .AsNoTracking()
                .Where(w => w.Slug == slug)
                .Select(w => new
                {
                    w.Id,
                    w.Name,
                    w.Slug,
                    w.Logo,
                    w.Timezone,
                    OwnerName = w.Owner.FullName,
                    MemberCount = w.Members.Count(m => m.IsActive),
                    ProjectCount = w.Projects.Count(p => !p.IsDeleted),
                    w.CreatedAt
                })
                .FirstOrDefaultAsync();

            if (workspace == null)
                return NotFound(new { statusCode = 404, message = "Workspace không tồn tại." });

            // Check membership
            var authorization = await _authorizationService.AuthorizeWorkspaceAsync(
                parsedUserId,
                workspace.Id,
                ResourcePermissionCodes.WorkspaceRead);

            if (!authorization.Succeeded)
                return StatusCode(403, new { statusCode = 403, message = "Bạn không phải thành viên của workspace này." });

            return Ok(new { statusCode = 200, message = "Success", data = workspace });
        }

        /// <summary>
        /// Thêm thành viên vào workspace
        /// </summary>
        [HttpPost("{workspaceId}/members")]
        public async Task<IActionResult> AddMember(Guid workspaceId, [FromBody] AddWorkspaceMemberRequest request)
        {
            var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (!Guid.TryParse(userId, out Guid parsedUserId))
                return Unauthorized(new { statusCode = 401, message = "Vui lòng đăng nhập." });

            // Check requester is OWNER or ADMIN
            var authorization = await _authorizationService.AuthorizeWorkspaceAsync(
                parsedUserId,
                workspaceId,
                ResourcePermissionCodes.WorkspaceManage);

            if (!authorization.Succeeded)
                return StatusCode(403, new { statusCode = 403, message = "Bạn không có quyền thêm thành viên." });

            // Check user exists
            var targetUser = await _context.Users.FirstOrDefaultAsync(u => u.Email == request.Email);
            if (targetUser == null)
                return BadRequest(new { statusCode = 400, message = "Không tìm thấy người dùng với email này." });

            // Check not already member
            var existing = await _context.WorkspaceMembers
                .FirstOrDefaultAsync(wm => wm.WorkspaceId == workspaceId && wm.UserId == targetUser.Id);

            if (existing != null)
            {
                if (existing.IsActive)
                    return BadRequest(new { statusCode = 400, message = "Người dùng đã là thành viên." });
                existing.IsActive = true;
                existing.WorkspaceRole = request.Role ?? "MEMBER";
            }
            else
            {
                _context.WorkspaceMembers.Add(new WorkspaceMember
                {
                    WorkspaceId = workspaceId,
                    UserId = targetUser.Id,
                    WorkspaceRole = request.Role ?? "MEMBER",
                    JoinedAt = DateTime.UtcNow,
                    IsActive = true
                });
            }

            await _context.SaveChangesAsync();
            if (_hub != null)
            {
                await _hub.PublishWorkspaceEntityChangedAsync(
                    workspaceId,
                    "WorkspaceMember",
                    "created",
                    targetUser.Id,
                    new { userId = targetUser.Id, targetUser.FullName, targetUser.Email, role = request.Role ?? "MEMBER" });
            }

            return Ok(new { statusCode = 200, message = "Thêm thành viên thành công." });
        }

        /// <summary>
        /// Lấy danh sách thành viên workspace
        /// </summary>
        [HttpGet("{workspaceId}/members")]
        public async Task<IActionResult> GetMembers(Guid workspaceId)
        {
            var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (!Guid.TryParse(userId, out var parsedUserId))
                return Unauthorized(new { statusCode = 401, message = "Authentication is required." });

            var authorization = await _authorizationService.AuthorizeWorkspaceAsync(
                parsedUserId,
                workspaceId,
                ResourcePermissionCodes.WorkspaceRead);
            if (!authorization.Succeeded)
                return StatusCode(403, new { statusCode = 403, message = "Active workspace membership is required." });

            var directMembers = await _context.WorkspaceMembers
                .AsNoTracking()
                .Where(wm => wm.WorkspaceId == workspaceId && wm.IsActive)
                .Select(wm => new WorkspaceMemberListItem
                {
                    UserId = wm.UserId,
                    FullName = wm.User.FullName,
                    Email = wm.User.Email,
                    AvatarUrl = wm.User.AvatarUrl,
                    WorkspaceRole = wm.WorkspaceRole,
                    JoinedAt = wm.JoinedAt,
                    AccessSource = "DIRECT"
                })
                .ToListAsync();

            var teamMembers = await _context.WorkspaceDepartmentAccesses
                .AsNoTracking()
                .Where(access =>
                    access.WorkspaceId == workspaceId &&
                    access.Department.IsActive &&
                    !access.Department.IsDeleted)
                .SelectMany(
                    access => access.Department.DepartmentMembers.Where(member =>
                        member.LeftAt == null &&
                        member.User.IsActive &&
                        !member.User.IsDeleted),
                    (access, member) => new WorkspaceMemberListItem
                    {
                        UserId = member.UserId,
                        FullName = member.User.FullName,
                        Email = member.User.Email,
                        AvatarUrl = member.User.AvatarUrl,
                        WorkspaceRole = "MEMBER",
                        JoinedAt = access.GrantedAt,
                        AccessSource = "TEAM",
                        TeamId = access.DepartmentId,
                        TeamName = access.Department.Name
                    })
                .ToListAsync();

            var members = directMembers
                .Concat(teamMembers)
                .GroupBy(member => member.UserId)
                .Select(group => group
                    .OrderBy(member => member.AccessSource == "DIRECT" ? 0 : 1)
                    .First())
                .OrderBy(member => member.FullName)
                .ToList();

            return Ok(new { statusCode = 200, message = "Success", data = members });
        }

        [HttpGet("{workspaceId}/teams")]
        public async Task<IActionResult> GetTeams(Guid workspaceId)
        {
            if (!Guid.TryParse(User.FindFirst(ClaimTypes.NameIdentifier)?.Value, out var userId))
                return Unauthorized(new { statusCode = 401, message = "Authentication is required." });

            var authorization = await _authorizationService.AuthorizeWorkspaceAsync(
                userId,
                workspaceId,
                ResourcePermissionCodes.WorkspaceRead);
            if (!authorization.Succeeded)
                return StatusCode(403, new { statusCode = 403, message = "Workspace access is required." });

            var teams = await _context.WorkspaceDepartmentAccesses
                .AsNoTracking()
                .Where(access => access.WorkspaceId == workspaceId)
                .OrderBy(access => access.Department.Name)
                .Select(access => new
                {
                    access.DepartmentId,
                    access.Department.Name,
                    access.Department.Description,
                    MemberCount = access.Department.DepartmentMembers.Count(member => member.LeftAt == null),
                    access.GrantedAt,
                    access.GrantedByUserId
                })
                .ToListAsync();

            return Ok(new { statusCode = 200, message = "Success", data = teams });
        }

        [HttpPost("{workspaceId}/teams/{departmentId}")]
        public async Task<IActionResult> GrantTeamAccess(Guid workspaceId, Guid departmentId)
        {
            if (!Guid.TryParse(User.FindFirst(ClaimTypes.NameIdentifier)?.Value, out var userId))
                return Unauthorized(new { statusCode = 401, message = "Authentication is required." });

            var authorization = await _authorizationService.AuthorizeWorkspaceAsync(
                userId,
                workspaceId,
                ResourcePermissionCodes.WorkspaceManage);
            if (!authorization.Succeeded)
                return StatusCode(403, new { statusCode = 403, message = "Workspace management permission is required." });

            var teamExists = await _context.Departments.AnyAsync(department =>
                department.Id == departmentId && department.IsActive && !department.IsDeleted);
            if (!teamExists)
                return NotFound(new { statusCode = 404, message = "Team does not exist or is inactive." });

            var accessExists = await _context.WorkspaceDepartmentAccesses.AnyAsync(access =>
                access.WorkspaceId == workspaceId && access.DepartmentId == departmentId);
            if (!accessExists)
            {
                _context.WorkspaceDepartmentAccesses.Add(new WorkspaceDepartmentAccess
                {
                    WorkspaceId = workspaceId,
                    DepartmentId = departmentId,
                    GrantedByUserId = userId,
                    GrantedAt = DateTime.UtcNow
                });
                await _context.SaveChangesAsync();
            }

            return Ok(new { statusCode = 200, message = "Team access granted.", data = new { workspaceId, departmentId } });
        }

        [HttpDelete("{workspaceId}/teams/{departmentId}")]
        public async Task<IActionResult> RevokeTeamAccess(Guid workspaceId, Guid departmentId)
        {
            if (!Guid.TryParse(User.FindFirst(ClaimTypes.NameIdentifier)?.Value, out var userId))
                return Unauthorized(new { statusCode = 401, message = "Authentication is required." });

            var authorization = await _authorizationService.AuthorizeWorkspaceAsync(
                userId,
                workspaceId,
                ResourcePermissionCodes.WorkspaceManage);
            if (!authorization.Succeeded)
                return StatusCode(403, new { statusCode = 403, message = "Workspace management permission is required." });

            var access = await _context.WorkspaceDepartmentAccesses.FirstOrDefaultAsync(item =>
                item.WorkspaceId == workspaceId && item.DepartmentId == departmentId);
            if (access == null)
                return NoContent();

            _context.WorkspaceDepartmentAccesses.Remove(access);
            await _context.SaveChangesAsync();
            return NoContent();
        }

        /// <summary>
        /// Cập nhật thông tin workspace
        /// </summary>
        [HttpPut("{workspaceId}")]
        public async Task<IActionResult> UpdateWorkspace(Guid workspaceId, [FromBody] UpdateWorkspaceRequest request)
        {
            var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (!Guid.TryParse(userId, out Guid parsedUserId))
                return Unauthorized(new { statusCode = 401, message = "Vui lòng đăng nhập." });

            var authorization = await _authorizationService.AuthorizeWorkspaceAsync(
                parsedUserId,
                workspaceId,
                ResourcePermissionCodes.WorkspaceManage);

            if (!authorization.Succeeded)
                return StatusCode(403, new { statusCode = 403, message = "Bạn không có quyền cập nhật workspace này." });

            var workspace = await _context.Workspaces.FindAsync(workspaceId);
            if (workspace == null || workspace.IsDeleted)
                return NotFound(new { statusCode = 404, message = "Workspace không tồn tại." });

            if (!string.IsNullOrEmpty(request.Slug) && request.Slug != workspace.Slug)
            {
                var slugExists = await _context.Workspaces.AnyAsync(w => w.Slug == request.Slug && w.Id != workspaceId);
                if (slugExists) return BadRequest(new { statusCode = 400, message = "Slug đã tồn tại." });
                workspace.Slug = request.Slug.ToLower().Trim();
            }

            if (!string.IsNullOrEmpty(request.Name)) workspace.Name = request.Name;
            if (request.Logo != null) workspace.Logo = request.Logo;
            if (!string.IsNullOrEmpty(request.Timezone)) workspace.Timezone = request.Timezone;

            workspace.UpdatedAt = DateTime.UtcNow;
            await _context.SaveChangesAsync();
            if (_hub != null)
            {
                await _hub.PublishWorkspaceEntityChangedAsync(
                    workspaceId,
                    "Workspace",
                    "updated",
                    workspace.Id,
                    new { workspace.Id, workspace.Name, workspace.Slug, workspace.Logo, workspace.Timezone });
            }

            return Ok(new { statusCode = 200, message = "Cập nhật thành công.", data = workspace });
        }

        /// <summary>
        /// Xóa workspace
        /// </summary>
        [HttpDelete("{workspaceId}")]
        public async Task<IActionResult> DeleteWorkspace(Guid workspaceId)
        {
            var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (!Guid.TryParse(userId, out Guid parsedUserId))
                return Unauthorized(new { statusCode = 401, message = "Vui lòng đăng nhập." });

            var authorization = await _authorizationService.AuthorizeWorkspaceAsync(
                parsedUserId,
                workspaceId,
                ResourcePermissionCodes.WorkspaceDelete);

            if (!authorization.Succeeded)
                return StatusCode(403, new { statusCode = 403, message = "Chỉ OWNER mới có thể xóa workspace." });

            var workspace = await _context.Workspaces.FindAsync(workspaceId);
            if (workspace == null)
                return NotFound(new { statusCode = 404, message = "Workspace không tồn tại." });

            workspace.IsDeleted = true;
            workspace.UpdatedAt = DateTime.UtcNow;
            await _context.SaveChangesAsync();
            if (_hub != null)
            {
                await _hub.PublishWorkspaceEntityChangedAsync(
                    workspaceId,
                    "Workspace",
                    "deleted",
                    workspace.Id);
            }

            return Ok(new { statusCode = 200, message = "Xóa workspace thành công." });
        }

        /// <summary>
        /// Cập nhật vai trò thành viên
        /// </summary>
        [HttpPut("{workspaceId}/members/{memberId}")]
        public async Task<IActionResult> UpdateMemberRole(Guid workspaceId, Guid memberId, [FromBody] UpdateMemberRoleRequest request)
        {
            var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (!Guid.TryParse(userId, out Guid parsedUserId))
                return Unauthorized(new { statusCode = 401, message = "Vui lòng đăng nhập." });

            var authorization = await _authorizationService.AuthorizeWorkspaceAsync(
                parsedUserId,
                workspaceId,
                ResourcePermissionCodes.WorkspaceManage);

            if (!authorization.Succeeded)
                return StatusCode(403, new { statusCode = 403, message = "Bạn không có quyền." });

            var targetMember = await _context.WorkspaceMembers
                .FirstOrDefaultAsync(wm => wm.WorkspaceId == workspaceId && wm.UserId == memberId && wm.IsActive);

            if (targetMember == null)
                return NotFound(new { statusCode = 404, message = "Thành viên không tồn tại." });
            
            // Limit: ADMIN cannot modify OWNER
            if (ResourcePermissionPolicy.NormalizeWorkspaceRole(authorization.WorkspaceRole) == "admin" &&
                ResourcePermissionPolicy.NormalizeWorkspaceRole(targetMember.WorkspaceRole) == "owner")
                return StatusCode(403, new { statusCode = 403, message = "Admin không thể sửa quyền của Owner." });

            targetMember.WorkspaceRole = request.Role.ToUpper();
            await _context.SaveChangesAsync();
            if (_hub != null)
            {
                await _hub.PublishWorkspaceEntityChangedAsync(
                    workspaceId,
                    "WorkspaceMember",
                    "updated",
                    memberId,
                    new { userId = memberId, role = targetMember.WorkspaceRole });
            }

            return Ok(new { statusCode = 200, message = "Cập nhật vai trò thành công." });
        }

        /// <summary>
        /// Xóa thành viên
        /// </summary>
        [HttpDelete("{workspaceId}/members/{memberId}")]
        public async Task<IActionResult> RemoveMember(Guid workspaceId, Guid memberId)
        {
            var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (!Guid.TryParse(userId, out Guid parsedUserId))
                return Unauthorized(new { statusCode = 401, message = "Vui lòng đăng nhập." });

            var authorization = await _authorizationService.AuthorizeWorkspaceAsync(
                parsedUserId,
                workspaceId,
                ResourcePermissionCodes.WorkspaceRead);

            if (!authorization.Succeeded)
                return NotFound(new { statusCode = 404, message = "Workspace không tồn tại." });

            // People can remove themselves, or OWNER/ADMIN can remove others.
            var requesterRole = ResourcePermissionPolicy.NormalizeWorkspaceRole(authorization.WorkspaceRole);
            if (parsedUserId != memberId && requesterRole is not ("owner" or "admin"))
                return StatusCode(403, new { statusCode = 403, message = "Bạn không có quyền xóa thành viên này." });

            var targetMember = await _context.WorkspaceMembers
                .FirstOrDefaultAsync(wm => wm.WorkspaceId == workspaceId && wm.UserId == memberId && wm.IsActive);

            if (targetMember == null)
                return NotFound(new { statusCode = 404, message = "Thành viên không tồn tại." });

            // Prevent removing the last OWNER
            if (targetMember.WorkspaceRole == "OWNER")
            {
                var ownerCount = await _context.WorkspaceMembers.CountAsync(wm => wm.WorkspaceId == workspaceId && wm.WorkspaceRole == "OWNER" && wm.IsActive);
                if (ownerCount <= 1)
                    return BadRequest(new { statusCode = 400, message = "Không thể xóa Owner duy nhất. Cần chỉ định Owner mới trước." });
            }

            targetMember.IsActive = false;
            await _context.SaveChangesAsync();
            if (_hub != null)
            {
                await _hub.PublishWorkspaceEntityChangedAsync(
                    workspaceId,
                    "WorkspaceMember",
                    "deleted",
                    memberId,
                    new { userId = memberId });
            }

            return Ok(new { statusCode = 200, message = "Xóa thành viên thành công." });
        }
    }

    public class CreateWorkspaceRequest
    {
        public string Name { get; set; } = string.Empty;
        public string Slug { get; set; } = string.Empty;
        public string? Timezone { get; set; }
    }

    public class AddWorkspaceMemberRequest
    {
        public string Email { get; set; } = string.Empty;
        public string? Role { get; set; }
    }

    public class UpdateWorkspaceRequest
    {
        public string? Name { get; set; }
        public string? Slug { get; set; }
        public string? Logo { get; set; }
        public string? Timezone { get; set; }
    }

    public class UpdateMemberRoleRequest
    {
        public string Role { get; set; } = string.Empty;
    }

    public sealed class WorkspaceMemberListItem
    {
        public Guid UserId { get; set; }
        public string FullName { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public string? AvatarUrl { get; set; }
        public string WorkspaceRole { get; set; } = "MEMBER";
        public DateTime JoinedAt { get; set; }
        public string AccessSource { get; set; } = "DIRECT";
        public Guid? TeamId { get; set; }
        public string? TeamName { get; set; }
    }
}
