using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;
using TaskManagement.Application.Common;
using TaskManagement.Application.Interfaces;
using TaskManagement.Infrastructure.Data;
using TaskManagement.Domain.Entities;

namespace TaskManagement.API.Controllers
{
    [ApiController]
    [Route("api/workspaces")]
    [Authorize]
    public class WorkspacesController : ControllerBase
    {
        private readonly ApplicationDbContext _context;
        private readonly IResourceAuthorizationService _authorizationService;

        public WorkspacesController(
            ApplicationDbContext context,
            IResourceAuthorizationService authorizationService)
        {
            _context = context;
            _authorizationService = authorizationService;
        }

        /// <summary>
        /// Lấy tất cả workspaces mà user hiện tại là thành viên
        /// </summary>
        [HttpGet]
        public async Task<IActionResult> GetMyWorkspaces()
        {
            var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (!Guid.TryParse(userId, out Guid parsedUserId))
                return Unauthorized(new { statusCode = 401, message = "Vui lòng đăng nhập." });

            var workspaces = await _context.WorkspaceMembers
                .AsNoTracking()
                .Where(wm => wm.UserId == parsedUserId && wm.IsActive)
                .Select(wm => new
                {
                    wm.Workspace.Id,
                    wm.Workspace.Name,
                    wm.Workspace.Slug,
                    wm.Workspace.Logo,
                    wm.Workspace.Timezone,
                    wm.WorkspaceRole,
                    OwnerName = wm.Workspace.Owner.FullName,
                    MemberCount = wm.Workspace.Members.Count(m => m.IsActive),
                    ProjectCount = wm.Workspace.Projects.Count(p => !p.IsDeleted),
                    wm.Workspace.CreatedAt
                })
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

            var members = await _context.WorkspaceMembers
                .AsNoTracking()
                .Where(wm => wm.WorkspaceId == workspaceId && wm.IsActive)
                .Select(wm => new
                {
                    wm.UserId,
                    wm.User.FullName,
                    wm.User.Email,
                    wm.User.AvatarUrl,
                    wm.WorkspaceRole,
                    wm.JoinedAt
                })
                .OrderBy(m => m.FullName)
                .ToListAsync();

            return Ok(new { statusCode = 200, message = "Success", data = members });
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
}
