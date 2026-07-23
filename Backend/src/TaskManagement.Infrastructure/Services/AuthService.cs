using Microsoft.EntityFrameworkCore;
using TaskManagement.Application.Common;
using TaskManagement.Application.DTOs.Auth;
using TaskManagement.Application.Interfaces;
using TaskManagement.Infrastructure.Data;
using TaskManagement.Domain.Entities;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Net.Http.Headers;
using Google.Apis.Auth;
using Microsoft.Extensions.Configuration;

namespace TaskManagement.Infrastructure.Services
{
    public class AuthService : IAuthService
    {
        private readonly ApplicationDbContext _context;
        private readonly IJwtService _jwtService;
        private readonly IConfiguration _configuration;
        private readonly IOtpService _otpService;
        private readonly IEmailService _emailService;

        public AuthService(ApplicationDbContext context, IJwtService jwtService, IConfiguration configuration, IOtpService otpService, IEmailService emailService)
        {
            _context = context;
            _jwtService = jwtService;
            _configuration = configuration;
            _otpService = otpService;
            _emailService = emailService;
        }

        public async Task<(AuthResponseDto? response, string? refreshToken, bool requires2FA)> LoginAsync(LoginRequestDto request)
        {
            var canonicalEmail = EmailCanonicalizer.Normalize(request.Email);
            var user = await _context.Users
                .Include(u => u.UserRoles)
                .ThenInclude(ur => ur.Role)
                .FirstOrDefaultAsync(u => u.Email.ToLower() == canonicalEmail && !u.IsDeleted);

            if (user == null || string.IsNullOrEmpty(user.PasswordHash) || !VerifyPassword(request.Password, user.PasswordHash))
            {
                throw new UnauthorizedAccessException("Email hoặc mật khẩu không chính xác.");
            }

            if (!user.IsActive)
            {
                throw new UnauthorizedAccessException("Email hoặc mật khẩu không chính xác.");
            }

            var tenantConfig = await _context.TenantConfigs.FirstOrDefaultAsync() 
                                ?? new TenantConfig();

            if (user.Is2FAEnabled || tenantConfig.Require2FA)
            {
                // Prevent login if tenant strictly requires 2FA but user didn't set it up
                if (tenantConfig.Require2FA && !user.Is2FAEnabled)
                {
                    throw new UnauthorizedAccessException("Hệ thống yêu cầu cài đặt bảo mật 2 lớp (2FA). Vui lòng cấu hình trước khi đăng nhập.");
                }

                var otpCode = _otpService.GenerateOtp();
                var issueResult = _otpService.StoreOtp(user.Email, otpCode);
                if (!issueResult.Issued)
                {
                    throw new OtpRateLimitException(issueResult.RetryAfterSeconds);
                }
                await _emailService.SendOtpEmailAsync(user.Email, otpCode);
                return (null, null, true);
            }

            return await GenerateTokensForUser(user, request.DeviceId);
        }

        public async Task<(AuthResponseDto response, string refreshToken)> Login2FAAsync(string email, string password, string otp)
        {
            var canonicalEmail = EmailCanonicalizer.Normalize(email);
            var user = await _context.Users
                .Include(u => u.UserRoles)
                .ThenInclude(ur => ur.Role)
                .FirstOrDefaultAsync(u => u.Email.ToLower() == canonicalEmail && !u.IsDeleted);

            if (user == null || string.IsNullOrEmpty(user.PasswordHash) || !VerifyPassword(password, user.PasswordHash))
                throw new UnauthorizedAccessException("Email hoặc mật khẩu không chính xác.");

            if (!user.IsActive)
                throw new UnauthorizedAccessException("Email hoặc mật khẩu không chính xác.");

            var otpValidation = _otpService.ValidateOtp(canonicalEmail, otp);
            if (otpValidation.Status == OtpValidationStatus.Locked)
                throw new OtpRateLimitException(otpValidation.RetryAfterSeconds);
            if (!otpValidation.IsValid)
                throw new UnauthorizedAccessException("Mã OTP không hợp lệ hoặc đã hết hạn.");

            var tokens = await GenerateTokensForUser(user, "2FA-Verified-Device");
            return (tokens.response!, tokens.refreshToken!);
        }

