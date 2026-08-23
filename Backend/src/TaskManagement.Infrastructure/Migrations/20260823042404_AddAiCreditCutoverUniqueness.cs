using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TaskManagement.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddAiCreditCutoverUniqueness : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateIndex(
                name: "IX_AiCreditBuckets_SourceType_SourceReference",
                table: "AiCreditBuckets",
                columns: new[] { "SourceType", "SourceReference" },
                unique: true,
                filter: "[SourceReference] IS NOT NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_AiCreditBuckets_SourceType_SourceReference",
                table: "AiCreditBuckets");
        }
    }
}
