namespace TaskManagement.Domain.Entities;

/// <summary>
/// Grants every active member of a team access to a workspace (child site).
/// The grant is independent from direct WorkspaceMember access.
/// </summary>
public sealed class WorkspaceDepartmentAccess
{
    public Guid WorkspaceId { get; set; }
    public Workspace Workspace { get; set; } = null!;

    public Guid DepartmentId { get; set; }
    public Department Department { get; set; } = null!;

    public Guid GrantedByUserId { get; set; }
    public DateTime GrantedAt { get; set; } = DateTime.UtcNow;
}