        private async Task<(AuthResponseDto? response, string? refreshToken, bool requires2FA)> GenerateTokensForUser(User user, string deviceId = "Unknown")
        {
            var roles = user.UserRoles?.Select(ur => ur.Role.Name).ToList() ?? new List<string>();

            // Users without system roles should stay role-less until explicitly assigned.

            var accessToken = _jwtService.GenerateAccessToken(user, roles);
            var refreshToken = _jwtService.GenerateRefreshToken();

            // Cập nhật cả ở bảng User và lưu mới vào RefreshTokens (Concurrent Session Tracking)
            user.RefreshToken = refreshToken;
            user.RefreshTokenExpiryTime = DateTime.UtcNow.AddDays(7);
            
            _context.RefreshTokens.Add(new RefreshToken 
            {
                Id = Guid.NewGuid(),
                UserId = user.Id,
                Token = refreshToken,
                DeviceId = deviceId,
                ExpiryTime = DateTime.UtcNow.AddDays(7),
                IsRevoked = false
            });

            await _context.SaveChangesAsync();

            var response = new AuthResponseDto
            {
                AccessToken = accessToken,
                Id = user.Id,
                Email = user.Email,
                FullName = user.FullName,
                AvatarUrl = user.AvatarUrl,
                SystemRoles = roles.ToArray()
            };

            return (response, refreshToken, false);
        }

        public async Task<(AuthResponseDto response, string refreshToken)> GoogleLoginAsync(GoogleLoginRequestDto request)
        {
            if (string.IsNullOrEmpty(request.Credential))
            {
                throw new ArgumentException("Thiếu token xác thực từ Google (Credential is empty). Vui lòng thử lại.");
            }

            string email;
            string name;

            // Thử 2 luồng: JWT ID Token (One Tap) hoặc Access Token (Popup TOKEN flow)
            try
            {
                // Luồng 1: Nếu Credential là JWT ID Token → validate trực tiếp
                var googleClientId = _configuration["Google:ClientId"]
                    ?? throw new InvalidOperationException("Google ClientId is not configured.");
                var settings = new GoogleJsonWebSignature.ValidationSettings
                {
                    Audience = new List<string> { googleClientId }
                };
                var jwtPayload = await GoogleJsonWebSignature.ValidateAsync(request.Credential, settings);
                email = jwtPayload.Email;
                name = jwtPayload.Name;
            }
            catch
            {
                // Luồng 2: Nếu Credential là Access Token → gọi Google Userinfo API
                using var httpClient = new HttpClient();
                httpClient.DefaultRequestHeaders.Authorization = 
                    new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", request.Credential);
                
                var userInfoResponse = await httpClient.GetAsync("https://www.googleapis.com/oauth2/v3/userinfo");
                if (!userInfoResponse.IsSuccessStatusCode)
                {
                    throw new UnauthorizedAccessException("Token Google không hợp lệ hoặc đã hết hạn. Vui lòng đăng nhập lại.");
                }

                var content = await userInfoResponse.Content.ReadAsStringAsync();
                var userInfo = JsonSerializer.Deserialize<JsonElement>(content);
                
                email = userInfo.GetProperty("email").GetString() 
                    ?? throw new UnauthorizedAccessException("Không thể lấy email từ Google.");
                name = userInfo.TryGetProperty("name", out var nameEl) ? nameEl.GetString() ?? email : email;
            }
            email = EmailCanonicalizer.Normalize(email);
            var user = await _context.Users
                .Include(u => u.UserRoles)
                .ThenInclude(ur => ur.Role)
                .FirstOrDefaultAsync(u => u.Email.ToLower() == email);

            if (user?.IsDeleted == true)
            {
                throw new UnauthorizedAccessException("Unable to authenticate this account.");
            }

            if (user == null)
            {
                user = new User
                {
                    Id = Guid.NewGuid(),
                    Email = email,
                    FullName = name,
                    PasswordHash = string.Empty, // Empty or random hash since they login via Google
                    CreatedAt = DateTime.UtcNow,
                    IsDeleted = false
                };
                
                _context.Users.Add(user);
                
                var defaultRole = await _context.Roles.FirstOrDefaultAsync(r => r.Name == "Developer" || r.Name == "DEV");
                if (defaultRole != null)
                {
                    var ur = new UserRole
                    {
                        UserId = user.Id,
                        RoleId = defaultRole.Id,
                        Role = defaultRole
                    };
                    _context.UserRoles.Add(ur);
                     // Assign for instant token generation using the exact same tracked instance
                    user.UserRoles = new List<UserRole> { ur };
                }
                
                await _context.SaveChangesAsync();
            }

            if (!user.IsActive)
            {
                throw new UnauthorizedAccessException("Unable to authenticate this account.");
            }

            var roles = user.UserRoles?.Select(ur => ur.Role.Name).ToList() ?? new List<string>();

            // Users without system roles should stay role-less until explicitly assigned.

            var accessToken = _jwtService.GenerateAccessToken(user, roles);
            var refreshToken = _jwtService.GenerateRefreshToken();

            user.RefreshToken = refreshToken;
            user.RefreshTokenExpiryTime = DateTime.UtcNow.AddDays(7);
            
            _context.RefreshTokens.Add(new RefreshToken 
            {
                Id = Guid.NewGuid(),
                UserId = user.Id,
                Token = refreshToken,
                DeviceId = request.Credential?.Length > 30 ? "Google-App-SSO" : "SSO-WEB",
                ExpiryTime = DateTime.UtcNow.AddDays(7),
                IsRevoked = false
            });

            await _context.SaveChangesAsync();

            var response = new AuthResponseDto
            {
                AccessToken = accessToken,
                Id = user.Id,
                Email = user.Email,
                FullName = user.FullName,
                AvatarUrl = user.AvatarUrl,
                SystemRoles = roles.ToArray()
            };

            return (response, refreshToken);
        }

