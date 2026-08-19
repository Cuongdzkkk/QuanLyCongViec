namespace TaskManagement.Domain.Entities;

public sealed class CollaborationChannel
{
    public Guid Id { get; set; }
    public Guid WorkspaceId { get; set; }
    public Workspace Workspace { get; set; } = null!;
    public Guid ProjectId { get; set; }
    public Project Project { get; set; } = null!;
    public Guid CreatedByUserId { get; set; }
    public User CreatedByUser { get; set; } = null!;
    public string ChannelScope { get; set; } = "Private";
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string? ProvisioningKey { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
    public bool IsArchived { get; set; }
    public bool IsDeleted { get; set; }
    public ICollection<CollaborationChannelMember> Members { get; set; } = new List<CollaborationChannelMember>();
    public ICollection<ChannelMessage> Messages { get; set; } = new List<ChannelMessage>();
    public ICollection<CollaborationChannelReadState> ReadStates { get; set; } =
        new List<CollaborationChannelReadState>();
}
