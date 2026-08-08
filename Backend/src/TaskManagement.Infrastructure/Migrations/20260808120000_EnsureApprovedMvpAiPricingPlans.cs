using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using TaskManagement.Infrastructure.Data;

#nullable disable

namespace TaskManagement.Infrastructure.Migrations;

[DbContext(typeof(ApplicationDbContext))]
[Migration("20260808120000_EnsureApprovedMvpAiPricingPlans")]
public partial class EnsureApprovedMvpAiPricingPlans : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql(
            """
            IF NOT EXISTS (SELECT 1 FROM [AiPricingPlans] WHERE [Code] = N'free')
            INSERT INTO [AiPricingPlans]
                ([Id], [Code], [Name], [MonthlyPriceVnd], [PerUser], [IncludedUsers], [IncludedAiCredits], [ExtraAiCreditsEnabled], [Audience], [IsRecommended], [IsPublished], [PricingStatus], [FeaturesJson], [CreatedAt], [UpdatedAt])
            VALUES
                ('f1000000-0000-0000-0000-000000000001', N'free', N'Free', 0, 0, NULL, 100, 0, N'Personal', 0, 1, N'Published', N'["100 AI credits"]', SYSUTCDATETIME(), SYSUTCDATETIME());

            IF NOT EXISTS (SELECT 1 FROM [AiPricingPlans] WHERE [Code] = N'team')
            INSERT INTO [AiPricingPlans]
                ([Id], [Code], [Name], [MonthlyPriceVnd], [PerUser], [IncludedUsers], [IncludedAiCredits], [ExtraAiCreditsEnabled], [Audience], [IsRecommended], [IsPublished], [PricingStatus], [FeaturesJson], [CreatedAt], [UpdatedAt])
            VALUES
                ('f1000000-0000-0000-0000-000000000003', N'team', N'Team', 499000, 0, NULL, 9000, 0, N'Team', 0, 1, N'Published', N'["9,000 AI credits"]', SYSUTCDATETIME(), SYSUTCDATETIME());
            """);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
    }
}