        public async Task<(AuthResponseDto response, string refreshToken)> GitHubLoginAsync(GitHubLoginRequestDto request)
        {
            var gitHubConfig = _configuration.GetSection("GitHub");
            var clientId = gitHubConfig["ClientId"] ?? throw new InvalidOperationException("GitHub ClientId chưa được cấu hình.");
            var clientSecret = gitHubConfig["ClientSecret"] ?? throw new InvalidOperationException("GitHub ClientSecret chưa được cấu hình.");

            // Bước 1: Đổi authorization code lấy access_token từ GitHub
            if (string.IsNullOrWhiteSpace(clientId))
                throw new InvalidOperationException("GitHub ClientId is not configured.");

            if (string.IsNullOrWhiteSpace(clientSecret))
                throw new InvalidOperationException("GitHub ClientSecret is not configured.");

            var frontendBaseUrl = (_configuration["Frontend:BaseUrl"] ?? "http://localhost:5173").TrimEnd('/');
            var redirectUri = gitHubConfig["RedirectUri"] ?? $"{frontendBaseUrl}/auth/github/callback";

            using var httpClient = new HttpClient();
            httpClient.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
            httpClient.DefaultRequestHeaders.UserAgent.Add(new ProductInfoHeaderValue("SprintA", "1.0"));

            var tokenRequest = new Dictionary<string, string>
            {
                { "client_id", clientId },
                { "client_secret", clientSecret },
                { "code", request.Code },
                { "redirect_uri", redirectUri }
            };

            var tokenResponse = await httpClient.PostAsync(
                "https://github.com/login/oauth/access_token",
                new FormUrlEncodedContent(tokenRequest));
            
            var tokenContent = await tokenResponse.Content.ReadAsStringAsync();
            var tokenJson = JsonSerializer.Deserialize<JsonElement>(tokenContent);

            if (!tokenJson.TryGetProperty("access_token", out var accessTokenElement))
            {
                var errorDesc = tokenJson.TryGetProperty("error_description", out var errEl) 
                    ? errEl.GetString() 
                    : "Không thể xác thực với GitHub.";
                throw new UnauthorizedAccessException(errorDesc);
            }

            var githubAccessToken = accessTokenElement.GetString()!;

            // Bước 2: Dùng access_token lấy thông tin user từ GitHub API
            httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", githubAccessToken);

            var userResponse = await httpClient.GetAsync("https://api.github.com/user");
            var userContent = await userResponse.Content.ReadAsStringAsync();
            var githubUser = JsonSerializer.Deserialize<JsonElement>(userContent);

            var githubEmail = githubUser.TryGetProperty("email", out var emailEl) && emailEl.ValueKind != JsonValueKind.Null
                ? emailEl.GetString()
                : null;

            // Nếu email bị ẩn, gọi thêm API emails
            if (string.IsNullOrEmpty(githubEmail))
            {
                var emailsResponse = await httpClient.GetAsync("https://api.github.com/user/emails");
                var emailsContent = await emailsResponse.Content.ReadAsStringAsync();
                var emails = JsonSerializer.Deserialize<JsonElement>(emailsContent);

                foreach (var emailItem in emails.EnumerateArray())
                {
                    if (emailItem.TryGetProperty("primary", out var primary) && primary.GetBoolean())
                    {
                        githubEmail = emailItem.GetProperty("email").GetString();
                        break;
                    }
                }
            }

            if (string.IsNullOrEmpty(githubEmail))
            {
                throw new UnauthorizedAccessException("Không thể lấy email từ tài khoản GitHub. Vui lòng cho phép truy cập email.");
            }

            var githubName = githubUser.TryGetProperty("name", out var nameEl) && nameEl.ValueKind != JsonValueKind.Null
                ? nameEl.GetString()
                : githubUser.GetProperty("login").GetString();

            // Bước 3: Tìm hoặc tạo User mới
            githubEmail = EmailCanonicalizer.Normalize(githubEmail);
            var user = await _context.Users
                .Include(u => u.UserRoles)
                .ThenInclude(ur => ur.Role)
                .FirstOrDefaultAsync(u => u.Email.ToLower() == githubEmail);

            if (user?.IsDeleted == true)
            {
                throw new UnauthorizedAccessException("Unable to authenticate this account.");
            }

            if (user == null)
            {
                user = new User
                {
                    Id = Guid.NewGuid(),
                    Email = githubEmail,
                    FullName = githubName ?? githubEmail,
                    PasswordHash = string.Empty,
                    CreatedAt = DateTime.UtcNow,
                    IsDeleted = false
                };

                _context.Users.Add(user);

                var defaultRole = await _context.Roles.FirstOrDefaultAsync(r => r.Name == "Developer" || r.Name == "DEV");
                if (defaultRole != null)
                {
                    var ur = new UserRole
                    {
                        UserId = user.Id,
                        RoleId = defaultRole.Id,
                        Role = defaultRole
                    };
                    _context.UserRoles.Add(ur);
                    user.UserRoles = new List<UserRole> { ur };
                }

                await _context.SaveChangesAsync();
            }

            if (!user.IsActive)
            {
                throw new UnauthorizedAccessException("Unable to authenticate this account.");
            }

            var roles = user.UserRoles?.Select(ur => ur.Role.Name).ToList() ?? new List<string>();

            var accessToken = _jwtService.GenerateAccessToken(user, roles);
            var refreshToken = _jwtService.GenerateRefreshToken();

            user.RefreshToken = refreshToken;
            user.RefreshTokenExpiryTime = DateTime.UtcNow.AddDays(7);

            _context.RefreshTokens.Add(new RefreshToken 
            {
                Id = Guid.NewGuid(),
                UserId = user.Id,
                Token = refreshToken,
                DeviceId = request.Code?.Length > 10 ? "GitHub-App-SSO" : "SSO-WEB",
                ExpiryTime = DateTime.UtcNow.AddDays(7),
                IsRevoked = false
            });

            await _context.SaveChangesAsync();

            var response = new AuthResponseDto
            {
                AccessToken = accessToken,
                Id = user.Id,
                Email = user.Email,
                FullName = user.FullName,
                AvatarUrl = user.AvatarUrl,
                SystemRoles = roles.ToArray()
            };

            return (response, refreshToken);
        }

