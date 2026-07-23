using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using TaskManagement.Infrastructure.Data;
using TaskManagement.Application.DTOs.Auth;
using TaskManagement.Application.Interfaces;
using System.Security.Claims;
using System;
using System.Threading.Tasks;
using System.Linq;
using System.Text.Json;
using TaskManagement.Domain.Entities;
using TaskManagement.API.Security;

namespace TaskManagement.API.Controllers
{
    [ApiController]
    [Route("api/users")]
    [Authorize]
    public class UsersController : ControllerBase
    {
        private const int PasswordChangeCooldownDays = 7;

        private static readonly JsonSerializerOptions ProfileJsonOptions = new()
        {
            PropertyNameCaseInsensitive = true
        };

        private readonly ApplicationDbContext _context;
        private readonly IOtpService _otpService;
        private readonly IEmailService _emailService;

        public UsersController(ApplicationDbContext context, IOtpService otpService, IEmailService emailService)
        {
            _context = context;
            _otpService = otpService;
            _emailService = emailService;
        }

        [HttpGet("me")]
        public async Task<IActionResult> GetMyProfile()
        {
            var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrEmpty(userIdClaim) || !Guid.TryParse(userIdClaim, out Guid userId))
                return Unauthorized(new { statusCode = 401, message = "Không có quyền truy cập." });

            var user = await _context.Users
                .Include(u => u.UserRoles)
                .ThenInclude(ur => ur.Role)
                .Include(u => u.DepartmentMemberships)
                .ThenInclude(dm => dm.Department)
                .FirstOrDefaultAsync(u => u.Id == userId);
                
            if (user == null) return NotFound(new { message = "Không tìm thấy người dùng." });

            // We read extra fields if available, otherwise fallback.
            // Since User entity might not have JobTitle, Organization, Department natively,
            // we will simulate them via SystemSetting or allow the schema to adapt.
            // Since we can't change the db schema without migrations, we will store profile extra data
            // into SystemSettings where Key = "Profile_" + userId.
            
            var extraProfileSetting = await _context.SystemSettings
                .FirstOrDefaultAsync(s => s.Key == "Profile_" + userId.ToString());

            var extra = new UpdateProfileRequest();

            if (extraProfileSetting != null && !string.IsNullOrEmpty(extraProfileSetting.Value))
            {
                try
                {
                    extra = JsonSerializer.Deserialize<UpdateProfileRequest>(extraProfileSetting.Value, ProfileJsonOptions) ?? extra;
                }
                catch
                {
                }
            }

            var activeDepartment = user.DepartmentMemberships
                .Select(dm => dm.Department)
                .FirstOrDefault(department => department != null && department.IsActive && !department.IsDeleted);
            var lastPasswordChangedAt = await GetPasswordChangedAtAsync(userId);
            var canChangePasswordAt = lastPasswordChangedAt?.AddDays(PasswordChangeCooldownDays);

            return Ok(new
            {
                statusCode = 200,
                data = new
                {
                    id = user.Id,
                    email = user.Email,
                    fullName = user.FullName,
                    avatarUrl = user.AvatarUrl,
                    coverUrl = user.CoverUrl,
                    publicName = string.IsNullOrEmpty(extra.PublicName) ? user.FullName : extra.PublicName,
                    jobTitle = user.JobTitle,
                    bio = user.Bio,
                    location = user.Location,
                    timezone = user.Timezone,
                    hobbies = extra.Hobbies,
                    departmentName = activeDepartment?.Name ?? extra.DepartmentName,
                    organizationName = extra.OrganizationName,
                    collaborationRules = extra.CollaborationRules,
                    coverPositionY = extra.CoverPositionY,
                    hasPassword = !string.IsNullOrEmpty(user.PasswordHash),
                    is2FaEnabled = user.Is2FAEnabled,
                    lastPasswordChangedAt,
                    canChangePasswordAt
                }
            });
        }

        public class UpdateProfileRequest
        {
            [MaxLength(100, ErrorMessage = "Tên đầy đủ không được vượt quá 100 ký tự.")]
            public string FullName { get; set; } = string.Empty;
            
            [MaxLength(100, ErrorMessage = "Tên hiển thị không được vượt quá 100 ký tự.")]
            public string PublicName { get; set; } = string.Empty;
            
            [MaxLength(100, ErrorMessage = "Chức danh không được vượt quá 100 ký tự.")]
            public string JobTitle { get; set; } = string.Empty;
            
            [MaxLength(100)]
            public string DepartmentName { get; set; } = string.Empty;
            
            [MaxLength(100)]
            public string OrganizationName { get; set; } = string.Empty;
            
            [MaxLength(500)]
            public string CollaborationRules { get; set; } = string.Empty;
            
            public int CoverPositionY { get; set; } = 50;
            public string Hobbies { get; set; } = string.Empty;
            
            [MaxLength(500, ErrorMessage = "Tiểu sử không được vượt quá 500 ký tự.")]
            public string Bio { get; set; } = string.Empty;
            
            [MaxLength(200, ErrorMessage = "Vị trí không được vượt quá 200 ký tự.")]
            public string Location { get; set; } = string.Empty;
            
            [MaxLength(100)]
            public string Timezone { get; set; } = string.Empty;
        }

