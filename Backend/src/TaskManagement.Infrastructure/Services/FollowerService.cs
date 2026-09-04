using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using TaskManagement.Application.Common;
using TaskManagement.Application.Interfaces;
using TaskManagement.Domain.Entities;
using TaskManagement.Infrastructure.Data;

namespace TaskManagement.Infrastructure.Services
{
    public class FollowerService : IFollowerService
    {
        private readonly ApplicationDbContext _context;
        private readonly IResourceAuthorizationService _authorization;

        public FollowerService(
            ApplicationDbContext context,
            IResourceAuthorizationService authorization)
        {
            _context = context;
            _authorization = authorization;
        }

        public async Task<IEnumerable<object>> GetAllFollowedAsync(Guid userId, Guid workspaceId)
        {
            await EnsureWorkspaceAccessAsync(userId, workspaceId);
            var followers = await _context.EntityFollowers
                .AsNoTracking()
                .Where(f => f.UserId == userId)
                .ToListAsync();
            var visibleFollowers = new List<EntityFollower>();
            foreach (var follower in followers)
            {
                if (await IsAuthorizedEntityAsync(userId, workspaceId, follower.EntityType, follower.EntityId))
                    visibleFollowers.Add(follower);
            }

            return visibleFollowers.Select(f => new
            {
                f.Id,
                f.EntityId,
                f.EntityType,
                f.CreatedAt
            });
        }

        public async Task<IEnumerable<object>> GetFollowersAsync(
            Guid actorUserId,
            Guid workspaceId,
            string entityType,
            Guid entityId)
        {
            await EnsureAuthorizedEntityAsync(actorUserId, workspaceId, entityType, entityId);
            var normalizedType = NormalizeEntityType(entityType);

            var followers = await _context.EntityFollowers
                .AsNoTracking()
                .Include(f => f.User)
                .Where(f => f.EntityType == normalizedType && f.EntityId == entityId)
                .OrderByDescending(f => f.CreatedAt)
                .ToListAsync();

            return followers.Select(f => new
            {
                f.Id,
                f.UserId,
                Name = f.User.FullName,
                FullName = f.User.FullName,
                f.User.Email,
                AvatarUrl = f.User.AvatarUrl,
                f.EntityType,
                f.EntityId,
                f.CreatedAt
            });
        }

        public async Task<IEnumerable<object>> AddFollowersAsync(
            Guid actorUserId,
            Guid workspaceId,
            string entityType,
            Guid entityId,
            IEnumerable<Guid> userIds)
        {
            await EnsureAuthorizedEntityAsync(actorUserId, workspaceId, entityType, entityId);
            var normalizedType = NormalizeEntityType(entityType);
            var distinctUserIds = userIds
                .Where(id => id != Guid.Empty)
                .Distinct()
                .ToList();

            if (distinctUserIds.Count == 0)
            {
                return await GetFollowersAsync(actorUserId, workspaceId, normalizedType, entityId);
            }

            var validUserIds = await _context.Users
                .AsNoTracking()
                .Where(u => distinctUserIds.Contains(u.Id))
                .Select(u => u.Id)
                .ToListAsync();

            var existingUserIds = await _context.EntityFollowers
                .Where(f => f.EntityType == normalizedType && f.EntityId == entityId && validUserIds.Contains(f.UserId))
                .Select(f => f.UserId)
                .ToListAsync();

            var now = DateTime.UtcNow;
            var newFollowers = validUserIds
                .Except(existingUserIds)
                .Select(userId => new EntityFollower
                {
                    UserId = userId,
                    EntityType = normalizedType,
                    EntityId = entityId,
                    CreatedAt = now
                })
                .ToList();

            if (newFollowers.Count > 0)
            {
                await _context.EntityFollowers.AddRangeAsync(newFollowers);
                _context.SiteAuditLogs.Add(new SiteAuditLog
                {
                    EntityId = entityId,
                    EntityType = normalizedType,
                    Action = "AddFollowers",
                    UserId = actorUserId,
                    NewValue = string.Join(",", newFollowers.Select(f => f.UserId)),
                    CreatedAt = now
                });

                await _context.SaveChangesAsync();
            }

            return await GetFollowersAsync(actorUserId, workspaceId, normalizedType, entityId);
        }

