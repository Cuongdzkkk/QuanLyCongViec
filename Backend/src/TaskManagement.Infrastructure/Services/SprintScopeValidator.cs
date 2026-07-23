using Microsoft.EntityFrameworkCore;
using TaskManagement.Application.Common;
using TaskManagement.Infrastructure.Data;

namespace TaskManagement.Infrastructure.Services
{
    public static class SprintScopeValidator
    {
        public static async Task ValidateTargetSprintAsync(
            ApplicationDbContext context,
            Guid sourceProjectId,
            Guid targetSprintId,
            CancellationToken cancellationToken = default)
        {
            var sourceProject = await context.Projects
                .AsNoTracking()
                .Where(project => project.Id == sourceProjectId && !project.IsDeleted)
                .Select(project => new { project.Id, project.WorkspaceId })
                .SingleOrDefaultAsync(cancellationToken);

            var target = await context.Sprints
                .AsNoTracking()
                .Where(sprint => sprint.Id == targetSprintId)
                .Select(sprint => new
                {
                    sprint.ProjectId,
                    sprint.Project.WorkspaceId,
                    sprint.Project.IsDeleted
                })
                .SingleOrDefaultAsync(cancellationToken);

            if (sourceProject == null || target == null || target.IsDeleted ||
                target.ProjectId != sourceProject.Id ||
                target.WorkspaceId != sourceProject.WorkspaceId)
            {
                throw new ResourceScopeException(
                    "Target sprint must belong to the same project and workspace as the source sprint (phải thuộc cùng dự án và Workspace).");
            }
        }

        public static async Task EnsureTasksBelongToProjectAsync(
            ApplicationDbContext context,
            Guid projectId,
            IEnumerable<(Guid TaskId, Guid ProjectId, Guid WorkspaceId)> tasks,
            CancellationToken cancellationToken = default)
        {
            var workspaceId = await context.Projects
                .AsNoTracking()
                .Where(project => project.Id == projectId && !project.IsDeleted)
                .Select(project => (Guid?)project.WorkspaceId)
                .SingleOrDefaultAsync(cancellationToken);

            if (!workspaceId.HasValue || tasks.Any(task =>
                    task.ProjectId != projectId || task.WorkspaceId != workspaceId.Value))
            {
                throw new ResourceScopeException(
                    "Every rollover task must belong to the source sprint project and workspace.");
            }
        }
    }
}
