using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TaskManagement.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddDirectConversationPersistence : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<string>(
                name: "Content",
                table: "DirectMessages",
                type: "nvarchar(4000)",
                maxLength: 4000,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(max)");

            migrationBuilder.AddColumn<Guid>(
                name: "ConversationId",
                table: "DirectMessages",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "DirectConversations",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    WorkspaceId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    UserLowId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    UserHighId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    LastMessageAt = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DirectConversations", x => x.Id);
                    table.CheckConstraint("CK_DirectConversations_DistinctUsers", "[UserLowId] <> [UserHighId]");
                    table.ForeignKey(
                        name: "FK_DirectConversations_Users_UserHighId",
                        column: x => x.UserHighId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_DirectConversations_Users_UserLowId",
                        column: x => x.UserLowId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_DirectConversations_Workspaces_WorkspaceId",
                        column: x => x.WorkspaceId,
                        principalTable: "Workspaces",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "DirectConversationParticipants",
                columns: table => new
                {
                    ConversationId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    UserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    JoinedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DirectConversationParticipants", x => new { x.ConversationId, x.UserId });
                    table.ForeignKey(
                        name: "FK_DirectConversationParticipants_DirectConversations_ConversationId",
                        column: x => x.ConversationId,
                        principalTable: "DirectConversations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_DirectConversationParticipants_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_DirectMessages_ConversationId_SentAt_Id",
                table: "DirectMessages",
                columns: new[] { "ConversationId", "SentAt", "Id" });

            migrationBuilder.CreateIndex(
                name: "IX_DirectConversationParticipants_UserId_ConversationId",
                table: "DirectConversationParticipants",
                columns: new[] { "UserId", "ConversationId" });

            migrationBuilder.CreateIndex(
                name: "IX_DirectConversations_UserHighId",
                table: "DirectConversations",
                column: "UserHighId");

            migrationBuilder.CreateIndex(
                name: "IX_DirectConversations_UserLowId_UserHighId",
                table: "DirectConversations",
                columns: new[] { "UserLowId", "UserHighId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_DirectConversations_WorkspaceId_LastMessageAt_CreatedAt_Id",
                table: "DirectConversations",
                columns: new[] { "WorkspaceId", "LastMessageAt", "CreatedAt", "Id" });

            migrationBuilder.AddForeignKey(
                name: "FK_DirectMessages_DirectConversations_ConversationId",
                table: "DirectMessages",
                column: "ConversationId",
                principalTable: "DirectConversations",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_DirectMessages_DirectConversations_ConversationId",
                table: "DirectMessages");

            migrationBuilder.DropTable(
                name: "DirectConversationParticipants");

            migrationBuilder.DropTable(
                name: "DirectConversations");

            migrationBuilder.DropIndex(
                name: "IX_DirectMessages_ConversationId_SentAt_Id",
                table: "DirectMessages");

            migrationBuilder.DropColumn(
                name: "ConversationId",
                table: "DirectMessages");

            migrationBuilder.AlterColumn<string>(
                name: "Content",
                table: "DirectMessages",
                type: "nvarchar(max)",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(4000)",
                oldMaxLength: 4000);
        }
    }
}
