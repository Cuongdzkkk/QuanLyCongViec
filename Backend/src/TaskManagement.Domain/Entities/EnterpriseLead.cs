using System;

namespace TaskManagement.Domain.Entities;

public enum EnterpriseLeadStatus
{
    New,
    Contacted,
    InDiscussion,
    DemoScheduled,
    Won,
    Closed
}

public class EnterpriseLead
{
    public Guid Id { get; set; }
    public string ContactName { get; set; } = string.Empty;
    public string WorkEmail { get; set; } = string.Empty;
    public string? PhoneOrZalo { get; set; }
    public string Company { get; set; } = string.Empty;
    public string TeamSize { get; set; } = string.Empty;
    public string? Need { get; set; }
    public string? Notes { get; set; }
    public string? PreferredContactTime { get; set; }
    public EnterpriseLeadStatus Status { get; set; } = EnterpriseLeadStatus.New;
    public Guid? AssignedToUserId { get; set; }
    public User? AssignedToUser { get; set; }
    public string? InternalNote { get; set; }
    public string Source { get; set; } = "public-website";
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}
