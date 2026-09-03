using System;
using System.ComponentModel.DataAnnotations;
using TaskManagement.Domain.Entities;

namespace TaskManagement.Application.DTOs.Enterprise;

public sealed class CreateEnterpriseLeadRequest
{
    [Required, StringLength(120)]
    public string? ContactName { get; set; }

    [Required, EmailAddress, StringLength(320)]
    public string? WorkEmail { get; set; }

    [StringLength(50)]
    public string? PhoneOrZalo { get; set; }

    [Required, StringLength(200)]
    public string? Company { get; set; }

    [Required, StringLength(20)]
    public string? TeamSize { get; set; }

    [StringLength(80)]
    public string? Need { get; set; }

    [StringLength(2000)]
    public string? Notes { get; set; }

    [StringLength(100)]
    public string? PreferredContactTime { get; set; }
}

public sealed class UpdateEnterpriseLeadRequest
{
    [Required]
    public EnterpriseLeadStatus? Status { get; set; }

    [StringLength(2000)]
    public string? InternalNote { get; set; }

    public Guid? AssignedToUserId { get; set; }
}
