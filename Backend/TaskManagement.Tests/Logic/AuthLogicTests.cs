using System;
using System.Security.Claims;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Options;
using Moq;
using TaskManagement.Application.Configuration;
using TaskManagement.Application.DTOs.Auth;
using TaskManagement.Application.Interfaces;
using TaskManagement.Domain.Entities;
using TaskManagement.Infrastructure.Data;
using TaskManagement.Infrastructure.Services;
using Xunit;
using FluentAssertions;

namespace TaskManagement.Tests.Logic
{
    public class AuthLogicTests : IDisposable
    {
        private readonly ApplicationDbContext _context;
        private readonly IMemoryCache _memoryCache;
        private readonly OtpService _otpService;
        private readonly AuthService _authService;
        private readonly Mock<IJwtService> _jwtServiceMock;
        private readonly Mock<IConfiguration> _configurationMock;
        private readonly Mock<IEmailService> _emailServiceMock;

        public AuthLogicTests()
        {
            // Set up DB
            var options = new DbContextOptionsBuilder<ApplicationDbContext>()
                .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
                .Options;
            _context = new ApplicationDbContext(options);

            // Set up memory cache
            _memoryCache = new MemoryCache(new MemoryCacheOptions());
            _otpService = CreateOtpService(_memoryCache);

            // Set up mocks
            _jwtServiceMock = new Mock<IJwtService>();
            _configurationMock = new Mock<IConfiguration>();
            _emailServiceMock = new Mock<IEmailService>();

            _authService = new AuthService(
                _context,
                _jwtServiceMock.Object,
                _configurationMock.Object,
                _otpService,
                _emailServiceMock.Object
            );
        }

        [Fact]
        public void OtpService_Generate_ReturnsSixCharacters()
        {
            var otp = _otpService.GenerateOtp();
            otp.Should().NotBeNullOrWhiteSpace();
            otp.Length.Should().Be(6);
            otp.Should().MatchRegex("^[0-9]{6}$");
        }

        [Fact]
        public void OtpService_StoreAndValidate_SucceedsAndRemoves()
        {
            var email = "test@example.com";
            var otp = _otpService.GenerateOtp();

            _otpService.StoreOtp(email, otp);

            // First validation should succeed
            var isValid = _otpService.ValidateOtp(email, otp);
            isValid.IsValid.Should().BeTrue();

            // Second validation should fail (deleted from cache)
            var isStillValid = _otpService.ValidateOtp(email, otp);
            isStillValid.IsValid.Should().BeFalse();
        }

        [Fact]
        public void OtpService_ValidateWrongCode_Fails()
        {
            var email = "test@example.com";
            var otp = _otpService.GenerateOtp();

            _otpService.StoreOtp(email, otp);

            var isValid = _otpService.ValidateOtp(email, "WRONG1");
            isValid.IsValid.Should().BeFalse();
        }

        [Fact]
        public void OtpService_Generate_LargeSampleHasReasonableDistribution()
        {
            const int sampleSize = 100_000;
            var counts = new int[10];

            for (var sample = 0; sample < sampleSize; sample++)
            {
                foreach (var digit in _otpService.GenerateOtp())
                {
                    counts[digit - '0']++;
                }
            }

            var expected = sampleSize * 6d / 10d;
            var chiSquare = counts.Sum(count => Math.Pow(count - expected, 2) / expected);
            chiSquare.Should().BeLessThan(30d);
        }

        [Fact]
        public void OtpService_CanonicalEmail_TrimAndCaseAreEquivalent()
        {
            var otp = _otpService.GenerateOtp();
            _otpService.StoreOtp("  USER@Example.COM ", otp).Issued.Should().BeTrue();

            _otpService.ValidateOtp("user@example.com", otp).IsValid.Should().BeTrue();
        }

