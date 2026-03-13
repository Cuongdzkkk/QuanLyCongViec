using System;
using System.Collections.Generic;
using TaskStatus = TaskManagement.Domain.Entities.TaskStatus;

namespace TaskManagement.Domain.Entities
{
    public class WorkTask
    {
        public Guid Id { get; set; }
        public Guid ProjectId { get; set; }
        public Project Project { get; set; } = null!;
        public Guid? SprintId { get; set; }
        public Sprint? Sprint { get; set; }
        public Guid TaskTypeId { get; set; }
        public TaskType TaskType { get; set; } = null!;
        public Guid TaskStatusId { get; set; }
        public TaskStatus TaskStatus { get; set; } = null!;
        public string Title { get; set; } = string.Empty;
        public string? Description { get; set; }
        public int Priority { get; set; }
        public int StoryPoints { get; set; }
        public DateTime? DueDate { get; set; }
        public Guid ReporterId { get; set; }
        public User Reporter { get; set; } = null!;

        // Navigation properties
        public ICollection<TaskAssignment> TaskAssignments { get; set; } = new List<TaskAssignment>();
        public ICollection<TaskDependency> PredecessorDependencies { get; set; } = new List<TaskDependency>();
        public ICollection<TaskDependency> SuccessorDependencies { get; set; } = new List<TaskDependency>();
        public ICollection<Comment> Comments { get; set; } = new List<Comment>();
        public ICollection<Attachment> Attachments { get; set; } = new List<Attachment>();
        public TaskVectorEmbedding? TaskVectorEmbedding { get; set; }
    }
}
