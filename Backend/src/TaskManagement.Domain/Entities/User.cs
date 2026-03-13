using System;
using System.Collections.Generic;

namespace TaskManagement.Domain.Entities
{
    public class User
    {
        public Guid Id { get; set; }
        public string Email { get; set; } = string.Empty;
        public string PasswordHash { get; set; } = string.Empty;
        public string FullName { get; set; } = string.Empty;
        public string? AvatarUrl { get; set; }
        public bool IsActive { get; set; }

        // Navigation properties
        public ICollection<UserRole> UserRoles { get; set; } = new List<UserRole>();
        public ICollection<Department> ManagedDepartments { get; set; } = new List<Department>();
        public ICollection<DepartmentMember> DepartmentMemberships { get; set; } = new List<DepartmentMember>();
        public ICollection<Project> OwnedProjects { get; set; } = new List<Project>();
        public ICollection<WorkTask> ReportedTasks { get; set; } = new List<WorkTask>();
        public ICollection<TaskAssignment> TaskAssignments { get; set; } = new List<TaskAssignment>();
        public ICollection<Comment> Comments { get; set; } = new List<Comment>();
        public ICollection<Attachment> Attachments { get; set; } = new List<Attachment>();
        public ICollection<AuditLog> AuditLogs { get; set; } = new List<AuditLog>();
        public ICollection<Notification> Notifications { get; set; } = new List<Notification>();
        public ICollection<PerformanceReview> ReviewsGiven { get; set; } = new List<PerformanceReview>();
        public ICollection<PerformanceReview> ReviewsReceived { get; set; } = new List<PerformanceReview>();
        public UserWallet? Wallet { get; set; }
        public ICollection<AITokenUsage> AITokenUsages { get; set; } = new List<AITokenUsage>();
        public ICollection<AIFeedback> AIFeedbacks { get; set; } = new List<AIFeedback>();
    }
}
