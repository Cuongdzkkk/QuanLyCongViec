using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TaskManagement.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddImmutableRewardLedger : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "IdempotencyKey",
                table: "PointTransactions",
                type: "nvarchar(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "ReversalOfTransactionId",
                table: "PointTransactions",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "RewardEventId",
                table: "PointTransactions",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "RewardRuleVersion",
                table: "PointTransactions",
                type: "nvarchar(40)",
                maxLength: 40,
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_PointTransactions_IdempotencyKey",
                table: "PointTransactions",
                column: "IdempotencyKey",
                unique: true,
                filter: "[IdempotencyKey] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_PointTransactions_ReversalOfTransactionId",
                table: "PointTransactions",
                column: "ReversalOfTransactionId",
                unique: true,
                filter: "[ReversalOfTransactionId] IS NOT NULL");

            migrationBuilder.AddForeignKey(
                name: "FK_PointTransactions_PointTransactions_ReversalOfTransactionId",
                table: "PointTransactions",
                column: "ReversalOfTransactionId",
                principalTable: "PointTransactions",
                principalColumn: "Id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_PointTransactions_PointTransactions_ReversalOfTransactionId",
                table: "PointTransactions");

            migrationBuilder.DropIndex(
                name: "IX_PointTransactions_IdempotencyKey",
                table: "PointTransactions");

            migrationBuilder.DropIndex(
                name: "IX_PointTransactions_ReversalOfTransactionId",
                table: "PointTransactions");

            migrationBuilder.DropColumn(
                name: "IdempotencyKey",
                table: "PointTransactions");

            migrationBuilder.DropColumn(
                name: "ReversalOfTransactionId",
                table: "PointTransactions");

            migrationBuilder.DropColumn(
                name: "RewardEventId",
                table: "PointTransactions");

            migrationBuilder.DropColumn(
                name: "RewardRuleVersion",
                table: "PointTransactions");
        }
    }
}
