using System;
using System.Collections.Generic;

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
        public int Status { get; set; }

        public ICollection<WorkTask> WorkTasks { get; set; } = new List<WorkTask>();
    }
}
