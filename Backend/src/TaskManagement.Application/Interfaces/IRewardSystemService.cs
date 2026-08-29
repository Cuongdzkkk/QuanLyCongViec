using System;
using System.Threading.Tasks;
using TaskManagement.Application.DTOs.Rewards;

namespace TaskManagement.Application.Interfaces;

public interface IRewardSystemService
{
    Task<RewardDashboardDto> GetDashboardAsync(Guid projectId, Guid userId);
    Task<IReadOnlyList<RewardSeasonDto>> GetSeasonsAsync(Guid projectId, Guid userId);
    Task<RewardSeasonDto> CreateSeasonAsync(Guid projectId, Guid userId, CreateRewardSeasonRequest request);
    Task<RewardSeasonDto> ActivateSeasonAsync(Guid projectId, Guid seasonId, Guid userId);
    Task<RewardSeasonDto> CloseSeasonAsync(Guid projectId, Guid seasonId, Guid userId);
    Task<RewardDefinitionDto> CreateDefinitionAsync(Guid projectId, Guid seasonId, Guid userId, CreateRewardDefinitionRequest request);
    Task HandleTaskStatusChangeAsync(Guid workTaskId, Guid actorUserId, string? oldStatusName, string? newStatusName);
    Task<RewardPointEventDto> ReviewPointEventAsync(Guid projectId, Guid eventId, Guid reviewerId, bool approve, string? reason);
    Task SettleSeasonAsync(Guid projectId, Guid seasonId, Guid userId);
    Task<RewardGrantDto> ResolveGrantAsync(Guid projectId, Guid grantId, Guid userId, bool award, string? note);
    Task<RewardGrantDto> FulfillGrantAsync(Guid projectId, Guid grantId, Guid userId);
}
