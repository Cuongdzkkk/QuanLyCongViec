using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;
using System.Text.Json;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.AspNetCore.WebUtilities;
using System.Security.Cryptography;
using TaskManagement.Application.Auth;
using TaskManagement.Application.Common;
using TaskManagement.Application.DTOs.Auth;
using TaskManagement.Application.Interfaces;
using TaskManagement.Domain.Entities;
using TaskManagement.Infrastructure.Data;
using TaskManagement.Infrastructure.Services;

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
        private readonly IConfiguration _configuration;
        private readonly IGoogleAuthorizationCodeExchange _googleAuthorizationCodeExchange;
        private readonly IGoogleLoginOAuthStateStore _googleLoginOAuthStateStore;
        private readonly IOAuthStateStore _oauthStateStore;
        private readonly ILogger<AuthController> _logger;

        private const string GoogleLoginStateCookieName = "sprinta_google_login_state";
        private const string GitHubLoginStateCookieName = "sprinta_github_login_state";
        private const string ExternalLinkStateCookieName = "sprinta_external_link_state";
        private const int OAuthStateLifetimeMinutes = 5;
        private const string GitHubLoginStatePrefix = "login.";
        private const string GitHubLinkStatePrefix = "link.";

        public AuthController(
            IAuthService authService,
            IOtpService otpService,
            IEmailService emailService,
            ApplicationDbContext context,
            IConfiguration configuration,
            IGoogleAuthorizationCodeExchange googleAuthorizationCodeExchange,
            IGoogleLoginOAuthStateStore googleLoginOAuthStateStore,
            ILogger<AuthController>? logger = null,
            IOAuthStateStore? oauthStateStore = null)
        {
            _authService = authService;
            _otpService = otpService;
            _emailService = emailService;
            _context = context;
            _configuration = configuration;
            _googleAuthorizationCodeExchange = googleAuthorizationCodeExchange;
            _googleLoginOAuthStateStore = googleLoginOAuthStateStore;
            _oauthStateStore = oauthStateStore ?? new OAuthStateStore();
            _logger = logger ?? NullLogger<AuthController>.Instance;
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
                    .Include(user => user.ExternalLogins)
                    .FirstOrDefaultAsync(user => user.Email.ToLower() == email);
                var canCompleteRegistration = account == null ||
                    (!account.IsDeleted &&
                     !account.IsActive &&
                     string.IsNullOrWhiteSpace(account.PasswordHash) &&
                     account.ExternalLogins.Count == 0);

                if (purpose == "register" && !canCompleteRegistration)
                {
                    return Conflict(new
                    {
                        statusCode = StatusCodes.Status409Conflict,
                        message = "Email này đã được sử dụng. Vui lòng đăng nhập hoặc sử dụng Quên mật khẩu."
                    });
                }

                var shouldSend = purpose switch
                {
                    "register" => canCompleteRegistration,
                    "login" or "reset" or "forgot-password" => account is { IsActive: true, IsDeleted: false },
                    "invite" => true,
                    _ => false
                };

                var otpCode = _otpService.GenerateOtp();
                var issueResult = _otpService.StoreOtp(email, otpCode, GetOtpFingerprint());
                if (!issueResult.Issued)
                {
                    var retryAfterSeconds = Math.Max(1, issueResult.RetryAfterSeconds);
                    Response.Headers["Retry-After"] = retryAfterSeconds.ToString();
                    return StatusCode(StatusCodes.Status429TooManyRequests, new
                    {
                        statusCode = StatusCodes.Status429TooManyRequests,
                        message = issueResult.Locked
                            ? "Quá nhiều yêu cầu hoặc lần thử. Vui lòng thử lại sau."
                            : "Vui lòng chờ trước khi yêu cầu mã OTP mới.",
                        retryAfterSeconds
                    });
                }

                if (shouldSend)
                {
                    try
                    {
                        await _emailService.SendOtpEmailAsync(email, otpCode);
                    }
                    catch (Exception exception)
                    {
                        _otpService.InvalidateOtp(email);
                        _logger.LogWarning(
                            "OTP email delivery failed for {Purpose} request. ErrorType={ErrorType}",
                            purpose,
                            exception.GetType().Name);
                        return StatusCode(StatusCodes.Status503ServiceUnavailable, new
                        {
                            statusCode = StatusCodes.Status503ServiceUnavailable,
                            message = "Không thể gửi email xác thực lúc này. Vui lòng thử lại sau."
                        });
                    }
                }

                var configuredCooldown = _configuration["OtpSecurity:ResendCooldownSeconds"];
                var resendCooldownSeconds = int.TryParse(configuredCooldown, out var parsedCooldown)
                    ? Math.Max(1, parsedCooldown)
                    : 60;

                return Ok(new
                {
                    statusCode = 200,
                    message = "Nếu email hợp lệ, mã OTP sẽ được gửi đến hộp thư của bạn.",
                    resendCooldownSeconds
                });
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
                        projectId = result.ProjectId,
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
            catch (InvalidOperationException ex)
            {
                return Conflict(new { statusCode = 409, message = ex.Message });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { statusCode = 500, message = "Internal server error: " + ex.Message });
            }
        }

        [HttpPost("/api/project-invitations/{invitationId:guid}/accept")]
        [Microsoft.AspNetCore.Authorization.Authorize]
        public async Task<IActionResult> AcceptProjectInvitation(Guid invitationId)
        {
            if (!Guid.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var userId))
                return Unauthorized(new { statusCode = 401, message = "Authenticated user is required." });

            try
            {
                var result = await _authService.AcceptProjectInvitationAsync(invitationId, userId);
                return Ok(new
                {
                    statusCode = 200,
                    message = "Lời mời đã được chấp nhận.",
                    data = new { projectId = result.ProjectId, redirectPath = result.RedirectPath }
                });
            }
            catch (KeyNotFoundException ex)
            {
                return NotFound(new { statusCode = 404, message = ex.Message });
            }
            catch (InvalidOperationException ex)
            {
                return Conflict(new { statusCode = 409, message = ex.Message });
            }
        }

        [HttpPost("/api/project-invitations/{invitationId:guid}/decline")]
        [Microsoft.AspNetCore.Authorization.Authorize]
        public async Task<IActionResult> DeclineProjectInvitation(Guid invitationId)
        {
            if (!Guid.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var userId))
                return Unauthorized(new { statusCode = 401, message = "Authenticated user is required." });

            try
            {
                await _authService.DeclineProjectInvitationAsync(invitationId, userId);
                return Ok(new { statusCode = 200, message = "Lời mời đã được từ chối." });
            }
            catch (KeyNotFoundException ex)
            {
                return NotFound(new { statusCode = 404, message = ex.Message });
            }
            catch (InvalidOperationException ex)
            {
                return Conflict(new { statusCode = 409, message = ex.Message });
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
                _logger.LogInformation("Google sign-in succeeded for internal user {UserId}", response.Id);
                return Ok(new { statusCode = 200, message = "Success", data = response });
            }
            catch (ArgumentException)
            {
                _logger.LogWarning("Google sign-in rejected: invalid request");
                return BadRequest(new { statusCode = 400, message = "Google credential is required." });
            }
            catch (GoogleCredentialException)
            {
                _logger.LogWarning("Google sign-in rejected: invalid credential");
                return Unauthorized(new { statusCode = 401, message = "Google credential is invalid or expired." });
            }
            catch (GoogleAccountForbiddenException)
            {
                _logger.LogWarning("Google sign-in rejected: account unavailable");
                return StatusCode(StatusCodes.Status403Forbidden, new
                {
                    statusCode = StatusCodes.Status403Forbidden,
                    message = "This account cannot sign in with Google."
                });
            }
            catch (GoogleAccountConflictException)
            {
                _logger.LogWarning("Google sign-in rejected: account linking conflict");
                return Conflict(new
                {
                    statusCode = StatusCodes.Status409Conflict,
                    message = "This email is already associated with another sign-in method."
                });
            }
            catch (ExternalAccountConflictException)
            {
                _logger.LogWarning("Google sign-in rejected: account linking conflict");
                return Conflict(new
                {
                    statusCode = StatusCodes.Status409Conflict,
                    message = "This email is already associated with another sign-in method."
                });
            }
            catch (GoogleProviderUnavailableException)
            {
                _logger.LogWarning("Google sign-in failed: provider unavailable");
                return StatusCode(StatusCodes.Status503ServiceUnavailable, new
                {
                    statusCode = StatusCodes.Status503ServiceUnavailable,
                    message = "Google authentication is temporarily unavailable."
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Google sign-in failed unexpectedly");
                return StatusCode(StatusCodes.Status500InternalServerError, new
                {
                    statusCode = StatusCodes.Status500InternalServerError,
                    message = "Google sign-in could not be completed."
                });
            }
        }

        [HttpPost("google-code/start")]
        public IActionResult StartGoogleAuthorizationCodeLogin()
        {
            if (!HasTrustedGoogleOrigin() ||
                !TryGetGoogleAuthorizationCodeConfig(out _, out _, out var redirectUri) ||
                !IsConfiguredRedirectUriForRequestOrigin(redirectUri))
            {
                return StatusCode(StatusCodes.Status503ServiceUnavailable, new
                {
                    statusCode = StatusCodes.Status503ServiceUnavailable,
                    message = "Google authentication is temporarily unavailable."
                });
            }

            var state = WebEncoders.Base64UrlEncode(RandomNumberGenerator.GetBytes(32));
            _googleLoginOAuthStateStore.Store(state, DateTime.UtcNow.AddMinutes(OAuthStateLifetimeMinutes));
            SetGoogleLoginStateCookie(state);

            return Ok(new { statusCode = 200, data = new { state } });
        }

        [HttpPost("google-code/login")]
        public async Task<IActionResult> GoogleAuthorizationCodeLogin([FromBody] GoogleAuthorizationCodeLoginRequestDto request)
        {
            if (!HasTrustedGoogleOrigin() ||
                !string.Equals(Request.Headers["X-Requested-With"].ToString(), "XmlHttpRequest", StringComparison.Ordinal))
            {
                return BadRequest(new { statusCode = 400, message = "Google sign-in request is invalid." });
            }

            if (string.IsNullOrWhiteSpace(request.Code) || string.IsNullOrWhiteSpace(request.State) ||
                !Request.Cookies.TryGetValue(GoogleLoginStateCookieName, out var cookieState) ||
                !CryptographicOperations.FixedTimeEquals(
                    System.Text.Encoding.UTF8.GetBytes(request.State),
                    System.Text.Encoding.UTF8.GetBytes(cookieState ?? string.Empty)) ||
                !_googleLoginOAuthStateStore.TryConsume(request.State))
            {
                DeleteGoogleLoginStateCookie();
                return Unauthorized(new { statusCode = 401, message = "Google sign-in state is invalid or expired." });
            }

            DeleteGoogleLoginStateCookie();

            if (!TryGetGoogleAuthorizationCodeConfig(out var clientId, out var clientSecret, out var redirectUri) ||
                !IsConfiguredRedirectUriForRequestOrigin(redirectUri))
            {
                return StatusCode(StatusCodes.Status503ServiceUnavailable, new
                {
                    statusCode = StatusCodes.Status503ServiceUnavailable,
                    message = "Google authentication is temporarily unavailable."
                });
            }

            try
            {
                var idToken = await _googleAuthorizationCodeExchange.ExchangeAsync(
                    request.Code.Trim(),
                    clientId,
                    clientSecret,
                    redirectUri,
                    HttpContext.RequestAborted);

                return await GoogleLogin(new GoogleLoginRequestDto { Credential = idToken });
            }
            catch (GoogleCredentialException)
            {
                _logger.LogWarning("Google authorization code rejected: invalid provider response");
                return Unauthorized(new { statusCode = 401, message = "Google credential is invalid or expired." });
            }
            catch (GoogleProviderUnavailableException)
            {
                _logger.LogWarning("Google authorization code exchange failed: provider unavailable");
                return StatusCode(StatusCodes.Status503ServiceUnavailable, new
                {
                    statusCode = StatusCodes.Status503ServiceUnavailable,
                    message = "Google authentication is temporarily unavailable."
                });
            }
        }

        [HttpPost("google-code/link/start")]
        [Microsoft.AspNetCore.Authorization.Authorize]
        public IActionResult StartGoogleAccountLink()
        {
            if (!TryGetAuthenticatedUserId(out var userId) || !HasTrustedGoogleOrigin() ||
                !TryGetGoogleAuthorizationCodeConfig(out _, out _, out var redirectUri) ||
                !IsConfiguredRedirectUriForRequestOrigin(redirectUri))
            {
                return StatusCode(StatusCodes.Status503ServiceUnavailable, new
                {
                    statusCode = StatusCodes.Status503ServiceUnavailable,
                    message = "Google authentication is temporarily unavailable."
                });
            }

            var state = WebEncoders.Base64UrlEncode(RandomNumberGenerator.GetBytes(32));
            _oauthStateStore.Store(
                state,
                userId,
                "GoogleLink",
                string.Empty,
                DateTime.UtcNow.AddMinutes(OAuthStateLifetimeMinutes));
            SetExternalLinkStateCookie(state);
            return Ok(new { statusCode = 200, data = new { state } });
        }

        [HttpPost("google-code/link")]
        [Microsoft.AspNetCore.Authorization.Authorize]
        public async Task<IActionResult> LinkGoogleAccount([FromBody] GoogleAuthorizationCodeLoginRequestDto request)
        {
            if (!TryGetAuthenticatedUserId(out var userId) || !HasTrustedGoogleOrigin() ||
                !string.Equals(Request.Headers["X-Requested-With"].ToString(), "XmlHttpRequest", StringComparison.Ordinal))
                return BadRequest(new { statusCode = 400, message = "Google account link request is invalid." });

            if (!TryConsumeExternalLinkState(request.State ?? string.Empty, userId, "GoogleLink"))
                return Unauthorized(new { statusCode = 401, message = "Google account link state is invalid or expired." });

            try
            {
                if (!TryGetGoogleAuthorizationCodeConfig(out var clientId, out var clientSecret, out var redirectUri) ||
                    !IsConfiguredRedirectUriForRequestOrigin(redirectUri))
                    return StatusCode(StatusCodes.Status503ServiceUnavailable, new { statusCode = 503, message = "Google authentication is temporarily unavailable." });

                var idToken = await _googleAuthorizationCodeExchange.ExchangeAsync(
                    request.Code.Trim(), clientId, clientSecret, redirectUri, HttpContext.RequestAborted);
                await _authService.LinkGoogleAsync(userId, new GoogleLoginRequestDto { Credential = idToken });
                return Ok(new { statusCode = 200, message = "Đã liên kết Google thành công." });
            }
            catch (GoogleCredentialException)
            {
                return Unauthorized(new { statusCode = 401, message = "Google credential is invalid or expired." });
            }
            catch (GoogleProviderUnavailableException)
            {
                _logger.LogWarning("Google account link failed: provider unavailable");
                return StatusCode(StatusCodes.Status503ServiceUnavailable, new
                {
                    statusCode = StatusCodes.Status503ServiceUnavailable,
                    message = "Google authentication is temporarily unavailable."
                });
            }
            catch (AccountLinkConflictException ex)
            {
                return Conflict(new { statusCode = 409, message = ex.Message });
            }
            catch (ArgumentException ex)
            {
                return BadRequest(new { statusCode = 400, message = ex.Message });
            }
            catch (UnauthorizedAccessException ex)
            {
                return Unauthorized(new { statusCode = 401, message = ex.Message });
            }
            catch (Exception)
            {
                return StatusCode(500, new { statusCode = 500, message = "Google account link could not be completed." });
            }
        }

        [HttpGet("github-link/start")]
        [Microsoft.AspNetCore.Authorization.Authorize]
        public IActionResult StartGitHubAccountLink()
        {
            if (!TryGetAuthenticatedUserId(out var userId))
                return Unauthorized(new { statusCode = 401, message = "Authenticated account is required." });

            var gitHubConfig = _configuration.GetSection("GitHub");
            var clientId = gitHubConfig["ClientId"]?.Trim();
            var redirectUri = gitHubConfig["RedirectUri"]?.Trim();
            if (string.IsNullOrWhiteSpace(clientId) || string.IsNullOrWhiteSpace(redirectUri))
                return StatusCode(503, new { statusCode = 503, message = "GitHub authentication is temporarily unavailable." });

            var state = GitHubLinkStatePrefix + WebEncoders.Base64UrlEncode(RandomNumberGenerator.GetBytes(32));
            _oauthStateStore.Store(
                state,
                userId,
                "GitHubLink",
                string.Empty,
                DateTime.UtcNow.AddMinutes(OAuthStateLifetimeMinutes));
            SetExternalLinkStateCookie(state);
            var url = "https://github.com/login/oauth/authorize" +
                $"?client_id={Uri.EscapeDataString(clientId)}" +
                $"&redirect_uri={Uri.EscapeDataString(redirectUri)}" +
                "&scope=user%3Aemail" +
                $"&state={Uri.EscapeDataString(state)}";
            return Ok(new { statusCode = 200, data = new { url } });
        }

        [HttpGet("github-login/start")]
        public IActionResult StartGitHubLogin()
        {
            var gitHubConfig = _configuration.GetSection("GitHub");
            var clientId = gitHubConfig["ClientId"]?.Trim();
            var redirectUri = gitHubConfig["RedirectUri"]?.Trim();
            if (string.IsNullOrWhiteSpace(clientId) || string.IsNullOrWhiteSpace(redirectUri))
                return StatusCode(503, new { statusCode = 503, message = "GitHub authentication is temporarily unavailable." });

            var state = GitHubLoginStatePrefix + WebEncoders.Base64UrlEncode(RandomNumberGenerator.GetBytes(32));
            _oauthStateStore.Store(
                state,
                Guid.Empty,
                "GitHubLogin",
                string.Empty,
                DateTime.UtcNow.AddMinutes(OAuthStateLifetimeMinutes));
            Response.Cookies.Append(GitHubLoginStateCookieName, state, new CookieOptions
            {
                HttpOnly = true,
                Secure = Request.IsHttps,
                SameSite = SameSiteMode.Lax,
                IsEssential = true,
                MaxAge = TimeSpan.FromMinutes(OAuthStateLifetimeMinutes),
                Path = "/api/auth"
            });

            var url = "https://github.com/login/oauth/authorize" +
                $"?client_id={Uri.EscapeDataString(clientId)}" +
                $"&redirect_uri={Uri.EscapeDataString(redirectUri)}" +
                "&scope=user%3Aemail" +
                $"&state={Uri.EscapeDataString(state)}";
            return Ok(new { statusCode = 200, data = new { url } });
        }

        [HttpPost("github-link")]
        [Microsoft.AspNetCore.Authorization.Authorize]
        public async Task<IActionResult> LinkGitHubAccount([FromBody] GitHubLoginRequestDto request)
        {
            if (!TryGetAuthenticatedUserId(out var userId))
                return Unauthorized(new { statusCode = 401, message = "Authenticated account is required." });
            if (!TryConsumeExternalLinkState(request.State ?? string.Empty, userId, "GitHubLink"))
                return Unauthorized(new { statusCode = 401, message = "GitHub account link state is invalid or expired." });

            try
            {
                await _authService.LinkGitHubAsync(userId, request);
                return Ok(new { statusCode = 200, message = "Đã liên kết GitHub thành công." });
            }
            catch (AccountLinkConflictException ex)
            {
                return Conflict(new { statusCode = 409, message = ex.Message });
            }
            catch (UnauthorizedAccessException ex)
            {
                return Unauthorized(new { statusCode = 401, message = ex.Message });
            }
            catch (Exception)
            {
                return StatusCode(500, new { statusCode = 500, message = "GitHub account link could not be completed." });
            }
        }

        [HttpGet("external-logins")]
        [Microsoft.AspNetCore.Authorization.Authorize]
        public async Task<IActionResult> GetExternalLogins()
        {
            if (!TryGetAuthenticatedUserId(out var userId))
                return Unauthorized(new { statusCode = 401, message = "Authenticated account is required." });
            return Ok(new { statusCode = 200, data = await _authService.GetExternalLoginStatusAsync(userId) });
        }

        [HttpDelete("external-logins/{provider}")]
        [Microsoft.AspNetCore.Authorization.Authorize]
        public async Task<IActionResult> UnlinkExternalLogin(string provider)
        {
            if (!TryGetAuthenticatedUserId(out var userId))
                return Unauthorized(new { statusCode = 401, message = "Authenticated account is required." });

            try
            {
                await _authService.UnlinkExternalLoginAsync(userId, provider);
                return Ok(new { statusCode = 200, message = "Đã ngắt liên kết tài khoản." });
            }
            catch (LastLoginMethodException ex)
            {
                return Conflict(new { statusCode = 409, message = ex.Message });
            }
            catch (KeyNotFoundException ex)
            {
                return NotFound(new { statusCode = 404, message = ex.Message });
            }
            catch (ArgumentException ex)
            {
                return BadRequest(new { statusCode = 400, message = ex.Message });
            }
            catch (UnauthorizedAccessException ex)
            {
                return Unauthorized(new { statusCode = 401, message = ex.Message });
            }
        }

        [HttpPost("github-login")]
        public async Task<IActionResult> GitHubLogin([FromBody] GitHubLoginRequestDto request)
        {
            if (!TryConsumeGitHubLoginState(request.State ?? string.Empty))
                return Unauthorized(new { statusCode = 401, message = "GitHub sign-in state is invalid or expired." });

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
            catch (GitHubAccountConflictException ex)
            {
                return Conflict(new { statusCode = 409, message = ex.Message });
            }
            catch (GitHubAccountForbiddenException ex)
            {
                return StatusCode(403, new { statusCode = 403, message = ex.Message });
            }
            catch (Exception ex)
            {
                return Unauthorized(new { statusCode = 401, message = ex.Message });
            }
        }

        private bool TryGetGoogleAuthorizationCodeConfig(
            out string clientId,
            out string clientSecret,
            out string redirectUri)
        {
            clientId = _configuration["Google:ClientId"]?.Trim() ?? string.Empty;
            clientSecret = _configuration["Google:ClientSecret"]?.Trim() ?? string.Empty;
            redirectUri = _configuration["Google:RedirectUri"]?.Trim() ?? string.Empty;
            return _configuration.GetValue<bool>("Google:Enabled") &&
                !string.IsNullOrWhiteSpace(clientId) &&
                !string.IsNullOrWhiteSpace(clientSecret) &&
                Uri.TryCreate(redirectUri, UriKind.Absolute, out _);
        }

        private bool TryGetAuthenticatedUserId(out Guid userId)
        {
            return Guid.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out userId);
        }

        private bool TryConsumeExternalLinkState(string state, Guid userId, string provider)
        {
            if (string.IsNullOrWhiteSpace(state) ||
                !Request.Cookies.TryGetValue(ExternalLinkStateCookieName, out var cookieState) ||
                !CryptographicOperations.FixedTimeEquals(
                    System.Text.Encoding.UTF8.GetBytes(state),
                    System.Text.Encoding.UTF8.GetBytes(cookieState ?? string.Empty)) ||
                !_oauthStateStore.TryConsume(state, userId, provider, out _))
            {
                DeleteExternalLinkStateCookie();
                return false;
            }

            DeleteExternalLinkStateCookie();
            return true;
        }

        private bool TryConsumeGitHubLoginState(string state)
        {
            if (string.IsNullOrWhiteSpace(state) ||
                !Request.Cookies.TryGetValue(GitHubLoginStateCookieName, out var cookieState) ||
                !CryptographicOperations.FixedTimeEquals(
                    System.Text.Encoding.UTF8.GetBytes(state),
                    System.Text.Encoding.UTF8.GetBytes(cookieState ?? string.Empty)) ||
                !_oauthStateStore.TryConsume(state, Guid.Empty, "GitHubLogin", out _))
            {
                Response.Cookies.Delete(GitHubLoginStateCookieName, new CookieOptions { Path = "/api/auth" });
                return false;
            }

            Response.Cookies.Delete(GitHubLoginStateCookieName, new CookieOptions { Path = "/api/auth" });
            return true;
        }

        private void SetExternalLinkStateCookie(string state)
        {
            Response.Cookies.Append(ExternalLinkStateCookieName, state, new CookieOptions
            {
                HttpOnly = true,
                Secure = Request.IsHttps,
                SameSite = SameSiteMode.Lax,
                IsEssential = true,
                MaxAge = TimeSpan.FromMinutes(OAuthStateLifetimeMinutes),
                Path = "/api/auth"
            });
        }

        private void DeleteExternalLinkStateCookie()
        {
            Response.Cookies.Delete(ExternalLinkStateCookieName, new CookieOptions
            {
                Secure = Request.IsHttps,
                SameSite = SameSiteMode.Lax,
                Path = "/api/auth"
            });
        }

        private bool HasTrustedGoogleOrigin()
        {
            var origin = Request.Headers.Origin.ToString().TrimEnd('/');
            if (string.IsNullOrWhiteSpace(origin)) return false;

            var configuredOrigins = _configuration.GetSection("Cors:AllowedOrigins").Get<string[]>() ?? Array.Empty<string>();
            var frontendOrigin = _configuration["Frontend:BaseUrl"]?.TrimEnd('/');
            return configuredOrigins
                .Append(frontendOrigin)
                .Where(item => !string.IsNullOrWhiteSpace(item))
                .Any(item => string.Equals(item!.TrimEnd('/'), origin, StringComparison.OrdinalIgnoreCase));
        }

        private bool IsConfiguredRedirectUriForRequestOrigin(string redirectUri)
        {
            if (!Uri.TryCreate(redirectUri, UriKind.Absolute, out var configuredUri)) return false;

            var origin = Request.Headers.Origin.ToString().TrimEnd('/');
            if (!Uri.TryCreate(origin, UriKind.Absolute, out var requestOrigin)) return false;

            return configuredUri.Scheme.Equals(requestOrigin.Scheme, StringComparison.OrdinalIgnoreCase) &&
                configuredUri.Host.Equals(requestOrigin.Host, StringComparison.OrdinalIgnoreCase) &&
                configuredUri.Port == requestOrigin.Port &&
                string.IsNullOrEmpty(configuredUri.AbsolutePath.Trim('/')) &&
                string.IsNullOrEmpty(configuredUri.Query) &&
                string.IsNullOrEmpty(configuredUri.Fragment);
        }

        private void SetGoogleLoginStateCookie(string state)
        {
            Response.Cookies.Append(GoogleLoginStateCookieName, state, new CookieOptions
            {
                HttpOnly = true,
                Secure = Request.IsHttps,
                SameSite = SameSiteMode.Lax,
                IsEssential = true,
                MaxAge = TimeSpan.FromMinutes(OAuthStateLifetimeMinutes),
                Path = "/api/auth/google-code"
            });
        }

        private void DeleteGoogleLoginStateCookie()
        {
            Response.Cookies.Delete(GoogleLoginStateCookieName, new CookieOptions
            {
                Secure = Request.IsHttps,
                SameSite = SameSiteMode.Lax,
                Path = "/api/auth/google-code"
            });
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
                var accessToken = authHeader != null && authHeader.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase)
                    ? authHeader.Substring("Bearer ".Length).Trim()
                    : null;
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
                var refreshToken = Request.Cookies["refreshToken"];

                if (!string.IsNullOrWhiteSpace(refreshToken))
                {
                    await _authService.RevokeTokenAsync(refreshToken);
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
