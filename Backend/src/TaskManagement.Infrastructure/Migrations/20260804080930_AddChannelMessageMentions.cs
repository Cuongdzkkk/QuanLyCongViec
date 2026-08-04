using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TaskManagement.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddChannelMessageMentions : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Notifications_UserId",
                table: "Notifications");

            migrationBuilder.AddColumn<Guid>(
                name: "ChannelMessageId",
                table: "Notifications",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "CollaborationChannelId",
                table: "Notifications",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "ChannelMessageMentions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ChannelMessageId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    MentionedUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    StartIndex = table.Column<int>(type: "int", nullable: false),
                    Length = table.Column<int>(type: "int", nullable: false),
                    DisplayText = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ChannelMessageMentions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ChannelMessageMentions_ChannelMessages_ChannelMessageId",
                        column: x => x.ChannelMessageId,
                        principalTable: "ChannelMessages",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_ChannelMessageMentions_Users_MentionedUserId",
                        column: x => x.MentionedUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Notifications_ChannelMessageId",
                table: "Notifications",
                column: "ChannelMessageId");

            migrationBuilder.CreateIndex(
                name: "IX_Notifications_CollaborationChannelId_CreatedAt",
                table: "Notifications",
                columns: new[] { "CollaborationChannelId", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_Notifications_UserId_ChannelMessageId",
                table: "Notifications",
                columns: new[] { "UserId", "ChannelMessageId" },
                unique: true,
                filter: "[ChannelMessageId] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_ChannelMessageMentions_ChannelMessageId_MentionedUserId",
                table: "ChannelMessageMentions",
                columns: new[] { "ChannelMessageId", "MentionedUserId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ChannelMessageMentions_MentionedUserId_CreatedAt",
                table: "ChannelMessageMentions",
                columns: new[] { "MentionedUserId", "CreatedAt" });

            migrationBuilder.AddForeignKey(
                name: "FK_Notifications_ChannelMessages_ChannelMessageId",
                table: "Notifications",
                column: "ChannelMessageId",
                principalTable: "ChannelMessages",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_Notifications_CollaborationChannels_CollaborationChannelId",
                table: "Notifications",
                column: "CollaborationChannelId",
                principalTable: "CollaborationChannels",
                principalColumn: "Id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Notifications_ChannelMessages_ChannelMessageId",
                table: "Notifications");

            migrationBuilder.DropForeignKey(
                name: "FK_Notifications_CollaborationChannels_CollaborationChannelId",
                table: "Notifications");

            migrationBuilder.DropTable(
                name: "ChannelMessageMentions");

            migrationBuilder.DropIndex(
                name: "IX_Notifications_ChannelMessageId",
                table: "Notifications");

            migrationBuilder.DropIndex(
                name: "IX_Notifications_CollaborationChannelId_CreatedAt",
                table: "Notifications");

            migrationBuilder.DropIndex(
                name: "IX_Notifications_UserId_ChannelMessageId",
                table: "Notifications");

            migrationBuilder.DropColumn(
                name: "ChannelMessageId",
                table: "Notifications");

            migrationBuilder.DropColumn(
                name: "CollaborationChannelId",
                table: "Notifications");

            migrationBuilder.CreateIndex(
                name: "IX_Notifications_UserId",
                table: "Notifications",
                column: "UserId");
        }
    }
}
