using System;

namespace TaskManagement.Domain.Entities
{
    public class DailyCheckin
    {
        public Guid Id { get; set; }
        public Guid ProjectId { get; set; }
        public Project Project { get; set; } = null!;
        public Guid UserId { get; set; }
        public User User { get; set; } = null!;
        public DateOnly CheckinDate { get; set; }
        public string Yesterday { get; set; } = string.Empty;
        public string Today { get; set; } = string.Empty;
        public string Blocker { get; set; } = string.Empty;
        public DateTime CreatedAt { get; set; }
        public DateTime UpdatedAt { get; set; }
    }
}
