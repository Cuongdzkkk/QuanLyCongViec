namespace TaskManagement.Application.Interfaces
{
    public sealed class OtpRateLimitException : Exception
    {
        public OtpRateLimitException(int retryAfterSeconds)
            : base("Too many OTP requests or attempts. Please try again later.")
        {
            RetryAfterSeconds = retryAfterSeconds;
        }

        public int RetryAfterSeconds { get; }
    }

    public enum OtpValidationStatus
    {
        Valid,
        Invalid,
        Locked
    }

    public readonly record struct OtpIssueResult(bool Issued, bool Locked, int RetryAfterSeconds);
    public readonly record struct OtpValidationResult(OtpValidationStatus Status, int RetryAfterSeconds = 0)
    {
        public bool IsValid => Status == OtpValidationStatus.Valid;
    }

    public interface IOtpService
    {
        /// <summary>
        /// Tạo mã OTP ngẫu nhiên 6 ký tự (chữ + số)
        /// </summary>
        string GenerateOtp();

        /// <summary>
        /// Lưu OTP vào cache với thời gian hết hạn 5 phút
        /// </summary>
        OtpIssueResult StoreOtp(string email, string otp, string? fingerprint = null);

        /// <summary>
        /// Đổi OTP đã xác minh thành token opaque dùng một lần cho bước nghiệp vụ kế tiếp.
        /// </summary>
        string IssueVerificationToken(string email, string? fingerprint = null);

        /// <summary>
        /// Xác thực OTP - so sánh mã nhập vào với mã đã lưu
        /// </summary>
        OtpValidationResult ValidateOtp(string email, string otp, string? fingerprint = null);

        /// <summary>
        /// Xóa challenge/cooldown khi email không gửi được để user có thể retry an toàn.
        /// </summary>
        void InvalidateOtp(string email);
    }
}
