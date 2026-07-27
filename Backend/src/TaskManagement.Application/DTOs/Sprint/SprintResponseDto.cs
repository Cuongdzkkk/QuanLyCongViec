namespace TaskManagement.Application.DTOs.Sprint
{
    public class SprintResponseDto
    {
        public Guid Id { get; set; }
        public Guid ProjectId { get; set; }
        public Guid WorkspaceId { get; set; }
        public string Name { get; set; } = string.Empty;
        public DateTime StartDate { get; set; }
        public DateTime EndDate { get; set; }
        public bool Status { get; set; }
        public string State { get; set; } = "Upcoming";
        public DateTime? StartedAt { get; set; }
        public DateTime? CompletedAt { get; set; }
        public int ProgressPercent { get; set; }
        public int TaskCount { get; set; }
        public int CompletedTaskCount { get; set; }
        public int InProgressTaskCount { get; set; }
        public int BacklogTaskCount { get; set; }
        public bool IsFavorite { get; set; }
        public DateTime CreatedAt { get; set; }
    }
}
