using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;
using System.Text.Json;
using TaskManagement.Application.Common;
using TaskManagement.Application.DTOs.Auth;
using TaskManagement.Application.Interfaces;
using TaskManagement.Domain.Entities;
using TaskManagement.Infrastructure.Data;

namespace TaskManagement.API.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class AuthController : ControllerBase
    {
        private readonly IAuthService _authService;
        private readonly IOtpService _otpService;
        private readonly IEmailService _emailService;
        private readonly ApplicationDbContext _context;

        public AuthController(
            IAuthService authService,
            IOtpService otpService,
            IEmailService emailService,
            ApplicationDbContext context)
        {
            _authService = authService;
            _otpService = otpService;
            _emailService = emailService;
            _context = context;
        }

        [HttpPost("send-otp")]
        public async Task<IActionResult> SendOtp([FromBody] SendOtpRequestDto request)
        {
            try
            {
                var email = EmailCanonicalizer.Normalize(request.Email);
                if (string.IsNullOrWhiteSpace(email))
                {
                    return BadRequest(new { statusCode = 400, message = "Email là bắt buộc." });
                }

                var purpose = (request.Purpose ?? "register").Trim().ToLowerInvariant();
                if (purpose is not ("register" or "login" or "reset" or "forgot-password" or "invite"))
                {
                    return BadRequest(new { statusCode = 400, message = "Mục đích gửi OTP không hợp lệ." });
                }

                var account = await _context.Users
                    .AsNoTracking()
                    .FirstOrDefaultAsync(user => user.Email.ToLower() == email);
                var shouldSend = purpose switch
                {
                    "register" => account == null,
                    "login" or "reset" or "forgot-password" => account is { IsActive: true, IsDeleted: false },
                    "invite" => true,
                    _ => false
                };

                var otpCode = _otpService.GenerateOtp();
                var issueResult = _otpService.StoreOtp(email, otpCode, GetOtpFingerprint());
                if (!issueResult.Issued)
                {
                    return StatusCode(StatusCodes.Status429TooManyRequests, new
                    {
                        statusCode = StatusCodes.Status429TooManyRequests,
                        message = issueResult.Locked
                            ? "Quá nhiều yêu cầu hoặc lần thử. Vui lòng thử lại sau."
                            : "Vui lòng chờ trước khi yêu cầu mã OTP mới.",
                        retryAfterSeconds = issueResult.RetryAfterSeconds
                    });
                }

                if (shouldSend)
                {
                    try
                    {
                        await _emailService.SendOtpEmailAsync(email, otpCode);
                    }
                    catch
                    {
                        _otpService.InvalidateOtp(email);
                    }
                }

                return Ok(new { statusCode = 200, message = "Nếu email hợp lệ, mã OTP sẽ được gửi đến hộp thư của bạn." });
            }
            catch (Exception ex)
            {
                return BadRequest(new { statusCode = 400, message = ex.Message });
            }
        }

        [HttpPost("verify-otp")]
        public IActionResult VerifyOtp([FromBody] VerifyOtpRequestDto request)
        {
            var validation = _otpService.ValidateOtp(request.Email, request.OtpCode, GetOtpFingerprint());

            if (validation.Status == OtpValidationStatus.Locked)
            {
                return StatusCode(StatusCodes.Status429TooManyRequests, new
                {
                    statusCode = StatusCodes.Status429TooManyRequests,
                    message = "Quá nhiều lần thử OTP không hợp lệ. Vui lòng thử lại sau.",
                    verified = false,
                    retryAfterSeconds = validation.RetryAfterSeconds
                });
            }

            if (!validation.IsValid)
            {
                return BadRequest(new { statusCode = 400, message = "Mã OTP không hợp lệ hoặc đã hết hạn.", verified = false });
            }

            var verificationToken = _otpService.IssueVerificationToken(request.Email, GetOtpFingerprint());

            return Ok(new { statusCode = 200, message = "Xác thực OTP thành công.", verified = true, otpToken = verificationToken });
        }

        private string GetOtpFingerprint()
        {
            return HttpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown";
        }

        private ObjectResult OtpRateLimited(OtpRateLimitException exception)
        {
            return StatusCode(StatusCodes.Status429TooManyRequests, new
            {
                statusCode = StatusCodes.Status429TooManyRequests,
                message = "Quá nhiều yêu cầu hoặc lần thử OTP. Vui lòng thử lại sau.",
                retryAfterSeconds = exception.RetryAfterSeconds
            });
        }

        /// <summary>
        /// Đặt lại mật khẩu cho user quên mật khẩu (public). Yêu cầu otpToken hợp lệ do verify-otp cấp.
        /// </summary>
        [HttpPost("reset-password")]
        public async Task<IActionResult> ResetPassword([FromBody] ResetPasswordRequestDto request)
        {
            try
            {
                await _authService.ResetPasswordAsync(request);
                return Ok(new { statusCode = 200, message = "Đặt lại mật khẩu thành công. Vui lòng đăng nhập bằng mật khẩu mới." });
            }
            catch (OtpRateLimitException ex)
            {
                return OtpRateLimited(ex);
            }
            catch (UnauthorizedAccessException ex)
            {
                return BadRequest(new { statusCode = 400, message = ex.Message });
            }
            catch (ArgumentException ex)
            {
                return BadRequest(new { statusCode = 400, message = ex.Message });
            }
            catch (Exception)
            {
                return StatusCode(500, new { statusCode = 500, message = "Internal server error" });
            }
        }

        [HttpPost("login")]
        public async Task<IActionResult> Login([FromBody] LoginRequestDto request)
        {
            try
            {
                var result = await _authService.LoginAsync(request);

                if (result.requires2FA)
                {
                    return Ok(new { statusCode = 200, message = "Requires 2FA", requires2FA = true });
                }

                var cookieOptions = new CookieOptions
                {
                    HttpOnly = true,
                    Secure = Request.IsHttps,
                    SameSite = SameSiteMode.Lax,
                    Expires = DateTime.UtcNow.AddDays(7)
                };
                Response.Cookies.Append("refreshToken", result.refreshToken!, cookieOptions);

                await RecordLoginActivityAsync(result.response!.Id, "Password");
                return Ok(new { statusCode = 200, message = "Success", data = result.response });
            }
            catch (OtpRateLimitException ex)
            {
                return OtpRateLimited(ex);
            }
            catch (UnauthorizedAccessException ex)
            {
                return Unauthorized(new { statusCode = 401, message = ex.Message });
            }
        }

        [HttpPost("accept-invite")]
        [Microsoft.AspNetCore.Authorization.Authorize]
        public async Task<IActionResult> AcceptInvite()
        {
            var userIdClaim = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrEmpty(userIdClaim) || !Guid.TryParse(userIdClaim, out var userId))
            {
                return Unauthorized(new { statusCode = 401, message = "Không xác định được danh tính người dùng." });
            }

            try
            {
                await _authService.AcceptInviteAsync(userId);
                return Ok(new { statusCode = 200, message = "Chào mừng bạn! Tính năng được mở khóa thành công." });
            }
            catch (ArgumentException ex)
            {
                return BadRequest(new { statusCode = 400, message = ex.Message });
            }
            catch (InvalidOperationException ex)
            {
                return Conflict(new { statusCode = 409, message = ex.Message });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { statusCode = 500, message = "Internal server error: " + ex.Message });
            }
        }

        /// <summary>
        /// Lấy thông tin lời mời từ token trong link email (không cần đăng nhập)
        /// </summary>
        [HttpGet("invite-info")]
        public async Task<IActionResult> GetInviteInfo([FromQuery] string token)
        {
            try
            {
                var result = await _authService.GetInviteInfoAsync(token);
                return Ok(new { statusCode = 200, data = result });
            }
            catch (ArgumentException ex)
            {
                return BadRequest(new { statusCode = 400, message = ex.Message });
            }
            catch (UnauthorizedAccessException ex)
            {
                return BadRequest(new { statusCode = 400, message = ex.Message });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { statusCode = 500, message = "Internal server error: " + ex.Message });
            }
        }

        /// <summary>
        /// Chấp nhận lời mời qua token: thiết lập mật khẩu (nếu chưa đăng ký) hoặc kích hoạt tài khoản (nếu đã đăng ký)
        /// </summary>
        [HttpPost("accept-invite-token")]
        public async Task<IActionResult> AcceptInviteToken([FromBody] TaskManagement.Application.DTOs.Auth.AcceptInviteTokenRequestDto request)
        {
            try
            {
                var result = await _authService.AcceptInviteTokenAsync(request);

                // Nếu là user mới (chưa đăng ký), set refresh token cookie và trả về access token
                if (!result.RequiresLogin && !string.IsNullOrEmpty(result.RefreshToken))
                {
                    var cookieOptions = new CookieOptions
                    {
                        HttpOnly = true,
                        Secure = Request.IsHttps,
                        SameSite = SameSiteMode.Lax,
                        Expires = DateTime.UtcNow.AddDays(7)
                    };
                    Response.Cookies.Append("refreshToken", result.RefreshToken, cookieOptions);
                }

                return Ok(new
                {
                    statusCode = 200,
                    message = "Lời mời đã được chấp nhận thành công.",
                    data = new
                    {
                        requiresLogin = result.RequiresLogin,
                        redirectPath = result.RedirectPath,
                        auth = result.Response == null ? null : new
                        {
                            accessToken = result.Response.AccessToken,
                            id = result.Response.Id,
                            fullName = result.Response.FullName,
                            email = result.Response.Email,
                            avatarUrl = result.Response.AvatarUrl,
                            systemRoles = result.Response.SystemRoles
                        }
                    }
                });
            }
            catch (ArgumentException ex)
            {
                return BadRequest(new { statusCode = 400, message = ex.Message });
            }
            catch (UnauthorizedAccessException ex)
            {
                return BadRequest(new { statusCode = 400, message = ex.Message });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { statusCode = 500, message = "Internal server error: " + ex.Message });
            }
        }

        public class Login2FARequestDto : LoginRequestDto
        {
            public string OtpCode { get; set; } = string.Empty;
        }

        [HttpPost("login-2fa")]
        public async Task<IActionResult> Login2FA([FromBody] Login2FARequestDto request)
        {
            try
            {
                var (response, refreshToken) = await _authService.Login2FAAsync(request.Email, request.Password, request.OtpCode);

                var cookieOptions = new CookieOptions
                {
                    HttpOnly = true,
                    Secure = Request.IsHttps,
                    SameSite = SameSiteMode.Lax,
                    Expires = DateTime.UtcNow.AddDays(7)
                };
                Response.Cookies.Append("refreshToken", refreshToken, cookieOptions);

                await RecordLoginActivityAsync(response.Id, "Password+2FA");
                return Ok(new { statusCode = 200, message = "Success", data = response });
            }
            catch (OtpRateLimitException ex)
            {
                return OtpRateLimited(ex);
            }
            catch (UnauthorizedAccessException ex)
            {
                return Unauthorized(new { statusCode = 401, message = ex.Message });
            }
        }

        [HttpPost("google-login")]
        public async Task<IActionResult> GoogleLogin([FromBody] GoogleLoginRequestDto request)
        {
            try
            {
                var (response, refreshToken) = await _authService.GoogleLoginAsync(request);

                var cookieOptions = new CookieOptions
                {
                    HttpOnly = true,
                    Secure = Request.IsHttps,
                    SameSite = SameSiteMode.Lax,
                    Expires = DateTime.UtcNow.AddDays(7)
                };
                Response.Cookies.Append("refreshToken", refreshToken, cookieOptions);

                await RecordLoginActivityAsync(response.Id, "Google SSO");
                return Ok(new { statusCode = 200, message = "Success", data = response });
            }
            catch (Exception ex)
            {
                Console.WriteLine("GoogleLogin failure: " + ex);
                return BadRequest(new { statusCode = 400, message = "Không thể xác thực với Google: " + ex.Message });
            }
        }

        [HttpPost("github-login")]
        public async Task<IActionResult> GitHubLogin([FromBody] GitHubLoginRequestDto request)
        {
            try
            {
                var (response, refreshToken) = await _authService.GitHubLoginAsync(request);

                var cookieOptions = new CookieOptions
                {
                    HttpOnly = true,
                    Secure = Request.IsHttps,
                    SameSite = SameSiteMode.Strict,
                    Expires = DateTime.UtcNow.AddDays(7)
                };
                Response.Cookies.Append("refreshToken", refreshToken, cookieOptions);

                await RecordLoginActivityAsync(response.Id, "GitHub SSO");
                return Ok(new { statusCode = 200, message = "Success", data = response });
            }
            catch (Exception ex)
            {
                return Unauthorized(new { statusCode = 401, message = ex.Message });
            }
        }

        [HttpPost("register")]
        public async Task<IActionResult> Register([FromBody] RegisterRequestDto request)
        {
            try
            {
                await _authService.RegisterAsync(request);
                return Ok(new { statusCode = 200, message = "Đăng ký thành công" });
            }
            catch (OtpRateLimitException ex)
            {
                return OtpRateLimited(ex);
            }
            catch (InvalidOperationException ex)
            {
                return BadRequest(new { statusCode = 400, message = ex.Message });
            }
            catch (Exception)
            {
                return StatusCode(500, new { statusCode = 500, message = "Internal server error" });
            }
        }

        [HttpPost("refresh-token")]
        public async Task<IActionResult> RefreshToken()
        {
            try
            {
                var refreshToken = Request.Cookies["refreshToken"];
                if (string.IsNullOrEmpty(refreshToken))
                {
                    return Unauthorized(new { statusCode = 401, message = "Refresh token is missing" });
                }

                var authHeader = Request.Headers["Authorization"].FirstOrDefault();
                if (authHeader == null || !authHeader.StartsWith("Bearer "))
                {
                    return Unauthorized(new { statusCode = 401, message = "Access token is missing" });
                }

                var accessToken = authHeader.Substring("Bearer ".Length).Trim();
                var (newAccessToken, newRefreshToken) = await _authService.RefreshTokenAsync(accessToken, refreshToken);

                var cookieOptions = new CookieOptions
                {
                    HttpOnly = true,
                    Secure = Request.IsHttps,
                    SameSite = SameSiteMode.Lax,
                    Expires = DateTime.UtcNow.AddDays(7)
                };
                Response.Cookies.Append("refreshToken", newRefreshToken, cookieOptions);

                return Ok(new { statusCode = 200, message = "Success", data = new { accessToken = newAccessToken } });
            }
            catch (Exception ex)
            {
                return Unauthorized(new { statusCode = 401, message = ex.Message });
            }
        }

        [HttpPost("dev-login")]
        public async Task<IActionResult> DevLogin(
            [FromServices] IJwtService jwtService,
            [FromServices] ApplicationDbContext context,
            [FromServices] IWebHostEnvironment env)
        {
            if (!env.IsDevelopment())
            {
                return NotFound();
            }

            try
            {
                var email = "dev@sprinta.local";
                var user = await context.Users
                    .Include(u => u.UserRoles)
                    .ThenInclude(ur => ur.Role)
                    .FirstOrDefaultAsync(u => u.Email == email && !u.IsDeleted);

                if (user == null)
                {
                    user = new TaskManagement.Domain.Entities.User
                    {
                        Id = Guid.NewGuid(),
                        Email = email,
                        FullName = "Dev User",
                        PasswordHash = BCrypt.Net.BCrypt.HashPassword("dev123"),
                        CreatedAt = DateTime.UtcNow,
                        IsDeleted = false,
                        IsActive = true
                    };
                    context.Users.Add(user);

                    var adminRole = await context.Roles.FirstOrDefaultAsync(r => r.Name == "Admin");
                    if (adminRole == null)
                    {
                        adminRole = new TaskManagement.Domain.Entities.Role
                        {
                            Id = Guid.NewGuid(),
                            Name = "Admin",
                            Description = "System Administrator"
                        };
                        context.Roles.Add(adminRole);
                    }

                    var ur = new TaskManagement.Domain.Entities.UserRole
                    {
                        UserId = user.Id,
                        RoleId = adminRole.Id,
                        Role = adminRole
                    };
                    context.UserRoles.Add(ur);
                    user.UserRoles = new List<TaskManagement.Domain.Entities.UserRole> { ur };

                    await context.SaveChangesAsync();
                }

                if (!user.IsActive || user.IsDeleted)
                {
                    return Unauthorized(new { statusCode = 401, message = "Unable to authenticate this account." });
                }

                var allProjects = await context.Projects.ToListAsync();
                foreach (var proj in allProjects)
                {
                    var isMember = await context.ProjectMembers.AnyAsync(pm => pm.ProjectId == proj.Id && pm.UserId == user.Id);
                    if (!isMember)
                    {
                        context.ProjectMembers.Add(new TaskManagement.Domain.Entities.ProjectMember
                        {
                            ProjectId = proj.Id,
                            UserId = user.Id,
                            ProjectRole = "PM",
                            JoinedAt = DateTime.UtcNow,
                            Status = true
                        });
                    }
                }

                var allWorkspaces = await context.Workspaces.ToListAsync();
                foreach (var ws in allWorkspaces)
                {
                    var isWsMember = await context.WorkspaceMembers.AnyAsync(wm => wm.WorkspaceId == ws.Id && wm.UserId == user.Id);
                    if (!isWsMember)
                    {
                        context.WorkspaceMembers.Add(new TaskManagement.Domain.Entities.WorkspaceMember
                        {
                            WorkspaceId = ws.Id,
                            UserId = user.Id,
                            WorkspaceRole = "ADMIN",
                            JoinedAt = DateTime.UtcNow,
                            IsActive = true
                        });
                    }
                }

                await context.SaveChangesAsync();

                var roles = user.UserRoles?
                    .Where(ur => ur.Role != null)
                    .Select(ur => ur.Role!.Name)
                    .ToList() ?? new List<string>();

                if (roles.Count == 0)
                {
                    roles.Add("Admin");
                }

                var accessToken = jwtService.GenerateAccessToken(user, roles);
                var refreshToken = jwtService.GenerateRefreshToken();

                user.RefreshToken = refreshToken;
                user.RefreshTokenExpiryTime = DateTime.UtcNow.AddDays(7);

                context.RefreshTokens.Add(new TaskManagement.Domain.Entities.RefreshToken
                {
                    Id = Guid.NewGuid(),
                    UserId = user.Id,
                    Token = refreshToken,
                    DeviceId = "DevMode",
                    UserAgent = Request.Headers["User-Agent"].FirstOrDefault(),
                    IpAddress = HttpContext.Connection.RemoteIpAddress?.ToString(),
                    CreatedAt = DateTime.UtcNow,
                    ExpiryTime = DateTime.UtcNow.AddDays(7),
                    IsRevoked = false
                });

                await context.SaveChangesAsync();

                var cookieOptions = new CookieOptions
                {
                    HttpOnly = true,
                    Secure = false,
                    SameSite = SameSiteMode.Lax,
                    Expires = DateTime.UtcNow.AddDays(7)
                };
                Response.Cookies.Append("refreshToken", refreshToken, cookieOptions);

                return Ok(new
                {
                    statusCode = 200,
                    message = "Dev login thành công",
                    data = new TaskManagement.Application.DTOs.Auth.AuthResponseDto
                    {
                        AccessToken = accessToken,
                        Id = user.Id,
                        Email = user.Email,
                        FullName = user.FullName,
                        AvatarUrl = user.AvatarUrl,
                        SystemRoles = roles.ToArray()
                    }
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { statusCode = 500, message = "Dev login lỗi: " + ex.Message });
            }
        }

        [HttpPost("logout")]
        public async Task<IActionResult> Logout()
        {
            try
            {
                var userIdString = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
                if (Guid.TryParse(userIdString, out Guid userId))
                {
                    await _authService.RevokeTokenAsync(userId);
                }

                Response.Cookies.Delete("refreshToken");
                return Ok(new { statusCode = 200, message = "Logged out successfully" });
            }
            catch (Exception ex)
            {
                return BadRequest(new { statusCode = 400, message = ex.Message });
            }
        }

        /// <summary>
        /// Log login activity to SystemAuditLogs and update the latest RefreshToken with session metadata.
        /// </summary>
        private async Task RecordLoginActivityAsync(Guid userId, string loginMethod)
        {
            try
            {
                var userAgent = Request.Headers["User-Agent"].FirstOrDefault() ?? "Unknown";
                var ipAddress = HttpContext.Connection.RemoteIpAddress?.ToString() ?? "Unknown";

                // Update the most recent refresh token for this user with session metadata
                var latestToken = await _context.RefreshTokens
                    .Where(rt => rt.UserId == userId && !rt.IsRevoked)
                    .OrderByDescending(rt => rt.ExpiryTime)
                    .FirstOrDefaultAsync();

                if (latestToken != null)
                {
                    latestToken.UserAgent = userAgent;
                    latestToken.IpAddress = ipAddress;
                    latestToken.CreatedAt = DateTime.UtcNow;
                }

                // Write a login audit log entry
                _context.SystemAuditLogs.Add(new SystemAuditLog
                {
                    Id = Guid.NewGuid(),
                    UserId = userId,
                    Action = "Login",
                    Resource = "Auth",
                    Status = "Success",
                    IPAddress = ipAddress,
                    Details = JsonSerializer.Serialize(new { method = loginMethod, userAgent }),
                    CreatedAt = DateTime.UtcNow
                });

                await _context.SaveChangesAsync();
            }
            catch (Exception ex)
            {
                // Fail silently - don't break the login flow for audit logging
                Console.WriteLine($"Failed to record login activity: {ex.Message}");
            }
        }
        [HttpGet("me/permissions")]
        [Microsoft.AspNetCore.Authorization.Authorize]
        public async Task<IActionResult> GetMyPermissions()
        {
            var userIdString = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrEmpty(userIdString) || !Guid.TryParse(userIdString, out var userId))
            {
                return Unauthorized(new { statusCode = 401, message = "Không xác định được danh tính người dùng." });
            }

            var permissions = await _context.UserRoles
                .AsNoTracking()
                .Where(ur => ur.UserId == userId)
                .SelectMany(ur => ur.Role.RolePermissions)
                .Select(rp => rp.Permission.Code)
                .Distinct()
                .ToListAsync();

            return Ok(new { statusCode = 200, data = permissions });
        }
    }
}