        public async Task<(string newAccessToken, string newRefreshToken)> RefreshTokenAsync(string accessToken, string refreshToken)
        {
            var principal = _jwtService.GetPrincipalFromExpiredToken(accessToken);
            if (principal == null) throw new UnauthorizedAccessException("Invalid access token or refresh token");

            var userIdString = principal.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
            if (!Guid.TryParse(userIdString, out Guid userId))
                throw new UnauthorizedAccessException("Invalid token claims");

            var user = await _context.Users
                .Include(u => u.UserRoles)
                .ThenInclude(ur => ur.Role)
                .FirstOrDefaultAsync(u => u.Id == userId);

            if (user == null || user.IsDeleted || !user.IsActive)
            {
                if (user != null)
                {
                    user.RefreshToken = null;
                    user.RefreshTokenExpiryTime = null;
                    var sessions = await _context.RefreshTokens
                        .Where(token => token.UserId == user.Id && !token.IsRevoked)
                        .ToListAsync();
                    foreach (var session in sessions)
                    {
                        session.IsRevoked = true;
                    }
                    await _context.SaveChangesAsync();
                }

                throw new UnauthorizedAccessException("Invalid access token or refresh token");
            }

            var activeSession = await _context.RefreshTokens.FirstOrDefaultAsync(token =>
                token.UserId == userId &&
                token.Token == refreshToken &&
                !token.IsRevoked &&
                token.ExpiryTime > DateTime.UtcNow);

            if (user.RefreshToken != refreshToken || user.RefreshTokenExpiryTime <= DateTime.UtcNow || activeSession == null)
            {
                throw new UnauthorizedAccessException("Invalid access token or refresh token");
            }

            var roles = user.UserRoles.Select(ur => ur.Role.Name).ToList();

            var newAccessToken = _jwtService.GenerateAccessToken(user, roles);
            var newRefreshToken = _jwtService.GenerateRefreshToken();

            user.RefreshToken = newRefreshToken;
            user.RefreshTokenExpiryTime = DateTime.UtcNow.AddDays(7);
            activeSession.IsRevoked = true;
            _context.RefreshTokens.Add(new RefreshToken
            {
                Id = Guid.NewGuid(),
                UserId = user.Id,
                Token = newRefreshToken,
                DeviceId = activeSession.DeviceId,
                ExpiryTime = user.RefreshTokenExpiryTime.Value,
                IsRevoked = false
            });

            await _context.SaveChangesAsync();

            return (newAccessToken, newRefreshToken);
        }

