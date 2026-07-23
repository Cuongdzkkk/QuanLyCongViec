using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TaskManagement.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class PreserveTaskAssignmentHistory : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "RemovalReason",
                table: "TaskAssignments",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "RemovedAt",
                table: "TaskAssignments",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "RemovedBy",
                table: "TaskAssignments",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_TaskAssignments_RemovedBy",
                table: "TaskAssignments",
                column: "RemovedBy");

            migrationBuilder.AddForeignKey(
                name: "FK_TaskAssignments_Users_RemovedBy",
                table: "TaskAssignments",
                column: "RemovedBy",
                principalTable: "Users",
                principalColumn: "Id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_TaskAssignments_Users_RemovedBy",
                table: "TaskAssignments");

            migrationBuilder.DropIndex(
                name: "IX_TaskAssignments_RemovedBy",
                table: "TaskAssignments");

            migrationBuilder.DropColumn(
                name: "RemovalReason",
                table: "TaskAssignments");

            migrationBuilder.DropColumn(
                name: "RemovedAt",
                table: "TaskAssignments");

            migrationBuilder.DropColumn(
                name: "RemovedBy",
                table: "TaskAssignments");
        }
    }
}
