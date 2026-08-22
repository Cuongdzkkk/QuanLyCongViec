using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TaskManagement.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class BillingSupport : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "FailureReason",
                table: "PaymentWebhookEvents",
                type: "nvarchar(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "PaymentOrderId",
                table: "PaymentWebhookEvents",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "IncludedAiCredits",
                table: "PaymentTransactions",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "SubscriptionPeriodEnd",
                table: "PaymentTransactions",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "SubscriptionPeriodStart",
                table: "PaymentTransactions",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "DedupeKey",
                table: "Notifications",
                type: "nvarchar(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.CreateTable(
                name: "PaymentEmailDeliveries",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    PaymentOrderId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    UserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    RecipientEmail = table.Column<string>(type: "nvarchar(320)", maxLength: 320, nullable: false),
                    Kind = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    IsAutomatic = table.Column<bool>(type: "bit", nullable: false),
                    Attempt = table.Column<int>(type: "int", nullable: false),
                    Status = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    ProviderMessageId = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: true),
                    FailureReason = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    RequestedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    SentAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    FailedAt = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PaymentEmailDeliveries", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PaymentEmailDeliveries_PaymentOrders_PaymentOrderId",
                        column: x => x.PaymentOrderId,
                        principalTable: "PaymentOrders",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_PaymentEmailDeliveries_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_PaymentWebhookEvents_PaymentOrderId",
                table: "PaymentWebhookEvents",
                column: "PaymentOrderId");

            migrationBuilder.CreateIndex(
                name: "IX_Notifications_DedupeKey",
                table: "Notifications",
                column: "DedupeKey",
                unique: true,
                filter: "[DedupeKey] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_PaymentEmailDeliveries_PaymentOrderId_Kind_Attempt",
                table: "PaymentEmailDeliveries",
                columns: new[] { "PaymentOrderId", "Kind", "Attempt" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_PaymentEmailDeliveries_PaymentOrderId_Kind_IsAutomatic",
                table: "PaymentEmailDeliveries",
                columns: new[] { "PaymentOrderId", "Kind", "IsAutomatic" },
                unique: true,
                filter: "[IsAutomatic] = 1");

            migrationBuilder.CreateIndex(
                name: "IX_PaymentEmailDeliveries_UserId",
                table: "PaymentEmailDeliveries",
                column: "UserId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "PaymentEmailDeliveries");

            migrationBuilder.DropIndex(
                name: "IX_PaymentWebhookEvents_PaymentOrderId",
                table: "PaymentWebhookEvents");

            migrationBuilder.DropIndex(
                name: "IX_Notifications_DedupeKey",
                table: "Notifications");

            migrationBuilder.DropColumn(
                name: "FailureReason",
                table: "PaymentWebhookEvents");

            migrationBuilder.DropColumn(
                name: "PaymentOrderId",
                table: "PaymentWebhookEvents");

            migrationBuilder.DropColumn(
                name: "IncludedAiCredits",
                table: "PaymentTransactions");

            migrationBuilder.DropColumn(
                name: "SubscriptionPeriodEnd",
                table: "PaymentTransactions");

            migrationBuilder.DropColumn(
                name: "SubscriptionPeriodStart",
                table: "PaymentTransactions");

            migrationBuilder.DropColumn(
                name: "DedupeKey",
                table: "Notifications");
        }
    }
}
