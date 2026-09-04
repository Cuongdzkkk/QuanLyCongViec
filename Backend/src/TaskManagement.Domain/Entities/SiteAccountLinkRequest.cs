using System;

namespace TaskManagement.Domain.Entities;

/// <summary>
/// A request for one SprintA account to connect to another account's site-owner page.
/// It does not grant access to any child workspace by itself.
/// </summary>
public sealed class SiteAccountLinkRequest
{
    public Guid Id { get; set; }
    public Guid RequesterUserId { get; set; }
    public User RequesterUser { get; set; } = null!;
    public Guid TargetUserId { get; set; }
    public User TargetUser { get; set; } = null!;
    public string Status { get; set; } = "Pending";
    public DateTime CreatedAt { get; set; }
    public DateTime? RespondedAt { get; set; }
}
