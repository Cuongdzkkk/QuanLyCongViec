namespace TaskManagement.Application.Interfaces
{
    public enum TaskDependencyMutation
    {
        Created,
        Updated,
        Unchanged
    }

    public interface ITaskDependencyService
    {
        Task<TaskDependencyMutation> AddOrUpdateAsync(
            Guid projectId,
            Guid taskId,
            Guid relatedTaskId,
            string relationType,
            CancellationToken cancellationToken = default);
    }
}
