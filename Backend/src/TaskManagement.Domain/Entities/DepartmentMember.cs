using System;

namespace TaskManagement.Domain.Entities
{
    public class DepartmentMember
    {
        public Guid DepartmentId { get; set; }
        public Department Department { get; set; } = null!;

        public Guid UserId { get; set; }
        public User User { get; set; } = null!;

        public DateTime JoinedAt { get; set; }
    }
}