        public async Task RevokeTokenAsync(Guid userId)
        {
            var user = await _context.Users.FindAsync(userId);
            if (user == null) throw new ArgumentException("User not found");

            user.RefreshToken = null;
            user.RefreshTokenExpiryTime = null;
            
            await _context.SaveChangesAsync();
        }

        private async Task ActivatePendingProjectInvitesAsync(Guid userId, Guid? projectId = null)
        {
            var pendingProjects = await _context.ProjectMembers
                .Where(pm => pm.UserId == userId && !pm.Status && pm.LeftAt == null &&
                    (!projectId.HasValue || pm.ProjectId == projectId.Value))
                .ToListAsync();

            foreach (var projectMember in pendingProjects)
            {
                projectMember.Status = true;
            }

            var projectIds = pendingProjects.Select(member => member.ProjectId).ToList();
            var workspaceIds = await _context.Projects
                .Where(project => projectIds.Contains(project.Id))
                .Select(project => project.WorkspaceId)
                .Distinct()
                .ToListAsync();
            var workspaceMemberships = await _context.WorkspaceMembers
                .Where(member => member.UserId == userId && workspaceIds.Contains(member.WorkspaceId))
                .ToListAsync();
            foreach (var workspaceMember in workspaceMemberships)
            {
                workspaceMember.IsActive = true;
            }
        }

        private bool VerifyPassword(string inputPassword, string storedHash)
        {
            if(storedHash.StartsWith("$2a$") || storedHash.StartsWith("$2b$") || storedHash.StartsWith("$2y$")) {
                return BCrypt.Net.BCrypt.Verify(inputPassword, storedHash);
            }
            return inputPassword == storedHash;
        }

