namespace TaskManagement.Domain.Entities;

/// <summary>Configurable plan metadata. Monetary values stay null until Product Owner approval.</summary>
public class AiPricingPlan
{
    public Guid Id { get; set; }
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public decimal? MonthlyPriceVnd { get; set; }
    public bool PerUser { get; set; }
    public int? IncludedUsers { get; set; }
    public int IncludedAiCredits { get; set; }
    public bool ExtraAiCreditsEnabled { get; set; }
    public string Audience { get; set; } = "Personal";
    public bool IsRecommended { get; set; }
    public bool IsPublished { get; set; } = true;
    public string PricingStatus { get; set; } = "PendingConfirmation";
    public string? FeaturesJson { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}
