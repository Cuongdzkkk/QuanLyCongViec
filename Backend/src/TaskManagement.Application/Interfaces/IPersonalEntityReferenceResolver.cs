using TaskManagement.Application.DTOs.StarredRecent;

namespace TaskManagement.Application.Interfaces
{
    public interface IPersonalEntityReferenceResolver
    {
        string NormalizeType(string? entityType);

        Task<PersonalEntityReferenceDto> ResolveReadableAsync(
            Guid userId,
            Guid? workspaceId,
            string entityType,
            Guid entityId);

        Task<IReadOnlyDictionary<PersonalEntityKey, PersonalEntityReferenceDto>> ResolveReadableAsync(
            Guid userId,
            Guid? workspaceId,
            IEnumerable<PersonalEntityKey> entities);
    }
}
