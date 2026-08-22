using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TaskManagement.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class ChatAi2CallTranscriptFoundation : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "CallTranscriptChunks",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CallSessionId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ProjectId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    VoiceChannelId = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    SpeakerUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    SpeakerDisplayName = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                    StartedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    EndedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    Text = table.Column<string>(type: "nvarchar(max)", maxLength: 12000, nullable: false),
                    Confidence = table.Column<double>(type: "float", nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CallTranscriptChunks", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_CallTranscriptChunks_CallSessionId_CreatedAt",
                table: "CallTranscriptChunks",
                columns: new[] { "CallSessionId", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_CallTranscriptChunks_ProjectId_VoiceChannelId_CallSessionId_StartedAt",
                table: "CallTranscriptChunks",
                columns: new[] { "ProjectId", "VoiceChannelId", "CallSessionId", "StartedAt" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "CallTranscriptChunks");
        }
    }
}