        [Fact]
        public void OtpService_TooManyFailures_LocksChallenge()
        {
            _otpService.StoreOtp("locked@example.com", "123456").Issued.Should().BeTrue();

            OtpValidationResult result = default;
            for (var attempt = 0; attempt < 5; attempt++)
            {
                result = _otpService.ValidateOtp("locked@example.com", "000000", "127.0.0.1");
            }

            result.Status.Should().Be(OtpValidationStatus.Locked);
            _otpService.ValidateOtp("locked@example.com", "123456", "127.0.0.1").Status
                .Should().Be(OtpValidationStatus.Locked);
        }

        [Fact]
        public void OtpService_ResendWithinCooldown_IsRejected()
        {
            _otpService.StoreOtp("cooldown@example.com", "123456").Issued.Should().BeTrue();

            var resend = _otpService.StoreOtp("cooldown@example.com", "654321");

            resend.Issued.Should().BeFalse();
            resend.RetryAfterSeconds.Should().BeGreaterThan(0);
        }

        [Fact]
        public async Task OtpService_ResendInvalidatesPreviousOtp()
        {
            using var cache = new MemoryCache(new MemoryCacheOptions());
            var service = CreateOtpService(cache, new OtpSecurityOptions
            {
                ResendCooldownSeconds = 1,
                OtpExpirationSeconds = 300,
                VerificationTokenExpirationSeconds = 300,
                MaxFailedAttempts = 5,
                LockoutSeconds = 300
            });
            service.StoreOtp("resend@example.com", "123456").Issued.Should().BeTrue();
            await Task.Delay(1100);
            service.StoreOtp("resend@example.com", "654321").Issued.Should().BeTrue();

            service.ValidateOtp("resend@example.com", "123456").IsValid.Should().BeFalse();
            service.ValidateOtp("resend@example.com", "654321").IsValid.Should().BeTrue();
        }

        [Fact]
        public async Task OtpService_ExpiredOtp_IsRejected()
        {
            using var cache = new MemoryCache(new MemoryCacheOptions());
            var service = CreateOtpService(cache, new OtpSecurityOptions
            {
                ResendCooldownSeconds = 1,
                OtpExpirationSeconds = 1,
                VerificationTokenExpirationSeconds = 30,
                MaxFailedAttempts = 5,
                LockoutSeconds = 30
            });
            service.StoreOtp("expired@example.com", "123456").Issued.Should().BeTrue();
            await Task.Delay(1100);

            service.ValidateOtp("expired@example.com", "123456").IsValid.Should().BeFalse();
        }

        [Fact]
        public void OtpService_CacheDoesNotStorePlaintextOtp()
        {
            _otpService.StoreOtp("hash@example.com", "123456").Issued.Should().BeTrue();

            _memoryCache.TryGetValue("otp:challenge:hash@example.com", out object? cached).Should().BeTrue();
            cached.Should().NotBeOfType<string>();
            cached?.ToString().Should().NotContain("123456");
        }

        [Fact]
        public async Task ResetPassword_ValidOtp_UpdatesPasswordCorrectly()
        {
            // Seed user
            var email = "user@example.com";
            var user = new User
            {
                Id = Guid.NewGuid(),
                Email = email,
                FullName = "Test User",
                PasswordHash = BCrypt.Net.BCrypt.HashPassword("OldPass123!"),
                IsActive = true,
                CreatedAt = DateTime.UtcNow
            };
            _context.Users.Add(user);
            await _context.SaveChangesAsync();

            // Store OTP
            var otp = _otpService.GenerateOtp();
            _otpService.StoreOtp(email, otp);

            // Reset request
            var request = new ResetPasswordRequestDto
            {
                Email = email,
                OtpToken = otp,
                NewPassword = "NewPass123!"
            };

            // Act
            await _authService.ResetPasswordAsync(request);

            // Assert - check password hash
            var updatedUser = await _context.Users.FindAsync(user.Id);
            updatedUser.Should().NotBeNull();
            BCrypt.Net.BCrypt.Verify("NewPass123!", updatedUser!.PasswordHash).Should().BeTrue();
        }

