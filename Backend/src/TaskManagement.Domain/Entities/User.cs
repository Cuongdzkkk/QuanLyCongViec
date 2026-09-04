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
        public string? CoverUrl { get; set; }
        
        // Profile Info
        public string? Bio { get; set; }
        public string? JobTitle { get; set; }
        public string? Location { get; set; }
        public string? Timezone { get; set; }

        public bool IsActive { get; set; } = true;
        public DateTime CreatedAt { get; set; }
        public DateTime UpdatedAt { get; set; }

        // JWT Authentication
        public string? RefreshToken { get; set; }
        public DateTime? RefreshTokenExpiryTime { get; set; }

        // Soft Delete
        public bool IsDeleted { get; set; } = false;
        public bool Is2FAEnabled { get; set; } = false;
        public string? OrganizationId { get; set; }
        public Organization? Organization { get; set; }

        // Navigation properties
        public ICollection<UserRole> UserRoles { get; set; } = new List<UserRole>();
        public ICollection<Department> ManagedDepartments { get; set; } = new List<Department>();
        public ICollection<DepartmentMember> DepartmentMemberships { get; set; } = new List<DepartmentMember>();
        public ICollection<Project> CreatedProjects { get; set; } = new List<Project>();
        public ICollection<ProjectMember> ProjectMemberships { get; set; } = new List<ProjectMember>();
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
        public ICollection<AiUsageLedger> AiUsageLedgerEntries { get; set; } = new List<AiUsageLedger>();
        public AiSubscription? AiSubscription { get; set; }
        public ICollection<AiCreditAdjustment> AiCreditAdjustments { get; set; } = new List<AiCreditAdjustment>();
        public ICollection<PaymentOrder> PaymentOrders { get; set; } = new List<PaymentOrder>();
        public ICollection<NotificationPreference> NotificationPreferences { get; set; } = new List<NotificationPreference>();
        public ICollection<AIFeedback> AIFeedbacks { get; set; } = new List<AIFeedback>();
        public ICollection<AITrainingDataset> AITrainingDatasets { get; set; } = new List<AITrainingDataset>();
        public ICollection<TimeLog> TimeLogs { get; set; } = new List<TimeLog>();
        public ICollection<SystemAuditLog> SystemAuditLogs { get; set; } = new List<SystemAuditLog>();
        // === Workspace-level navigation ===
        public ICollection<WorkspaceMember> WorkspaceMemberships { get; set; } = new List<WorkspaceMember>();
        public ICollection<Workspace> OwnedWorkspaces { get; set; } = new List<Workspace>();
        public ICollection<SiteAccountLinkRequest> OutgoingSiteAccountLinkRequests { get; set; } = new List<SiteAccountLinkRequest>();
        public ICollection<SiteAccountLinkRequest> IncomingSiteAccountLinkRequests { get; set; } = new List<SiteAccountLinkRequest>();
        
        // Concurrent Sessions
        public ICollection<RefreshToken> RefreshTokens { get; set; } = new List<RefreshToken>();
        public ICollection<ExternalLogin> ExternalLogins { get; set; } = new List<ExternalLogin>();
    }
}
