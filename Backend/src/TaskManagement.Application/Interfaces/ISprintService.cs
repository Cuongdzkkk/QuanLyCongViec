using TaskManagement.Application.DTOs.Sprint;

namespace TaskManagement.Application.Interfaces
{
    public interface ISprintService
    {
        Task<List<SprintResponseDto>> GetByProjectAsync(Guid projectId);
        Task<SprintResponseDto?> GetByIdAsync(Guid id);
        Task<SprintResponseDto> CreateAsync(Guid projectId, CreateSprintDto dto);
        Task<SprintResponseDto> UpdateAsync(Guid projectId, Guid sprintId, UpdateSprintDto dto);
        Task<SprintResponseDto> StartAsync(Guid projectId, Guid sprintId);
        Task<SprintResponseDto> CloseAsync(Guid projectId, Guid sprintId, CloseSprintDto dto, Guid actorUserId);
        Task<List<BurndownDataDto>> GetBurndownChartAsync(Guid sprintId);
    }
}