        public async Task RegisterAsync(RegisterRequestDto request)
        {
            var canonicalEmail = EmailCanonicalizer.Normalize(request.Email);
            var otpValidation = _otpService.ValidateOtp(canonicalEmail, request.OtpCode);
            if (otpValidation.Status == OtpValidationStatus.Locked)
                throw new OtpRateLimitException(otpValidation.RetryAfterSeconds);
            if (!otpValidation.IsValid)
            {
                throw new InvalidOperationException("Mã OTP không hợp lệ hoặc đã hết hạn.");
            }

            var existingUser = await _context.Users.FirstOrDefaultAsync(u => u.Email.ToLower() == canonicalEmail && !u.IsDeleted);
            if (existingUser != null && !string.IsNullOrEmpty(existingUser.PasswordHash))
            {
                throw new InvalidOperationException("Email da duoc su dung.");
            }

            var newUser = existingUser ?? new User
            {
                Id = Guid.NewGuid(),
                Email = canonicalEmail,
                CreatedAt = DateTime.UtcNow,
                IsDeleted = false
            };

            newUser.FullName = request.FullName;
            newUser.PasswordHash = BCrypt.Net.BCrypt.HashPassword(request.Password);
            newUser.IsActive = true;
            newUser.UpdatedAt = DateTime.UtcNow;

            if (existingUser == null)
            {
                _context.Users.Add(newUser);
            }

            // Assign default role (e.g. Developer)
            var defaultRole = await _context.Roles.FirstOrDefaultAsync(r => r.Name == "Developer" || r.Name == "DEV");
            if (defaultRole != null)
            {
                var hasRole = await _context.UserRoles.AnyAsync(ur => ur.UserId == newUser.Id && ur.RoleId == defaultRole.Id);
                if (!hasRole)
                {
                    _context.UserRoles.Add(new UserRole
                    {
                        UserId = newUser.Id,
                        RoleId = defaultRole.Id
                    });
                }
            }

            await ActivatePendingProjectInvitesAsync(newUser.Id);

            await _context.SaveChangesAsync();
        }

        public async Task ResetPasswordAsync(ResetPasswordRequestDto request)
        {
            var email = EmailCanonicalizer.Normalize(request.Email);

            // OTP dùng 1 lần: verify-otp đã cấp lại otpToken (OTP mới) cho chính email này.
            // ValidateOtp sẽ xoá token khỏi cache khi hợp lệ.
            var otpValidation = _otpService.ValidateOtp(email, request.OtpToken?.Trim() ?? string.Empty);
            if (otpValidation.Status == OtpValidationStatus.Locked)
                throw new OtpRateLimitException(otpValidation.RetryAfterSeconds);
            if (!otpValidation.IsValid)
                throw new UnauthorizedAccessException("Mã xác thực không hợp lệ hoặc đã hết hạn. Vui lòng thực hiện lại bước quên mật khẩu.");

            var user = await _context.Users.FirstOrDefaultAsync(u => u.Email.ToLower() == email && !u.IsDeleted && u.IsActive);
            if (user == null)
                throw new ArgumentException("Không thể đặt lại mật khẩu cho tài khoản này.");

            // Dùng lại cơ chế hash hiện có (BCrypt). Không đổi IsActive để tránh kích hoạt lại tài khoản đã bị khoá.
            user.PasswordHash = BCrypt.Net.BCrypt.HashPassword(request.NewPassword);
            user.UpdatedAt = DateTime.UtcNow;

            await _context.SaveChangesAsync();
        }

        public async Task AcceptInviteAsync(Guid userId)
        {
            var user = await _context.Users.FindAsync(userId);
            if (user == null)
                throw new ArgumentException("User không tồn tại trong hệ thống.");

            user.IsActive = true; // Đảm bảo đã active

            // Kích hoạt Project Members (những dự án đã được mời nhưng đang pending)
            var pendingProjects = await _context.ProjectMembers
                .Where(pm => pm.UserId == user.Id && !pm.Status && pm.LeftAt == null)
                .ToListAsync();

            if (pendingProjects.Count == 0)
                throw new InvalidOperationException("Bạn không có lời mời dự án nào đang chờ.");

            foreach (var pm in pendingProjects)
            {
                pm.Status = true;
            }

            var pendingProjectIds = pendingProjects.Select(member => member.ProjectId).ToList();
            var pendingWorkspaceIds = await _context.Projects
                .Where(project => pendingProjectIds.Contains(project.Id))
                .Select(project => project.WorkspaceId)
                .Distinct()
                .ToListAsync();
            var workspaceMemberships = await _context.WorkspaceMembers
                .Where(member => member.UserId == user.Id && pendingWorkspaceIds.Contains(member.WorkspaceId))
                .ToListAsync();
            foreach (var workspaceMember in workspaceMemberships)
            {
                workspaceMember.IsActive = true;
            }

            await _context.SaveChangesAsync();
        }

