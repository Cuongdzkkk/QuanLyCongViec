using System;

namespace TaskManagement.Application.DTOs.Project
{
    public class ProjectMemberResponseDto
    {
        public Guid UserId { get; set; }
        public string Email { get; set; } = null!;
        public string FullName { get; set; } = null!;
        public string ProjectRole { get; set; } = null!;
        public DateTime JoinedAt { get; set; }
    }

    public class ProjectMemberCandidateResponseDto
    {
        public Guid UserId { get; set; }
        public string Email { get; set; } = null!;
        public string FullName { get; set; } = null!;
    }
}
