using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;
using TaskManagement.Infrastructure.Data;

namespace TaskManagement.API.Filters
{
    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Method, AllowMultiple = true, Inherited = true)]
    public class RequirePermissionAttribute : TypeFilterAttribute
    {
        public RequirePermissionAttribute(string permissionCode) : base(typeof(RequirePermissionFilter))
        {
            Arguments = new object[] { permissionCode };
        }
    }

    public class RequirePermissionFilter : IAsyncActionFilter
    {
        private readonly string _permissionCode;
        private readonly ApplicationDbContext _dbContext;

        public RequirePermissionFilter(string permissionCode, ApplicationDbContext dbContext)
        {
            _permissionCode = permissionCode?.Trim().ToLowerInvariant() ?? string.Empty;
            _dbContext = dbContext;
        }

        public async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
        {
            // 1. Get UserId
            var userIdString = context.HttpContext.User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrEmpty(userIdString) || !Guid.TryParse(userIdString, out Guid userId))
            {
                context.Result = new UnauthorizedObjectResult(new { statusCode = 401, message = "Unauthorized. JWT is missing or invalid." });
                return;
            }

            // System roles are permission inputs, never an implicit bypass.
            var isActiveUser = await _dbContext.Users
                .AsNoTracking()
                .AnyAsync(user => user.Id == userId && user.IsActive && !user.IsDeleted);

            if (!isActiveUser)
            {
                context.Result = new ObjectResult(new { statusCode = 403, message = "Forbidden." }) { StatusCode = 403 };
                return;
            }

            // Workspace-scoped endpoints require an active membership in addition
            // to the assigned role permission.
            var workspaceIdString = context.RouteData.Values["workspaceId"]?.ToString();
            if (Guid.TryParse(workspaceIdString, out var workspaceId))
            {
                var isActiveWorkspaceMember = await _dbContext.WorkspaceMembers
                    .AsNoTracking()
                    .AnyAsync(member => member.WorkspaceId == workspaceId &&
                                        member.UserId == userId &&
                                        member.IsActive);

                if (!isActiveWorkspaceMember)
                {
                    context.Result = new ObjectResult(new { statusCode = 403, message = "Forbidden." }) { StatusCode = 403 };
                    return;
                }
            }

            // 3. Gather User's Role Names
            var roleNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            // 3.1. System Roles
            var systemRoleNames = await _dbContext.UserRoles
                .AsNoTracking()
                .Where(ur => ur.UserId == userId)
                .Select(ur => ur.Role.Name)
                .ToListAsync();

            foreach (var r in systemRoleNames)
            {
                if (!string.IsNullOrWhiteSpace(r)) roleNames.Add(r);
            }

            // 3.2. Project Role (if applicable)
            var projectIdString = context.RouteData.Values["projectId"]?.ToString()
                                  ?? context.RouteData.Values["id"]?.ToString();

            if (!string.IsNullOrEmpty(projectIdString) && Guid.TryParse(projectIdString, out Guid projectId))
            {
                var member = await _dbContext.ProjectMembers
                    .AsNoTracking()
                    .FirstOrDefaultAsync(pm => pm.ProjectId == projectId && pm.UserId == userId);

                if (member != null && member.Status && !string.IsNullOrWhiteSpace(member.ProjectRole))
                {
                    roleNames.Add(member.ProjectRole);
                }
            }

            if (!roleNames.Any())
            {
                context.Result = new ObjectResult(new { statusCode = 403, message = $"Forbidden. You lack the permission '{_permissionCode}'." }) { StatusCode = 403 };
                return;
            }

            // 4. Check if any of these roles have the requested permission
            var hasPermission = await _dbContext.Roles
                .AsNoTracking()
                .Where(r => roleNames.Contains(r.Name))
                .SelectMany(r => r.RolePermissions)
                .AnyAsync(rp => rp.Permission.Code == _permissionCode);

            if (!hasPermission)
            {
                context.Result = new ObjectResult(new { statusCode = 403, message = $"Forbidden. You lack the permission '{_permissionCode}'." }) { StatusCode = 403 };
                return;
            }

            await next();
        }
    }
}
