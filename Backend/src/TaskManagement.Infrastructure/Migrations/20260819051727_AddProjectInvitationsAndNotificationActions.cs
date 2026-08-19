using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TaskManagement.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddProjectInvitationsAndNotificationActions : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "ProjectInvitationId",
                table: "RefreshTokens",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ActionState",
                table: "Notifications",
                type: "nvarchar(32)",
                maxLength: 32,
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "RelatedInvitationId",
                table: "Notifications",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "ProjectInvitations",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ProjectId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    UserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    InvitedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    InvitedEmail = table.Column<string>(type: "nvarchar(320)", maxLength: 320, nullable: false),
                    Status = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ExpiresAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    AcceptedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    DeclinedAt = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ProjectInvitations", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ProjectInvitations_Projects_ProjectId",
                        column: x => x.ProjectId,
                        principalTable: "Projects",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_ProjectInvitations_Users_InvitedByUserId",
                        column: x => x.InvitedByUserId,
                        principalTable: "Users",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_ProjectInvitations_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateIndex(
                name: "IX_RefreshTokens_ProjectInvitationId",
                table: "RefreshTokens",
                column: "ProjectInvitationId");

            migrationBuilder.CreateIndex(
                name: "IX_Notifications_RelatedInvitationId",
                table: "Notifications",
                column: "RelatedInvitationId");

            migrationBuilder.CreateIndex(
                name: "IX_ProjectInvitations_InvitedByUserId",
                table: "ProjectInvitations",
                column: "InvitedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_ProjectInvitations_ProjectId_UserId_Status",
                table: "ProjectInvitations",
                columns: new[] { "ProjectId", "UserId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_ProjectInvitations_UserId",
                table: "ProjectInvitations",
                column: "UserId");

            migrationBuilder.AddForeignKey(
                name: "FK_Notifications_ProjectInvitations_RelatedInvitationId",
                table: "Notifications",
                column: "RelatedInvitationId",
                principalTable: "ProjectInvitations",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_RefreshTokens_ProjectInvitations_ProjectInvitationId",
                table: "RefreshTokens",
                column: "ProjectInvitationId",
                principalTable: "ProjectInvitations",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Notifications_ProjectInvitations_RelatedInvitationId",
                table: "Notifications");

            migrationBuilder.DropForeignKey(
                name: "FK_RefreshTokens_ProjectInvitations_ProjectInvitationId",
                table: "RefreshTokens");

            migrationBuilder.DropTable(
                name: "ProjectInvitations");

            migrationBuilder.DropIndex(
                name: "IX_RefreshTokens_ProjectInvitationId",
                table: "RefreshTokens");

            migrationBuilder.DropIndex(
                name: "IX_Notifications_RelatedInvitationId",
                table: "Notifications");

            migrationBuilder.DropColumn(
                name: "ProjectInvitationId",
                table: "RefreshTokens");

            migrationBuilder.DropColumn(
                name: "ActionState",
                table: "Notifications");

            migrationBuilder.DropColumn(
                name: "RelatedInvitationId",
                table: "Notifications");
        }
    }
}
