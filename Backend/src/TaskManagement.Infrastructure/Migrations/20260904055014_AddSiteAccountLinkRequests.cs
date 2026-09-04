using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TaskManagement.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddSiteAccountLinkRequests : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "RelatedSiteAccountLinkRequestId",
                table: "Notifications",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "SiteAccountLinkRequests",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    RequesterUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TargetUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Status = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    RespondedAt = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SiteAccountLinkRequests", x => x.Id);
                    table.ForeignKey(
                        name: "FK_SiteAccountLinkRequests_Users_RequesterUserId",
                        column: x => x.RequesterUserId,
                        principalTable: "Users",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_SiteAccountLinkRequests_Users_TargetUserId",
                        column: x => x.TargetUserId,
                        principalTable: "Users",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateIndex(
                name: "IX_Notifications_RelatedSiteAccountLinkRequestId",
                table: "Notifications",
                column: "RelatedSiteAccountLinkRequestId");

            migrationBuilder.CreateIndex(
                name: "IX_SiteAccountLinkRequests_RequesterUserId_TargetUserId_Status",
                table: "SiteAccountLinkRequests",
                columns: new[] { "RequesterUserId", "TargetUserId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_SiteAccountLinkRequests_TargetUserId",
                table: "SiteAccountLinkRequests",
                column: "TargetUserId");

            migrationBuilder.AddForeignKey(
                name: "FK_Notifications_SiteAccountLinkRequests_RelatedSiteAccountLinkRequestId",
                table: "Notifications",
                column: "RelatedSiteAccountLinkRequestId",
                principalTable: "SiteAccountLinkRequests",
                principalColumn: "Id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Notifications_SiteAccountLinkRequests_RelatedSiteAccountLinkRequestId",
                table: "Notifications");

            migrationBuilder.DropTable(
                name: "SiteAccountLinkRequests");

            migrationBuilder.DropIndex(
                name: "IX_Notifications_RelatedSiteAccountLinkRequestId",
                table: "Notifications");

            migrationBuilder.DropColumn(
                name: "RelatedSiteAccountLinkRequestId",
                table: "Notifications");
        }
    }
}
