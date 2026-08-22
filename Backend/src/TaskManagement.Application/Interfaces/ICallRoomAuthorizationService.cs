namespace TaskManagement.Application.Interfaces;

public sealed record CallParticipantProfile(Guid UserId, string DisplayName, string? AvatarUrl);

public interface ICallRoomAuthorizationService
{
    Task AuthorizeVoiceRoomJoinAsync(Guid projectId, Guid userId, CancellationToken cancellationToken = default);
    Task<CallParticipantProfile> GetParticipantProfileAsync(Guid userId, CancellationToken cancellationToken = default);
}
