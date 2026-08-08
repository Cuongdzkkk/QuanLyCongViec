using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using TaskManagement.Infrastructure.Data;

#nullable disable

namespace TaskManagement.Infrastructure.Migrations;

[DbContext(typeof(ApplicationDbContext))]
[Migration("20260804140000_SeedAiCreditSourceOfTruth")]
public partial class SeedAiCreditSourceOfTruth : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql(
            """
            IF NOT EXISTS (SELECT 1 FROM [AiPricingPlans] WHERE [Code] = N'free')
            INSERT INTO [AiPricingPlans]
                ([Id], [Code], [Name], [MonthlyPriceVnd], [PerUser], [IncludedUsers], [IncludedAiCredits], [ExtraAiCreditsEnabled], [PricingStatus], [FeaturesJson], [CreatedAt], [UpdatedAt])
            VALUES
                ('f1000000-0000-0000-0000-000000000001', N'free', N'Free', NULL, 0, 3, 100, 0, N'PendingConfirmation', NULL, SYSUTCDATETIME(), SYSUTCDATETIME());

            IF NOT EXISTS (SELECT 1 FROM [AiPricingPlans] WHERE [Code] = N'team')
            INSERT INTO [AiPricingPlans]
                ([Id], [Code], [Name], [MonthlyPriceVnd], [PerUser], [IncludedUsers], [IncludedAiCredits], [ExtraAiCreditsEnabled], [PricingStatus], [FeaturesJson], [CreatedAt], [UpdatedAt])
            VALUES
                ('f1000000-0000-0000-0000-000000000002', N'team', N'Team', NULL, 0, NULL, 0, 0, N'PendingConfirmation', NULL, SYSUTCDATETIME(), SYSUTCDATETIME());

            IF NOT EXISTS (SELECT 1 FROM [AiPricingPlans] WHERE [Code] = N'business')
            INSERT INTO [AiPricingPlans]
                ([Id], [Code], [Name], [MonthlyPriceVnd], [PerUser], [IncludedUsers], [IncludedAiCredits], [ExtraAiCreditsEnabled], [PricingStatus], [FeaturesJson], [CreatedAt], [UpdatedAt])
            VALUES
                ('f1000000-0000-0000-0000-000000000003', N'business', N'Business', NULL, 0, NULL, 0, 0, N'PendingConfirmation', NULL, SYSUTCDATETIME(), SYSUTCDATETIME());

            DECLARE @Disclaimer nvarchar(max) = N'Mức sử dụng là ước tính và có thể thay đổi theo độ dài nội dung.';

            IF NOT EXISTS (SELECT 1 FROM [AiCreditRules] WHERE [ActionType] = N'summarize_project')
            INSERT INTO [AiCreditRules] ([Id], [ActionType], [EstimatedCredits], [IsActive], [Disclaimer], [CreatedAt], [UpdatedAt])
            VALUES ('c1000000-0000-0000-0000-000000000001', N'summarize_project', 1, 1, @Disclaimer, SYSUTCDATETIME(), SYSUTCDATETIME());

            IF NOT EXISTS (SELECT 1 FROM [AiCreditRules] WHERE [ActionType] = N'create_project')
            INSERT INTO [AiCreditRules] ([Id], [ActionType], [EstimatedCredits], [IsActive], [Disclaimer], [CreatedAt], [UpdatedAt])
            VALUES ('c1000000-0000-0000-0000-000000000002', N'create_project', 1, 1, @Disclaimer, SYSUTCDATETIME(), SYSUTCDATETIME());

            IF NOT EXISTS (SELECT 1 FROM [AiCreditRules] WHERE [ActionType] = N'create_task')
            INSERT INTO [AiCreditRules] ([Id], [ActionType], [EstimatedCredits], [IsActive], [Disclaimer], [CreatedAt], [UpdatedAt])
            VALUES ('c1000000-0000-0000-0000-000000000003', N'create_task', 1, 1, @Disclaimer, SYSUTCDATETIME(), SYSUTCDATETIME());

            IF NOT EXISTS (SELECT 1 FROM [AiCreditRules] WHERE [ActionType] = N'create_cycle')
            INSERT INTO [AiCreditRules] ([Id], [ActionType], [EstimatedCredits], [IsActive], [Disclaimer], [CreatedAt], [UpdatedAt])
            VALUES ('c1000000-0000-0000-0000-000000000004', N'create_cycle', 1, 1, @Disclaimer, SYSUTCDATETIME(), SYSUTCDATETIME());

            IF NOT EXISTS (SELECT 1 FROM [AiCreditRules] WHERE [ActionType] = N'create_goal')
            INSERT INTO [AiCreditRules] ([Id], [ActionType], [EstimatedCredits], [IsActive], [Disclaimer], [CreatedAt], [UpdatedAt])
            VALUES ('c1000000-0000-0000-0000-000000000005', N'create_goal', 1, 1, @Disclaimer, SYSUTCDATETIME(), SYSUTCDATETIME());

            IF NOT EXISTS (SELECT 1 FROM [AiCreditRules] WHERE [ActionType] = N'list_overdue_tasks')
            INSERT INTO [AiCreditRules] ([Id], [ActionType], [EstimatedCredits], [IsActive], [Disclaimer], [CreatedAt], [UpdatedAt])
            VALUES ('c1000000-0000-0000-0000-000000000006', N'list_overdue_tasks', 1, 1, @Disclaimer, SYSUTCDATETIME(), SYSUTCDATETIME());
            """);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql(
            """
            DELETE FROM [AiCreditRules]
            WHERE [Id] IN (
                'c1000000-0000-0000-0000-000000000001',
                'c1000000-0000-0000-0000-000000000002',
                'c1000000-0000-0000-0000-000000000003',
                'c1000000-0000-0000-0000-000000000004',
                'c1000000-0000-0000-0000-000000000005',
                'c1000000-0000-0000-0000-000000000006');

            DELETE FROM [AiPricingPlans]
            WHERE [Id] IN (
                'f1000000-0000-0000-0000-000000000001',
                'f1000000-0000-0000-0000-000000000002',
                'f1000000-0000-0000-0000-000000000003');
            """);
    }
}
