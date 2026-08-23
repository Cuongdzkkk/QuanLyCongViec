using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TaskManagement.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddAiCreditReservations : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<byte[]>(
                name: "RowVersion",
                table: "AiCreditBuckets",
                type: "rowversion",
                rowVersion: true,
                nullable: false);

            migrationBuilder.CreateTable(
                name: "AiCreditReservations",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    UserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    IdempotencyKey = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    RequestedCredits = table.Column<int>(type: "int", nullable: false),
                    ReservedCredits = table.Column<int>(type: "int", nullable: false),
                    FinalizedCredits = table.Column<int>(type: "int", nullable: false),
                    Status = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ExpiresAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    FinalizedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ReleasedAt = table.Column<DateTime>(type: "datetime2", nullable: true)
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
                name: "AiCreditReservationAllocations",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ReservationId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CreditBucketId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    AllocatedCredits = table.Column<int>(type: "int", nullable: false),
                    ConsumedCredits = table.Column<int>(type: "int", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AiCreditReservationAllocations", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AiCreditReservationAllocations_AiCreditBuckets_CreditBucketId",
                        column: x => x.CreditBucketId,
                        principalTable: "AiCreditBuckets",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_AiCreditReservationAllocations_AiCreditReservations_ReservationId",
                        column: x => x.ReservationId,
                        principalTable: "AiCreditReservations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_AiCreditReservationAllocations_CreditBucketId",
                table: "AiCreditReservationAllocations",
                column: "CreditBucketId");

            migrationBuilder.CreateIndex(
                name: "IX_AiCreditReservationAllocations_ReservationId_CreditBucketId",
                table: "AiCreditReservationAllocations",
                columns: new[] { "ReservationId", "CreditBucketId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_AiCreditReservations_UserId_IdempotencyKey",
                table: "AiCreditReservations",
                columns: new[] { "UserId", "IdempotencyKey" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AiCreditReservationAllocations");

            migrationBuilder.DropTable(
                name: "AiCreditReservations");

            migrationBuilder.DropColumn(
                name: "RowVersion",
                table: "AiCreditBuckets");
        }
    }
}
