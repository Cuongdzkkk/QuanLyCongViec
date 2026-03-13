using System;

namespace TaskManagement.Domain.Entities
{
    public class TaskVectorEmbedding
    {
        public Guid WorkTaskId { get; set; }
        public WorkTask WorkTask { get; set; } = null!;
        public string VectorData { get; set; } = string.Empty;
        public DateTime LastCalculatedAt { get; set; }
    }
}
