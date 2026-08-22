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

        Task<string?> SendPaymentReceiptEmailAsync(
            string toEmail,
            string customerName,
            string receiptNumber,
            string planName,
            decimal amount,
            string currency,
            DateTime paidAt);
    }
}
