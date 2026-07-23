using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;
using TaskManagement.Application.Common;
using TaskManagement.Application.Configuration;
using TaskManagement.Application.Interfaces;
using System.Security.Cryptography;
using System.Text;

namespace TaskManagement.Infrastructure.Services
{
    public class OtpService : IOtpService
    {
        private readonly IMemoryCache _cache;
        private readonly OtpSecurityOptions _options;
        private static readonly object CacheLock = new();

        public OtpService(IMemoryCache cache, IOptions<OtpSecurityOptions> options)
        {
            _cache = cache;
            _options = options.Value;
        }

        /// <summary>
        /// Tạo mã OTP ngẫu nhiên 6 ký tự (chữ + số) sử dụng RandomNumberGenerator (an toàn hơn Random)
        /// </summary>
        public string GenerateOtp()
        {
            var otpChars = new char[_options.OtpLength];
            for (var i = 0; i < otpChars.Length; i++)
            {
                otpChars[i] = (char)('0' + RandomNumberGenerator.GetInt32(0, 10));
            }

            return new string(otpChars);
        }

        /// <summary>
        /// Lưu OTP vào MemoryCache với key là email, hết hạn sau 5 phút
        /// </summary>
        public OtpIssueResult StoreOtp(string email, string otp, string? fingerprint = null)
        {
            var canonicalEmail = RequireCanonicalEmail(email);
            var now = DateTimeOffset.UtcNow;

            lock (CacheLock)
            {
                var lockout = GetLockout(canonicalEmail, fingerprint, now);
                if (lockout > 0)
                {
                    return new OtpIssueResult(false, true, lockout);
                }

                if (_cache.TryGetValue<DateTimeOffset>(CooldownKey(canonicalEmail), out var cooldownUntil) && cooldownUntil > now)
                {
                    return new OtpIssueResult(false, false, SecondsUntil(cooldownUntil, now));
                }

                StoreChallenge(canonicalEmail, otp, TimeSpan.FromSeconds(_options.OtpExpirationSeconds));
                var nextSendAt = now.AddSeconds(_options.ResendCooldownSeconds);
                _cache.Set(CooldownKey(canonicalEmail), nextSendAt, nextSendAt);
                return new OtpIssueResult(true, false, 0);
            }
        }

        public string IssueVerificationToken(string email, string? fingerprint = null)
        {
            var canonicalEmail = RequireCanonicalEmail(email);
            var tokenBytes = RandomNumberGenerator.GetBytes(32);
            var token = Convert.ToBase64String(tokenBytes)
                .TrimEnd('=')
                .Replace('+', '-')
                .Replace('/', '_');

            lock (CacheLock)
            {
                StoreChallenge(canonicalEmail, token, TimeSpan.FromSeconds(_options.VerificationTokenExpirationSeconds));
                ClearAttemptState(canonicalEmail, fingerprint);
            }

            return token;
        }

        /// <summary>
        /// Xác thực OTP: so sánh mã nhập vào với mã đã lưu trong cache
        /// Sau khi xác thực thành công, xóa OTP khỏi cache (chỉ dùng 1 lần)
        /// </summary>
        public OtpValidationResult ValidateOtp(string email, string otp, string? fingerprint = null)
        {
            var canonicalEmail = RequireCanonicalEmail(email);
            var suppliedValue = (otp ?? string.Empty).Trim();
            var now = DateTimeOffset.UtcNow;

            lock (CacheLock)
            {
                var lockout = GetLockout(canonicalEmail, fingerprint, now);
                if (lockout > 0)
                {
                    return new OtpValidationResult(OtpValidationStatus.Locked, lockout);
                }

                var challenge = _cache.Get<OtpChallenge>(ChallengeKey(canonicalEmail));
                if (challenge != null && IsChallengeMatch(canonicalEmail, suppliedValue, challenge))
                {
                    _cache.Remove(ChallengeKey(canonicalEmail));
                    _cache.Remove(CooldownKey(canonicalEmail));
                    ClearAttemptState(canonicalEmail, fingerprint);
                    return new OtpValidationResult(OtpValidationStatus.Valid);
                }

                return RegisterFailure(canonicalEmail, fingerprint, now);
            }
        }

        public void InvalidateOtp(string email)
        {
            var canonicalEmail = RequireCanonicalEmail(email);
            lock (CacheLock)
            {
                _cache.Remove(ChallengeKey(canonicalEmail));
                _cache.Remove(CooldownKey(canonicalEmail));
            }
        }

        private void StoreChallenge(string canonicalEmail, string value, TimeSpan lifetime)
        {
            var salt = RandomNumberGenerator.GetBytes(32);
            var challenge = new OtpChallenge(salt, HashValue(canonicalEmail, value, salt));
            _cache.Set(ChallengeKey(canonicalEmail), challenge, lifetime);
        }

