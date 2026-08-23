using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace TaskManagement.API.Controllers;

[ApiController]
[Authorize]
[Route("api/webrtc")]
public sealed class WebRtcController : ControllerBase
{
    private readonly IConfiguration _configuration;

    public WebRtcController(IConfiguration configuration) => _configuration = configuration;

    [HttpGet("ice-servers")]
    public IActionResult GetIceServers()
    {
        var now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        var turnEnabled = _configuration.GetValue("WebRtc:Enabled", true);
        var servers = new List<WebRtcIceServerDto>();
        foreach (var section in _configuration.GetSection("WebRtc:IceServers").GetChildren())
        {
            var urls = (section.GetSection("Urls").Get<string[]>() ?? [])
                .Where(url => !string.IsNullOrWhiteSpace(url))
                .ToArray();
            if (urls.Length == 0) continue;

            var sharedSecret = section["SharedSecret"];
            var turnUrls = urls.Any(url => url.StartsWith("turn:", StringComparison.OrdinalIgnoreCase));
            if (turnUrls)
            {
                if (!turnEnabled) continue;
                if (string.IsNullOrWhiteSpace(sharedSecret)) continue;
                var expiresAt = now + Math.Clamp(section.GetValue("CredentialTtlSeconds", 3600), 300, 86400);
                var username = $"{expiresAt}:{User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value}";
                var credential = Convert.ToBase64String(
                    HMACSHA1.HashData(Encoding.UTF8.GetBytes(sharedSecret), Encoding.UTF8.GetBytes(username)));
                servers.Add(new WebRtcIceServerDto(urls, username, credential, expiresAt));
                continue;
            }

            servers.Add(new WebRtcIceServerDto(urls, null, null, null));
        }

        return Ok(new WebRtcIceServerResponseDto(servers));
    }

    private sealed record WebRtcIceServerResponseDto(IReadOnlyList<WebRtcIceServerDto> IceServers);

    private sealed record WebRtcIceServerDto(
        IReadOnlyList<string> Urls,
        string? Username,
        string? Credential,
        long? ExpiresAt);
}
