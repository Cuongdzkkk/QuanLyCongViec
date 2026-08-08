using TaskManagement.Application.DTOs.StarredRecent;

namespace TaskManagement.Application.Interfaces
{
    public interface IStarredItemService
    {
        Task<PersonalCollectionPageDto<StarredItemDto>> GetAllAsync(
            Guid userId,
            Guid workspaceId,
            int page,
            int pageSize);

        Task<StarredItemMutationDto> StarAsync(
            Guid userId,
            Guid workspaceId,
            string itemType,
            Guid itemId);

        Task<StarredItemMutationDto> UnstarAsync(
            Guid userId,
            Guid workspaceId,
            string itemType,
            Guid itemId);

        Task<StarredItemMutationDto> ToggleStarAsync(
            Guid userId,
            Guid workspaceId,
            string itemType,
            Guid itemId);
    }
}
