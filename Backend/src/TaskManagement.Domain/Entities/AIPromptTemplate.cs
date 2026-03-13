using System;

namespace TaskManagement.Domain.Entities
{
    public class AIPromptTemplate
    {
        public Guid Id { get; set; }
        public string Code { get; set; } = string.Empty;
        public string TemplateContent { get; set; } = string.Empty;
        public bool IsActive { get; set; }
    }
}
