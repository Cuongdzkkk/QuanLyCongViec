namespace TaskManagement.Application.DTOs.WorkTask
{
    public class PersonalWorkPageDto
    {
        public int TotalCount { get; set; }
        public int Page { get; set; }
        public int PageSize { get; set; }
        public List<WorkTaskResponseDto> Items { get; set; } = new();
    }

    public class PersonalWorkSummaryDto
    {
        public int Assigned { get; set; }
        public int Created { get; set; }
        public int Following { get; set; }
        public int WorkedOn { get; set; }
        public int Suggested { get; set; }
        public int Overdue { get; set; }
        public int Completed { get; set; }
    }
}
