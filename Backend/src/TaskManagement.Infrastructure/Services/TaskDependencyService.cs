using System.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using TaskManagement.Application.Interfaces;
using TaskManagement.Domain.Entities;
using TaskManagement.Infrastructure.Data;

namespace TaskManagement.Infrastructure.Services
{
    public sealed class TaskDependencyService : ITaskDependencyService
    {
        private const int MaxGraphNodes = 10_000;
        private readonly ApplicationDbContext _context;

        public TaskDependencyService(ApplicationDbContext context)
        {
            _context = context;
        }

        public async Task<TaskDependencyMutation> AddOrUpdateAsync(
            Guid projectId,
            Guid taskId,
            Guid relatedTaskId,
            string relationType,
            CancellationToken cancellationToken = default)
        {
            if (taskId == relatedTaskId)
            {
                throw new ArgumentException("A task cannot depend on itself.");
            }

            var (predecessorId, successorId, dependencyType) = NormalizeRelation(taskId, relatedTaskId, relationType);

            IDbContextTransaction? transaction = null;
            if (_context.Database.IsRelational())
            {
                // Serializable prevents two concurrent edge inserts from both validating
                // against the same stale graph and jointly creating a cycle.
                transaction = await _context.Database.BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken);
            }

            try
            {
                if (_context.Database.ProviderName?.Contains("SqlServer", StringComparison.OrdinalIgnoreCase) == true)
                {
                    var lockResource = $"task-dependency-project:{projectId:N}";
                    await _context.Database.ExecuteSqlInterpolatedAsync(
                        $"EXEC sys.sp_getapplock @Resource={lockResource}, @LockMode='Exclusive', @LockOwner='Transaction', @LockTimeout=10000;",
                        cancellationToken);
                }

                await ValidateTaskScopeAsync(projectId, taskId, relatedTaskId, cancellationToken);

                var existing = await _context.TaskDependencies
                    .SingleOrDefaultAsync(edge => edge.PredecessorTaskId == predecessorId &&
                                                  edge.SuccessorTaskId == successorId,
                        cancellationToken);

                if (existing != null && existing.DependencyType == dependencyType)
                {
                    if (transaction != null) await transaction.CommitAsync(cancellationToken);
                    return TaskDependencyMutation.Unchanged;
                }

                if (dependencyType == 1 &&
                    await HasPathAsync(projectId, successorId, predecessorId, cancellationToken))
                {
                    throw new InvalidOperationException("Adding this dependency would create a cycle.");
                }

                TaskDependencyMutation mutation;
                if (existing == null)
                {
                    _context.TaskDependencies.Add(new TaskDependency
                    {
                        PredecessorTaskId = predecessorId,
                        SuccessorTaskId = successorId,
                        DependencyType = dependencyType
                    });
                    mutation = TaskDependencyMutation.Created;
                }
                else
                {
                    existing.DependencyType = dependencyType;
                    mutation = TaskDependencyMutation.Updated;
                }

                await _context.SaveChangesAsync(cancellationToken);
                if (transaction != null) await transaction.CommitAsync(cancellationToken);
                return mutation;
            }
            catch
            {
                if (transaction != null) await transaction.RollbackAsync(cancellationToken);
                throw;
            }
            finally
            {
                if (transaction != null) await transaction.DisposeAsync();
            }
        }

        private async Task ValidateTaskScopeAsync(
            Guid projectId,
            Guid taskId,
            Guid relatedTaskId,
            CancellationToken cancellationToken)
        {
            var tasks = await _context.WorkTasks
                .AsNoTracking()
                .Where(task => (task.Id == taskId || task.Id == relatedTaskId) && !task.IsDeleted)
                .Select(task => new
                {
                    task.Id,
                    task.ProjectId,
                    TaskWorkspaceId = task.WorkspaceId,
                    ProjectWorkspaceId = task.Project.WorkspaceId
                })
                .ToListAsync(cancellationToken);

            if (tasks.Count != 2 || tasks.Any(task => task.ProjectId != projectId))
            {
                throw new ArgumentException("Both tasks must belong to the requested project.");
            }

            if (tasks.Any(task => task.TaskWorkspaceId != task.ProjectWorkspaceId) ||
                tasks.Select(task => task.TaskWorkspaceId).Distinct().Count() != 1)
            {
                throw new ArgumentException("Both tasks must belong to the same workspace.");
            }
        }

        private async Task<bool> HasPathAsync(
            Guid projectId,
            Guid startTaskId,
            Guid targetTaskId,
            CancellationToken cancellationToken)
        {
            var edges = await _context.TaskDependencies
                .AsNoTracking()
                .Where(edge => edge.DependencyType == 1 &&
                               edge.PredecessorTask.ProjectId == projectId &&
                               edge.SuccessorTask.ProjectId == projectId)
                .Select(edge => new { edge.PredecessorTaskId, edge.SuccessorTaskId })
                .ToListAsync(cancellationToken);

            var adjacency = edges
                .GroupBy(edge => edge.PredecessorTaskId)
                .ToDictionary(group => group.Key, group => group.Select(edge => edge.SuccessorTaskId).ToArray());
            var visited = new HashSet<Guid>();
            var queue = new Queue<Guid>();
            queue.Enqueue(startTaskId);

            while (queue.Count > 0)
            {
                var current = queue.Dequeue();
                if (!visited.Add(current)) continue;
                if (visited.Count > MaxGraphNodes)
                {
                    throw new InvalidOperationException("Dependency graph exceeds the safe traversal limit.");
                }
                if (current == targetTaskId) return true;

                if (!adjacency.TryGetValue(current, out var successors)) continue;
                foreach (var successor in successors)
                {
                    if (!visited.Contains(successor)) queue.Enqueue(successor);
                }
            }

            return false;
        }

        private static (Guid PredecessorId, Guid SuccessorId, int DependencyType) NormalizeRelation(
            Guid taskId,
            Guid relatedTaskId,
            string relationType)
        {
            return relationType?.Trim().ToLowerInvariant() switch
            {
                "blocked_by" => (relatedTaskId, taskId, 1),
                "blocks" => (taskId, relatedTaskId, 1),
                "relates_to" => (taskId, relatedTaskId, 2),
                "duplicate" => (taskId, relatedTaskId, 3),
                _ => throw new ArgumentException("Unsupported dependency relation type.")
            };
        }
    }
}
