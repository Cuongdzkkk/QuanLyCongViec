using System;

namespace TaskManagement.Domain.Entities
{
    public class AITokenUsage
    {
        public Guid Id { get; set; }
        public Guid UserId { get; set; }
        public User User { get; set; } = null!;
        public string FeatureCode { get; set; } = string.Empty;
        public int TokensUsed { get; set; }
        public DateTime CreatedAt { get; set; }
    }
}