        [HttpGet]
        public async Task<IActionResult> GetUsers(
            [FromQuery] string? search,
            [FromQuery] Guid? projectId,
            [FromQuery] Guid? goalId,
            [FromQuery] Guid? teamId,
            [FromQuery] Guid? managerId,
            [FromQuery] string? jobTitle,
            [FromQuery] string? department,
            [FromQuery] string? location,
            [FromQuery] int page = 1,
            [FromQuery] int pageSize = 20)
        {
            var query = _context.Users
                .AsNoTracking()
                .Include(u => u.DepartmentMemberships)
                .ThenInclude(dm => dm.Department)
                .Include(u => u.ProjectMemberships)
                .ThenInclude(pm => pm.Project)
                .Where(u => !u.IsDeleted && u.IsActive)
                .AsQueryable();

            if (!string.IsNullOrWhiteSpace(search))
            {
                var lowerSearch = search.ToLower();
                query = query.Where(u => 
                    u.FullName.ToLower().Contains(lowerSearch) || 
                    u.Email.ToLower().Contains(lowerSearch) ||
                    (u.JobTitle != null && u.JobTitle.ToLower().Contains(lowerSearch)));
            }

            if (projectId.HasValue)
            {
                query = query.Where(u =>
                    u.ProjectMemberships.Any(pm => pm.ProjectId == projectId.Value) ||
                    _context.Projects.Any(p => p.Id == projectId.Value && p.CreatorId == u.Id));
            }

            if (goalId.HasValue)
            {
                query = query.Where(u => _context.Goals.Any(g => g.Id == goalId.Value && g.OwnerId == u.Id));
            }

            if (teamId.HasValue)
            {
                query = query.Where(u => u.DepartmentMemberships.Any(dm => dm.DepartmentId == teamId.Value));
            }

            if (managerId.HasValue)
            {
                query = query.Where(u => u.DepartmentMemberships.Any(dm =>
                    dm.Department != null && dm.Department.ManagerId == managerId.Value));
            }

            if (!string.IsNullOrWhiteSpace(jobTitle))
            {
                var normalizedJobTitle = jobTitle.ToLower();
                query = query.Where(u => u.JobTitle != null && u.JobTitle.ToLower().Contains(normalizedJobTitle));
            }

            if (!string.IsNullOrWhiteSpace(department))
            {
                var normalizedDepartment = department.ToLower();
                query = query.Where(u => u.DepartmentMemberships.Any(dm =>
                    dm.Department != null && dm.Department.Name.ToLower().Contains(normalizedDepartment)));
            }

            if (!string.IsNullOrWhiteSpace(location))
            {
                var normalizedLocation = location.ToLower();
                query = query.Where(u => u.Location != null && u.Location.ToLower().Contains(normalizedLocation));
            }

            var totalCount = await query.CountAsync();
            var users = await query
                .OrderBy(u => u.FullName)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();

            var result = users.Select(u => new
            {
                id = u.Id,
                email = u.Email,
                fullName = u.FullName,
                avatarUrl = u.AvatarUrl,
                coverUrl = u.CoverUrl,
                jobTitle = u.JobTitle,
                location = u.Location,
                timezone = u.Timezone,
                departmentName = u.DepartmentMemberships.FirstOrDefault(dm => dm.Department != null && dm.Department.IsActive && !dm.Department.IsDeleted)?.Department?.Name,
                departments = u.DepartmentMemberships
                    .Where(dm => dm.Department != null && dm.Department.IsActive && !dm.Department.IsDeleted)
                    .Select(dm => new
                    {
                        id = dm.DepartmentId,
                        name = dm.Department!.Name,
                        managerId = dm.Department.ManagerId
                    })
                    .ToList(),
                projects = u.ProjectMemberships
                    .Where(pm => pm.Project != null && !pm.Project.IsDeleted)
                    .Select(pm => new
                    {
                        id = pm.ProjectId,
                        name = pm.Project.Name
                    })
                    .ToList(),
                ownedProjectIds = _context.Projects
                    .Where(p => p.CreatorId == u.Id && !p.IsDeleted)
                    .Select(p => p.Id)
                    .ToList(),
                ownedGoalIds = _context.Goals
                    .Where(g => g.OwnerId == u.Id && !g.IsArchived)
                    .Select(g => g.Id)
                    .ToList()
            }).ToList();

            return Ok(new { statusCode = 200, data = result, total = totalCount, page, pageSize });
        }

