namespace TaskManagement.Application.Interfaces
{
    public interface IEmailService
    {
        Task SendOtpEmailAsync(string toEmail, string otpCode);

        Task SendInviteEmailAsync(
            string toEmail,
            string inviteeName,
            string inviterName,
            string organizationName,
            string? projectName,
            string acceptUrl,
            string? personalMessage);

        Task SendPasswordChangeRequestEmailAsync(
            string toEmail,
            string requesterName,
            string requesterEmail,
            DateTime? lastChangedAt,
            DateTime eligibleAt);

        Task SendPaymentPendingEmailAsync(string toEmail, string planName, decimal amountVnd, string transferCode, string checkoutUrl);
        Task SendPaymentPaidEmailAsync(string toEmail, string planName, DateTime? currentPeriodEnd, string checkoutUrl);
        Task SendPaymentRejectedEmailAsync(string toEmail, string planName, string? reason, string pricingUrl);
    }
}
