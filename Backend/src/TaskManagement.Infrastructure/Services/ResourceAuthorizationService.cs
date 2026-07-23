using Microsoft.EntityFrameworkCore;
using TaskManagement.Application.Common;
using TaskManagement.Application.Interfaces;
using TaskManagement.Infrastructure.Data;

namespace TaskManagement.Infrastructure.Services
{
    public sealed class ResourceAuthorizationService : IResourceAuthorizationService
    {
        private readonly ApplicationDbContext _dbContext;

        public ResourceAuthorizationService(ApplicationDbContext dbContext)
        {
            _dbContext = dbContext;
        }

        public async Task<ResourceAuthorizationResult> AuthorizeWorkspaceAsync(
            Guid userId,
            Guid workspaceId,
            string permissionCode)
        {
            var membership = await _dbContext.WorkspaceMembers
                .AsNoTracking()
                .Where(member =>
                    member.UserId == userId &&
                    member.WorkspaceId == workspaceId &&
                    member.IsActive &&
                    !member.Workspace.IsDeleted &&
                    member.User.IsActive &&
                    !member.User.IsDeleted)
                .Select(member => new { member.WorkspaceRole })
                .FirstOrDefaultAsync();

            if (membership == null)
            {
                return new(false, FailureReason: "Active workspace membership is required.");
            }

            if (!ResourcePermissionPolicy.WorkspaceRoleHasPermission(membership.WorkspaceRole, permissionCode))
            {
                return new(false, membership.WorkspaceRole, FailureReason: "Workspace permission is required.");
            }

            return new(true, membership.WorkspaceRole);
        }

        public async Task<ResourceAuthorizationResult> AuthorizeProjectAsync(
            Guid userId,
            Guid projectId,
            string permissionCode)
        {
            var project = await _dbContext.Projects
                .AsNoTracking()
                .Where(item => item.Id == projectId && !item.IsDeleted && !item.Workspace.IsDeleted)
                .Select(item => new { item.WorkspaceId })
                .FirstOrDefaultAsync();
            if (project == null)
            {
                return new(false, FailureReason: "Project does not exist in an active workspace.");
            }

            var workspaceMembership = await _dbContext.WorkspaceMembers
                .AsNoTracking()
                .Where(member =>
                    member.UserId == userId &&
                    member.WorkspaceId == project.WorkspaceId &&
                    member.IsActive &&
                    member.User.IsActive &&
                    !member.User.IsDeleted)
                .Select(member => member.WorkspaceRole)
                .FirstOrDefaultAsync();
            if (string.IsNullOrWhiteSpace(workspaceMembership))
            {
                return new(false, FailureReason: "Active workspace membership is required.");
            }

            var membership = await _dbContext.ProjectMembers
                .AsNoTracking()
                .Where(member =>
                    member.UserId == userId &&
                    member.ProjectId == projectId &&
                    member.Status &&
                    !member.Project.IsDeleted &&
                    member.User.IsActive &&
                    !member.User.IsDeleted)
                .Select(member => member.ProjectRole)
                .FirstOrDefaultAsync();

            if (string.IsNullOrWhiteSpace(membership))
            {
                return new(false, FailureReason: "Active workspace and project membership are required.");
            }

            if (!ResourcePermissionPolicy.ProjectRoleHasPermission(membership, permissionCode))
            {
                return new(false, workspaceMembership, membership, "Project permission is required.");
            }

            return new(true, workspaceMembership, membership);
        }
    }
}
