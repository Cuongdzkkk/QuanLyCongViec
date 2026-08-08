using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TaskManagement.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class EnforceCycleTransitions : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Sprints_ProjectId",
                table: "Sprints");

            migrationBuilder.AddColumn<DateTime>(
                name: "CompletedAt",
                table: "Sprints",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsDeleted",
                table: "Sprints",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<DateTime>(
                name: "StartedAt",
                table: "Sprints",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "State",
                table: "Sprints",
                type: "nvarchar(20)",
                maxLength: 20,
                nullable: false,
                defaultValue: "Planned");

            migrationBuilder.Sql(
                """
                UPDATE [Sprints]
                SET [State] = CASE
                        WHEN [Status] = 1 THEN N'Active'
                        WHEN CONVERT(date, [EndDate]) < CONVERT(date, SYSUTCDATETIME()) THEN N'Completed'
                        ELSE N'Planned'
                    END,
                    [StartedAt] = CASE
                        WHEN [Status] = 1 OR CONVERT(date, [EndDate]) < CONVERT(date, SYSUTCDATETIME())
                            THEN [StartDate]
                        ELSE NULL
                    END,
                    [CompletedAt] = CASE
                        WHEN [Status] = 0 AND CONVERT(date, [EndDate]) < CONVERT(date, SYSUTCDATETIME())
                            THEN [EndDate]
                        ELSE NULL
                    END;

                WITH [RankedActive] AS
                (
                    SELECT [Id],
                           ROW_NUMBER() OVER (
                               PARTITION BY [ProjectId]
                               ORDER BY [StartDate] DESC, [CreatedAt] DESC, [Id] ASC
                           ) AS [ActiveRank]
                    FROM [Sprints]
                    WHERE [State] = N'Active' AND [IsDeleted] = 0
                )
                UPDATE [s]
                SET [State] = CASE
                        WHEN CONVERT(date, [s].[EndDate]) < CONVERT(date, SYSUTCDATETIME())
                            THEN N'Completed'
                        ELSE N'Planned'
                    END,
                    [Status] = 0,
                    [CompletedAt] = CASE
                        WHEN CONVERT(date, [s].[EndDate]) < CONVERT(date, SYSUTCDATETIME())
                            THEN [s].[EndDate]
                        ELSE NULL
                    END,
                    [StartedAt] = CASE
                        WHEN CONVERT(date, [s].[EndDate]) < CONVERT(date, SYSUTCDATETIME())
                            THEN [s].[StartDate]
                        ELSE NULL
                    END
                FROM [Sprints] AS [s]
                INNER JOIN [RankedActive] AS [ranked] ON [ranked].[Id] = [s].[Id]
                WHERE [ranked].[ActiveRank] > 1;
                """);

            migrationBuilder.CreateIndex(
                name: "IX_Sprints_Project_State_Order",
                table: "Sprints",
                columns: new[] { "ProjectId", "State", "StartDate", "CreatedAt", "Id" });

            migrationBuilder.CreateIndex(
                name: "UX_Sprints_Project_Active",
                table: "Sprints",
                column: "ProjectId",
                unique: true,
                filter: "[State] = N'Active' AND [IsDeleted] = 0");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Sprints_Project_State_Order",
                table: "Sprints");

            migrationBuilder.DropIndex(
                name: "UX_Sprints_Project_Active",
                table: "Sprints");

            migrationBuilder.DropColumn(
                name: "CompletedAt",
                table: "Sprints");

            migrationBuilder.DropColumn(
                name: "IsDeleted",
                table: "Sprints");

            migrationBuilder.DropColumn(
                name: "StartedAt",
                table: "Sprints");

            migrationBuilder.DropColumn(
                name: "State",
                table: "Sprints");

            migrationBuilder.CreateIndex(
                name: "IX_Sprints_ProjectId",
                table: "Sprints",
                column: "ProjectId");
        }
    }
}
