namespace TaskManagement.Application.DTOs.Auth
{
    public class InviteInfoDto
    {
        public string Email { get; set; } = string.Empty;
        public string FullName { get; set; } = string.Empty;
        public bool IsRegistered { get; set; }
        public bool RequiresAccountSetup { get; set; }
        public bool CanAcceptAuthenticated { get; set; }
        public string[] ProjectNames { get; set; } = Array.Empty<string>();
        public DateTime ExpiresAt { get; set; }
    }
}
