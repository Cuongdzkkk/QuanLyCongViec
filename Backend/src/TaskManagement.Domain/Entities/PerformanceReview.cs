using System;

namespace TaskManagement.Domain.Entities
{
    public class PerformanceReview
    {
        public Guid Id { get; set; }
        public Guid ReviewerId { get; set; }
        public User Reviewer { get; set; } = null!;
        public Guid RevieweeId { get; set; }
        public User Reviewee { get; set; } = null!;
        public double Score { get; set; }
        public string? Feedback { get; set; }
        public string? ReviewPeriod { get; set; }
    }
}
