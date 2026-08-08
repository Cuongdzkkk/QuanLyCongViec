using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TaskManagement.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddCollaborationChannelDiscovery : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Description",
                table: "CollaborationChannels",
                type: "nvarchar(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ProvisioningKey",
                table: "CollaborationChannels",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_CollaborationChannels_ProjectId_CreatedByUserId_ProvisioningKey",
                table: "CollaborationChannels",
                columns: new[] { "ProjectId", "CreatedByUserId", "ProvisioningKey" },
                unique: true,
                filter: "[ProvisioningKey] IS NOT NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_CollaborationChannels_ProjectId_CreatedByUserId_ProvisioningKey",
                table: "CollaborationChannels");

            migrationBuilder.DropColumn(
                name: "Description",
                table: "CollaborationChannels");

            migrationBuilder.DropColumn(
                name: "ProvisioningKey",
                table: "CollaborationChannels");
        }
    }
}
