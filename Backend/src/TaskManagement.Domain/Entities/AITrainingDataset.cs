using System;

namespace TaskManagement.Domain.Entities
{
    public class AITrainingDataset
    {
        public Guid Id { get; set; }
        public string Category { get; set; } = string.Empty;
        public string InputData { get; set; } = string.Empty;
        public string OutputData { get; set; } = string.Empty;
        public bool IsApproved { get; set; }
    }
}
