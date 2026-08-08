using Microsoft.EntityFrameworkCore;
using TaskManagement.Application.DTOs.StarredRecent;
using TaskManagement.Application.Interfaces;
using TaskManagement.Domain.Entities;
using TaskManagement.Infrastructure.Data;

namespace TaskManagement.Infrastructure.Services
{
    public sealed class StarredItemService : IStarredItemService
    {
        private readonly ApplicationDbContext _context;
        private readonly IPersonalEntityReferenceResolver _referenceResolver;

        public StarredItemService(
            ApplicationDbContext context,
            IPersonalEntityReferenceResolver referenceResolver)
        {
            _context = context;
            _referenceResolver = referenceResolver;
        }

        public async Task<PersonalCollectionPageDto<StarredItemDto>> GetAllAsync(
            Guid userId,
            Guid workspaceId,
            int page,
            int pageSize)
        {
            page = Math.Max(1, page);
            pageSize = Math.Clamp(pageSize, 1, 100);

            var records = await _context.StarredItems
                .AsNoTracking()
                .Where(item => item.UserId == userId && item.WorkspaceId == workspaceId)
                .OrderByDescending(item => item.CreatedAt)
                .ThenBy(item => item.Id)
                .ToListAsync();

            var canonicalRecords = records
                .Select(item => new
                {
                    Record = item,
                    CanonicalType = StarredItemTypes.Normalize(item.ItemType)
                })
                .Where(item => item.CanonicalType != null)
                .ToList();
            var references = await _referenceResolver.ResolveReadableAsync(
                userId,
                workspaceId,
                canonicalRecords.Select(item =>
                    new PersonalEntityKey(item.CanonicalType!, item.Record.ItemId)));

            var readable = canonicalRecords
                .Where(item => references.ContainsKey(
                    new PersonalEntityKey(item.CanonicalType!, item.Record.ItemId)))
                .ToList();
            var items = readable
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .Select(item => Map(
                    item.Record,
                    references[new PersonalEntityKey(item.CanonicalType!, item.Record.ItemId)]))
                .ToList();

            return new PersonalCollectionPageDto<StarredItemDto>
            {
                TotalCount = readable.Count,
                Page = page,
                PageSize = pageSize,
                Items = items
            };
        }

        public async Task<StarredItemMutationDto> StarAsync(
            Guid userId,
            Guid workspaceId,
            string itemType,
            Guid itemId)
        {
            var canonicalType = _referenceResolver.NormalizeType(itemType);
            using var mutationLock = await PersonalEntityMutationLock.AcquireAsync(
                $"star:{userId}:{workspaceId}:{canonicalType}:{itemId}");
            return await StarCoreAsync(userId, workspaceId, canonicalType, itemId);
        }

        public async Task<StarredItemMutationDto> UnstarAsync(
            Guid userId,
            Guid workspaceId,
            string itemType,
            Guid itemId)
        {
            var canonicalType = _referenceResolver.NormalizeType(itemType);
            using var mutationLock = await PersonalEntityMutationLock.AcquireAsync(
                $"star:{userId}:{workspaceId}:{canonicalType}:{itemId}");
            return await UnstarCoreAsync(userId, workspaceId, canonicalType, itemId);
        }

        public async Task<StarredItemMutationDto> ToggleStarAsync(
            Guid userId,
            Guid workspaceId,
            string itemType,
            Guid itemId)
        {
            var canonicalType = _referenceResolver.NormalizeType(itemType);
            using var mutationLock = await PersonalEntityMutationLock.AcquireAsync(
                $"star:{userId}:{workspaceId}:{canonicalType}:{itemId}");
            var exists = await _context.StarredItems.AnyAsync(item =>
                item.UserId == userId &&
                item.WorkspaceId == workspaceId &&
                item.ItemType == canonicalType &&
                item.ItemId == itemId);

            return exists
                ? await UnstarCoreAsync(userId, workspaceId, canonicalType, itemId)
                : await StarCoreAsync(userId, workspaceId, canonicalType, itemId);
        }

        private async Task<StarredItemMutationDto> StarCoreAsync(
            Guid userId,
            Guid workspaceId,
            string canonicalType,
            Guid itemId)
        {
            var reference = await _referenceResolver.ResolveReadableAsync(
                userId,
                workspaceId,
                canonicalType,
                itemId);
            var existing = await _context.StarredItems.FirstOrDefaultAsync(item =>
                item.UserId == userId &&
                item.WorkspaceId == workspaceId &&
                item.ItemType == canonicalType &&
                item.ItemId == itemId);
            if (existing != null)
            {
                return new StarredItemMutationDto
                {
                    Status = "starred",
                    Item = Map(existing, reference)
                };
            }

            var now = DateTime.UtcNow;
            var starredItem = new StarredItem
            {
                Id = Guid.NewGuid(),
                UserId = userId,
                WorkspaceId = workspaceId,
                ItemType = canonicalType,
                ItemId = itemId,
                CreatedAt = now,
                UpdatedAt = now
            };
            _context.StarredItems.Add(starredItem);
            AddAudit(userId, itemId, canonicalType, "Star");

            try
            {
                await _context.SaveChangesAsync();
            }
            catch (DbUpdateException)
            {
                _context.ChangeTracker.Clear();
                var concurrent = await _context.StarredItems
                    .AsNoTracking()
                    .FirstOrDefaultAsync(item =>
                        item.UserId == userId &&
                        item.WorkspaceId == workspaceId &&
                        item.ItemType == canonicalType &&
                        item.ItemId == itemId);
                if (concurrent == null) throw;
                starredItem = concurrent;
            }

            return new StarredItemMutationDto
            {
                Status = "starred",
                Item = Map(starredItem, reference)
            };
        }

        private async Task<StarredItemMutationDto> UnstarCoreAsync(
            Guid userId,
            Guid workspaceId,
            string canonicalType,
            Guid itemId)
        {
            // Authorization is checked even when the record no longer exists.
            await _referenceResolver.ResolveReadableAsync(
                userId,
                workspaceId,
                canonicalType,
                itemId);
            var existing = await _context.StarredItems.FirstOrDefaultAsync(item =>
                item.UserId == userId &&
                item.WorkspaceId == workspaceId &&
                item.ItemType == canonicalType &&
                item.ItemId == itemId);
            if (existing == null)
            {
                return new StarredItemMutationDto { Status = "unstarred" };
            }

            _context.StarredItems.Remove(existing);
            AddAudit(userId, itemId, canonicalType, "Unstar");
            await _context.SaveChangesAsync();
            return new StarredItemMutationDto { Status = "unstarred" };
        }

        private void AddAudit(Guid userId, Guid itemId, string itemType, string action)
        {
            _context.SiteAuditLogs.Add(new SiteAuditLog
            {
                Id = Guid.NewGuid(),
                EntityId = itemId,
                EntityType = itemType,
                Action = action,
                UserId = userId,
                CreatedAt = DateTime.UtcNow
            });
        }

        private static StarredItemDto Map(
            StarredItem record,
            PersonalEntityReferenceDto reference)
        {
            return new StarredItemDto
            {
                Id = record.Id,
                ItemType = reference.EntityType,
                ItemId = record.ItemId,
                WorkspaceId = record.WorkspaceId,
                ProjectId = reference.ProjectId,
                ItemName = reference.Title,
                Title = reference.Title,
                Subtitle = reference.Subtitle,
                Url = reference.Url,
                Icon = reference.Icon,
                CreatedAt = record.CreatedAt,
                UpdatedAt = record.UpdatedAt == default ? record.CreatedAt : record.UpdatedAt
            };
        }
    }
}
