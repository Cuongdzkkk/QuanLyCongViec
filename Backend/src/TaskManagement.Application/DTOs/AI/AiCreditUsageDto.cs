namespace TaskManagement.Application.DTOs.AI;

public sealed class AiCreditUsageDto
{
    public string PlanCode { get; set; } = string.Empty;
    public string EntitlementSource { get; set; } = string.Empty;
    public string UsageSource { get; set; } = string.Empty;
    public int IncludedCredits { get; set; }
    public int UsedCredits { get; set; }
    public int AdjustmentCredits { get; set; }
    public int RemainingCredits => Math.Max(0, IncludedCredits + AdjustmentCredits - UsedCredits);
    public bool IsQuotaExceeded
    {
        get
        {
            var entitlement = IncludedCredits + AdjustmentCredits;
            return entitlement < 0 || (entitlement == 0 && UsedCredits > 0) || (entitlement > 0 && UsedCredits >= entitlement);
        }
    }
    public bool HasConfiguredEntitlement { get; set; }
    public long TotalTokens { get; set; }
    public DateTime CurrentPeriodStart { get; set; }
    public DateTime CurrentPeriodEnd { get; set; }
    public string SubscriptionStatus { get; set; } = "Active";
}
