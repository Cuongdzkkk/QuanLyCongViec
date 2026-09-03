using System.ComponentModel.DataAnnotations;

namespace TaskManagement.Application.DTOs.Auth
{
    public class GitHubLoginRequestDto
    {
        [Required]
        public string Code { get; set; } = string.Empty;

        public string? State { get; set; }
    }
}
