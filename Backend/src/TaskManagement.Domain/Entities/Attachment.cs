using System;

namespace TaskManagement.Domain.Entities
{
    public class Attachment
    {
        public Guid Id { get; set; }
        public Guid WorkTaskId { get; set; }
        public WorkTask WorkTask { get; set; } = null!;
        public Guid UserId { get; set; }
        public User User { get; set; } = null!;
        public string FileName { get; set; } = string.Empty;
        public string FileUrl { get; set; } = string.Empty;
        public int FileSize { get; set; }
    }
}
