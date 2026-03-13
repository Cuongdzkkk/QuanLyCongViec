using System;
using System.Collections.Generic;

namespace TaskManagement.Domain.Entities
{
    public class TaskType
    {
        public Guid Id { get; set; }
        public Guid ProjectId { get; set; }
        public Project Project { get; set; } = null!;
        public string Name { get; set; } = string.Empty;
        public string? ColorCode { get; set; }
        public string? Icon { get; set; }

        public ICollection<WorkTask> WorkTasks { get; set; } = new List<WorkTask>();
    }
}
