using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TaskManagement.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class RemoveRewardGrantUniqueConstraint : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_RewardGrants_RewardDefinitionId_SeasonId_RecipientUserId",
                table: "RewardGrants");

            migrationBuilder.CreateIndex(
                name: "IX_RewardGrants_RewardDefinitionId_SeasonId_RecipientUserId",
                table: "RewardGrants",
                columns: new[] { "RewardDefinitionId", "SeasonId", "RecipientUserId" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_RewardGrants_RewardDefinitionId_SeasonId_RecipientUserId",
                table: "RewardGrants");

            migrationBuilder.CreateIndex(
                name: "IX_RewardGrants_RewardDefinitionId_SeasonId_RecipientUserId",
                table: "RewardGrants",
                columns: new[] { "RewardDefinitionId", "SeasonId", "RecipientUserId" },
                unique: true);
        }
    }
}
