using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TaskManagement.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddCollaborationReadState : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "CollaborationChannelReadStates",
                columns: table => new
                {
                    ChannelId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    UserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    LastReadMessageId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    LastReadAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CollaborationChannelReadStates", x => new { x.ChannelId, x.UserId });
                    table.ForeignKey(
                        name: "FK_CollaborationChannelReadStates_ChannelMessages_LastReadMessageId",
                        column: x => x.LastReadMessageId,
                        principalTable: "ChannelMessages",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_CollaborationChannelReadStates_CollaborationChannels_ChannelId",
                        column: x => x.ChannelId,
                        principalTable: "CollaborationChannels",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_CollaborationChannelReadStates_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "DirectConversationReadStates",
                columns: table => new
                {
                    ConversationId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    UserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    LastReadMessageId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    LastReadAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DirectConversationReadStates", x => new { x.ConversationId, x.UserId });
                    table.ForeignKey(
                        name: "FK_DirectConversationReadStates_DirectConversations_ConversationId",
                        column: x => x.ConversationId,
                        principalTable: "DirectConversations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_DirectConversationReadStates_DirectMessages_LastReadMessageId",
                        column: x => x.LastReadMessageId,
                        principalTable: "DirectMessages",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_DirectConversationReadStates_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_CollaborationChannelReadStates_LastReadMessageId",
                table: "CollaborationChannelReadStates",
                column: "LastReadMessageId");

            migrationBuilder.CreateIndex(
                name: "IX_CollaborationChannelReadStates_UserId_ChannelId",
                table: "CollaborationChannelReadStates",
                columns: new[] { "UserId", "ChannelId" });

            migrationBuilder.CreateIndex(
                name: "IX_DirectConversationReadStates_LastReadMessageId",
                table: "DirectConversationReadStates",
                column: "LastReadMessageId");

            migrationBuilder.CreateIndex(
                name: "IX_DirectConversationReadStates_UserId_ConversationId",
                table: "DirectConversationReadStates",
                columns: new[] { "UserId", "ConversationId" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "CollaborationChannelReadStates");

            migrationBuilder.DropTable(
                name: "DirectConversationReadStates");
        }
    }
}