        [Fact]
        public async Task ResetPassword_InvalidOtp_ThrowsUnauthorizedAccessException()
        {
            var email = "user@example.com";
            var user = new User
            {
                Id = Guid.NewGuid(),
                Email = email,
                FullName = "Test User",
                PasswordHash = BCrypt.Net.BCrypt.HashPassword("OldPass123!"),
                IsActive = true,
                CreatedAt = DateTime.UtcNow
            };
            _context.Users.Add(user);
            await _context.SaveChangesAsync();

            var request = new ResetPasswordRequestDto
            {
                Email = email,
                OtpToken = "INVALID",
                NewPassword = "NewPass123!"
            };

            // Act & Assert
            await Assert.ThrowsAsync<UnauthorizedAccessException>(() => _authService.ResetPasswordAsync(request));
        }

        [Fact]
        public async Task ResetPassword_NonExistentEmail_ThrowsArgumentException()
        {
            // Seed OTP for email that doesn't belong to any user
            var email = "nonexistent@example.com";
            var otp = _otpService.GenerateOtp();
            _otpService.StoreOtp(email, otp);

            var request = new ResetPasswordRequestDto
            {
                Email = email,
                OtpToken = otp,
                NewPassword = "NewPass123!"
            };

            // Act & Assert
            await Assert.ThrowsAsync<ArgumentException>(() => _authService.ResetPasswordAsync(request));
        }

        [Fact]
        public async Task Register_ValidOtp_Succeeds()
        {
            var email = "register@example.com";
            var otp = _otpService.GenerateOtp();
            _otpService.StoreOtp(email, otp);

            var request = new RegisterRequestDto
            {
                Email = email,
                FullName = "Register User",
                Password = "Password123!",
                OtpCode = otp
            };

            await _authService.RegisterAsync(request);

            var user = await _context.Users.FirstOrDefaultAsync(u => u.Email == email);
            user.Should().NotBeNull();
            user!.FullName.Should().Be("Register User");
            BCrypt.Net.BCrypt.Verify("Password123!", user.PasswordHash).Should().BeTrue();
        }

        [Fact]
        public async Task Register_InvalidOtp_ThrowsInvalidOperationException()
        {
            var email = "register@example.com";

            var request = new RegisterRequestDto
            {
                Email = email,
                FullName = "Register User",
                Password = "Password123!",
                OtpCode = "INVALID"
            };

            await Assert.ThrowsAsync<InvalidOperationException>(() => _authService.RegisterAsync(request));
        }

        [Fact]
        public async Task Login_ActiveUser_WithCanonicalEmail_Succeeds()
        {
            var user = new User
            {
                Id = Guid.NewGuid(),
                Email = "active@example.com",
                FullName = "Active User",
                PasswordHash = BCrypt.Net.BCrypt.HashPassword("Password123!"),
                IsActive = true,
                IsDeleted = false
            };
            _context.Users.Add(user);
            await _context.SaveChangesAsync();
            _jwtServiceMock.Setup(service => service.GenerateAccessToken(user, It.IsAny<IList<string>>())).Returns("access-token");
            _jwtServiceMock.Setup(service => service.GenerateRefreshToken()).Returns("refresh-token");

            var result = await _authService.LoginAsync(new LoginRequestDto
            {
                Email = "  ACTIVE@Example.COM  ",
                Password = "Password123!"
            });

            result.response.Should().NotBeNull();
            result.response!.AccessToken.Should().Be("access-token");
            result.refreshToken.Should().Be("refresh-token");
            (await _context.RefreshTokens.SingleAsync()).IsRevoked.Should().BeFalse();
        }

        [Fact]
        public async Task Login_InactiveUser_IsRejectedWithGenericCredentialsError()
        {
            _context.Users.Add(new User
            {
                Id = Guid.NewGuid(),
                Email = "inactive@example.com",
                FullName = "Inactive User",
                PasswordHash = BCrypt.Net.BCrypt.HashPassword("Password123!"),
                IsActive = false,
                IsDeleted = false
            });
            await _context.SaveChangesAsync();

            var action = () => _authService.LoginAsync(new LoginRequestDto
            {
                Email = "inactive@example.com",
                Password = "Password123!"
            });

            var exception = await action.Should().ThrowAsync<UnauthorizedAccessException>();
            exception.Which.Message.Should().Be("Email hoặc mật khẩu không chính xác.");
        }

