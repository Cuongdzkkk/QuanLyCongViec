namespace TaskManagement.Application.DTOs.Auth;

public sealed class ExternalLoginStatusDto
{
    public string Provider { get; init; } = string.Empty;
    public bool IsLinked { get; init; }
    public string? ProviderEmail { get; init; }
    public DateTime? LinkedAt { get; init; }
}
