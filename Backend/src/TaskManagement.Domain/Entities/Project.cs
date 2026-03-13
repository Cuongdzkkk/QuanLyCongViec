using System;
using System.Collections.Generic;
using TaskStatus = TaskManagement.Domain.Entities.TaskStatus;

namespace TaskManagement.Domain.Entities
{
    public class Project
    {
        public Guid Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string? Description { get; set; }
        public DateTime StartDate { get; set; }
        public DateTime EndDate { get; set; }
        public int Status { get; set; }
        public Guid OwnerId { get; set; }
        public User Owner { get; set; } = null!;

        public ICollection<Sprint> Sprints { get; set; } = new List<Sprint>();
        public ICollection<TaskType> TaskTypes { get; set; } = new List<TaskType>();
        public ICollection<TaskStatus> TaskStatuses { get; set; } = new List<TaskStatus>();
        public ICollection<WorkTask> WorkTasks { get; set; } = new List<WorkTask>();
    }
}
