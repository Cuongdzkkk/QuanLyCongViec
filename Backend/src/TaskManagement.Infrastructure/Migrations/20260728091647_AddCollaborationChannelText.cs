using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TaskManagement.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddCollaborationChannelText : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<Guid>(
                name: "ChannelId",
                table: "ChannelMessages",
                type: "uniqueidentifier",
                nullable: true,
                oldClrType: typeof(Guid),
                oldType: "uniqueidentifier");

            migrationBuilder.AddColumn<Guid>(
                name: "CollaborationChannelId",
                table: "ChannelMessages",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "CollaborationChannels",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    WorkspaceId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ProjectId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CreatedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    IsArchived = table.Column<bool>(type: "bit", nullable: false),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CollaborationChannels", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CollaborationChannels_Projects_ProjectId",
                        column: x => x.ProjectId,
                        principalTable: "Projects",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_CollaborationChannels_Users_CreatedByUserId",
                        column: x => x.CreatedByUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_CollaborationChannels_Workspaces_WorkspaceId",
                        column: x => x.WorkspaceId,
                        principalTable: "Workspaces",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "CollaborationChannelMembers",
                columns: table => new
                {
                    ChannelId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    UserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    JoinedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    LeftAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    CanSendMessages = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CollaborationChannelMembers", x => new { x.ChannelId, x.UserId });
                    table.ForeignKey(
                        name: "FK_CollaborationChannelMembers_CollaborationChannels_ChannelId",
                        column: x => x.ChannelId,
                        principalTable: "CollaborationChannels",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_CollaborationChannelMembers_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ChannelMessages_CollaborationChannelId_SentAt_Id",
                table: "ChannelMessages",
                columns: new[] { "CollaborationChannelId", "SentAt", "Id" });

            migrationBuilder.CreateIndex(
                name: "IX_CollaborationChannelMembers_UserId_IsActive",
                table: "CollaborationChannelMembers",
                columns: new[] { "UserId", "IsActive" });

            migrationBuilder.CreateIndex(
                name: "IX_CollaborationChannels_CreatedByUserId",
                table: "CollaborationChannels",
                column: "CreatedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_CollaborationChannels_ProjectId_IsDeleted_IsArchived",
                table: "CollaborationChannels",
                columns: new[] { "ProjectId", "IsDeleted", "IsArchived" });

            migrationBuilder.CreateIndex(
                name: "IX_CollaborationChannels_WorkspaceId_ProjectId",
                table: "CollaborationChannels",
                columns: new[] { "WorkspaceId", "ProjectId" });

            migrationBuilder.AddForeignKey(
                name: "FK_ChannelMessages_CollaborationChannels_CollaborationChannelId",
                table: "ChannelMessages",
                column: "CollaborationChannelId",
                principalTable: "CollaborationChannels",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_ChannelMessages_CollaborationChannels_CollaborationChannelId",
                table: "ChannelMessages");

            migrationBuilder.DropTable(
                name: "CollaborationChannelMembers");

            migrationBuilder.DropTable(
                name: "CollaborationChannels");

            migrationBuilder.DropIndex(
                name: "IX_ChannelMessages_CollaborationChannelId_SentAt_Id",
                table: "ChannelMessages");

            migrationBuilder.DropColumn(
                name: "CollaborationChannelId",
                table: "ChannelMessages");

            migrationBuilder.AlterColumn<Guid>(
                name: "ChannelId",
                table: "ChannelMessages",
                type: "uniqueidentifier",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"),
                oldClrType: typeof(Guid),
                oldType: "uniqueidentifier",
                oldNullable: true);
        }
    }
}
