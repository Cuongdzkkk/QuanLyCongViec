using Microsoft.EntityFrameworkCore;
using TaskManagement.Application.Common;
using TaskManagement.Application.Interfaces;
using TaskManagement.Infrastructure.Data;

namespace TaskManagement.Infrastructure.Services;

public sealed class CallRoomAuthorizationService : ICallRoomAuthorizationService
{
    private readonly ApplicationDbContext _context;
    private readonly IResourceAuthorizationService _resourceAuthorization;

    public CallRoomAuthorizationService(ApplicationDbContext context, IResourceAuthorizationService resourceAuthorization)
    {
        _context = context;
        _resourceAuthorization = resourceAuthorization;
    }

    public async Task AuthorizeVoiceRoomJoinAsync(Guid projectId, Guid userId, CancellationToken cancellationToken = default)
    {
        var result = await _resourceAuthorization.AuthorizeProjectAsync(userId, projectId, ResourcePermissionCodes.ProjectRead);
        if (!result.Succeeded) throw new UnauthorizedAccessException("Project voice-room access denied.");
        var projectExists = await _context.Projects.AsNoTracking().AnyAsync(project =>
            project.Id == projectId && project.Status && !project.IsArchived && !project.IsDeleted, cancellationToken);
        if (!projectExists) throw new UnauthorizedAccessException("Project voice-room access denied.");
    }

    public async Task<CallParticipantProfile> GetParticipantProfileAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        var profile = await _context.Users.AsNoTracking()
            .Where(user => user.Id == userId && user.IsActive && !user.IsDeleted)
            .Select(user => new CallParticipantProfile(user.Id, string.IsNullOrWhiteSpace(user.FullName) ? user.Email : user.FullName, user.AvatarUrl))
            .SingleOrDefaultAsync(cancellationToken);
        return profile ?? throw new UnauthorizedAccessException("Active user is required.");
    }
}
