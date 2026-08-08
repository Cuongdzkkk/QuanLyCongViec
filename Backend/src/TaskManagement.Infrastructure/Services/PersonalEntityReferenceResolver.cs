using Microsoft.EntityFrameworkCore;
using TaskManagement.Application.DTOs.StarredRecent;
using TaskManagement.Application.Interfaces;
using TaskManagement.Domain.Entities;
using TaskManagement.Infrastructure.Data;

namespace TaskManagement.Infrastructure.Services
{
    public sealed class PersonalEntityReferenceResolver : IPersonalEntityReferenceResolver
    {
        private readonly ApplicationDbContext _context;

        public PersonalEntityReferenceResolver(ApplicationDbContext context)
        {
            _context = context;
        }

        public string NormalizeType(string? entityType)
        {
            return StarredItemTypes.Normalize(entityType)
                ?? throw new ArgumentException("Unsupported entity type.", nameof(entityType));
        }

        public async Task<PersonalEntityReferenceDto> ResolveReadableAsync(
            Guid userId,
            Guid? workspaceId,
            string entityType,
            Guid entityId)
        {
            if (entityId == Guid.Empty)
            {
                throw new ArgumentException("Entity ID is required.", nameof(entityId));
            }

            var canonicalType = NormalizeType(entityType);
            var key = new PersonalEntityKey(canonicalType, entityId);
            var resolved = await ResolveReadableAsync(userId, workspaceId, [key]);
            if (!resolved.TryGetValue(key, out var reference))
            {
                throw new UnauthorizedAccessException("The entity does not exist or is not readable.");
            }

            return reference;
        }

        public async Task<IReadOnlyDictionary<PersonalEntityKey, PersonalEntityReferenceDto>> ResolveReadableAsync(
            Guid userId,
            Guid? workspaceId,
            IEnumerable<PersonalEntityKey> entities)
        {
            if (userId == Guid.Empty)
            {
                throw new UnauthorizedAccessException("Authenticated user context is required.");
            }

            var activeWorkspaceIds = await _context.WorkspaceMembers
                .AsNoTracking()
                .Where(member =>
                    member.UserId == userId &&
                    member.IsActive &&
                    member.User.IsActive &&
                    !member.User.IsDeleted &&
                    !member.Workspace.IsDeleted)
                .Select(member => member.WorkspaceId)
                .Distinct()
                .ToListAsync();

            if (workspaceId.HasValue && !activeWorkspaceIds.Contains(workspaceId.Value))
            {
                throw new UnauthorizedAccessException("Active membership in the selected workspace is required.");
            }

            var keys = entities
                .Where(item => item.EntityId != Guid.Empty)
                .Distinct()
                .ToList();
            var result = new Dictionary<PersonalEntityKey, PersonalEntityReferenceDto>();

            await ResolveProjectsAsync(userId, workspaceId, activeWorkspaceIds, keys, result);
            await ResolveTasksAsync(userId, workspaceId, activeWorkspaceIds, keys, result);
            await ResolveGoalsAsync(workspaceId, activeWorkspaceIds, keys, result);

            // Teams and people do not carry a workspace key in the current schema.
            // They are therefore resolved only inside an explicit, authorized workspace route.
            if (workspaceId.HasValue)
            {
                await ResolveTeamsAsync(userId, workspaceId.Value, keys, result);
                await ResolveUsersAsync(workspaceId.Value, keys, result);
            }

            return result;
        }

