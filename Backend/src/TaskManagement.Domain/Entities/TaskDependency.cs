using System;

namespace TaskManagement.Domain.Entities
{
    public class TaskDependency
    {
        public Guid PredecessorTaskId { get; set; }
        public WorkTask PredecessorTask { get; set; } = null!;

        public Guid SuccessorTaskId { get; set; }
        public WorkTask SuccessorTask { get; set; } = null!;

        public int DependencyType { get; set; }
    }
}
