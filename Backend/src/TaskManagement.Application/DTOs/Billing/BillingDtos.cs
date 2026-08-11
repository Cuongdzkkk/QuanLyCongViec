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
    public DateTime CreatedAt { get; set; }
    public DateTime? PaidAt { get; set; }
    public Guid? ApprovedByUserId { get; set; }
    public string? AdminNote { get; set; }
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
