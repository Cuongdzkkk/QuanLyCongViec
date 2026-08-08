using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TaskManagement.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddCollaborationMessageAttachments : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "CollaborationMessageAttachments",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ChannelMessageId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    DirectMessageId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    StorageKey = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: false),
                    OriginalFileName = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: false),
                    ContentType = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: false),
                    SizeBytes = table.Column<long>(type: "bigint", nullable: false),
                    UploadedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CollaborationMessageAttachments", x => x.Id);
                    table.CheckConstraint("CK_CollaborationMessageAttachments_ExactlyOneMessage", "([ChannelMessageId] IS NOT NULL AND [DirectMessageId] IS NULL) OR ([ChannelMessageId] IS NULL AND [DirectMessageId] IS NOT NULL)");
                    table.ForeignKey(
                        name: "FK_CollaborationMessageAttachments_ChannelMessages_ChannelMessageId",
                        column: x => x.ChannelMessageId,
                        principalTable: "ChannelMessages",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_CollaborationMessageAttachments_DirectMessages_DirectMessageId",
                        column: x => x.DirectMessageId,
                        principalTable: "DirectMessages",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_CollaborationMessageAttachments_Users_UploadedByUserId",
                        column: x => x.UploadedByUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_CollaborationMessageAttachments_ChannelMessageId",
                table: "CollaborationMessageAttachments",
                column: "ChannelMessageId");

            migrationBuilder.CreateIndex(
                name: "IX_CollaborationMessageAttachments_DirectMessageId",
                table: "CollaborationMessageAttachments",
                column: "DirectMessageId");

            migrationBuilder.CreateIndex(
                name: "IX_CollaborationMessageAttachments_StorageKey",
                table: "CollaborationMessageAttachments",
                column: "StorageKey",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_CollaborationMessageAttachments_UploadedByUserId_CreatedAt",
                table: "CollaborationMessageAttachments",
                columns: new[] { "UploadedByUserId", "CreatedAt" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "CollaborationMessageAttachments");
        }
    }
}
