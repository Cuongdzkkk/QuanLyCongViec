using System;
using System.Collections.Generic;

namespace TaskManagement.Domain.Entities
{
    public class Permission
    {
        public Guid Id { get; set; }
        public string Code { get; set; } = string.Empty;
        public string Module { get; set; } = string.Empty;

        public ICollection<RolePermission> RolePermissions { get; set; } = new List<RolePermission>();
    }
}
