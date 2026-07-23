using System;
using System.Threading.Tasks;
using TaskManagement.Application.DTOs.Project;

namespace TaskManagement.Application.Interfaces
{
    public interface IProjectMemberService
    {
        Task<ProjectInvitationOutcome> InviteMemberAsync(Guid projectId, ProjectMemberRequestDto request, string inviterName);
        Task RemoveMemberAsync(Guid projectId, Guid userId, Guid removedBy, string? removalReason = null);
        Task UpdateMemberRoleAsync(Guid projectId, Guid userId, string newRole);
        Task<System.Collections.Generic.IEnumerable<ProjectMemberResponseDto>> GetProjectMembersAsync(Guid projectId);
    }
}
