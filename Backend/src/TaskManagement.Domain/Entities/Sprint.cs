using System;
using System.Collections.Generic;
using TaskManagement.Domain.Rules;

namespace TaskManagement.Domain.Entities
{
    public class Sprint
    {
        public Guid Id { get; set; }
        public Guid ProjectId { get; set; }
        public Project Project { get; set; } = null!;
        public string Name { get; set; } = string.Empty;
        public DateTime StartDate { get; set; }
        public DateTime EndDate { get; set; }
        public bool Status { get; set; } = false;
        public string State { get; set; } = SprintStates.Planned;
        public DateTime? StartedAt { get; set; }
        public DateTime? CompletedAt { get; set; }
        public bool IsDeleted { get; set; }
        public bool IsFavorite { get; set; } = false;
        public DateTime CreatedAt { get; set; }

        public ICollection<WorkTask> WorkTasks { get; set; } = new List<WorkTask>();
    }
}
