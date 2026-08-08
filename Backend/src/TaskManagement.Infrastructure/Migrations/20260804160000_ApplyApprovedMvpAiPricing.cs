using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using TaskManagement.Infrastructure.Data;

#nullable disable

namespace TaskManagement.Infrastructure.Migrations;

[DbContext(typeof(ApplicationDbContext))]
[Migration("20260804160000_ApplyApprovedMvpAiPricing")]
public partial class ApplyApprovedMvpAiPricing : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<string>(
            name: "Audience",
            table: "AiPricingPlans",
            type: "nvarchar(32)",
            maxLength: 32,
            nullable: false,
            defaultValue: "Personal");

        migrationBuilder.AddColumn<bool>(
            name: "IsRecommended",
            table: "AiPricingPlans",
            type: "bit",
            nullable: false,
            defaultValue: false);

        migrationBuilder.AddColumn<bool>(
            name: "IsPublished",
            table: "AiPricingPlans",
            type: "bit",
            nullable: false,
            defaultValue: true);

        migrationBuilder.Sql(
            """
            UPDATE [AiPricingPlans]
            SET [Name] = N'Free', [MonthlyPriceVnd] = 0, [IncludedAiCredits] = 100,
                [Audience] = N'Personal', [IsRecommended] = 0, [IsPublished] = 1,
                [PricingStatus] = N'Published'
            WHERE [Code] = N'free';

            UPDATE [AiPricingPlans]
            SET [Name] = N'Team', [MonthlyPriceVnd] = 499000, [IncludedAiCredits] = 9000,
                [Audience] = N'Team', [IsRecommended] = 1, [IsPublished] = 1,
                [PricingStatus] = N'Published'
            WHERE [Code] = N'team';

            UPDATE [AiPricingPlans]
            SET [Audience] = N'Legacy', [IsRecommended] = 0, [IsPublished] = 0
            WHERE [Code] = N'business';

            IF NOT EXISTS (SELECT 1 FROM [AiPricingPlans] WHERE [Code] = N'starter')
            INSERT INTO [AiPricingPlans]
                ([Id], [Code], [Name], [MonthlyPriceVnd], [PerUser], [IncludedUsers], [IncludedAiCredits], [ExtraAiCreditsEnabled], [Audience], [IsRecommended], [IsPublished], [PricingStatus], [FeaturesJson], [CreatedAt], [UpdatedAt])
            VALUES
                ('f1000000-0000-0000-0000-000000000004', N'starter', N'Starter', 49000, 0, NULL, 500, 0, N'Personal', 0, 1, N'Published', N'["500 AI credits"]', SYSUTCDATETIME(), SYSUTCDATETIME());

            IF NOT EXISTS (SELECT 1 FROM [AiPricingPlans] WHERE [Code] = N'plus')
            INSERT INTO [AiPricingPlans]
                ([Id], [Code], [Name], [MonthlyPriceVnd], [PerUser], [IncludedUsers], [IncludedAiCredits], [ExtraAiCreditsEnabled], [Audience], [IsRecommended], [IsPublished], [PricingStatus], [FeaturesJson], [CreatedAt], [UpdatedAt])
            VALUES
                ('f1000000-0000-0000-0000-000000000005', N'plus', N'Plus', 99000, 0, NULL, 1200, 0, N'Personal', 1, 1, N'Published', N'["1,200 AI credits"]', SYSUTCDATETIME(), SYSUTCDATETIME());

            IF NOT EXISTS (SELECT 1 FROM [AiPricingPlans] WHERE [Code] = N'pro')
            INSERT INTO [AiPricingPlans]
                ([Id], [Code], [Name], [MonthlyPriceVnd], [PerUser], [IncludedUsers], [IncludedAiCredits], [ExtraAiCreditsEnabled], [Audience], [IsRecommended], [IsPublished], [PricingStatus], [FeaturesJson], [CreatedAt], [UpdatedAt])
            VALUES
                ('f1000000-0000-0000-0000-000000000006', N'pro', N'Pro', 199000, 0, NULL, 3000, 0, N'Personal', 0, 1, N'Published', N'["3,000 AI credits"]', SYSUTCDATETIME(), SYSUTCDATETIME());

            IF NOT EXISTS (SELECT 1 FROM [AiPricingPlans] WHERE [Code] = N'enterprise')
            INSERT INTO [AiPricingPlans]
                ([Id], [Code], [Name], [MonthlyPriceVnd], [PerUser], [IncludedUsers], [IncludedAiCredits], [ExtraAiCreditsEnabled], [Audience], [IsRecommended], [IsPublished], [PricingStatus], [FeaturesJson], [CreatedAt], [UpdatedAt])
            VALUES
                ('f1000000-0000-0000-0000-000000000007', N'enterprise', N'Enterprise', NULL, 0, NULL, 0, 0, N'Team', 0, 1, N'Contact', N'["Credit by agreement"]', SYSUTCDATETIME(), SYSUTCDATETIME());
            """);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql(
            """
            DELETE FROM [AiPricingPlans]
            WHERE [Code] IN (N'starter', N'plus', N'pro', N'enterprise');
            """);

        migrationBuilder.DropColumn(name: "Audience", table: "AiPricingPlans");
        migrationBuilder.DropColumn(name: "IsRecommended", table: "AiPricingPlans");
        migrationBuilder.DropColumn(name: "IsPublished", table: "AiPricingPlans");
    }
}
