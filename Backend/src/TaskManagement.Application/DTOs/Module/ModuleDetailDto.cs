using TaskManagement.Application.DTOs.WorkTask;

namespace TaskManagement.Application.DTOs.Module;

public sealed class ModuleDetailDto
{
    public Guid Id { get; set; }
    public Guid ProjectId { get; set; }
    public Guid WorkspaceId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string Status { get; set; } = string.Empty;
    public DateTime? StartDate { get; set; }
    public DateTime? TargetDate { get; set; }
    public Guid? LeadId { get; set; }
    public string? LeadName { get; set; }
    public int TaskCount { get; set; }
    public int CompletedCount { get; set; }
    public int InProgressCount { get; set; }
    public int OverdueCount { get; set; }
    public int ProgressPercent { get; set; }
    public ModuleTaskPageDto Tasks { get; set; } = new();
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}

public sealed class ModuleTaskPageDto
{
    public List<ModuleTaskDto> Items { get; set; } = new();
    public int Page { get; set; }
    public int PageSize { get; set; }
    public int TotalCount { get; set; }
    public int TotalPages { get; set; }
    public bool HasPreviousPage { get; set; }
    public bool HasNextPage { get; set; }
}

public sealed class ModuleTaskDto
{
    public Guid Id { get; set; }
    public string? SequenceId { get; set; }
    public string Title { get; set; } = string.Empty;
    public string StatusName { get; set; } = string.Empty;
    public int Priority { get; set; }
    public DateTime? DueDate { get; set; }
    public Guid ProjectId { get; set; }
    public string ProjectName { get; set; } = string.Empty;
    public Guid ModuleId { get; set; }
    public Guid? SprintId { get; set; }
    public string? SprintName { get; set; }
    public Guid? AssignedUserId { get; set; }
    public string? AssigneeName { get; set; }
    public List<TaskAssigneeDto> Assignees { get; set; } = new();
    public Guid? ParentTaskId { get; set; }
    public DateTime UpdatedAt { get; set; }
}
