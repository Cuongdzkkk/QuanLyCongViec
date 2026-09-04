using System;
using System.Collections.Generic;

namespace TaskManagement.Domain.Entities
{
    /// <summary>
    /// Multi-tenant container. Tất cả dữ liệu nghiệp vụ thuộc về 1 Workspace.
    /// Tương đương "Space" trong Plane, hoặc "Organization" trong Jira.
    /// </summary>
    public class Workspace
    {
        public Guid Id { get; set; }

        /// <summary>URL-friendly slug, ví dụ: "cun", "my-company"</summary>
        public string Slug { get; set; } = string.Empty;

        public string Name { get; set; } = string.Empty;
        public string? Logo { get; set; }

        /// <summary>Múi giờ mặc định của workspace (VD: "Asia/Ho_Chi_Minh")</summary>
        public string Timezone { get; set; } = "Asia/Ho_Chi_Minh";

        public Guid OwnerId { get; set; }
        public User Owner { get; set; } = null!;

        public DateTime CreatedAt { get; set; }
        public DateTime UpdatedAt { get; set; }
        public bool IsDeleted { get; set; } = false;

        // Navigation properties
        public ICollection<WorkspaceMember> Members { get; set; } = new List<WorkspaceMember>();
        public ICollection<WorkspaceDepartmentAccess> TeamAccesses { get; set; } = new List<WorkspaceDepartmentAccess>();
        public ICollection<Project> Projects { get; set; } = new List<Project>();
    }
}
