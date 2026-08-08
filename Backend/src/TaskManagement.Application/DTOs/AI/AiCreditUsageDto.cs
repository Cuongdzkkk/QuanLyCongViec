namespace TaskManagement.Application.DTOs.AI;

public sealed class AiCreditUsageDto
{
    public string PlanCode { get; set; } = string.Empty;
    public string EntitlementSource { get; set; } = string.Empty;
    public string UsageSource { get; set; } = string.Empty;
    public int IncludedCredits { get; set; }
    public int UsedCredits { get; set; }
    public int RemainingCredits => Math.Max(0, IncludedCredits - UsedCredits);
    public bool IsQuotaExceeded => IncludedCredits > 0 && UsedCredits >= IncludedCredits;
    public bool HasConfiguredEntitlement { get; set; }
    public long TotalTokens { get; set; }
}