        private async Task ResolveProjectsAsync(
            Guid userId,
            Guid? requestedWorkspaceId,
            IReadOnlyCollection<Guid> activeWorkspaceIds,
            IReadOnlyCollection<PersonalEntityKey> keys,
            IDictionary<PersonalEntityKey, PersonalEntityReferenceDto> result)
        {
            var ids = IdsFor(keys, StarredItemTypes.Project);
            if (ids.Count == 0) return;

            var rows = await _context.Projects
                .AsNoTracking()
                .Where(project =>
                    ids.Contains(project.Id) &&
                    !project.IsArchived &&
                    activeWorkspaceIds.Contains(project.WorkspaceId) &&
                    (!requestedWorkspaceId.HasValue || project.WorkspaceId == requestedWorkspaceId.Value) &&
                    project.ProjectMembers.Any(member =>
                        member.UserId == userId &&
                        member.Status &&
                        member.User.IsActive &&
                        !member.User.IsDeleted))
                .Select(project => new
                {
                    project.Id,
                    project.WorkspaceId,
                    project.Name,
                    project.UpdatedAt
                })
                .ToListAsync();

            foreach (var row in rows)
            {
                Add(result, new PersonalEntityReferenceDto
                {
                    EntityType = StarredItemTypes.Project,
                    EntityId = row.Id,
                    WorkspaceId = row.WorkspaceId,
                    Title = row.Name,
                    Subtitle = "Project",
                    Url = $"/home/projects/{row.Id}",
                    Icon = "fa-solid fa-rocket",
                    UpdatedAt = row.UpdatedAt
                });
            }
        }

        private async Task ResolveTasksAsync(
            Guid userId,
            Guid? requestedWorkspaceId,
            IReadOnlyCollection<Guid> activeWorkspaceIds,
            IReadOnlyCollection<PersonalEntityKey> keys,
            IDictionary<PersonalEntityKey, PersonalEntityReferenceDto> result)
        {
            var ids = IdsFor(keys, StarredItemTypes.WorkTask);
            if (ids.Count == 0) return;

            var rows = await _context.WorkTasks
                .AsNoTracking()
                .Where(task =>
                    ids.Contains(task.Id) &&
                    !task.IsArchived &&
                    !task.Project.IsArchived &&
                    activeWorkspaceIds.Contains(task.Project.WorkspaceId) &&
                    (!requestedWorkspaceId.HasValue || task.Project.WorkspaceId == requestedWorkspaceId.Value) &&
                    task.Project.ProjectMembers.Any(member =>
                        member.UserId == userId &&
                        member.Status &&
                        member.User.IsActive &&
                        !member.User.IsDeleted))
                .Select(task => new
                {
                    task.Id,
                    task.Project.WorkspaceId,
                    task.ProjectId,
                    task.Title,
                    task.SequenceId,
                    ProjectName = task.Project.Name,
                    task.UpdatedAt
                })
                .ToListAsync();

            foreach (var row in rows)
            {
                Add(result, new PersonalEntityReferenceDto
                {
                    EntityType = StarredItemTypes.WorkTask,
                    EntityId = row.Id,
                    WorkspaceId = row.WorkspaceId,
                    ProjectId = row.ProjectId,
                    Title = row.Title,
                    Subtitle = string.IsNullOrWhiteSpace(row.SequenceId)
                        ? row.ProjectName
                        : $"{row.ProjectName} - {row.SequenceId}",
                    Url = $"/space/{row.ProjectId}/work-items?task={row.Id}",
                    Icon = "fa-solid fa-square-check",
                    UpdatedAt = row.UpdatedAt
                });
            }
        }

        private async Task ResolveGoalsAsync(
            Guid? requestedWorkspaceId,
            IReadOnlyCollection<Guid> activeWorkspaceIds,
            IReadOnlyCollection<PersonalEntityKey> keys,
            IDictionary<PersonalEntityKey, PersonalEntityReferenceDto> result)
        {
            var ids = IdsFor(keys, StarredItemTypes.Goal);
            if (ids.Count == 0) return;

            var rows = await _context.Goals
                .AsNoTracking()
                .Where(goal =>
                    ids.Contains(goal.Id) &&
                    !goal.IsArchived &&
                    activeWorkspaceIds.Contains(goal.WorkspaceId) &&
                    (!requestedWorkspaceId.HasValue || goal.WorkspaceId == requestedWorkspaceId.Value))
                .Select(goal => new
                {
                    goal.Id,
                    goal.WorkspaceId,
                    goal.Title,
                    goal.UpdatedAt
                })
                .ToListAsync();

            foreach (var row in rows)
            {
                Add(result, new PersonalEntityReferenceDto
                {
                    EntityType = StarredItemTypes.Goal,
                    EntityId = row.Id,
                    WorkspaceId = row.WorkspaceId,
                    Title = row.Title,
                    Subtitle = "Goal",
                    Url = $"/home/goals/{row.Id}",
                    Icon = "fa-solid fa-bullseye",
                    UpdatedAt = row.UpdatedAt
                });
            }
        }

