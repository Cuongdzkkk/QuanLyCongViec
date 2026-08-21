using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TaskManagement.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class PaymentP0Foundation : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_PaymentOrders_UserId",
                table: "PaymentOrders");

            migrationBuilder.AddColumn<string>(
                name: "Currency",
                table: "PaymentOrders",
                type: "nvarchar(8)",
                maxLength: 8,
                nullable: false,
                defaultValue: "VND");

            migrationBuilder.AddColumn<DateTime>(
                name: "ExpiresAt",
                table: "PaymentOrders",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "IncludedAiCreditsSnapshot",
                table: "PaymentOrders",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "PlanNameSnapshot",
                table: "PaymentOrders",
                type: "nvarchar(128)",
                maxLength: 128,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "Provider",
                table: "PaymentOrders",
                type: "nvarchar(64)",
                maxLength: 64,
                nullable: false,
                defaultValue: "manual_bank_transfer");

            migrationBuilder.Sql(@"
UPDATE po
SET po.Currency = CASE WHEN NULLIF(po.Currency, N'') IS NULL THEN N'VND' ELSE po.Currency END,
    po.Provider = CASE WHEN NULLIF(po.Provider, N'') IS NULL THEN N'manual_bank_transfer' ELSE po.Provider END,
    po.PlanNameSnapshot = CASE WHEN NULLIF(po.PlanNameSnapshot, N'') IS NULL THEN COALESCE(pricing.Name, po.PlanCode) ELSE po.PlanNameSnapshot END,
    po.IncludedAiCreditsSnapshot = CASE WHEN po.IncludedAiCreditsSnapshot = 0 THEN COALESCE(pricing.IncludedAiCredits, 0) ELSE po.IncludedAiCreditsSnapshot END,
    po.ExpiresAt = CASE WHEN po.ExpiresAt IS NULL AND po.Status = N'Pending' THEN DATEADD(minute, 30, po.CreatedAt) ELSE po.ExpiresAt END
FROM PaymentOrders po
LEFT JOIN AiPricingPlans pricing ON pricing.Code = po.PlanCode;");

            migrationBuilder.CreateTable(
                name: "AiCreditReservations",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    UserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Credits = table.Column<int>(type: "int", nullable: false),
                    IdempotencyKey = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Status = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    ExpiresAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CompletedAt = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AiCreditReservations", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AiCreditReservations_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "PaymentTransactions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    PaymentOrderId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Provider = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    ProviderTransactionId = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                    Amount = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    Currency = table.Column<string>(type: "nvarchar(8)", maxLength: 8, nullable: false),
                    Status = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    PaidAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ProviderReference = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PaymentTransactions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PaymentTransactions_PaymentOrders_PaymentOrderId",
                        column: x => x.PaymentOrderId,
                        principalTable: "PaymentOrders",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "PaymentWebhookEvents",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Provider = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    ProviderEventId = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                    EventType = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    RawPayload = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Status = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    ReceivedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ProcessedAt = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PaymentWebhookEvents", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_PaymentOrders_UserId_PlanCode_Status",
                table: "PaymentOrders",
                columns: new[] { "UserId", "PlanCode", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_AiCreditReservations_IdempotencyKey",
                table: "AiCreditReservations",
                column: "IdempotencyKey",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_AiCreditReservations_UserId_Status_ExpiresAt",
                table: "AiCreditReservations",
                columns: new[] { "UserId", "Status", "ExpiresAt" });

            migrationBuilder.CreateIndex(
                name: "IX_PaymentTransactions_PaymentOrderId",
                table: "PaymentTransactions",
                column: "PaymentOrderId");

            migrationBuilder.CreateIndex(
                name: "IX_PaymentTransactions_Provider_ProviderTransactionId",
                table: "PaymentTransactions",
                columns: new[] { "Provider", "ProviderTransactionId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_PaymentWebhookEvents_Provider_ProviderEventId",
                table: "PaymentWebhookEvents",
                columns: new[] { "Provider", "ProviderEventId" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AiCreditReservations");

            migrationBuilder.DropTable(
                name: "PaymentTransactions");

            migrationBuilder.DropTable(
                name: "PaymentWebhookEvents");

            migrationBuilder.DropIndex(
                name: "IX_PaymentOrders_UserId_PlanCode_Status",
                table: "PaymentOrders");

            migrationBuilder.DropColumn(
                name: "Currency",
                table: "PaymentOrders");

            migrationBuilder.DropColumn(
                name: "ExpiresAt",
                table: "PaymentOrders");

            migrationBuilder.DropColumn(
                name: "IncludedAiCreditsSnapshot",
                table: "PaymentOrders");

            migrationBuilder.DropColumn(
                name: "PlanNameSnapshot",
                table: "PaymentOrders");

            migrationBuilder.DropColumn(
                name: "Provider",
                table: "PaymentOrders");

            migrationBuilder.CreateIndex(
                name: "IX_PaymentOrders_UserId",
                table: "PaymentOrders",
                column: "UserId");
        }
    }
}
