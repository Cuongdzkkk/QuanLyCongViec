namespace TaskManagement.Application.Common
{
    public static class ResourcePermissionCodes
    {
        public const string WorkspaceRead = "workspace.read";
        public const string WorkspaceManage = "workspace.manage";
        public const string WorkspaceDelete = "workspace.delete";
        public const string ProjectRead = "project.read";
        public const string ProjectWrite = "project.write";
        public const string SprintManage = "sprint.manage";
    }

    public static class ResourcePermissionPolicy
    {
        private static readonly IReadOnlyDictionary<string, HashSet<string>> ProjectRolePermissions =
            new Dictionary<string, HashSet<string>>(StringComparer.OrdinalIgnoreCase)
            {
                ["pm"] = ProjectManagerPermissions(),
                ["po"] = ProjectManagerPermissions(),
                ["sm"] = ProjectManagerPermissions(),
                ["project_lead"] = ProjectManagerPermissions(),
                ["admin"] = ProjectManagerPermissions(),
                ["developer"] = ProjectReaderPermissions(),
                ["qa"] = ProjectReaderPermissions(),
                ["designer"] = ProjectReaderPermissions(),
                ["devops"] = ProjectReaderPermissions(),
                ["guest"] = ProjectReaderPermissions(),
                ["stakeholder"] = ProjectReaderPermissions()
            };

        public static string NormalizeWorkspaceRole(string? value) =>
            string.IsNullOrWhiteSpace(value) ? string.Empty : value.Trim().ToLowerInvariant();

        public static bool IsKnownPermission(string? permissionCode) => permissionCode is
            ResourcePermissionCodes.WorkspaceRead or
            ResourcePermissionCodes.WorkspaceManage or
            ResourcePermissionCodes.WorkspaceDelete or
            ResourcePermissionCodes.ProjectRead or
            ResourcePermissionCodes.ProjectWrite or
            ResourcePermissionCodes.SprintManage;

        public static bool WorkspaceRoleHasPermission(string? role, string permissionCode)
        {
            var canonicalRole = NormalizeWorkspaceRole(role);
            return permissionCode switch
            {
                ResourcePermissionCodes.WorkspaceRead => canonicalRole is "owner" or "admin" or "member" or "guest",
                ResourcePermissionCodes.WorkspaceManage => canonicalRole is "owner" or "admin",
                ResourcePermissionCodes.WorkspaceDelete => canonicalRole == "owner",
                _ => false
            };
        }

        public static bool ProjectRoleHasPermission(string? role, string permissionCode)
        {
            var canonicalRole = ProjectExecutionRuleHelper.NormalizeProjectRole(role);
            return ProjectRolePermissions.TryGetValue(canonicalRole, out var permissions)
                && permissions.Contains(permissionCode);
        }

        private static HashSet<string> ProjectManagerPermissions() => new(StringComparer.OrdinalIgnoreCase)
        {
            ResourcePermissionCodes.ProjectRead,
            ResourcePermissionCodes.ProjectWrite,
            ResourcePermissionCodes.SprintManage
        };

        private static HashSet<string> ProjectReaderPermissions() => new(StringComparer.OrdinalIgnoreCase)
        {
            ResourcePermissionCodes.ProjectRead
        };
    }
}
