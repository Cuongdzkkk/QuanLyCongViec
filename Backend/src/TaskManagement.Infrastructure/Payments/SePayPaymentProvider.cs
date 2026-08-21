using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Configuration;
using TaskManagement.Application.DTOs.Billing;
using TaskManagement.Application.Interfaces;
using TaskManagement.Domain.Entities;

namespace TaskManagement.Infrastructure.Payments;

public sealed class SePayPaymentProvider : IPaymentProvider
{
    private readonly IConfiguration _configuration;
    public SePayPaymentProvider(IConfiguration configuration) => _configuration = configuration;
    public string Code => "sepay";
    public bool IsConfigured => bool.TryParse(_configuration["PaymentProviders:SePay:Enabled"], out var enabled) && enabled
        && !string.IsNullOrWhiteSpace(_configuration["PaymentProviders:SePay:WebhookSecret"])
        && !string.IsNullOrWhiteSpace(_configuration["PaymentProviders:SePay:BankCode"])
        && !string.IsNullOrWhiteSpace(_configuration["PaymentProviders:SePay:AccountNumber"]);

    public PaymentInstructionsDto BuildInstructions(PaymentOrder order)
    {
        var bank = _configuration["PaymentProviders:SePay:BankCode"] ?? string.Empty;
        var account = _configuration["PaymentProviders:SePay:AccountNumber"] ?? string.Empty;
        var name = _configuration["PaymentProviders:SePay:AccountName"] ?? string.Empty;
        var content = order.TransferCode;
        var qrUrl = string.IsNullOrWhiteSpace(bank) || string.IsNullOrWhiteSpace(account)
            ? null
            : $"https://vietqr.app/img?acc={Uri.EscapeDataString(account)}&bank={Uri.EscapeDataString(bank)}&amount={order.AmountVnd:0}&des={Uri.EscapeDataString(content)}&template=compact";
        return new PaymentInstructionsDto
        {
            Provider = Code, BankCode = bank, AccountName = name, AccountNumber = account,
            AmountVnd = order.AmountVnd, TransferContent = content, QrUrl = qrUrl
        };
    }

    public Task<PaymentWebhookVerificationResult> VerifyWebhookAsync(string rawBody, string? signature, string? timestamp, CancellationToken cancellationToken = default)
    {
        var secret = _configuration["PaymentProviders:SePay:WebhookSecret"];
        if (string.IsNullOrWhiteSpace(secret) || string.IsNullOrWhiteSpace(signature) || !long.TryParse(timestamp, out var unixTimestamp))
            return Task.FromResult(new PaymentWebhookVerificationResult { Error = "Webhook authentication is not configured." });
        var now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        if (Math.Abs(now - unixTimestamp) > 300)
            return Task.FromResult(new PaymentWebhookVerificationResult { Error = "Webhook timestamp is outside the replay window." });
        var signed = $"{timestamp}.{rawBody}";
        var hash = HMACSHA256.HashData(Encoding.UTF8.GetBytes(secret), Encoding.UTF8.GetBytes(signed));
        var expected = "sha256=" + Convert.ToHexString(hash).ToLowerInvariant();
        if (!CryptographicOperations.FixedTimeEquals(Encoding.UTF8.GetBytes(expected), Encoding.UTF8.GetBytes(signature.Trim())))
            return Task.FromResult(new PaymentWebhookVerificationResult { Error = "Invalid webhook signature." });
        try
        {
            using var json = JsonDocument.Parse(rawBody);
            var root = json.RootElement;
            var amount = root.TryGetProperty("transferAmount", out var amountElement) ? amountElement.GetDecimal() : 0;
            var accountNumber = ReadString(root, "accountNumber");
            var configuredAccount = _configuration["PaymentProviders:SePay:AccountNumber"];
            if (!string.IsNullOrWhiteSpace(configuredAccount) && !string.Equals(accountNumber, configuredAccount, StringComparison.OrdinalIgnoreCase))
                return Task.FromResult(new PaymentWebhookVerificationResult { Error = "Webhook destination account does not match configuration." });
            var transactionAt = root.TryGetProperty("transactionDate", out var dateElement) && DateTime.TryParse(dateElement.GetString(), CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out var parsed)
                ? parsed.ToUniversalTime() : (DateTime?)null;
            return Task.FromResult(new PaymentWebhookVerificationResult
            {
                IsValid = true,
                ProviderEventId = ReadString(root, "id"),
                TransactionType = ReadString(root, "transferType"),
                AccountNumber = accountNumber,
                Amount = amount,
                TransferContent = ReadString(root, "content"),
                ProviderReference = ReadString(root, "referenceCode") ?? ReadString(root, "reference"),
                TransactionAt = transactionAt
            });
        }
        catch (JsonException)
        {
            return Task.FromResult(new PaymentWebhookVerificationResult { Error = "Invalid webhook JSON." });
        }
    }

    private static string ReadString(JsonElement root, string name) =>
        root.TryGetProperty(name, out var value) ? value.ToString() : string.Empty;
}
