using System;

namespace TaskManagement.Domain.Entities
{
    public class AIFeedback
    {
        public Guid Id { get; set; }
        public Guid UserId { get; set; }
        public User User { get; set; } = null!;
        public string PromptContent { get; set; } = string.Empty;
        public string AIResponse { get; set; } = string.Empty;
        public string? CorrectedResponse { get; set; }
        public int? Rating { get; set; }
    }
}
