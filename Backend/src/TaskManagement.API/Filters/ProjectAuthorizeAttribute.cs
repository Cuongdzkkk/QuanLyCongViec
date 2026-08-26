using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using System.Security.Claims;
using TaskManagement.Application.Common;
using TaskManagement.Application.Interfaces;

namespace TaskManagement.API.Filters
{
    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Method, AllowMultiple = true, Inherited = true)]
    public class ProjectAuthorizeAttribute : TypeFilterAttribute
    {
        public ProjectAuthorizeAttribute(string roles) : base(typeof(ProjectAuthorizeFilter))
        {
            Arguments = new object[] { roles };
        }
    }

    public class ProjectAuthorizeFilter : IAsyncActionFilter
    {
        private readonly string _authorizationRule;
        private readonly string[] _allowedRoles;
        private readonly IResourceAuthorizationService _authorizationService;

        public ProjectAuthorizeFilter(string roles, IResourceAuthorizationService authorizationService)
        {
            _authorizationRule = roles.Trim();
            _allowedRoles = roles
                .Split(',', StringSplitOptions.RemoveEmptyEntries)
                .Select(ProjectExecutionRuleHelper.NormalizeProjectRole)
                .Where(r => !string.IsNullOrWhiteSpace(r))
                .ToArray();
            _authorizationService = authorizationService;
        }

        public async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
        {
            // 1. Get UserId from JWT
            var userIdString = context.HttpContext.User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrEmpty(userIdString) || !Guid.TryParse(userIdString, out Guid userId))
            {
                context.Result = new UnauthorizedObjectResult(new { statusCode = 401, message = "Unauthorized. JWT is missing or invalid." });
                return;
            }

            // 2. Extract ProjectId from Route Data
            // Assumes route is something like /api/projects/{projectId}/...
            var projectIdString =
                context.RouteData.Values["projectId"]?.ToString()
                ?? context.RouteData.Values["id"]?.ToString();
            if (string.IsNullOrEmpty(projectIdString) || !Guid.TryParse(projectIdString, out Guid projectId))
            {
                // Nếu API không cung cấp projectId trong url, chặn ngay lập tức hoặc bỏ qua tuỳ thiết kế.
                // Theo BusinessLogic, các API liên quan đến project BAO GIỜ cũng có projectId
                context.Result = new BadRequestObjectResult(new { statusCode = 400, message = "Missing projectId in route." });
                return;
            }

            var permissionCode = ResourcePermissionPolicy.IsKnownPermission(_authorizationRule)
                ? _authorizationRule
                : ResourcePermissionCodes.ProjectRead;
            var authorization = await _authorizationService.AuthorizeProjectAsync(userId, projectId, permissionCode);
            if (!authorization.Succeeded)
            {
                context.Result = new ObjectResult(new { statusCode = 403, message = "Forbidden. Active workspace/project membership and permission are required." })
                {
                    StatusCode = 403
                };
                return;
            }

            var normalizedMemberRole = ProjectExecutionRuleHelper.NormalizeProjectRole(authorization.ProjectRole);

            if (!ResourcePermissionPolicy.IsKnownPermission(_authorizationRule) &&
                _allowedRoles.Length > 0 &&
                !_allowedRoles.Contains(normalizedMemberRole, StringComparer.OrdinalIgnoreCase))
            {
                context.Result = new ObjectResult(new { statusCode = 403, message = "Forbidden. Project permission is required." })
                {
                    StatusCode = 403
                };
                return;
            }

            // 6. Special Case for Data Modification using HTTP methods: POST, PUT, DELETE for Guest/Stakeholder
            var httpMethod = context.HttpContext.Request.Method;
            var isWriteMethod = httpMethod == HttpMethod.Post.Method || 
                                httpMethod == HttpMethod.Put.Method || 
                                httpMethod == HttpMethod.Patch.Method ||
                                httpMethod == HttpMethod.Delete.Method;

            if (isWriteMethod && normalizedMemberRole is "guest" or "stakeholder")
            {
                context.Result = new ObjectResult(new { statusCode = 403, message = "Forbidden. Guests and Stakeholders cannot modify project data." })
                {
                    StatusCode = 403
                };
                return;
            }

            // Save project member info into HttpContext items for later usage in controllers if needed
            context.HttpContext.Items["ProjectRole"] = authorization.ProjectRole;

            // Authorized, continue
            await next();
        }
    }
}