        [HttpGet("{id}")]
        public async Task<IActionResult> GetUserById(Guid id)
        {
            var user = await _context.Users
                .AsNoTracking()
                .Include(u => u.DepartmentMemberships)
                .ThenInclude(dm => dm.Department)
                .FirstOrDefaultAsync(u => u.Id == id && !u.IsDeleted && u.IsActive);
                
            if (user == null) return NotFound(new { message = "Không tìm thấy người dùng." });

            var extraProfileSetting = await _context.SystemSettings
                .FirstOrDefaultAsync(s => s.Key == "Profile_" + id.ToString());

            var extra = new UpdateProfileRequest();
            if (extraProfileSetting != null && !string.IsNullOrEmpty(extraProfileSetting.Value))
            {
                try { extra = JsonSerializer.Deserialize<UpdateProfileRequest>(extraProfileSetting.Value, ProfileJsonOptions) ?? extra; }
                catch { }
            }

            var activeDepartment = user.DepartmentMemberships
                .Select(dm => dm.Department)
                .FirstOrDefault(department => department != null && department.IsActive && !department.IsDeleted);

            var teamIds = user.DepartmentMemberships.Select(dm => dm.DepartmentId).ToList();
            var linkedGoals = await _context.Goals
                .AsNoTracking()
                .Where(g =>
                    !g.IsArchived
                    && (g.OwnerId == id
                        || (g.DepartmentId.HasValue && teamIds.Contains(g.DepartmentId.Value))
                        || g.TeamGoals.Any(tg => teamIds.Contains(tg.DepartmentId))))
                .OrderByDescending(g => g.UpdatedAt)
                .Select(g => new
                {
                    id = g.Id,
                    title = g.Title,
                    status = g.Status,
                    progress = g.Progress,
                    updatedAt = g.UpdatedAt
                })
                .ToListAsync();

            var linkedProjects = await _context.Projects
                .AsNoTracking()
                .Where(p =>
                    !p.IsDeleted
                    && (p.CreatorId == id
                        || p.ProjectMembers.Any(pm => pm.UserId == id)
                        || (p.DepartmentId.HasValue && teamIds.Contains(p.DepartmentId.Value))))
                .OrderByDescending(p => p.UpdatedAt)
                .Select(p => new
                {
                    id = p.Id,
                    title = p.Name,
                    name = p.Name,
                    status = p.IsArchived ? "Archived" : (p.Status ? "Active" : "Inactive"),
                    updatedAt = p.UpdatedAt
                })
                .ToListAsync();

            var kudos = await _context.Kudos
                .AsNoTracking()
                .Include(k => k.Sender)
                .Include(k => k.Department)
                .Where(k => k.ReceiverId == id || (k.DepartmentId.HasValue && teamIds.Contains(k.DepartmentId.Value)))
                .OrderByDescending(k => k.CreatedAt)
                .Select(k => new
                {
                    id = k.Id,
                    message = k.Message,
                    sender = k.Sender.FullName ?? k.Sender.Email,
                    senderName = k.Sender.FullName ?? k.Sender.Email,
                    senderEmail = k.Sender.Email,
                    senderAvatarUrl = k.Sender.AvatarUrl,
                    department = k.Department == null ? null : k.Department.Name,
                    departmentId = k.DepartmentId,
                    icon = k.Icon ?? "Star",
                    createdAt = k.CreatedAt
                })
                .ToListAsync();

            var history = await _context.SiteAuditLogs
                .AsNoTracking()
                .Include(log => log.User)
                .Where(log => log.UserId == id)
                .OrderByDescending(log => log.CreatedAt)
                .Take(100)
                .Select(log => new
                {
                    id = log.Id,
                    time = log.CreatedAt,
                    action = log.Action,
                    entityType = log.EntityType,
                    entityId = log.EntityId,
                    oldValue = log.OldValue,
                    newValue = log.NewValue,
                    actorName = log.User.FullName ?? log.User.Email
                })
                .ToListAsync();

            return Ok(new
            {
                statusCode = 200,
                data = new
                {
                    id = user.Id,
                    email = user.Email,
                    fullName = user.FullName,
                    avatarUrl = user.AvatarUrl,
                    coverUrl = user.CoverUrl,
                    publicName = string.IsNullOrEmpty(extra.PublicName) ? user.FullName : extra.PublicName,
                    jobTitle = user.JobTitle,
                    bio = user.Bio,
                    location = user.Location,
                    timezone = user.Timezone,
                    departmentName = activeDepartment?.Name ?? extra.DepartmentName,
                    departments = user.DepartmentMemberships
                        .Where(dm => dm.Department != null && dm.Department.IsActive && !dm.Department.IsDeleted)
                        .Select(dm => new
                        {
                            id = dm.DepartmentId,
                            name = dm.Department!.Name,
                            managerId = dm.Department.ManagerId
                        })
                        .ToList(),
                    organizationName = extra.OrganizationName,
                    collaborationRules = extra.CollaborationRules,
                    coverPositionY = extra.CoverPositionY,
                    linkedGoals,
                    linkedProjects,
                    kudos,
                    history
                }
            });
        }

        [HttpPut("profile")]
        public async Task<IActionResult> UpdateProfile([FromBody] UpdateProfileRequest request)
        {
            var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrEmpty(userIdClaim) || !Guid.TryParse(userIdClaim, out Guid userId))
                return Unauthorized(new { statusCode = 401, message = "Không có quyền truy cập." });

            var user = await _context.Users.FindAsync(userId);
            if (user == null) return NotFound(new { message = "Không tìm thấy người dùng." });

            user.FullName = string.IsNullOrWhiteSpace(request.FullName) ? user.FullName : request.FullName.Trim();
            user.JobTitle = request.JobTitle?.Trim() ?? string.Empty;
            user.Bio = request.Bio?.Trim() ?? string.Empty;
            user.Location = request.Location?.Trim() ?? string.Empty;
            user.Timezone = request.Timezone?.Trim() ?? string.Empty;
            user.UpdatedAt = DateTime.UtcNow;

            var extraProfileSetting = await _context.SystemSettings
                .FirstOrDefaultAsync(s => s.Key == "Profile_" + userId.ToString());
            
            if (extraProfileSetting == null)
            {
                extraProfileSetting = new TaskManagement.Domain.Entities.SystemSetting
                {
                    Id = Guid.NewGuid(),
                    Key = "Profile_" + userId.ToString(),
                    SettingGroup = "UserProfile"
                };
                _context.SystemSettings.Add(extraProfileSetting);
            }

            extraProfileSetting.Value = JsonSerializer.Serialize(new UpdateProfileRequest
            {
                FullName = user.FullName,
                PublicName = request.PublicName?.Trim() ?? string.Empty,
                DepartmentName = request.DepartmentName?.Trim() ?? string.Empty,
                OrganizationName = request.OrganizationName?.Trim() ?? string.Empty,
                CollaborationRules = request.CollaborationRules?.Trim() ?? string.Empty,
                CoverPositionY = request.CoverPositionY
            });
            extraProfileSetting.LastModifiedAt = DateTime.UtcNow;

            var normalizedDepartmentName = request.DepartmentName?.Trim();
            var currentMemberships = await _context.DepartmentMembers
                .Where(dm => dm.UserId == userId)
                .ToListAsync();

