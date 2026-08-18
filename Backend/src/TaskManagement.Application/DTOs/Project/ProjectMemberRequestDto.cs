using System;

namespace TaskManagement.Application.DTOs.Project
{
    public class ProjectMemberRequestDto
    {
        public string Email { get; set; } = string.Empty;
        public string Role { get; set; } = string.Empty;
        public string? InviteMessage { get; set; }
    }

    public class AddExistingProjectMemberRequestDto
    {
        public Guid UserId { get; set; }
        public string Role { get; set; } = string.Empty;
    }
}
