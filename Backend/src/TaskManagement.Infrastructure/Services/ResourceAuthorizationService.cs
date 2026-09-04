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
            var workspaceRole = await _dbContext.WorkspaceMembers
                .AsNoTracking()
                .Where(member =>
                    member.UserId == userId &&
                    member.WorkspaceId == workspaceId &&
                    member.IsActive &&
                    !member.Workspace.IsDeleted &&
                    member.User.IsActive &&
                    !member.User.IsDeleted)
                .Select(member => member.WorkspaceRole)
                .FirstOrDefaultAsync();

            if (string.IsNullOrWhiteSpace(workspaceRole))
            {
                var isOwner = await _dbContext.Workspaces
                    .AsNoTracking()
                    .AnyAsync(workspace =>
                        workspace.Id == workspaceId &&
                        workspace.OwnerId == userId &&
                        !workspace.IsDeleted &&
                        workspace.Owner.IsActive &&
                        !workspace.Owner.IsDeleted);
                if (isOwner)
                {
                    workspaceRole = "OWNER";
                }
            }

            if (string.IsNullOrWhiteSpace(workspaceRole))
            {
                var hasTeamAccess = await _dbContext.WorkspaceDepartmentAccesses
                    .AsNoTracking()
                    .AnyAsync(access =>
                        access.WorkspaceId == workspaceId &&
                        !access.Workspace.IsDeleted &&
                        access.Department.IsActive &&
                        !access.Department.IsDeleted &&
                        access.Department.DepartmentMembers.Any(member =>
                            member.UserId == userId &&
                            member.LeftAt == null &&
                            member.User.IsActive &&
                            !member.User.IsDeleted));
                if (hasTeamAccess)
                {
                    workspaceRole = "MEMBER";
                }
            }

            if (string.IsNullOrWhiteSpace(workspaceRole))
            {
                return new(false, FailureReason: "Direct or team workspace access is required.");
            }

            if (!ResourcePermissionPolicy.WorkspaceRoleHasPermission(workspaceRole, permissionCode))
            {
                return new(false, workspaceRole, FailureReason: "Workspace permission is required.");
            }

            return new(true, workspaceRole);
        }

        public async Task<ResourceAuthorizationResult> AuthorizeDepartmentAsync(
            Guid userId,
            Guid departmentId)
        {
            var isMember = await _dbContext.DepartmentMembers
                .AsNoTracking()
                .AnyAsync(member =>
                    member.DepartmentId == departmentId &&
                    member.Department.IsActive &&
                    !member.Department.IsDeleted &&
                    member.UserId == userId &&
                    member.User.IsActive &&
                    !member.User.IsDeleted);

            return isMember
                ? new(true)
                : new(false, FailureReason: "Active department membership is required.");
        }

        public Task<List<Guid>> GetSharedActiveDepartmentIdsAsync(
            Guid firstUserId,
            Guid secondUserId)
        {
            var memberships = _dbContext.DepartmentMembers
                .AsNoTracking()
                .Where(first =>
                    first.UserId == firstUserId &&
                    first.Department.IsActive &&
                    !first.Department.IsDeleted &&
                    first.User.IsActive &&
                    !first.User.IsDeleted)
                .Join(
                    _dbContext.DepartmentMembers.AsNoTracking(),
                    first => first.DepartmentId,
                    second => second.DepartmentId,
                    (first, second) => second)
                .Where(second =>
                    second.UserId == secondUserId &&
                    second.User.IsActive &&
                    !second.User.IsDeleted)
                .Select(second => second.DepartmentId)
                .Distinct();

            return memberships.ToListAsync();
        }

        public async Task<ResourceAuthorizationResult> AuthorizeProjectAsync(
            Guid userId,
            Guid projectId,
            string permissionCode,
            bool requireDirectProjectMembership = false)
        {
            var project = await _dbContext.Projects
                .AsNoTracking()
                .Where(item =>
                    item.Id == projectId &&
                    item.Status &&
                    !item.IsDeleted &&
                    !item.Workspace.IsDeleted)
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
                var isOwner = await _dbContext.Workspaces
                    .AsNoTracking()
                    .AnyAsync(workspace =>
                        workspace.Id == project.WorkspaceId &&
                        workspace.OwnerId == userId &&
                        !workspace.IsDeleted &&
                        workspace.Owner.IsActive &&
                        !workspace.Owner.IsDeleted);
                if (isOwner)
                {
                    workspaceMembership = "OWNER";
                }
            }

            if (string.IsNullOrWhiteSpace(workspaceMembership))
            {
                var hasTeamAccess = await _dbContext.WorkspaceDepartmentAccesses
                    .AsNoTracking()
                    .AnyAsync(access =>
                        access.WorkspaceId == project.WorkspaceId &&
                        access.Department.IsActive &&
                        !access.Department.IsDeleted &&
                        access.Department.DepartmentMembers.Any(member =>
                            member.UserId == userId &&
                            member.LeftAt == null &&
                            member.User.IsActive &&
                            !member.User.IsDeleted));
                if (hasTeamAccess)
                {
                    workspaceMembership = "MEMBER";
                }
            }

            if (string.IsNullOrWhiteSpace(workspaceMembership))
            {
                return new(false, FailureReason: "Direct or team workspace access is required.");
            }

            if (ProjectAccessPolicy.IsUnrestricted && !requireDirectProjectMembership)
            {
                var fallbackProjectRole = ResourcePermissionPolicy.NormalizeWorkspaceRole(workspaceMembership) is "owner" or "admin"
                    ? "admin"
                    : "developer";
                if (!ResourcePermissionPolicy.ProjectRoleHasPermission(fallbackProjectRole, permissionCode))
                {
                    return new(false, workspaceMembership, fallbackProjectRole, "Workspace permission is required.");
                }

                return new(true, workspaceMembership, fallbackProjectRole);
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

        public async Task<ResourceAuthorizationResult> AuthorizeProjectForRestoreAsync(
            Guid userId,
            Guid projectId)
        {
            var project = await _dbContext.Projects
                .IgnoreQueryFilters()
                .AsNoTracking()
                .Where(item => item.Id == projectId && !item.Workspace.IsDeleted)
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
                    member.User.IsActive &&
                    !member.User.IsDeleted)
                .Select(member => member.ProjectRole)
                .FirstOrDefaultAsync();
            if (string.IsNullOrWhiteSpace(membership))
            {
                return new(false, workspaceMembership, FailureReason: "Active project management membership is required.");
            }

            return ResourcePermissionPolicy.ProjectRoleHasPermission(membership, ResourcePermissionCodes.ProjectWrite)
                ? new(true, workspaceMembership, membership)
                : new(false, workspaceMembership, membership, "Project management permission is required.");
        }

        public async Task<ResourceAuthorizationResult> AuthorizeProjectResourceAsync(
            Guid userId,
            string resourceType,
            Guid resourceId,
            string permissionCode)
        {
            var projectIds = await ResolveProjectIdsAsync(resourceType, resourceId);
            if (projectIds.Count == 0)
            {
                return new(false, FailureReason: "Project-owned resource does not exist.");
            }

            foreach (var projectId in projectIds)
            {
                var authorization = await AuthorizeProjectAsync(
                    userId,
                    projectId,
                    permissionCode,
                    requireDirectProjectMembership: true);
                if (authorization.Succeeded)
                {
                    return authorization;
                }
            }

            return new(false, FailureReason: "Active project membership and permission are required.");
        }

        private async Task<List<Guid>> ResolveProjectIdsAsync(string resourceType, Guid resourceId)
        {
            var normalizedType = resourceType.Trim().ToLowerInvariant();
            return normalizedType switch
            {
                "worktask" => await _dbContext.WorkTasks
                    .AsNoTracking()
                    .Where(task => task.Id == resourceId && !task.IsDeleted)
                    .Select(task => task.ProjectId)
                    .ToListAsync(),
                "project" => await _dbContext.Projects
                    .AsNoTracking()
                    .Where(project => project.Id == resourceId && !project.IsDeleted)
                    .Select(project => project.Id)
                    .ToListAsync(),
                "projectlesson" => await _dbContext.ProjectLessons
                    .AsNoTracking()
                    .Where(item => item.Id == resourceId)
                    .Select(item => item.ProjectId)
                    .ToListAsync(),
                "projectrisk" => await _dbContext.ProjectRisks
                    .AsNoTracking()
                    .Where(item => item.Id == resourceId)
                    .Select(item => item.ProjectId)
                    .ToListAsync(),
                "projectdecision" => await _dbContext.ProjectDecisions
                    .AsNoTracking()
                    .Where(item => item.Id == resourceId)
                    .Select(item => item.ProjectId)
                    .ToListAsync(),
                "projectupdate" => await _dbContext.ProjectUpdates
                    .AsNoTracking()
                    .Where(item => item.Id == resourceId)
                    .Select(item => item.ProjectId)
                    .ToListAsync(),
                "goal" => await ProjectIdsForGoalAsync(resourceId),
                "goalupdate" => await ProjectIdsForGoalUpdateAsync(resourceId),
                "goallesson" => await ProjectIdsForGoalLessonAsync(resourceId),
                "goalrisk" => await ProjectIdsForGoalRiskAsync(resourceId),
                "goaldecision" => await ProjectIdsForGoalDecisionAsync(resourceId),
                "lesson" => await ProjectIdsForLegacyLessonAsync(resourceId),
                "risk" => await ProjectIdsForLegacyRiskAsync(resourceId),
                "decision" => await ProjectIdsForLegacyDecisionAsync(resourceId),
                _ => new List<Guid>()
            };
        }

        private Task<List<Guid>> ProjectIdsForGoalAsync(Guid goalId) =>
            _dbContext.ProjectLinks
                .AsNoTracking()
                .Where(link => link.LinkedType == "Goal" && link.LinkedId == goalId && !link.Project.IsDeleted)
                .Select(link => link.ProjectId)
                .Distinct()
                .ToListAsync();

        private Task<List<Guid>> ProjectIdsForGoalUpdateAsync(Guid updateId) =>
            _dbContext.GoalUpdates
                .AsNoTracking()
                .Where(update => update.Id == updateId)
                .SelectMany(update => _dbContext.ProjectLinks
                    .Where(link => link.LinkedType == "Goal" && link.LinkedId == update.GoalId && !link.Project.IsDeleted)
                    .Select(link => link.ProjectId))
                .Distinct()
                .ToListAsync();

        private Task<List<Guid>> ProjectIdsForGoalLessonAsync(Guid lessonId) =>
            _dbContext.GoalLessons
                .AsNoTracking()
                .Where(item => item.Id == lessonId)
                .SelectMany(item => _dbContext.ProjectLinks
                    .Where(link => link.LinkedType == "Goal" && link.LinkedId == item.GoalId && !link.Project.IsDeleted)
                    .Select(link => link.ProjectId))
                .Distinct()
                .ToListAsync();

        private Task<List<Guid>> ProjectIdsForGoalRiskAsync(Guid riskId) =>
            _dbContext.GoalRisks
                .AsNoTracking()
                .Where(item => item.Id == riskId)
                .SelectMany(item => _dbContext.ProjectLinks
                    .Where(link => link.LinkedType == "Goal" && link.LinkedId == item.GoalId && !link.Project.IsDeleted)
                    .Select(link => link.ProjectId))
                .Distinct()
                .ToListAsync();

        private Task<List<Guid>> ProjectIdsForGoalDecisionAsync(Guid decisionId) =>
            _dbContext.GoalDecisions
                .AsNoTracking()
                .Where(item => item.Id == decisionId)
                .SelectMany(item => _dbContext.ProjectLinks
                    .Where(link => link.LinkedType == "Goal" && link.LinkedId == item.GoalId && !link.Project.IsDeleted)
                    .Select(link => link.ProjectId))
                .Distinct()
                .ToListAsync();

        private async Task<List<Guid>> ProjectIdsForLegacyLessonAsync(Guid itemId)
        {
            var projectIds = await _dbContext.ProjectLessons.AsNoTracking()
                .Where(item => item.Id == itemId)
                .Select(item => item.ProjectId)
                .ToListAsync();
            var goalIds = await _dbContext.GoalLessons.AsNoTracking()
                .Where(item => item.Id == itemId)
                .Select(item => item.GoalId)
                .ToListAsync();
            return projectIds
                .Concat(await ProjectIdsForGoalsAsync(goalIds))
                .Distinct()
                .ToList();
        }

        private async Task<List<Guid>> ProjectIdsForLegacyRiskAsync(Guid itemId)
        {
            var projectIds = await _dbContext.ProjectRisks.AsNoTracking()
                .Where(item => item.Id == itemId)
                .Select(item => item.ProjectId)
                .ToListAsync();
            var goalIds = await _dbContext.GoalRisks.AsNoTracking()
                .Where(item => item.Id == itemId)
                .Select(item => item.GoalId)
                .ToListAsync();
            return projectIds
                .Concat(await ProjectIdsForGoalsAsync(goalIds))
                .Distinct()
                .ToList();
        }

        private async Task<List<Guid>> ProjectIdsForLegacyDecisionAsync(Guid itemId)
        {
            var projectIds = await _dbContext.ProjectDecisions.AsNoTracking()
                .Where(item => item.Id == itemId)
                .Select(item => item.ProjectId)
                .ToListAsync();
            var goalIds = await _dbContext.GoalDecisions.AsNoTracking()
                .Where(item => item.Id == itemId)
                .Select(item => item.GoalId)
                .ToListAsync();
            return projectIds
                .Concat(await ProjectIdsForGoalsAsync(goalIds))
                .Distinct()
                .ToList();
        }

        private Task<List<Guid>> ProjectIdsForGoalsAsync(IEnumerable<Guid> goalIds) =>
            _dbContext.ProjectLinks
                .AsNoTracking()
                .Where(link => link.LinkedType == "Goal" && link.LinkedId.HasValue && goalIds.Contains(link.LinkedId.Value) && !link.Project.IsDeleted)
                .Select(link => link.ProjectId)
                .Distinct()
                .ToListAsync();

        public async Task<List<Guid>> GetAccessibleProjectIdsAsync(
            Guid userId,
            bool includeArchived = false,
            bool includeDeleted = false)
        {
            var query = _dbContext.Projects
                .AsNoTracking()
                .Where(project =>
                    (project.Workspace.OwnerId == userId ||
                     project.Workspace.Members.Any(member =>
                         member.UserId == userId &&
                         member.IsActive) ||
                     project.Workspace.TeamAccesses.Any(access =>
                         access.Department.IsActive &&
                         !access.Department.IsDeleted &&
                         access.Department.DepartmentMembers.Any(member =>
                             member.UserId == userId &&
                             member.LeftAt == null &&
                             member.User.IsActive &&
                             !member.User.IsDeleted))) &&
                    (includeDeleted || !project.IsDeleted) &&
                    (includeArchived || !project.IsArchived));

            if (ProjectAccessPolicy.RestrictionsEnabled)
            {
                query = query.Where(project =>
                    project.ProjectMembers.Any(member =>
                        member.UserId == userId &&
                        member.Status));
            }

            return await query.Select(project => project.Id).ToListAsync();
        }
    }
}
