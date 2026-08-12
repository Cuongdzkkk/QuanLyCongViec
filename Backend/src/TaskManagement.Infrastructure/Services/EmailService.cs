using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using TaskManagement.Application.Interfaces;
using System.Net;
using System.Text;
using System.Text.Json;

namespace TaskManagement.Infrastructure.Services
{
    public class EmailService : IEmailService
    {
        private readonly IConfiguration _configuration;
        private readonly HttpClient _httpClient;
        private readonly ILogger<EmailService> _logger;

        public EmailService(IConfiguration configuration, HttpClient httpClient, ILogger<EmailService> logger)
        {
            _configuration = configuration;
            _httpClient = httpClient;
            _logger = logger;
        }

        public async Task SendOtpEmailAsync(string toEmail, string otpCode)
        {
            var subject = "Mã xác thực OTP - SprintA";
            var html = $@"
                <div style='font-family: Arial, sans-serif; max-width: 480px; margin: 0 auto; padding: 32px; background:#ffffff;'>
                    <div style='text-align:center; margin-bottom: 24px;'>
                        <span style='font-size:22px; font-weight:700; color:#0ea5e9;'>SprintA</span>
                    </div>
                    <h2 style='color: #0f172a; text-align: center; font-size:20px;'>Mã xác thực OTP</h2>
                    <p style='color: #64748b; text-align: center;'>Sử dụng mã bên dưới để hoàn tất đăng ký tài khoản SprintA:</p>
                    <div style='background: #f1f5f9; border-radius: 12px; padding: 24px; text-align: center; margin: 24px 0;'>
                        <span style='font-size: 32px; font-weight: 700; letter-spacing: 8px; color: #0ea5e9;'>{WebUtility.HtmlEncode(otpCode)}</span>
                    </div>
                    <p style='color: #94a3b8; font-size: 13px; text-align: center;'>Mã này có hiệu lực trong 5 phút. Không chia sẻ mã này với bất kỳ ai.</p>
                    <hr style='border:none; border-top:1px solid #e2e8f0; margin: 24px 0;'/>
                    <p style='color: #94a3b8; font-size: 12px; text-align: center; line-height:1.6;'>
                        Bạn nhận email này vì đang đăng ký hoặc đặt lại mật khẩu tại SprintA.<br/>
                        Nếu bạn không thực hiện hành động này, hãy bỏ qua email này.
                    </p>
                </div>";

            var text = $"Mã xác thực OTP của bạn là: {otpCode}\n\nMã này có hiệu lực trong 5 phút. Không chia sẻ mã này với bất kỳ ai.\n\nBạn nhận email này vì đang đăng ký hoặc đặt lại mật khẩu tại SprintA.";

            try
            {
                // Giới hạn thời gian gửi qua API Resend tối đa 10 giây để tránh bị treo lâu khi mạng có sự cố
                using (var cts = new System.Threading.CancellationTokenSource(TimeSpan.FromSeconds(10)))
                {
                    await SendResendEmailAsync(toEmail, subject, html, text, cts.Token);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Resend OTP delivery failed for a recipient.");
                throw new InvalidOperationException("Không thể gửi email OTP qua Resend. Vui lòng thử lại sau.");
            }
        }

        public Task SendPaymentPendingEmailAsync(string toEmail, string planName, decimal amountVnd, string transferCode, string checkoutUrl) =>
            SendResendEmailAsync(toEmail, "Đơn thanh toán SprintA đang chờ xác nhận", PaymentHtml(
                "Đơn thanh toán đang chờ xác nhận",
                $"Gói <strong>{WebUtility.HtmlEncode(planName)}</strong> · {amountVnd:N0} VND",
                $"Mã chuyển khoản: <strong>{WebUtility.HtmlEncode(transferCode)}</strong><br/>Trạng thái: Chờ xác nhận",
                checkoutUrl, "Mở trang thanh toán"), null);

        public Task SendPaymentPaidEmailAsync(string toEmail, string planName, DateTime? currentPeriodEnd, string checkoutUrl) =>
            SendResendEmailAsync(toEmail, "Gói SprintA đã được kích hoạt", PaymentHtml(
                "Thanh toán đã được xác nhận",
                $"Gói <strong>{WebUtility.HtmlEncode(planName)}</strong> đã được kích hoạt.",
                $"Trạng thái: Đã thanh toán<br/>Kỳ hiện tại kết thúc: {WebUtility.HtmlEncode(currentPeriodEnd?.ToString("dd/MM/yyyy HH:mm 'UTC'") ?? "Đang cập nhật")}",
                checkoutUrl, "Mở SprintA"), null);

        public Task SendPaymentRejectedEmailAsync(string toEmail, string planName, string? reason, string pricingUrl) =>
            SendResendEmailAsync(toEmail, "Đơn thanh toán SprintA bị từ chối", PaymentHtml(
                "Đơn thanh toán chưa được chấp nhận",
                $"Đơn cho gói <strong>{WebUtility.HtmlEncode(planName)}</strong> có trạng thái: Từ chối.",
                string.IsNullOrWhiteSpace(reason) ? "Vui lòng xem lại thông tin và thử lại." : $"Lý do: {WebUtility.HtmlEncode(reason)}",
                pricingUrl, "Xem bảng giá"), null);

        public async Task SendInviteEmailAsync(
            string toEmail,
            string inviteeName,
            string inviterName,
            string organizationName,
            string? projectName,
            string acceptUrl,
            string? personalMessage)
        {
            var safeInviteeName = WebUtility.HtmlEncode(inviteeName);
            var safeInviterName = WebUtility.HtmlEncode(inviterName);
            var safeOrganizationName = WebUtility.HtmlEncode(organizationName);
            var safeProjectName = WebUtility.HtmlEncode(projectName ?? "SprintA");
            var safeAcceptUrl = WebUtility.HtmlEncode(acceptUrl);
            var safePersonalMessage = WebUtility.HtmlEncode(personalMessage ?? string.Empty);

            var subject = $"Action requested: {inviterName} invited you to join {projectName ?? organizationName}";
            var projectLine = string.IsNullOrWhiteSpace(projectName)
                ? "your team in SprintA"
                : $"the project <strong>{safeProjectName}</strong>";

            var personalMessageBlock = string.IsNullOrWhiteSpace(personalMessage)
                ? string.Empty
                : $@"
                    <div style='margin: 22px 0; padding: 14px 16px; border-left: 3px solid #0c66e4; background: #f4f5f7; color: #172b4d; font-size: 14px; line-height: 1.55;'>
                        {safePersonalMessage}
                    </div>";

            var html = $@"
                <div style='margin:0; padding:0; background:#eaf2ff;'>
                    <table role='presentation' width='100%' cellpadding='0' cellspacing='0' style='background:#eaf2ff; padding:34px 14px;'>
                        <tr>
                            <td align='center'>
                                <table role='presentation' width='560' cellpadding='0' cellspacing='0' style='width:560px; max-width:100%; background:#ffffff; border-radius:8px; overflow:hidden; font-family:Arial, sans-serif;'>
                                    <tr>
                                        <td align='center' style='padding:42px 40px 18px;'>
                                            <div style='font-size:18px; color:#172b4d; font-weight:700;'>SprintA</div>
                                        </td>
                                    </tr>
                                    <tr>
                                        <td style='padding:18px 40px 8px; color:#172b4d;'>
                                            <p style='margin:0 0 22px; color:#44546f; font-size:14px;'>Hi {safeInviteeName},</p>
                                            <h1 style='margin:0; color:#172b4d; font-size:24px; line-height:1.28; font-weight:700;'>
                                                Your admin {safeInviterName} invited you to join {projectLine}
                                            </h1>
                                            <p style='margin:20px 0 0; color:#172b4d; font-size:14px; line-height:1.55;'>
                                                SprintA helps your team plan projects, track issues, and collaborate in one workspace.
                                            </p>
                                            {personalMessageBlock}
                                        </td>
                                    </tr>
                                    <tr>
                                        <td style='padding:18px 40px 30px;'>
                                            <a href='{safeAcceptUrl}' style='display:block; width:100%; box-sizing:border-box; background:#0c66e4; color:#ffffff; text-decoration:none; text-align:center; border-radius:4px; padding:12px 18px; font-size:14px; font-weight:700;'>
                                                Accept invite
                                            </a>
                                            <p style='margin:18px 0 0; color:#626f86; font-size:12px; line-height:1.55; text-align:center;'>
                                                This invite expires in 7 days. If the button does not work, paste this link into your browser:<br/>
                                                <span style='word-break:break-all; color:#0c66e4;'>{safeAcceptUrl}</span>
                                            </p>
                                        </td>
                                    </tr>
                                    <tr>
                                        <td style='padding:22px 40px 34px; border-top:1px solid #dfe1e6; text-align:center; color:#8590a2; font-size:12px;'>
                                            This message was sent by {safeOrganizationName}.<br/>
                                            SprintA Project Management
                                        </td>
                                    </tr>
                                </table>
                            </td>
                        </tr>
                    </table>
                </div>";

            await SendResendEmailAsync(toEmail, subject, html, null);
        }

        public async Task SendPasswordChangeRequestEmailAsync(
            string toEmail,
            string requesterName,
            string requesterEmail,
            DateTime? lastChangedAt,
            DateTime eligibleAt)
        {
            var safeRequesterName = WebUtility.HtmlEncode(string.IsNullOrWhiteSpace(requesterName) ? requesterEmail : requesterName);
            var safeRequesterEmail = WebUtility.HtmlEncode(requesterEmail);
            var safeLastChangedAt = WebUtility.HtmlEncode(lastChangedAt?.ToString("yyyy-MM-dd HH:mm 'UTC'") ?? "No previous password change recorded");
            var safeEligibleAt = WebUtility.HtmlEncode(eligibleAt.ToString("yyyy-MM-dd HH:mm 'UTC'"));

            var subject = $"Password change exception requested by {requesterEmail}";
            var html = $@"
                <div style='font-family: Arial, sans-serif; max-width: 560px; margin: 0 auto; padding: 32px; color: #172b4d;'>
                    <h2 style='margin:0 0 16px;'>Password change exception request</h2>
                    <p style='line-height:1.55;'>A user requested admin help to change their password before the 7-day cooldown is over.</p>
                    <table role='presentation' cellpadding='0' cellspacing='0' style='width:100%; margin:20px 0; border-collapse:collapse;'>
                        <tr>
                            <td style='padding:10px 12px; border:1px solid #dfe1e6; font-weight:700;'>User</td>
                            <td style='padding:10px 12px; border:1px solid #dfe1e6;'>{safeRequesterName}</td>
                        </tr>
                        <tr>
                            <td style='padding:10px 12px; border:1px solid #dfe1e6; font-weight:700;'>Email</td>
                            <td style='padding:10px 12px; border:1px solid #dfe1e6;'>{safeRequesterEmail}</td>
                        </tr>
                        <tr>
                            <td style='padding:10px 12px; border:1px solid #dfe1e6; font-weight:700;'>Last changed</td>
                            <td style='padding:10px 12px; border:1px solid #dfe1e6;'>{safeLastChangedAt}</td>
                        </tr>
                        <tr>
                            <td style='padding:10px 12px; border:1px solid #dfe1e6; font-weight:700;'>Eligible again</td>
                            <td style='padding:10px 12px; border:1px solid #dfe1e6;'>{safeEligibleAt}</td>
                        </tr>
                    </table>
                    <p style='color:#626f86; font-size:13px;'>Please review this request in SprintA and decide whether manual admin support is needed.</p>
                </div>";

            await SendResendEmailAsync(toEmail, subject, html, null);
        }

        private async Task SendResendEmailAsync(string toEmail, string subject, string html, string? text = null, System.Threading.CancellationToken cancellationToken = default)
        {
            var apiKey = _configuration["Resend:ApiKey"]?.Trim();
            if (string.IsNullOrWhiteSpace(apiKey))
                throw new InvalidOperationException("Resend API key is missing.");
            var fromEmail = _configuration["Resend:FromEmail"]?.Trim();
            if (string.IsNullOrWhiteSpace(fromEmail))
                throw new InvalidOperationException("Resend:FromEmail is not configured.");
            var replyTo = _configuration["Resend:ReplyTo"]?.Trim();
            var fromDisplay = $"SprintA <{fromEmail}>";

            var requestBody = new
            {
                from = fromDisplay,
                reply_to = string.IsNullOrWhiteSpace(replyTo) ? null : replyTo,
                to = new[] { toEmail },
                subject,
                html,
                text
            };

            var json = JsonSerializer.Serialize(requestBody);
            var content = new StringContent(json, Encoding.UTF8, "application/json");

            _httpClient.DefaultRequestHeaders.Clear();
            _httpClient.DefaultRequestHeaders.Add("Authorization", $"Bearer {apiKey}");

            var response = await _httpClient.PostAsync("https://api.resend.com/emails", content, cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                throw new InvalidOperationException($"Resend returned HTTP {(int)response.StatusCode}.");
            }
        }

        private static string PaymentHtml(string title, string summary, string details, string link, string linkText) => $@"
            <div style='font-family:Arial,sans-serif;max-width:560px;margin:0 auto;padding:32px;color:#172b4d'>
              <div style='font-size:22px;font-weight:700;color:#0c66e4;margin-bottom:24px'>SprintA</div>
              <h1 style='font-size:24px;margin:0 0 16px'>{title}</h1>
              <p style='line-height:1.6'>{summary}</p>
              <p style='line-height:1.7'>{details}</p>
              <a href='{WebUtility.HtmlEncode(link)}' style='display:inline-block;background:#0c66e4;color:#fff;text-decoration:none;border-radius:6px;padding:12px 18px;font-weight:700'>{linkText}</a>
            </div>";
    }
}
