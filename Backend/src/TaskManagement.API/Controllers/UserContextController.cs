using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using TaskManagement.Application.DTOs.Common;
using TaskManagement.Infrastructure.Data;
using TaskManagement.Application.Common;
using TaskManagement.Application.Interfaces;

namespace TaskManagement.API.Controllers
{
    [ApiController]
    [Route("api/auth/context")]
    [Authorize]
    public class UserContextController : ControllerBase
    {
        private readonly ApplicationDbContext _context;
        private readonly IResourceAuthorizationService _authorization;

        public UserContextController(
            ApplicationDbContext context,
            IResourceAuthorizationService authorization)
        {
            _context = context;
            _authorization = authorization;
        }

        [HttpGet]
        public async Task<IActionResult> GetContext()
        {
            var userId = GetUserId();
            var user = await _context.Users
                .AsNoTracking()
                .Where(item => item.Id == userId && item.IsActive && !item.IsDeleted)
                .Select(item => new
                {
                    item.Id,
                    item.Email,
                    item.FullName,
                    item.AvatarUrl,
                    item.CoverUrl,
                    item.JobTitle,
                    item.Timezone,
                    item.OrganizationId
                })
                .FirstOrDefaultAsync();

            if (user == null)
            {
                return Unauthorized(ApiResponse<object>.Error("User context is no longer valid."));
            }

            var roles = await _context.UserRoles
                .AsNoTracking()
                .Where(item => item.UserId == userId)
                .Select(item => item.Role.Name)
                .Distinct()
                .OrderBy(item => item)
                .ToListAsync();

            var permissions = await _context.RolePermissions
                .AsNoTracking()
                .Where(item => item.Role.UserRoles.Any(userRole => userRole.UserId == userId))
                .Select(item => item.Permission.Code)
                .Distinct()
                .OrderBy(item => item)
                .ToListAsync();

            var workspaces = await _context.WorkspaceMembers
                .AsNoTracking()
                .Where(item => item.UserId == userId && item.IsActive && !item.Workspace.IsDeleted)
                .OrderByDescending(item => item.WorkspaceRole == "OWNER")
                .ThenBy(item => item.Workspace.Name)
                .Select(item => new
                {
                    item.Workspace.Id,
                    item.Workspace.Slug,
                    item.Workspace.Name,
                    item.Workspace.Logo,
                    item.Workspace.Timezone,
                    role = item.WorkspaceRole
                })
                .ToListAsync();

            var accessibleProjectIds = await _authorization.GetAccessibleProjectIdsAsync(userId);
            var projectQuery = _context.Projects
                .AsNoTracking()
                .Where(item => accessibleProjectIds.Contains(item.Id) &&
                    !item.IsDeleted &&
                    !item.IsArchived &&
                    item.Status);
            var projects = await projectQuery
                .OrderBy(item => item.Name)
                .Select(item => new
                {
                    item.Id,
                    item.WorkspaceId,
                    item.Name,
                    key = item.Identifier,
                    item.CoverUrl,
                    item.CoverAltText,
                    item.NetworkType
                })
                .Take(50)
                .ToListAsync();

            return Ok(ApiResponse<object>.Success(new
            {
                user,
                roles,
                permissions,
                workspaces,
                currentWorkspace = workspaces.FirstOrDefault(),
                projects,
                authenticated = true,
                serverTime = DateTime.UtcNow
            }));
        }

        private Guid GetUserId()
        {
            var raw = User.FindFirstValue(ClaimTypes.NameIdentifier);
            return Guid.TryParse(raw, out var id) ? id : Guid.Empty;
        }
    }
}