        public async Task<object> ToggleFollowAsync(
            Guid userId,
            Guid workspaceId,
            string entityType,
            Guid entityId)
        {
            await EnsureAuthorizedEntityAsync(userId, workspaceId, entityType, entityId);
            entityType = NormalizeEntityType(entityType);
            var existing = await _context.EntityFollowers
                .FirstOrDefaultAsync(f => f.UserId == userId && f.EntityType == entityType && f.EntityId == entityId);

            bool isFollowing = false;

            if (existing != null)
            {
                _context.EntityFollowers.Remove(existing);
                _context.SiteAuditLogs.Add(new SiteAuditLog
                {
                    EntityId = entityId,
                    EntityType = entityType,
                    Action = "Unfollow",
                    UserId = userId,
                    OldValue = entityId.ToString(),
                    CreatedAt = DateTime.UtcNow
                });
            }
            else
            {
                var newFollower = new EntityFollower
                {
                    UserId = userId,
                    EntityType = entityType,
                    EntityId = entityId,
                    CreatedAt = DateTime.UtcNow
                };
                await _context.EntityFollowers.AddAsync(newFollower);
                _context.SiteAuditLogs.Add(new SiteAuditLog
                {
                    EntityId = entityId,
                    EntityType = entityType,
                    Action = "Follow",
                    UserId = userId,
                    NewValue = entityId.ToString(),
                    CreatedAt = DateTime.UtcNow
                });
                isFollowing = true;
            }

            await _context.SaveChangesAsync();

            return new { isFollowing };
        }

        private async Task EnsureWorkspaceAccessAsync(Guid userId, Guid workspaceId)
        {
            var authorization = await _authorization.AuthorizeWorkspaceAsync(
                userId,
                workspaceId,
                ResourcePermissionCodes.WorkspaceRead);
            if (!authorization.Succeeded)
                throw new KeyNotFoundException("Workspace is not accessible.");
        }

        private async Task EnsureAuthorizedEntityAsync(
            Guid userId,
            Guid workspaceId,
            string entityType,
            Guid entityId)
        {
            if (!await IsAuthorizedEntityAsync(userId, workspaceId, entityType, entityId))
                throw new KeyNotFoundException("Follower entity is not accessible.");
        }

        private async Task<bool> IsAuthorizedEntityAsync(
            Guid userId,
            Guid workspaceId,
            string entityType,
            Guid entityId)
        {
            var normalizedType = NormalizeEntityType(entityType).ToLowerInvariant();
            return normalizedType switch
            {
                "project" => await CanAccessProjectAsync(userId, workspaceId, entityId),
                "goal" => await CanAccessGoalAsync(userId, workspaceId, entityId),
                _ => false
            };
        }

        private async Task<bool> CanAccessProjectAsync(Guid userId, Guid workspaceId, Guid projectId)
        {
            var projectWorkspaceId = await _context.Projects
                .AsNoTracking()
                .Where(project => project.Id == projectId && !project.IsDeleted)
                .Select(project => (Guid?)project.WorkspaceId)
                .FirstOrDefaultAsync();
            if (projectWorkspaceId != workspaceId)
                return false;

            var authorization = await _authorization.AuthorizeProjectAsync(
                userId,
                projectId,
                ResourcePermissionCodes.ProjectRead,
                requireDirectProjectMembership: true);
            return authorization.Succeeded;
        }

        private async Task<bool> CanAccessGoalAsync(Guid userId, Guid workspaceId, Guid goalId)
        {
            var goalWorkspaceId = await _context.Goals
                .AsNoTracking()
                .Where(goal => goal.Id == goalId && !goal.IsArchived)
                .Select(goal => (Guid?)goal.WorkspaceId)
                .FirstOrDefaultAsync();
            if (goalWorkspaceId != workspaceId)
                return false;

            var authorization = await _authorization.AuthorizeWorkspaceAsync(
                userId,
                workspaceId,
                ResourcePermissionCodes.WorkspaceRead);
            return authorization.Succeeded;
        }

        private static string NormalizeEntityType(string entityType)
        {
            return string.IsNullOrWhiteSpace(entityType)
                ? string.Empty
                : entityType.Trim();
        }
    }
}
