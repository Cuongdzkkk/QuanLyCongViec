using System;
using System.Collections.Generic;

namespace TaskManagement.Domain.Entities
{
    public class Department
    {
        public Guid Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public Guid? ManagerId { get; set; }
        public DateTime CreatedAt { get; set; }

        // New properties
        public bool IsActive { get; set; } = true;
        public bool IsDeleted { get; set; } = false;
        public bool Require2FA { get; set; } = false;

        public Guid? ParentId { get; set; }
        public Department? Parent { get; set; }
        public ICollection<Department> Children { get; set; } = new List<Department>();
        
        public string? Description { get; set; }
        public string? CoverImage { get; set; }

        public User? Manager { get; set; }
        public ICollection<DepartmentMember> DepartmentMembers { get; set; } = new List<DepartmentMember>();
        public ICollection<Project> Projects { get; set; } = new List<Project>();
        public ICollection<ProjectDepartmentRole> ProjectDepartmentRoles { get; set; } = new List<ProjectDepartmentRole>();
        public ICollection<WorkspaceDepartmentAccess> WorkspaceAccesses { get; set; } = new List<WorkspaceDepartmentAccess>();
        public ICollection<TeamGoal> TeamGoals { get; set; } = new List<TeamGoal>();
    }
}