        public async Task<InviteInfoDto> GetInviteInfoAsync(string token)
        {
            var invite = await FindValidInviteTokenAsync(token);
            var user = invite.User;
            var inviteProjectId = GetInviteProjectId(invite);

            var projectNames = await _context.ProjectMembers
                .Where(pm => pm.UserId == user.Id && !pm.Status && pm.LeftAt == null &&
                    (!inviteProjectId.HasValue || pm.ProjectId == inviteProjectId.Value))
                .Select(pm => pm.Project.Name)
                .ToArrayAsync();

            return new InviteInfoDto
            {
                Email = user.Email,
                FullName = user.FullName,
                IsRegistered = !string.IsNullOrEmpty(user.PasswordHash),
                ProjectNames = projectNames,
                ExpiresAt = invite.ExpiryTime
            };
        }

        public async Task<AcceptInviteResultDto> AcceptInviteTokenAsync(AcceptInviteTokenRequestDto request)
        {
            var invite = await FindValidInviteTokenAsync(request.Token);
            var user = invite.User;

            if (!string.IsNullOrEmpty(user.PasswordHash) && !user.IsActive)
            {
                throw new UnauthorizedAccessException("Account is suspended.");
            }

            var isNewInvitedUser = string.IsNullOrEmpty(user.PasswordHash);
            if (isNewInvitedUser)
            {
                if (string.IsNullOrWhiteSpace(request.FullName))
                    throw new ArgumentException("Full name is required.");

                if (string.IsNullOrWhiteSpace(request.Password))
                    throw new ArgumentException("Password is required.");

                ValidateInvitePassword(request.Password);

                user.FullName = request.FullName.Trim();
                user.PasswordHash = BCrypt.Net.BCrypt.HashPassword(request.Password);
                user.IsActive = true;
                user.UpdatedAt = DateTime.UtcNow;
            }
            else
            {
                user.IsActive = true;
                user.UpdatedAt = DateTime.UtcNow;
            }

            await ActivatePendingProjectInvitesAsync(user.Id, GetInviteProjectId(invite));
            invite.IsRevoked = true;
            await _context.SaveChangesAsync();

            if (!isNewInvitedUser)
            {
                return new AcceptInviteResultDto
                {
                    Email = user.Email,
                    RequiresLogin = true,
                    RedirectPath = "/dashboard"
                };
            }

            var tokenResult = await GenerateTokensForUser(user, "Invite-Accept");
            return new AcceptInviteResultDto
            {
                Email = user.Email,
                RequiresLogin = false,
                RedirectPath = "/dashboard",
                Response = tokenResult.response,
                RefreshToken = tokenResult.refreshToken
            };
        }

        private async Task<RefreshToken> FindValidInviteTokenAsync(string rawToken)
        {
            if (string.IsNullOrWhiteSpace(rawToken))
                throw new ArgumentException("Invite token is missing.");

            var tokenHash = HashToken(rawToken.Trim());
            var invite = await _context.RefreshTokens
                .Include(rt => rt.User)
                    .ThenInclude(u => u.UserRoles)
                    .ThenInclude(ur => ur.Role)
                .FirstOrDefaultAsync(rt =>
                    rt.Token == tokenHash &&
                    (rt.DeviceId == "Invite" || (rt.DeviceId != null && rt.DeviceId.StartsWith("Invite:"))) &&
                    !rt.IsRevoked &&
                    rt.ExpiryTime > DateTime.UtcNow);

            if (invite == null)
                throw new UnauthorizedAccessException("Invite link is invalid or expired.");

            return invite;
        }

        private static Guid? GetInviteProjectId(RefreshToken invite)
        {
            const string prefix = "Invite:";
            if (invite.DeviceId?.StartsWith(prefix, StringComparison.Ordinal) != true)
            {
                return null;
            }

            return Guid.TryParseExact(invite.DeviceId[prefix.Length..], "N", out var projectId)
                ? projectId
                : null;
        }

        private static string HashToken(string token)
        {
            var hashBytes = SHA256.HashData(Encoding.UTF8.GetBytes(token));
            return Convert.ToHexString(hashBytes);
        }

        private static void ValidateInvitePassword(string password)
        {
            if (password.Length < 6)
                throw new ArgumentException("Password must be at least 6 characters.");

            if (!Regex.IsMatch(password, @"^(?=.*[A-Z])(?=.*\d)(?=.*[@$!%*?&])[A-Za-z\d@$!%*?&]{6,}$"))
                throw new ArgumentException("Password must contain at least 1 uppercase letter, 1 number, and 1 special character.");
        }
    }
}