        [Fact]
        public async Task RefreshToken_ActiveUser_RotatesTrackedSession()
        {
            var userId = Guid.NewGuid();
            var user = new User
            {
                Id = userId,
                Email = "refresh@example.com",
                FullName = "Refresh User",
                PasswordHash = "unused",
                IsActive = true,
                IsDeleted = false,
                RefreshToken = "old-refresh",
                RefreshTokenExpiryTime = DateTime.UtcNow.AddDays(1)
            };
            _context.Users.Add(user);
            _context.RefreshTokens.Add(new RefreshToken
            {
                Id = Guid.NewGuid(),
                UserId = userId,
                Token = "old-refresh",
                DeviceId = "browser",
                ExpiryTime = DateTime.UtcNow.AddDays(1),
                IsRevoked = false
            });
            await _context.SaveChangesAsync();

            var principal = new ClaimsPrincipal(new ClaimsIdentity(
                new[] { new Claim(ClaimTypes.NameIdentifier, userId.ToString()) },
                "ExpiredJwt"));
            _jwtServiceMock.Setup(service => service.GetPrincipalFromExpiredToken("expired-access")).Returns(principal);
            _jwtServiceMock.Setup(service => service.GenerateAccessToken(user, It.IsAny<IList<string>>())).Returns("new-access");
            _jwtServiceMock.Setup(service => service.GenerateRefreshToken()).Returns("new-refresh");

            var result = await _authService.RefreshTokenAsync("expired-access", "old-refresh");

            result.newAccessToken.Should().Be("new-access");
            result.newRefreshToken.Should().Be("new-refresh");
            (await _context.RefreshTokens.SingleAsync(token => token.Token == "old-refresh")).IsRevoked.Should().BeTrue();
            (await _context.RefreshTokens.SingleAsync(token => token.Token == "new-refresh")).IsRevoked.Should().BeFalse();
        }

        [Fact]
        public async Task RefreshToken_DeletedUser_IsRejectedAndSessionsAreRevoked()
        {
            var userId = Guid.NewGuid();
            _context.Users.Add(new User
            {
                Id = userId,
                Email = "deleted-refresh@example.com",
                FullName = "Deleted User",
                PasswordHash = "unused",
                IsActive = true,
                IsDeleted = true,
                RefreshToken = "deleted-refresh",
                RefreshTokenExpiryTime = DateTime.UtcNow.AddDays(1)
            });
            _context.RefreshTokens.Add(new RefreshToken
            {
                Id = Guid.NewGuid(),
                UserId = userId,
                Token = "deleted-refresh",
                DeviceId = "browser",
                ExpiryTime = DateTime.UtcNow.AddDays(1),
                IsRevoked = false
            });
            await _context.SaveChangesAsync();
            var principal = new ClaimsPrincipal(new ClaimsIdentity(
                new[] { new Claim(ClaimTypes.NameIdentifier, userId.ToString()) },
                "ExpiredJwt"));
            _jwtServiceMock.Setup(service => service.GetPrincipalFromExpiredToken("expired-access")).Returns(principal);

            var action = () => _authService.RefreshTokenAsync("expired-access", "deleted-refresh");

            await action.Should().ThrowAsync<UnauthorizedAccessException>();
            (await _context.Users.IgnoreQueryFilters().SingleAsync()).RefreshToken.Should().BeNull();
            (await _context.RefreshTokens.SingleAsync()).IsRevoked.Should().BeTrue();
        }

        public void Dispose()
        {
            _context.Database.EnsureDeleted();
            _context.Dispose();
            _memoryCache.Dispose();
        }

        private static OtpService CreateOtpService(IMemoryCache cache, OtpSecurityOptions? options = null)
        {
            return new OtpService(cache, Options.Create(options ?? new OtpSecurityOptions()));
        }
    }
}