            if (string.IsNullOrWhiteSpace(normalizedDepartmentName))
            {
                if (currentMemberships.Count > 0)
                {
                    _context.DepartmentMembers.RemoveRange(currentMemberships);
                }
            }
            else
            {
                var department = await _context.Departments
                    .FirstOrDefaultAsync(d => d.IsActive && !d.IsDeleted && d.Name.ToLower() == normalizedDepartmentName.ToLower());

                if (department != null)
                {
                    var alreadyAssigned = currentMemberships.Any(dm => dm.DepartmentId == department.Id);
                    if (!alreadyAssigned)
                    {
                        _context.DepartmentMembers.RemoveRange(currentMemberships);
                        _context.DepartmentMembers.Add(new DepartmentMember
                        {
                            DepartmentId = department.Id,
                            UserId = userId
                        });
                    }
                }
            }

            await _context.SaveChangesAsync();

            return Ok(new { statusCode = 200, message = "Cập nhật thông tin hồ sơ thành công." });
        }

        public class ChangePasswordRequest
        {
            public string OtpCode { get; set; } = string.Empty;
            public string NewPassword { get; set; } = string.Empty;
            public bool LogoutOthers { get; set; } = false;
        }

        [HttpPut("change-password")]
        public async Task<IActionResult> ChangePassword([FromBody] ChangePasswordRequest request)
        {
            var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrEmpty(userIdClaim) || !Guid.TryParse(userIdClaim, out Guid userId))
                return Unauthorized(new { statusCode = 401, message = "Không có quyền truy cập." });

            var user = await _context.Users.FindAsync(userId);
            if (user == null) return NotFound(new { message = "Không tìm thấy người dùng." });

            var cooldown = await GetPasswordCooldownAsync(userId);
            if (!cooldown.CanChange)
            {
                return BadRequest(new
                {
                    statusCode = 400,
                    message = $"Mật khẩu chỉ có thể được thay đổi {PasswordChangeCooldownDays} ngày một lần. Vui lòng gửi yêu cầu hỗ trợ tới Quản trị viên nếu đây là trường hợp khẩn cấp.",
                    cooldown.LastChangedAt,
                    cooldown.EligibleAt
                });
            }

            // Validate OTP
            if (string.IsNullOrEmpty(request.OtpCode))
                return BadRequest(new { statusCode = 400, message = "Vui lòng nhập mã OTP." });

            var otpValidation = _otpService.ValidateOtp(user.Email, request.OtpCode.Trim());
            if (otpValidation.Status == OtpValidationStatus.Locked)
                return StatusCode(StatusCodes.Status429TooManyRequests, new { statusCode = 429, message = "Quá nhiều lần thử OTP.", retryAfterSeconds = otpValidation.RetryAfterSeconds });
            if (!otpValidation.IsValid)
                return BadRequest(new { statusCode = 400, message = "Mã OTP không hợp lệ hoặc đã hết hạn." });

            user.PasswordHash = BCrypt.Net.BCrypt.HashPassword(request.NewPassword);
            user.UpdatedAt = DateTime.UtcNow;
            await SetPasswordChangedAtAsync(userId, user.UpdatedAt);

            await _context.SaveChangesAsync();

            return Ok(new { statusCode = 200, message = "Đổi mật khẩu thành công" });
        }

        public class SendChangePasswordOtpRequest
        {
            public string Email { get; set; } = string.Empty;
        }

        [HttpPost("send-change-password-otp")]
        public async Task<IActionResult> SendChangePasswordOtp([FromBody] SendChangePasswordOtpRequest request)
        {
            var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrEmpty(userIdClaim) || !Guid.TryParse(userIdClaim, out Guid userId))
                return Unauthorized(new { statusCode = 401, message = "Không có quyền truy cập." });

            var user = await _context.Users.FindAsync(userId);
            if (user == null) return NotFound(new { message = "Không tìm thấy người dùng." });

            // Verify the email matches the user's account email
            if (!string.Equals(user.Email, request.Email, StringComparison.OrdinalIgnoreCase))
                return BadRequest(new { statusCode = 400, message = "Email không khớp với tài khoản của bạn." });

            var cooldown = await GetPasswordCooldownAsync(userId);
            if (!cooldown.CanChange)
            {
                return BadRequest(new
                {
                    statusCode = 400,
                    message = $"Mật khẩu chỉ có thể được thay đổi {PasswordChangeCooldownDays} ngày một lần. Vui lòng gửi yêu cầu hỗ trợ tới Quản trị viên nếu đây là trường hợp khẩn cấp.",
                    cooldown.LastChangedAt,
                    cooldown.EligibleAt
                });
            }

            var otpCode = _otpService.GenerateOtp();
            var issueResult = _otpService.StoreOtp(user.Email, otpCode);
            if (!issueResult.Issued)
                return StatusCode(StatusCodes.Status429TooManyRequests, new { statusCode = 429, message = "Vui lòng thử lại sau.", retryAfterSeconds = issueResult.RetryAfterSeconds });
            await _emailService.SendOtpEmailAsync(user.Email, otpCode);

            return Ok(new { statusCode = 200, message = "Đã gửi mã OTP đến email của bạn." });
        }

        [HttpPost("request-password-change-exception")]
        public async Task<IActionResult> RequestPasswordChangeException()
        {
            var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrEmpty(userIdClaim) || !Guid.TryParse(userIdClaim, out Guid userId))
                return Unauthorized(new { statusCode = 401, message = "Không có quyền truy cập." });

            var user = await _context.Users.FindAsync(userId);
            if (user == null) return NotFound(new { message = "Không tìm thấy người dùng." });

            var cooldown = await GetPasswordCooldownAsync(userId);
            var eligibleAt = cooldown.EligibleAt ?? DateTime.UtcNow;

            var adminEmails = await _context.Users
                .Include(u => u.UserRoles)
                .ThenInclude(ur => ur.Role)
                .Where(u =>
                    u.IsActive &&
                    !u.IsDeleted &&
                    u.UserRoles.Any(ur =>
                        ur.Role != null &&
                        (ur.Role.Name == "Admin" || ur.Role.Name == "SuperAdmin")))
                .Select(u => u.Email)
                .Distinct()
                .ToListAsync();

            if (adminEmails.Count == 0)
            {
                return BadRequest(new { statusCode = 400, message = "Không có email của Quản trị viên hệ thống để gửi yêu cầu hỗ trợ đổi mật khẩu." });
            }

            foreach (var adminEmail in adminEmails)
            {
                await _emailService.SendPasswordChangeRequestEmailAsync(
                    adminEmail,
                    user.FullName,
                    user.Email,
                    cooldown.LastChangedAt,
                    eligibleAt);
            }

            return Ok(new
            {
                statusCode = 200,
                message = "Yêu cầu của bạn đã được gửi tới Quản trị viên hệ thống.",
                sentTo = adminEmails.Count,
                cooldown.LastChangedAt,
                eligibleAt
            });
        }

        [HttpPost("send-set-password-otp")]
        public async Task<IActionResult> SendSetPasswordOtp()
        {
            var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrEmpty(userIdClaim) || !Guid.TryParse(userIdClaim, out Guid userId))
                return Unauthorized(new { statusCode = 401, message = "Không có quyền truy cập." });

            var user = await _context.Users.FindAsync(userId);
            if (user == null) return NotFound(new { message = "Không tìm thấy người dùng." });

            if (!string.IsNullOrEmpty(user.PasswordHash))
                return BadRequest(new { message = "Tài khoản đã có mật khẩu." });

            var otpCode = _otpService.GenerateOtp();
            var issueResult = _otpService.StoreOtp(user.Email, otpCode);
            if (!issueResult.Issued)
                return StatusCode(StatusCodes.Status429TooManyRequests, new { statusCode = 429, message = "Vui lòng thử lại sau.", retryAfterSeconds = issueResult.RetryAfterSeconds });
            await _emailService.SendOtpEmailAsync(user.Email, otpCode);

            return Ok(new { statusCode = 200, message = "Đã gửi mã OTP đến email của bạn." });
        }

        public class SetPasswordRequest
        {
            public string OtpCode { get; set; } = string.Empty;
            public string NewPassword { get; set; } = string.Empty;
        }

        [HttpPost("set-password-with-otp")]
        public async Task<IActionResult> SetPasswordWithOtp([FromBody] SetPasswordRequest request)
        {
            var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrEmpty(userIdClaim) || !Guid.TryParse(userIdClaim, out Guid userId))
                return Unauthorized(new { statusCode = 401, message = "Không có quyền truy cập." });

            var user = await _context.Users.FindAsync(userId);
            if (user == null) return NotFound(new { message = "Không tìm thấy người dùng." });

            if (!string.IsNullOrEmpty(user.PasswordHash))
                return BadRequest(new { message = "Tài khoản đã có mật khẩu." });

            var otpValidation = _otpService.ValidateOtp(user.Email, request.OtpCode.Trim());
            if (otpValidation.Status == OtpValidationStatus.Locked)
                return StatusCode(StatusCodes.Status429TooManyRequests, new { statusCode = 429, message = "Quá nhiều lần thử OTP.", retryAfterSeconds = otpValidation.RetryAfterSeconds });
            if (!otpValidation.IsValid)
                return BadRequest(new { statusCode = 400, message = "Mã OTP không hợp lệ hoặc đã hết hạn." });

            user.PasswordHash = BCrypt.Net.BCrypt.HashPassword(request.NewPassword);
            user.UpdatedAt = DateTime.UtcNow;
            await SetPasswordChangedAtAsync(userId, user.UpdatedAt);

            await _context.SaveChangesAsync();

            return Ok(new { statusCode = 200, message = "Tạo mật khẩu thành công." });
        }

        public class Toggle2FARequest { public bool Enable { get; set; } }

        [HttpPost("toggle-2fa")]
        public async Task<IActionResult> Toggle2FA([FromBody] Toggle2FARequest request)
        {
            var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrEmpty(userIdClaim) || !Guid.TryParse(userIdClaim, out Guid userId))
                return Unauthorized(new { statusCode = 401, message = "Không có quyền truy cập." });

            var user = await _context.Users.FindAsync(userId);
            if (user == null) return NotFound(new { message = "Không tìm thấy người dùng." });

            user.Is2FAEnabled = request.Enable;
            user.UpdatedAt = DateTime.UtcNow;

            await _context.SaveChangesAsync();
            return Ok(new { statusCode = 200, message = request.Enable ? "Đã bật 2FA." : "Đã tắt 2FA.", is2FaEnabled = user.Is2FAEnabled });
        }

        private static string PasswordChangedAtKey(Guid userId) => $"PasswordChangedAt_{userId}";

        private async Task<DateTime?> GetPasswordChangedAtAsync(Guid userId)
        {
            var setting = await _context.SystemSettings
                .FirstOrDefaultAsync(s => s.Key == PasswordChangedAtKey(userId));

            if (setting == null || string.IsNullOrWhiteSpace(setting.Value))
            {
                return null;
            }

            return DateTime.TryParse(setting.Value, out var parsed)
                ? DateTime.SpecifyKind(parsed, DateTimeKind.Utc)
                : null;
        }

        private async Task<(bool CanChange, DateTime? LastChangedAt, DateTime? EligibleAt)> GetPasswordCooldownAsync(Guid userId)
        {
            var lastChangedAt = await GetPasswordChangedAtAsync(userId);
            if (!lastChangedAt.HasValue)
            {
                return (true, null, null);
            }

            var eligibleAt = lastChangedAt.Value.AddDays(PasswordChangeCooldownDays);
            return (DateTime.UtcNow >= eligibleAt, lastChangedAt, eligibleAt);
        }

        private async Task SetPasswordChangedAtAsync(Guid userId, DateTime changedAt)
        {
            var key = PasswordChangedAtKey(userId);
            var setting = await _context.SystemSettings.FirstOrDefaultAsync(s => s.Key == key);
            if (setting == null)
            {
                setting = new SystemSetting
                {
                    Id = Guid.NewGuid(),
                    Key = key,
                    SettingGroup = "Security",
                    Description = "Last successful user password change timestamp."
                };
                _context.SystemSettings.Add(setting);
            }

            setting.Value = changedAt.ToUniversalTime().ToString("O");
            setting.LastModifiedAt = DateTime.UtcNow;
        }

        /// <summary>
        /// PUT /api/users/avatar — Upload avatar image
        /// </summary>
        [HttpPut("avatar")]
        public async Task<IActionResult> UploadAvatar([FromForm] IFormFile file, [FromServices] IWebHostEnvironment env)
        {
            var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrEmpty(userIdClaim) || !Guid.TryParse(userIdClaim, out Guid userId))
                return Unauthorized();

            ValidatedUpload validated;
            try { validated = await UploadSecurity.ReadPublicImageAsync(file); }
            catch (InvalidDataException exception) { return BadRequest(new { message = exception.Message }); }

            var user = await _context.Users.FindAsync(userId);
            if (user == null) return NotFound();

            var uploadsDir = Path.Combine(env.ContentRootPath, "uploads", "avatars");
            if (!Directory.Exists(uploadsDir)) Directory.CreateDirectory(uploadsDir);

            var uniqueName = $"{Guid.NewGuid():N}{validated.Extension}";
            var filePath = UploadSecurity.ResolveUnderRoot(uploadsDir, uniqueName);
            await System.IO.File.WriteAllBytesAsync(filePath, validated.Bytes);

            user.AvatarUrl = $"/uploads/avatars/{uniqueName}";
            user.UpdatedAt = DateTime.UtcNow;
            await _context.SaveChangesAsync();

            return Ok(new { statusCode = 200, message = "Cập nhật ảnh đại diện thành công.", data = new { avatarUrl = user.AvatarUrl } });
        }

        /// <summary>
        /// PUT /api/users/cover — Upload cover/banner image
        /// </summary>
        [HttpPut("cover")]
        public async Task<IActionResult> UploadCover([FromForm] IFormFile file, [FromServices] IWebHostEnvironment env)
        {
            var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrEmpty(userIdClaim) || !Guid.TryParse(userIdClaim, out Guid userId))
                return Unauthorized();

            ValidatedUpload validated;
            try { validated = await UploadSecurity.ReadPublicImageAsync(file); }
            catch (InvalidDataException exception) { return BadRequest(new { message = exception.Message }); }

            var user = await _context.Users.FindAsync(userId);
            if (user == null) return NotFound();

            var uploadsDir = Path.Combine(env.ContentRootPath, "uploads", "covers");
            if (!Directory.Exists(uploadsDir)) Directory.CreateDirectory(uploadsDir);

            var uniqueName = $"{Guid.NewGuid():N}{validated.Extension}";
            var filePath = UploadSecurity.ResolveUnderRoot(uploadsDir, uniqueName);
            await System.IO.File.WriteAllBytesAsync(filePath, validated.Bytes);

            user.CoverUrl = $"/uploads/covers/{uniqueName}";
            user.UpdatedAt = DateTime.UtcNow;
            await _context.SaveChangesAsync();

            return Ok(new { statusCode = 200, message = "Cập nhật ảnh bìa thành công.", data = new { coverUrl = user.CoverUrl } });
        }

        public class EmailItem
        {
            public string Email { get; set; } = string.Empty;
            public bool IsPrimary { get; set; }
            public bool IsVerified { get; set; }
            public DateTime AddedAt { get; set; } = DateTime.UtcNow;
        }

        /// <summary>
        /// GET /api/users/emails — Retrieve all email addresses associated with the current user
        /// </summary>
        [HttpGet("emails")]
        public async Task<IActionResult> GetEmails()
        {
            var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrEmpty(userIdClaim) || !Guid.TryParse(userIdClaim, out Guid userId))
                return Unauthorized(new { statusCode = 401, message = "Không có quyền truy cập." });

            var user = await _context.Users.FindAsync(userId);
            if (user == null) return NotFound(new { statusCode = 404, message = "Không tìm thấy người dùng." });

            var settingKey = $"UserEmails_{userId}";
            var setting = await _context.SystemSettings.FirstOrDefaultAsync(s => s.Key == settingKey);

            List<EmailItem> emailsList;
            if (setting == null || string.IsNullOrEmpty(setting.Value))
            {
                emailsList = new List<EmailItem>
                {
                    new EmailItem
                    {
                        Email = user.Email,
                        IsPrimary = true,
                        IsVerified = true,
                        AddedAt = DateTime.UtcNow
                    }
                };
                // Store in DB
                _context.SystemSettings.Add(new SystemSetting
                {
                    Id = Guid.NewGuid(),
                    SettingGroup = "Profile",
                    Key = settingKey,
                    Value = JsonSerializer.Serialize(emailsList),
                    LastModifiedAt = DateTime.UtcNow
                });
                await _context.SaveChangesAsync();
            }
            else
            {
                emailsList = JsonSerializer.Deserialize<List<EmailItem>>(setting.Value) ?? new List<EmailItem>();
                // Make sure primary email is synced with user's main email if missing
                if (!emailsList.Any(e => e.Email.Equals(user.Email, StringComparison.OrdinalIgnoreCase)))
                {
                    emailsList.Insert(0, new EmailItem
                    {
                        Email = user.Email,
                        IsPrimary = true,
                        IsVerified = true,
                        AddedAt = DateTime.UtcNow
                    });
                    setting.Value = JsonSerializer.Serialize(emailsList);
                    setting.LastModifiedAt = DateTime.UtcNow;
                    await _context.SaveChangesAsync();
                }
            }

            return Ok(new { statusCode = 200, data = emailsList });
        }

        public class AddEmailRequest
        {
            public string Email { get; set; } = string.Empty;
        }

        /// <summary>
        /// POST /api/users/emails — Add a new email address
        /// </summary>
        [HttpPost("emails")]
        public async Task<IActionResult> AddEmail([FromBody] AddEmailRequest request)
        {
            var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrEmpty(userIdClaim) || !Guid.TryParse(userIdClaim, out Guid userId))
                return Unauthorized(new { statusCode = 401, message = "Không có quyền truy cập." });

            if (string.IsNullOrWhiteSpace(request.Email))
                return BadRequest(new { statusCode = 400, message = "Email không được để trống." });

            var settingKey = $"UserEmails_{userId}";
            var setting = await _context.SystemSettings.FirstOrDefaultAsync(s => s.Key == settingKey);

            List<EmailItem> emailsList = new List<EmailItem>();
            if (setting != null && !string.IsNullOrEmpty(setting.Value))
            {
                emailsList = JsonSerializer.Deserialize<List<EmailItem>>(setting.Value) ?? new List<EmailItem>();
            }

            if (emailsList.Any(e => e.Email.Equals(request.Email.Trim(), StringComparison.OrdinalIgnoreCase)))
                return BadRequest(new { statusCode = 400, message = "Email này đã được thêm vào tài khoản của bạn." });

            emailsList.Add(new EmailItem
            {
                Email = request.Email.Trim(),
                IsPrimary = false,
                IsVerified = false,
                AddedAt = DateTime.UtcNow
            });

            if (setting == null)
            {
                _context.SystemSettings.Add(new SystemSetting
                {
                    Id = Guid.NewGuid(),
                    SettingGroup = "Profile",
                    Key = settingKey,
                    Value = JsonSerializer.Serialize(emailsList),
                    LastModifiedAt = DateTime.UtcNow
                });
            }
            else
            {
                setting.Value = JsonSerializer.Serialize(emailsList);
                setting.LastModifiedAt = DateTime.UtcNow;
            }

            await _context.SaveChangesAsync();
            return Ok(new { statusCode = 200, message = "Thêm email thành công.", data = emailsList });
        }

        /// <summary>
        /// PUT /api/users/emails/primary — Set primary email address
        /// </summary>
        [HttpPut("emails/primary")]
        public async Task<IActionResult> MakeEmailPrimary([FromBody] AddEmailRequest request)
        {
            var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrEmpty(userIdClaim) || !Guid.TryParse(userIdClaim, out Guid userId))
                return Unauthorized(new { statusCode = 401, message = "Không có quyền truy cập." });

            var user = await _context.Users.FindAsync(userId);
            if (user == null) return NotFound();

            var settingKey = $"UserEmails_{userId}";
            var setting = await _context.SystemSettings.FirstOrDefaultAsync(s => s.Key == settingKey);
            if (setting == null)
                return BadRequest(new { statusCode = 400, message = "Không tìm thấy danh sách email." });

            var emailsList = JsonSerializer.Deserialize<List<EmailItem>>(setting.Value) ?? new List<EmailItem>();
            var target = emailsList.FirstOrDefault(e => e.Email.Equals(request.Email.Trim(), StringComparison.OrdinalIgnoreCase));

            if (target == null)
                return NotFound(new { statusCode = 404, message = "Email không có trong danh sách." });

            if (!target.IsVerified)
                return BadRequest(new { statusCode = 400, message = "Chỉ có thể đặt email đã xác minh làm email chính." });

            foreach (var emailItem in emailsList)
            {
                emailItem.IsPrimary = (emailItem.Email.Equals(request.Email.Trim(), StringComparison.OrdinalIgnoreCase));
            }

            // Sync user's primary email field in core user table
            user.Email = target.Email;
            user.UpdatedAt = DateTime.UtcNow;

            setting.Value = JsonSerializer.Serialize(emailsList);
            setting.LastModifiedAt = DateTime.UtcNow;
            await _context.SaveChangesAsync();

            return Ok(new { statusCode = 200, message = "Đã thay đổi email chính.", data = emailsList });
        }

        /// <summary>
        /// PUT /api/users/emails/verify — Verify email address (simplified flow)
        /// </summary>
        [HttpPut("emails/verify")]
        public async Task<IActionResult> VerifyEmail([FromBody] AddEmailRequest request)
        {
            var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrEmpty(userIdClaim) || !Guid.TryParse(userIdClaim, out Guid userId))
                return Unauthorized(new { statusCode = 401, message = "Không có quyền truy cập." });

            var settingKey = $"UserEmails_{userId}";
            var setting = await _context.SystemSettings.FirstOrDefaultAsync(s => s.Key == settingKey);
            if (setting == null)
                return BadRequest(new { statusCode = 400, message = "Không tìm thấy danh sách email." });

            var emailsList = JsonSerializer.Deserialize<List<EmailItem>>(setting.Value) ?? new List<EmailItem>();
            var target = emailsList.FirstOrDefault(e => e.Email.Equals(request.Email.Trim(), StringComparison.OrdinalIgnoreCase));

            if (target == null)
                return NotFound(new { statusCode = 404, message = "Email không có trong danh sách." });

            target.IsVerified = true;
            setting.Value = JsonSerializer.Serialize(emailsList);
            setting.LastModifiedAt = DateTime.UtcNow;
            await _context.SaveChangesAsync();

            return Ok(new { statusCode = 200, message = "Xác minh email thành công.", data = emailsList });
        }

        /// <summary>
        /// DELETE /api/users/emails/{email} — Remove email address
        /// </summary>
        [HttpDelete("emails/{email}")]
        public async Task<IActionResult> RemoveEmail(string email)
        {
            var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrEmpty(userIdClaim) || !Guid.TryParse(userIdClaim, out Guid userId))
                return Unauthorized(new { statusCode = 401, message = "Không có quyền truy cập." });

            var settingKey = $"UserEmails_{userId}";
            var setting = await _context.SystemSettings.FirstOrDefaultAsync(s => s.Key == settingKey);
            if (setting == null)
                return BadRequest(new { statusCode = 400, message = "Không tìm thấy danh sách email." });

            var emailsList = JsonSerializer.Deserialize<List<EmailItem>>(setting.Value) ?? new List<EmailItem>();
            var target = emailsList.FirstOrDefault(e => e.Email.Equals(email.Trim(), StringComparison.OrdinalIgnoreCase));

            if (target == null)
                return NotFound(new { statusCode = 404, message = "Email không có trong danh sách." });

            if (target.IsPrimary)
                return BadRequest(new { statusCode = 400, message = "Không thể xóa email chính." });

            emailsList.Remove(target);
            setting.Value = JsonSerializer.Serialize(emailsList);
            setting.LastModifiedAt = DateTime.UtcNow;
            await _context.SaveChangesAsync();

            return Ok(new { statusCode = 200, message = "Đã xóa email.", data = emailsList });
        }

        /// <summary>
        /// GET /api/users/sessions — Retrieve active login sessions for the current user
        /// </summary>
        [HttpGet("sessions")]
        public async Task<IActionResult> GetSessions()
        {
            var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrEmpty(userIdClaim) || !Guid.TryParse(userIdClaim, out Guid userId))
                return Unauthorized(new { statusCode = 401, message = "Không có quyền truy cập." });

            var currentRefreshToken = Request.Cookies["refreshToken"];

            var sessions = await _context.RefreshTokens
                .Where(rt => rt.UserId == userId && !rt.IsRevoked && rt.ExpiryTime > DateTime.UtcNow)
                .OrderByDescending(rt => rt.CreatedAt)
                .Select(rt => new
                {
                    id = rt.Id,
                    token = rt.Token, // to match current token
                    device = rt.UserAgent, // raw UserAgent first, parsed on client
                    ip = rt.IpAddress,
                    createdAt = rt.CreatedAt,
                    expiryTime = rt.ExpiryTime,
                    isCurrent = rt.Token == currentRefreshToken
                })
                .ToListAsync();

            return Ok(new { statusCode = 200, data = sessions });
        }

        /// <summary>
        /// DELETE /api/users/sessions/{tokenId} — Terminate a specific session
        /// </summary>
        [HttpDelete("sessions/{tokenId}")]
        public async Task<IActionResult> RevokeSession(Guid tokenId)
        {
            var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrEmpty(userIdClaim) || !Guid.TryParse(userIdClaim, out Guid userId))
                return Unauthorized(new { statusCode = 401, message = "Không có quyền truy cập." });

            var session = await _context.RefreshTokens.FirstOrDefaultAsync(rt => rt.Id == tokenId && rt.UserId == userId);
            if (session == null)
                return NotFound(new { statusCode = 404, message = "Không tìm thấy phiên đăng nhập." });

            session.IsRevoked = true;
            await _context.SaveChangesAsync();

            return Ok(new { statusCode = 200, message = "Đã thu hồi phiên đăng nhập thành công." });
        }

        /// <summary>
        /// DELETE /api/users/sessions/others — Terminate all sessions except the current one
        /// </summary>
        [HttpDelete("sessions/others")]
        public async Task<IActionResult> RevokeOtherSessions()
        {
            var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrEmpty(userIdClaim) || !Guid.TryParse(userIdClaim, out Guid userId))
                return Unauthorized(new { statusCode = 401, message = "Không có quyền truy cập." });

            var currentRefreshToken = Request.Cookies["refreshToken"];

            var otherSessions = await _context.RefreshTokens
                .Where(rt => rt.UserId == userId && rt.Token != currentRefreshToken && !rt.IsRevoked)
                .ToListAsync();

            foreach (var session in otherSessions)
            {
                session.IsRevoked = true;
            }

            await _context.SaveChangesAsync();
            return Ok(new { statusCode = 200, message = "Đã đăng xuất khỏi các thiết bị khác thành công." });
        }

        /// <summary>
        /// GET /api/users/login-activity — Get recent login activities from SystemAuditLogs
        /// </summary>
        [HttpGet("login-activity")]
        public async Task<IActionResult> GetLoginActivity()
        {
            var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrEmpty(userIdClaim) || !Guid.TryParse(userIdClaim, out Guid userId))
                return Unauthorized(new { statusCode = 401, message = "Không có quyền truy cập." });

            var logs = await _context.SystemAuditLogs
                .Where(l => l.UserId == userId && l.Action == "Login")
                .OrderByDescending(l => l.CreatedAt)
                .Take(10)
                .Select(l => new
                {
                    id = l.Id,
                    createdAt = l.CreatedAt,
                    ipAddress = l.IPAddress,
                    details = l.Details,
                    status = l.Status
                })
                .ToListAsync();

            return Ok(new { statusCode = 200, data = logs });
        }
    }
}
