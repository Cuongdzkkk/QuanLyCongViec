namespace TaskManagement.Application.Configuration
{
    public sealed class OtpSecurityOptions
    {
        public const string SectionName = "OtpSecurity";

        public int OtpLength { get; set; } = 6;
        public int OtpExpirationSeconds { get; set; } = 300;
        public int VerificationTokenExpirationSeconds { get; set; } = 300;
        public int ResendCooldownSeconds { get; set; } = 60;
        public int MaxFailedAttempts { get; set; } = 5;
        public int LockoutSeconds { get; set; } = 300;

        public bool IsValid()
        {
            return OtpLength == 6
                && OtpExpirationSeconds is >= 30 and <= 1800
                && VerificationTokenExpirationSeconds is >= 30 and <= 1800
                && ResendCooldownSeconds is >= 1 and <= 900
                && MaxFailedAttempts is >= 3 and <= 10
                && LockoutSeconds is >= 30 and <= 3600;
        }
    }
}
