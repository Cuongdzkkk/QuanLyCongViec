namespace TaskManagement.Application.Interfaces
{
    public sealed record ResourceAuthorizationResult(
        bool Succeeded,
        string? WorkspaceRole = null,
        string? ProjectRole = null,
        string? FailureReason = null);

    public interface IResourceAuthorizationService
    {
        Task<ResourceAuthorizationResult> AuthorizeWorkspaceAsync(
            Guid userId,
            Guid workspaceId,
            string permissionCode);

        Task<ResourceAuthorizationResult> AuthorizeDepartmentAsync(
            Guid userId,
            Guid departmentId);

        Task<List<Guid>> GetSharedActiveDepartmentIdsAsync(
            Guid firstUserId,
            Guid secondUserId);

        Task<ResourceAuthorizationResult> AuthorizeProjectAsync(
            Guid userId,
            Guid projectId,
            string permissionCode,
            bool requireDirectProjectMembership = false);

        Task<ResourceAuthorizationResult> AuthorizeProjectResourceAsync(
            Guid userId,
            string resourceType,
            Guid resourceId,
            string permissionCode);

        Task<List<Guid>> GetAccessibleProjectIdsAsync(
            Guid userId,
            bool includeArchived = false,
            bool includeDeleted = false);
    }
}
