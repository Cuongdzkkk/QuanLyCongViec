using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using TaskManagement.Application.DTOs.WorkTask;

namespace TaskManagement.Application.Interfaces
{
    public interface IWorkTaskService
    {
        /// <summary>
        /// Get tasks by project with role-based authorization.
        /// PM/PO/SM see all tasks. DEV/QA see only assigned/reported tasks.
        /// </summary>
        Task<List<WorkTaskResponseDto>> GetByProjectAsync(Guid projectId, Guid userId);
        
        Task<WorkTaskResponseDto> CreateAsync(Guid reporterId, CreateWorkTaskDto request);
        Task<WorkTaskResponseDto> UpdateAsync(Guid taskId, Guid userId, UpdateWorkTaskDto dto);
        Task UpdateTaskStatusAsync(Guid taskId, Guid userId, UpdateTaskStatusRequestDto request);
        Task<IEnumerable<WorkTaskResponseDto>> GetTasksByProjectIdAsync(Guid projectId);
        Task<IEnumerable<WorkTaskResponseDto>> GetMyTasksAsync(Guid userId);
        Task<List<WorkTaskResponseDto>> SearchTasksAsync(Guid userId, string? query, string? status, Guid? assigneeId, int? priority, Guid? projectId = null, string? scope = "all");
        Task<PersonalWorkPageDto> GetPersonalWorkPageAsync(Guid userId, string scope, int page, int pageSize);
        Task<PersonalWorkSummaryDto> GetPersonalWorkSummaryAsync(Guid userId);
        Task ArchiveAsync(Guid id);
        Task RestoreAsync(Guid id);
        Task<bool> ToggleSubscriptionAsync(Guid taskId, Guid userId);
        Task<IEnumerable<TaskManagement.Application.DTOs.WorkTask.ContingencyPlanDto>> GetContingencyPlansAsync(Guid taskId);
        Task<TaskManagement.Application.DTOs.WorkTask.ContingencyPlanDto?> GetContingencyPlanByIdAsync(Guid taskId, Guid planId);
        Task<TaskManagement.Application.DTOs.WorkTask.ContingencyPlanDto> CreateContingencyPlanAsync(Guid taskId, Guid userId, TaskManagement.Application.DTOs.WorkTask.CreateContingencyPlanDto dto);
        Task<TaskManagement.Application.DTOs.WorkTask.ContingencyPlanDto> UpdateContingencyPlanAsync(Guid taskId, Guid planId, Guid userId, TaskManagement.Application.DTOs.WorkTask.UpdateContingencyPlanDto dto);
        Task DeleteContingencyPlanAsync(Guid taskId, Guid planId, Guid userId);
        Task CreateContingencyTaskToPlanAsync(Guid taskId, Guid planId, TaskManagement.Application.DTOs.WorkTask.CreateContingencyTaskDto dto, Guid userId);
        Task AddContingencyTaskToPlanAsync(Guid taskId, Guid planId, Guid fallbackTaskId, Guid userId);
        Task RemoveContingencyTaskFromPlanAsync(Guid taskId, Guid planId, Guid fallbackTaskId, Guid userId);
        Task ActivateContingencyTaskAsync(Guid taskId, Guid planId, Guid fallbackTaskId, Guid userId);
    }
}
