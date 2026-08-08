using System;
using System.Collections.Generic;

namespace TaskManagement.Application.DTOs.WorkTask
{
    public class TaskAssigneeDto
    {
        public Guid UserId { get; set; }
        public string? FullName { get; set; }
        public string? Email { get; set; }
        public double ProgressPercent { get; set; }
        public double ContributionWeight { get; set; }
        public double EstimatedHours { get; set; }
        public double TotalActualHours { get; set; }
        public bool IsBlocked { get; set; }
        public Guid? BlockedByUserId { get; set; }
        public string? BlockReason { get; set; }
    }

    public class WorkTaskResponseDto
    {
        public Guid Id { get; set; }
        public string EntityType { get; set; } = "Task";
        public Guid WorkspaceId { get; set; }
        public Guid ProjectId { get; set; }
        public Guid? SprintId { get; set; }
        public string Title { get; set; } = string.Empty;
        public string? Description { get; set; }
        public int Priority { get; set; }
        public double StoryPoints { get; set; }
        public string StatusName { get; set; } = string.Empty;
        public Guid TaskStatusId { get; set; }
        public string TaskTypeName { get; set; } = string.Empty;
        public string TypeName { get; set; } = string.Empty;
        public Guid TaskTypeId { get; set; }
        public string? AssigneeName { get; set; }
        public Guid? AssignedUserId { get; set; }
        public List<TaskAssigneeDto> Assignees { get; set; } = new();
        public Guid? ModuleId { get; set; }
        public List<Guid> LabelIds { get; set; } = new();
        public string ReporterName { get; set; } = string.Empty;
        public Guid ReporterId { get; set; }
        public DateTime? PlannedStartDate { get; set; }
        public DateTime? PlannedEndDate { get; set; }
        public DateTime? DueDate { get; set; }
        public double TotalEstimatedHours { get; set; }
        public double TotalActualHours { get; set; }
        public Guid? ParentTaskId { get; set; }
        public byte[]? RowVersion { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime UpdatedAt { get; set; }
        public string? ProjectName { get; set; }

        /// <summary>SortOrder for Kanban drag-drop (LexoRank-style)</summary>
        public double SortOrder { get; set; }

        /// <summary>Human-readable Issue ID, e.g., "CUN-42"</summary>
        public string? SequenceId { get; set; }

        public bool IsSubscribed { get; set; }
        public string VisibilityMode { get; set; } = "project";
        public List<string> VisibleToRoles { get; set; } = new();
    }
}
