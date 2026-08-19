using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TaskManagement.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddCollaborationChannelScope : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "ChannelScope",
                table: "CollaborationChannels",
                type: "nvarchar(32)",
                maxLength: 32,
                nullable: false,
                defaultValue: "Private");

            migrationBuilder.CreateIndex(
                name: "IX_CollaborationChannels_ProjectId_ChannelScope",
                table: "CollaborationChannels",
                columns: new[] { "ProjectId", "ChannelScope" },
                unique: true,
                filter: "[ChannelScope] = 'ProjectDiscussion' AND [IsDeleted] = 0 AND [IsArchived] = 0");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_CollaborationChannels_ProjectId_ChannelScope",
                table: "CollaborationChannels");

            migrationBuilder.DropColumn(
                name: "ChannelScope",
                table: "CollaborationChannels");
        }
    }
}
