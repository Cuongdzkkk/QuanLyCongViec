using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TaskManagement.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddAiCreditBuckets : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "AiCreditBuckets",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    UserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    PlanCode = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    GrantedCredits = table.Column<int>(type: "int", nullable: false),
                    RemainingCredits = table.Column<int>(type: "int", nullable: false),
                    ValidFrom = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ExpiresAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    SourceType = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    SourcePaymentOrderId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    SourceReference = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AiCreditBuckets", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AiCreditBuckets_PaymentOrders_SourcePaymentOrderId",
                        column: x => x.SourcePaymentOrderId,
                        principalTable: "PaymentOrders",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_AiCreditBuckets_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_AiCreditBuckets_SourcePaymentOrderId",
                table: "AiCreditBuckets",
                column: "SourcePaymentOrderId",
                unique: true,
                filter: "[SourcePaymentOrderId] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_AiCreditBuckets_UserId_ExpiresAt_CreatedAt",
                table: "AiCreditBuckets",
                columns: new[] { "UserId", "ExpiresAt", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_AiCreditBuckets_UserId_ValidFrom_ExpiresAt",
                table: "AiCreditBuckets",
                columns: new[] { "UserId", "ValidFrom", "ExpiresAt" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AiCreditBuckets");
        }
    }
}