        private async Task ResolveTeamsAsync(
            Guid userId,
            Guid workspaceId,
            IReadOnlyCollection<PersonalEntityKey> keys,
            IDictionary<PersonalEntityKey, PersonalEntityReferenceDto> result)
        {
            var ids = IdsFor(keys, StarredItemTypes.Team);
            if (ids.Count == 0) return;

            var rows = await _context.Departments
                .AsNoTracking()
                .Where(team =>
                    ids.Contains(team.Id) &&
                    team.IsActive &&
                    (team.ManagerId == userId ||
                     team.DepartmentMembers.Any(member =>
                         member.UserId == userId &&
                         member.LeftAt == null &&
                         member.User.IsActive &&
                         !member.User.IsDeleted)))
                .Select(team => new
                {
                    team.Id,
                    team.Name,
                    team.CreatedAt
                })
                .ToListAsync();

            foreach (var row in rows)
            {
                Add(result, new PersonalEntityReferenceDto
                {
                    EntityType = StarredItemTypes.Team,
                    EntityId = row.Id,
                    WorkspaceId = workspaceId,
                    Title = row.Name,
                    Subtitle = "Team",
                    Url = $"/home/teams/{row.Id}",
                    Icon = "fa-solid fa-users",
                    UpdatedAt = row.CreatedAt
                });
            }
        }

        private async Task ResolveUsersAsync(
            Guid workspaceId,
            IReadOnlyCollection<PersonalEntityKey> keys,
            IDictionary<PersonalEntityKey, PersonalEntityReferenceDto> result)
        {
            var ids = IdsFor(keys, StarredItemTypes.User);
            if (ids.Count == 0) return;

            var rows = await _context.WorkspaceMembers
                .AsNoTracking()
                .Where(member =>
                    member.WorkspaceId == workspaceId &&
                    member.IsActive &&
                    ids.Contains(member.UserId) &&
                    member.User.IsActive &&
                    !member.User.IsDeleted)
                .Select(member => new
                {
                    member.UserId,
                    member.User.FullName,
                    member.User.Email,
                    member.User.UpdatedAt
                })
                .ToListAsync();

            foreach (var row in rows)
            {
                Add(result, new PersonalEntityReferenceDto
                {
                    EntityType = StarredItemTypes.User,
                    EntityId = row.UserId,
                    WorkspaceId = workspaceId,
                    Title = string.IsNullOrWhiteSpace(row.FullName) ? row.Email : row.FullName,
                    Subtitle = row.Email,
                    Url = $"/home/people/{row.UserId}",
                    Icon = "fa-solid fa-user",
                    UpdatedAt = row.UpdatedAt
                });
            }
        }

        private static HashSet<Guid> IdsFor(
            IEnumerable<PersonalEntityKey> keys,
            string entityType)
        {
            return keys
                .Where(item => item.EntityType == entityType)
                .Select(item => item.EntityId)
                .ToHashSet();
        }

        private static void Add(
            IDictionary<PersonalEntityKey, PersonalEntityReferenceDto> result,
            PersonalEntityReferenceDto reference)
        {
            result[new PersonalEntityKey(reference.EntityType, reference.EntityId)] = reference;
        }
    }
}