        private OtpValidationResult RegisterFailure(string canonicalEmail, string? fingerprint, DateTimeOffset now)
        {
            var emailAttempts = IncrementAttempts(EmailAttemptsKey(canonicalEmail));
            var fingerprintAttempts = string.IsNullOrWhiteSpace(fingerprint)
                ? 0
                : IncrementAttempts(FingerprintAttemptsKey(canonicalEmail, fingerprint));

            if (emailAttempts < _options.MaxFailedAttempts && fingerprintAttempts < _options.MaxFailedAttempts)
            {
                return new OtpValidationResult(OtpValidationStatus.Invalid);
            }

            var lockedUntil = now.AddSeconds(_options.LockoutSeconds);
            _cache.Set(EmailLockKey(canonicalEmail), lockedUntil, lockedUntil);
            if (!string.IsNullOrWhiteSpace(fingerprint))
            {
                _cache.Set(FingerprintLockKey(canonicalEmail, fingerprint), lockedUntil, lockedUntil);
            }

            _cache.Remove(ChallengeKey(canonicalEmail));
            return new OtpValidationResult(OtpValidationStatus.Locked, _options.LockoutSeconds);
        }

        private int IncrementAttempts(string key)
        {
            var attempts = (_cache.Get<int?>(key) ?? 0) + 1;
            _cache.Set(key, attempts, TimeSpan.FromSeconds(_options.LockoutSeconds));
            return attempts;
        }

        private int GetLockout(string canonicalEmail, string? fingerprint, DateTimeOffset now)
        {
            var emailLockout = RemainingSeconds(EmailLockKey(canonicalEmail), now);
            var fingerprintLockout = string.IsNullOrWhiteSpace(fingerprint)
                ? 0
                : RemainingSeconds(FingerprintLockKey(canonicalEmail, fingerprint), now);
            return Math.Max(emailLockout, fingerprintLockout);
        }

        private int RemainingSeconds(string key, DateTimeOffset now)
        {
            return _cache.TryGetValue<DateTimeOffset>(key, out var until) && until > now
                ? SecondsUntil(until, now)
                : 0;
        }

        private void ClearAttemptState(string canonicalEmail, string? fingerprint)
        {
            _cache.Remove(EmailAttemptsKey(canonicalEmail));
            _cache.Remove(EmailLockKey(canonicalEmail));
            if (!string.IsNullOrWhiteSpace(fingerprint))
            {
                _cache.Remove(FingerprintAttemptsKey(canonicalEmail, fingerprint));
                _cache.Remove(FingerprintLockKey(canonicalEmail, fingerprint));
            }
        }

        private static bool IsChallengeMatch(string canonicalEmail, string value, OtpChallenge challenge)
        {
            var candidate = HashValue(canonicalEmail, value, challenge.Salt);
            return CryptographicOperations.FixedTimeEquals(candidate, challenge.Hash);
        }

        private static byte[] HashValue(string canonicalEmail, string value, byte[] salt)
        {
            var emailBytes = Encoding.UTF8.GetBytes(canonicalEmail);
            var valueBytes = Encoding.UTF8.GetBytes(value);
            var buffer = new byte[salt.Length + emailBytes.Length + valueBytes.Length + 1];
            Buffer.BlockCopy(salt, 0, buffer, 0, salt.Length);
            Buffer.BlockCopy(emailBytes, 0, buffer, salt.Length, emailBytes.Length);
            buffer[salt.Length + emailBytes.Length] = 0;
            Buffer.BlockCopy(valueBytes, 0, buffer, salt.Length + emailBytes.Length + 1, valueBytes.Length);
            return SHA256.HashData(buffer);
        }

        private static string RequireCanonicalEmail(string? email)
        {
            var canonicalEmail = EmailCanonicalizer.Normalize(email);
            return !string.IsNullOrWhiteSpace(canonicalEmail)
                ? canonicalEmail
                : throw new ArgumentException("Email is required.", nameof(email));
        }

        private static int SecondsUntil(DateTimeOffset until, DateTimeOffset now)
            => Math.Max(1, (int)Math.Ceiling((until - now).TotalSeconds));

        private static string ChallengeKey(string email) => $"otp:challenge:{email}";
        private static string CooldownKey(string email) => $"otp:cooldown:{email}";
        private static string EmailAttemptsKey(string email) => $"otp:attempts:email:{email}";
        private static string EmailLockKey(string email) => $"otp:lock:email:{email}";
        private static string FingerprintAttemptsKey(string email, string fingerprint) => $"otp:attempts:fingerprint:{email}:{FingerprintHash(fingerprint)}";
        private static string FingerprintLockKey(string email, string fingerprint) => $"otp:lock:fingerprint:{email}:{FingerprintHash(fingerprint)}";
        private static string FingerprintHash(string fingerprint) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(fingerprint.Trim())));

        private sealed record OtpChallenge(byte[] Salt, byte[] Hash);
    }
}
