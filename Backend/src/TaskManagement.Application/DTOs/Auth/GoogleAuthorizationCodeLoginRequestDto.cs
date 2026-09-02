using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

namespace TaskManagement.Application.DTOs.Auth;

public sealed class GoogleAuthorizationCodeLoginRequestDto
{
    [Required]
    [JsonPropertyName("code")]
    public string Code { get; set; } = string.Empty;

    [Required]
    [JsonPropertyName("state")]
    public string State { get; set; } = string.Empty;
}
