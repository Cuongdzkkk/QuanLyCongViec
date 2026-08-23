using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;
using System.Text.Json;
using TaskManagement.Application.Common;
using TaskManagement.Infrastructure.Data;

namespace TaskManagement.API.Hubs
{
    [Authorize]
    public class KanbanHub : Hub
    {
        public const string Route = "/kanban-hub";

        private static readonly string[] SystemRoles = { "superadmin", "admin", "system admin", "systemadmin" };
        private readonly ApplicationDbContext _context;

        public KanbanHub(ApplicationDbContext context)
        {
            _context = context;
        }

        public override async Task OnConnectedAsync()
        {
            var userId = Context.User?.FindFirstValue(ClaimTypes.NameIdentifier);
            if (Guid.TryParse(userId, out var parsedUserId))
            {
                await Groups.AddToGroupAsync(Context.ConnectionId, $"user:{parsedUserId}");
            }

            await base.OnConnectedAsync();
        }

        public async Task JoinProjectGroup(string projectId)
        {
            var projectGuid = await RequireProjectAccessAsync(projectId);
            await Groups.AddToGroupAsync(Context.ConnectionId, projectGuid.ToString());
        }

        public async Task LeaveProjectGroup(string projectId)
        {
            if (Guid.TryParse(projectId, out var projectGuid))
            {
                await Groups.RemoveFromGroupAsync(Context.ConnectionId, projectGuid.ToString());
            }
        }

        public async Task JoinWorkspaceGroup(string workspaceId)
        {
            var workspaceGuid = await RequireWorkspaceAccessAsync(workspaceId);
            await Groups.AddToGroupAsync(Context.ConnectionId, $"workspace:{workspaceGuid}");
        }

        public async Task LeaveWorkspaceGroup(string workspaceId)
        {
            if (Guid.TryParse(workspaceId, out var workspaceGuid))
            {
                await Groups.RemoveFromGroupAsync(Context.ConnectionId, $"workspace:{workspaceGuid}");
            }
        }

        public async Task BroadcastProjectEvent(string projectId, string eventType, string? payloadJson)
        {
            var projectGuid = await RequireProjectAccessAsync(projectId);
            object? payload = null;

            if (!string.IsNullOrWhiteSpace(payloadJson))
            {
                try
                {
                    payload = JsonSerializer.Deserialize<object>(payloadJson);
                }
                catch
                {
                    payload = payloadJson;
                }
            }

            await Clients.OthersInGroup(projectGuid.ToString()).SendAsync("ProjectRealtimeEvent", new
            {
                projectId = projectGuid,
                type = eventType,
                payload
            });
        }

        private async Task<Guid> RequireProjectAccessAsync(string projectId)
        {
            if (!Guid.TryParse(projectId, out var projectGuid))
            {
                throw new HubException("Project ID is invalid.");
            }

            var userIdValue = Context.User?.FindFirstValue(ClaimTypes.NameIdentifier);
            if (!Guid.TryParse(userIdValue, out var userId))
            {
                throw new HubException("Authentication is required.");
            }

            if (ProjectAccessPolicy.IsUnrestricted)
            {
                var projectExists = await _context.Projects
                    .AsNoTracking()
                    .AnyAsync(project => project.Id == projectGuid && project.Status && !project.IsDeleted);
                if (!projectExists)
                {
                    throw new HubException("Project does not exist.");
                }

                return projectGuid;
            }

            var isMember = await _context.ProjectMembers
                .AsNoTracking()
                .AnyAsync(member =>
                    member.ProjectId == projectGuid &&
                    member.UserId == userId &&
                    member.Status &&
                    member.LeftAt == null &&
                    member.Project.Status &&
                    !member.Project.IsDeleted);

            if (isMember)
            {
                return projectGuid;
            }

            var isSystemAdmin = await _context.UserRoles
                .AsNoTracking()
                .Where(userRole => userRole.UserId == userId)
                .Select(userRole => userRole.Role.Name.Trim().ToLower())
                .AnyAsync(role => SystemRoles.Contains(role));

            if (!isSystemAdmin)
            {
                throw new HubException("You do not have access to this project.");
            }

            return projectGuid;
        }

        private async Task<Guid> RequireWorkspaceAccessAsync(string workspaceId)
        {
            if (!Guid.TryParse(workspaceId, out var workspaceGuid))
            {
                throw new HubException("Workspace ID is invalid.");
            }

            var userIdValue = Context.User?.FindFirstValue(ClaimTypes.NameIdentifier);
            if (!Guid.TryParse(userIdValue, out var userId))
            {
                throw new HubException("Authentication is required.");
            }

            var isMember = await _context.WorkspaceMembers
                .AsNoTracking()
                .AnyAsync(member =>
                    member.WorkspaceId == workspaceGuid &&
                    member.UserId == userId &&
                    member.IsActive);

            if (isMember)
            {
                return workspaceGuid;
            }

            var isSystemAdmin = await _context.UserRoles
                .AsNoTracking()
                .Where(userRole => userRole.UserId == userId)
                .Select(userRole => userRole.Role.Name.Trim().ToLower())
                .AnyAsync(role => SystemRoles.Contains(role));

            if (!isSystemAdmin)
            {
                throw new HubException("You do not have access to this workspace.");
            }

            return workspaceGuid;
        }
    }
}
