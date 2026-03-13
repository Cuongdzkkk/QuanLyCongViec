using System;

namespace TaskManagement.Domain.Entities
{
    public class TaskAssignment
    {
        public Guid WorkTaskId { get; set; }
        public WorkTask WorkTask { get; set; } = null!;

        public Guid UserId { get; set; }
        public User User { get; set; } = null!;
    }
}
