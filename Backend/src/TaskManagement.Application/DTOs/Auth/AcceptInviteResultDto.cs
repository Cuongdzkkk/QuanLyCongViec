namespace TaskManagement.Application.DTOs.Auth
{
    public class AcceptInviteResultDto
    {
        public string Email { get; set; } = string.Empty;
        public bool RequiresLogin { get; set; }
        public string RedirectPath { get; set; } = "/dashboard";
        public AuthResponseDto? Response { get; set; }
        public string? RefreshToken { get; set; }
        public Guid? ProjectId { get; set; }
    }
}
