using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TaskManagement.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddRewardShopFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "ClaimLimit",
                table: "RewardDefinitions",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "EndAt",
                table: "RewardDefinitions",
                type: "datetimeoffset",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Method",
                table: "RewardDefinitions",
                type: "nvarchar(max)",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<int>(
                name: "PointCost",
                table: "RewardDefinitions",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "Quantity",
                table: "RewardDefinitions",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "StartAt",
                table: "RewardDefinitions",
                type: "datetimeoffset",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ClaimLimit",
                table: "RewardDefinitions");

            migrationBuilder.DropColumn(
                name: "EndAt",
                table: "RewardDefinitions");

            migrationBuilder.DropColumn(
                name: "Method",
                table: "RewardDefinitions");

            migrationBuilder.DropColumn(
                name: "PointCost",
                table: "RewardDefinitions");

            migrationBuilder.DropColumn(
                name: "Quantity",
                table: "RewardDefinitions");

            migrationBuilder.DropColumn(
                name: "StartAt",
                table: "RewardDefinitions");
        }
    }
}
