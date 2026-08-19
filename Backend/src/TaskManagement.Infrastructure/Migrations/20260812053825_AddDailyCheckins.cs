using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TaskManagement.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddDailyCheckins : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "DailyCheckins",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ProjectId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    UserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CheckinDate = table.Column<DateOnly>(type: "date", nullable: false),
                    Yesterday = table.Column<string>(type: "nvarchar(4000)", maxLength: 4000, nullable: false),
                    Today = table.Column<string>(type: "nvarchar(4000)", maxLength: 4000, nullable: false),
                    Blocker = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DailyCheckins", x => x.Id);
                    table.ForeignKey(
                        name: "FK_DailyCheckins_Projects_ProjectId",
                        column: x => x.ProjectId,
                        principalTable: "Projects",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_DailyCheckins_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_DailyCheckins_ProjectId_CheckinDate",
                table: "DailyCheckins",
                columns: new[] { "ProjectId", "CheckinDate" });

            migrationBuilder.CreateIndex(
                name: "IX_DailyCheckins_ProjectId_UserId_CheckinDate",
                table: "DailyCheckins",
                columns: new[] { "ProjectId", "UserId", "CheckinDate" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_DailyCheckins_UserId_CheckinDate",
                table: "DailyCheckins",
                columns: new[] { "UserId", "CheckinDate" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "DailyCheckins");
        }
    }
}
