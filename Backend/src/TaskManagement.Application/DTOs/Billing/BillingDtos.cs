namespace TaskManagement.Application.DTOs.Billing;

public class BillingSummaryDto
{
    public string PlanCode { get; set; } = "free";
    public string PlanName { get; set; } = "Free";
    public string SubscriptionStatus { get; set; } = "Active";
    public int IncludedCredits { get; set; }
    public int AdjustmentCredits { get; set; }
    public int UsedCredits { get; set; }
    public int RemainingCredits { get; set; }
    public DateTime CurrentPeriodStart { get; set; }
    public DateTime CurrentPeriodEnd { get; set; }
    public PaymentOrderDto? PendingOrder { get; set; }
}

public sealed class BillingUserDto : BillingSummaryDto
{
    public Guid UserId { get; set; }
    public string UserName { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
}

public sealed class PaymentOrderDto
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    public string UserName { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string PlanCode { get; set; } = string.Empty;
    public string PlanName { get; set; } = string.Empty;
    public decimal AmountVnd { get; set; }
    public string Status { get; set; } = string.Empty;
    public string TransferCode { get; set; } = string.Empty;
    public string Currency { get; set; } = "VND";
    public string Provider { get; set; } = string.Empty;
    public DateTime? ExpiresAt { get; set; }
    public PaymentInstructionsDto? PaymentInstructions { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? PaidAt { get; set; }
    public Guid? ApprovedByUserId { get; set; }
    public string? AdminNote { get; set; }
    public string? ProviderTransactionId { get; set; }
    public string? ProviderReference { get; set; }
    public string? TransactionStatus { get; set; }
    public DateTime? TransactionCreatedAt { get; set; }
    public DateTime? TransactionPeriodStart { get; set; }
    public DateTime? TransactionPeriodEnd { get; set; }
    public string? WebhookStatus { get; set; }
    public DateTime? WebhookReceivedAt { get; set; }
    public DateTime? WebhookProcessedAt { get; set; }
    public bool HasFulfillmentMismatch { get; set; }
}

public sealed class BillingOrderQuery
{
    public string? Search { get; set; }
    public string? Status { get; set; }
    public string? PlanCode { get; set; }
    public string? Provider { get; set; }
    public DateTime? From { get; set; }
    public DateTime? To { get; set; }
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 25;
}

public sealed class PaymentOrderPageDto
{
    public IReadOnlyList<PaymentOrderDto> Items { get; set; } = Array.Empty<PaymentOrderDto>();
    public int Page { get; set; }
    public int PageSize { get; set; }
    public int TotalCount { get; set; }
    public PaymentOrderAggregateDto Summary { get; set; } = new();
}

public sealed class PaymentOrderAggregateDto
{
    public int TotalCount { get; set; }
    public decimal RevenueVnd { get; set; }
    public int SuccessfulPayments { get; set; }
    public int PendingPayments { get; set; }
    public int NeedsAttention { get; set; }
    public int FailedPayments { get; set; }
}

public sealed class PaymentTimelineEventDto
{
    public string Type { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public DateTime? OccurredAt { get; set; }
    public string? Reference { get; set; }
    public string? Note { get; set; }
}

public sealed class PaymentCreditLedgerEntryDto
{
    public string Source { get; set; } = string.Empty;
    public int Amount { get; set; }
    public DateTime OccurredAt { get; set; }
    public string? Description { get; set; }
}

public sealed class PaymentOrderDetailsDto
{
    public PaymentOrderDto Order { get; set; } = new();
    public IReadOnlyList<PaymentTimelineEventDto> Timeline { get; set; } = Array.Empty<PaymentTimelineEventDto>();
    public IReadOnlyList<PaymentCreditLedgerEntryDto> CreditLedger { get; set; } = Array.Empty<PaymentCreditLedgerEntryDto>();
    public bool ObservabilityAvailable { get; set; }
}

public sealed class PaymentReceiptDto
{
    public string ReceiptNumber { get; set; } = string.Empty;
    public PaymentOrderDto Order { get; set; } = new();
    public string CustomerName { get; set; } = string.Empty;
    public string CustomerEmail { get; set; } = string.Empty;
    public int? IncludedAiCredits { get; set; }
    public DateTime? SubscriptionPeriodStart { get; set; }
    public DateTime? SubscriptionPeriodEnd { get; set; }
    public bool IsTaxInvoice { get; set; }
}

public sealed class PaymentEmailDeliveryDto
{
    public Guid Id { get; set; }
    public string RecipientEmail { get; set; } = string.Empty;
    public string Kind { get; set; } = string.Empty;
    public int Attempt { get; set; }
    public string Status { get; set; } = string.Empty;
    public DateTime RequestedAt { get; set; }
    public DateTime? SentAt { get; set; }
    public DateTime? FailedAt { get; set; }
    public string? FailureReason { get; set; }
}

public sealed class PaymentInstructionsDto
{
    public string Provider { get; set; } = string.Empty;
    public string BankCode { get; set; } = string.Empty;
    public string AccountName { get; set; } = string.Empty;
    public string AccountNumber { get; set; } = string.Empty;
    public decimal AmountVnd { get; set; }
    public string TransferContent { get; set; } = string.Empty;
    public string? QrUrl { get; set; }
}

public sealed class CreatePaymentOrderRequest
{
    public string PlanCode { get; set; } = string.Empty;
}

public sealed class ChangeSubscriptionPlanRequest
{
    public string PlanCode { get; set; } = string.Empty;
    public bool AutoRenew { get; set; }
    public string? Reason { get; set; }
}

public class AdminReasonRequest
{
    public string Reason { get; set; } = string.Empty;
}

public sealed class CreditAdjustmentRequest : AdminReasonRequest
{
    public int Amount { get; set; }
}

public sealed class UpdatePricingPlanRequest
{
    public decimal MonthlyPriceVnd { get; set; }
    public int IncludedAiCredits { get; set; }
    public bool IsPublished { get; set; }
    public bool IsRecommended { get; set; }
}
