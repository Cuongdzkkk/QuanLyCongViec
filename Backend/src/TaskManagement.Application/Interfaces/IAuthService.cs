using TaskManagement.Application.DTOs.Auth;

namespace TaskManagement.Application.Interfaces
{
    public interface IAuthService
    {
        Task<(AuthResponseDto? response, string? refreshToken, bool requires2FA)> LoginAsync(LoginRequestDto request);
        Task<(AuthResponseDto response, string refreshToken)> Login2FAAsync(string email, string password, string otp);
        Task<(AuthResponseDto response, string refreshToken)> GoogleLoginAsync(GoogleLoginRequestDto request);
        Task<(AuthResponseDto response, string refreshToken)> GitHubLoginAsync(GitHubLoginRequestDto request);
        Task<IReadOnlyList<ExternalLoginStatusDto>> GetExternalLoginStatusAsync(Guid userId);
        Task LinkGoogleAsync(Guid userId, GoogleLoginRequestDto request);
        Task LinkGitHubAsync(Guid userId, GitHubLoginRequestDto request);
        Task UnlinkExternalLoginAsync(Guid userId, string provider);
        Task RegisterAsync(RegisterRequestDto request);
        Task ResetPasswordAsync(ResetPasswordRequestDto request);
        Task<(string newAccessToken, string newRefreshToken)> RefreshTokenAsync(string? accessToken, string refreshToken);
        Task RevokeTokenAsync(string refreshToken);
        Task AcceptInviteAsync(Guid userId);
        Task<InviteInfoDto> GetInviteInfoAsync(string token);
        Task<AcceptInviteResultDto> AcceptInviteTokenAsync(AcceptInviteTokenRequestDto request);
        Task<AcceptInviteResultDto> AcceptProjectInvitationAsync(Guid invitationId, Guid userId);
        Task DeclineProjectInvitationAsync(Guid invitationId, Guid userId);
    }
}
