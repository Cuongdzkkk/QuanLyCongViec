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

            migrationBuilder.AddColumn<int>(name: "RequestedCredits", table: "AiCreditReservations", type: "int", nullable: false, defaultValue: 0);
            migrationBuilder.AddColumn<int>(name: "ReservedCredits", table: "AiCreditReservations", type: "int", nullable: false, defaultValue: 0);
            migrationBuilder.AddColumn<int>(name: "FinalizedCredits", table: "AiCreditReservations", type: "int", nullable: false, defaultValue: 0);
            migrationBuilder.AddColumn<DateTime>(name: "FinalizedAt", table: "AiCreditReservations", type: "datetime2", nullable: true);
            migrationBuilder.AddColumn<DateTime>(name: "ReleasedAt", table: "AiCreditReservations", type: "datetime2", nullable: true);
            migrationBuilder.Sql("UPDATE AiCreditReservations SET RequestedCredits = Credits, ReservedCredits = Credits WHERE RequestedCredits = 0");

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

            migrationBuilder.DropColumn(name: "RequestedCredits", table: "AiCreditReservations");
            migrationBuilder.DropColumn(name: "ReservedCredits", table: "AiCreditReservations");
            migrationBuilder.DropColumn(name: "FinalizedCredits", table: "AiCreditReservations");
            migrationBuilder.DropColumn(name: "FinalizedAt", table: "AiCreditReservations");
            migrationBuilder.DropColumn(name: "ReleasedAt", table: "AiCreditReservations");

            migrationBuilder.DropColumn(
                name: "RowVersion",
                table: "AiCreditBuckets");
        }
    }
}
