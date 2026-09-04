using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using System.Security.Claims;
using TaskManagement.Application.Interfaces;

namespace TaskManagement.API.Filters;

[AttributeUsage(AttributeTargets.Method, AllowMultiple = false, Inherited = true)]
public sealed class ProjectRestoreAuthorizeAttribute : TypeFilterAttribute
{
    public ProjectRestoreAuthorizeAttribute() : base(typeof(ProjectRestoreAuthorizeFilter))
    {
    }
}

public sealed class ProjectRestoreAuthorizeFilter : IAsyncActionFilter
{
    private readonly IResourceAuthorizationService _authorizationService;

    public ProjectRestoreAuthorizeFilter(IResourceAuthorizationService authorizationService)
    {
        _authorizationService = authorizationService;
    }

    public async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
    {
        var userIdString = context.HttpContext.User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (!Guid.TryParse(userIdString, out var userId))
        {
            context.Result = new UnauthorizedObjectResult(new
            {
                statusCode = 401,
                message = "Unauthorized. JWT is missing or invalid."
            });
            return;
        }

        var projectIdString = context.RouteData.Values["projectId"]?.ToString();
        if (!Guid.TryParse(projectIdString, out var projectId))
        {
            context.Result = new BadRequestObjectResult(new
            {
                statusCode = 400,
                message = "Missing projectId in route."
            });
            return;
        }

        var authorization = await _authorizationService.AuthorizeProjectForRestoreAsync(userId, projectId);
        if (!authorization.Succeeded)
        {
            context.Result = new ObjectResult(new
            {
                statusCode = 403,
                message = "Forbidden. Active project management membership is required."
            })
            {
                StatusCode = 403
            };
            return;
        }

        context.HttpContext.Items["ProjectRole"] = authorization.ProjectRole;
        await next();
    }
}
