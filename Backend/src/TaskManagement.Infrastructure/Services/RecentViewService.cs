using Microsoft.EntityFrameworkCore;
using TaskManagement.Application.DTOs.StarredRecent;
using TaskManagement.Application.Interfaces;
using TaskManagement.Domain.Entities;
using TaskManagement.Infrastructure.Data;

namespace TaskManagement.Infrastructure.Services
{
    public sealed class RecentViewService : IRecentViewService
    {
        private readonly ApplicationDbContext _context;
        private readonly IPersonalEntityReferenceResolver _referenceResolver;

        public RecentViewService(
            ApplicationDbContext context,
            IPersonalEntityReferenceResolver referenceResolver)
        {
            _context = context;
            _referenceResolver = referenceResolver;
        }

        public async Task<PersonalCollectionPageDto<RecentViewDto>> GetAllAsync(
            Guid userId,
            int page,
            int pageSize)
        {
            page = Math.Max(1, page);
            pageSize = Math.Clamp(pageSize, 1, 100);

            var records = await _context.RecentViews
                .AsNoTracking()
                .Where(item => item.UserId == userId)
                .OrderByDescending(item => item.ViewedAt)
                .ThenBy(item => item.Id)
                .ToListAsync();
            var canonicalRecords = records
                .Select(item => new
                {
                    Record = item,
                    CanonicalType = StarredItemTypes.Normalize(item.EntityType)
                })
                .Where(item => item.CanonicalType != null)
                .ToList();
            var references = await _referenceResolver.ResolveReadableAsync(
                userId,
                null,
                canonicalRecords.Select(item =>
                    new PersonalEntityKey(item.CanonicalType!, item.Record.EntityId)));
            var readable = canonicalRecords
                .Where(item => references.ContainsKey(
                    new PersonalEntityKey(item.CanonicalType!, item.Record.EntityId)))
                .ToList();
            var items = readable
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .Select(item => Map(
                    item.Record,
                    references[new PersonalEntityKey(item.CanonicalType!, item.Record.EntityId)]))
                .ToList();

            return new PersonalCollectionPageDto<RecentViewDto>
            {
                TotalCount = readable.Count,
                Page = page,
                PageSize = pageSize,
                Items = items
            };
        }

        public async Task<RecentViewDto> RecordAsync(
            Guid userId,
            string entityType,
            Guid entityId)
        {
            var canonicalType = _referenceResolver.NormalizeType(entityType);
            var reference = await _referenceResolver.ResolveReadableAsync(
                userId,
                null,
                canonicalType,
                entityId);
            using var mutationLock = await PersonalEntityMutationLock.AcquireAsync(
                $"recent:{userId}:{canonicalType}:{entityId}");

            var existing = await _context.RecentViews.FirstOrDefaultAsync(item =>
                item.UserId == userId &&
                item.EntityType == canonicalType &&
                item.EntityId == entityId);
            if (existing == null)
            {
                existing = new RecentView
                {
                    Id = Guid.NewGuid(),
                    UserId = userId,
                    EntityType = canonicalType,
                    EntityId = entityId
                };
                _context.RecentViews.Add(existing);
            }

            existing.Title = reference.Title;
            existing.Subtitle = reference.Subtitle;
            existing.Url = reference.Url;
            existing.Icon = reference.Icon;
            existing.ViewedAt = DateTime.UtcNow;

            try
            {
                await _context.SaveChangesAsync();
            }
            catch (DbUpdateException)
            {
                _context.ChangeTracker.Clear();
                existing = await _context.RecentViews
                    .FirstOrDefaultAsync(item =>
                        item.UserId == userId &&
                        item.EntityType == canonicalType &&
                        item.EntityId == entityId);
                if (existing == null) throw;
                existing.Title = reference.Title;
                existing.Subtitle = reference.Subtitle;
                existing.Url = reference.Url;
                existing.Icon = reference.Icon;
                existing.ViewedAt = DateTime.UtcNow;
                await _context.SaveChangesAsync();
            }

            return Map(existing, reference);
        }

        private static RecentViewDto Map(
            RecentView record,
            PersonalEntityReferenceDto reference)
        {
            return new RecentViewDto
            {
                Id = record.Id,
                EntityType = reference.EntityType,
                EntityId = record.EntityId,
                WorkspaceId = reference.WorkspaceId,
                ProjectId = reference.ProjectId,
                Title = reference.Title,
                Subtitle = reference.Subtitle,
                Url = reference.Url,
                Icon = reference.Icon,
                ViewedAt = record.ViewedAt
            };
        }
    }
}
