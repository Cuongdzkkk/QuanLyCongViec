using TaskManagement.Application.DTOs.Collaboration;

namespace TaskManagement.Application.Interfaces;

public interface ICollaborationChannelService
{
    Task<CollaborationChannelPageDto> DiscoverAsync(
        Guid projectId,
        Guid userId,
        int page,
        int pageSize,
        CancellationToken cancellationToken = default);

    Task<ProvisionCollaborationChannelResult> CreateAsync(
        Guid projectId,
        Guid userId,
        CreateCollaborationChannelRequestDto request,
        string? idempotencyKey,
        CancellationToken cancellationToken = default);
}

public sealed class CollaborationProjectNotFoundException : Exception
{
    public CollaborationProjectNotFoundException() : base("Project was not found.") { }
}

public sealed class CollaborationChannelForbiddenException : Exception
{
    public CollaborationChannelForbiddenException() : base("You do not have permission to manage channels in this project.") { }
}

public sealed class CollaborationChannelConflictException : Exception
{
    public CollaborationChannelConflictException(string message) : base(message) { }
}
