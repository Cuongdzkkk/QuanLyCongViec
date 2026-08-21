using TaskManagement.Application.DTOs.Billing;
using TaskManagement.Domain.Entities;

namespace TaskManagement.Application.Interfaces;

public interface IPaymentProvider
{
    string Code { get; }
    bool IsConfigured { get; }
    PaymentInstructionsDto BuildInstructions(PaymentOrder order);
    Task<PaymentWebhookVerificationResult> VerifyWebhookAsync(string rawBody, string? signature, string? timestamp, CancellationToken cancellationToken = default);
}

public sealed class PaymentWebhookVerificationResult
{
    public bool IsValid { get; init; }
    public string Error { get; init; } = string.Empty;
    public string ProviderEventId { get; init; } = string.Empty;
    public string EventType { get; init; } = "payment.received";
    public string? TransactionType { get; init; }
    public string? AccountNumber { get; init; }
    public decimal Amount { get; init; }
    public string TransferContent { get; init; } = string.Empty;
    public string? ProviderReference { get; init; }
    public DateTime? TransactionAt { get; init; }
}
