using TaskManagement.Application.DTOs.StarredRecent;

namespace TaskManagement.Application.Interfaces
{
    public interface IRecentViewService
    {
        Task<PersonalCollectionPageDto<RecentViewDto>> GetAllAsync(
            Guid userId,
            int page,
            int pageSize);

        Task<RecentViewDto> RecordAsync(
            Guid userId,
            string entityType,
            Guid entityId);
    }
}
