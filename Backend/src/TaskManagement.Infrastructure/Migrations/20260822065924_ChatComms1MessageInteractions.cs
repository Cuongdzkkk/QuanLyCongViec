using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TaskManagement.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class ChatComms1MessageInteractions : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "ReplyToMessageId",
                table: "ChannelMessages",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "CollaborationMessagePins",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ChannelMessageId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    PinnedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    PinnedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CollaborationMessagePins", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CollaborationMessagePins_ChannelMessages_ChannelMessageId",
                        column: x => x.ChannelMessageId,
                        principalTable: "ChannelMessages",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_CollaborationMessagePins_Users_PinnedByUserId",
                        column: x => x.PinnedByUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "CollaborationMessageReactions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ChannelMessageId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    UserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Emoji = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CollaborationMessageReactions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CollaborationMessageReactions_ChannelMessages_ChannelMessageId",
                        column: x => x.ChannelMessageId,
                        principalTable: "ChannelMessages",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_CollaborationMessageReactions_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ChannelMessages_ReplyToMessageId",
                table: "ChannelMessages",
                column: "ReplyToMessageId");

            migrationBuilder.CreateIndex(
                name: "IX_CollaborationMessagePins_ChannelMessageId",
                table: "CollaborationMessagePins",
                column: "ChannelMessageId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_CollaborationMessagePins_ChannelMessageId_PinnedAt",
                table: "CollaborationMessagePins",
                columns: new[] { "ChannelMessageId", "PinnedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_CollaborationMessagePins_PinnedByUserId",
                table: "CollaborationMessagePins",
                column: "PinnedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_CollaborationMessageReactions_ChannelMessageId_Emoji",
                table: "CollaborationMessageReactions",
                columns: new[] { "ChannelMessageId", "Emoji" });

            migrationBuilder.CreateIndex(
                name: "IX_CollaborationMessageReactions_ChannelMessageId_UserId_Emoji",
                table: "CollaborationMessageReactions",
                columns: new[] { "ChannelMessageId", "UserId", "Emoji" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_CollaborationMessageReactions_UserId",
                table: "CollaborationMessageReactions",
                column: "UserId");

            migrationBuilder.AddForeignKey(
                name: "FK_ChannelMessages_ChannelMessages_ReplyToMessageId",
                table: "ChannelMessages",
                column: "ReplyToMessageId",
                principalTable: "ChannelMessages",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_ChannelMessages_ChannelMessages_ReplyToMessageId",
                table: "ChannelMessages");

            migrationBuilder.DropTable(
                name: "CollaborationMessagePins");

            migrationBuilder.DropTable(
                name: "CollaborationMessageReactions");

            migrationBuilder.DropIndex(
                name: "IX_ChannelMessages_ReplyToMessageId",
                table: "ChannelMessages");

            migrationBuilder.DropColumn(
                name: "ReplyToMessageId",
                table: "ChannelMessages");
        }
    }
}
