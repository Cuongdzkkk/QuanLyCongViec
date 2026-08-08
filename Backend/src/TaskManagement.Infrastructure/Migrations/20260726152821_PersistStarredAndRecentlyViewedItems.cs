using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TaskManagement.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class PersistStarredAndRecentlyViewedItems : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "CK_StarredItems_ItemType",
                table: "StarredItems");

            migrationBuilder.Sql(
                """
                ;WITH NormalizedStarred AS
                (
                    SELECT [Id],
                           ROW_NUMBER() OVER
                           (
                               PARTITION BY [UserId], [WorkspaceId],
                                   CASE
                                       WHEN LOWER([ItemType]) IN ('task', 'work-task', 'work_task', 'worktask') THEN 'WorkTask'
                                       WHEN LOWER([ItemType]) = 'project' THEN 'Project'
                                       WHEN LOWER([ItemType]) = 'goal' THEN 'Goal'
                                       WHEN LOWER([ItemType]) = 'team' THEN 'Team'
                                       WHEN LOWER([ItemType]) = 'user' THEN 'User'
                                       ELSE [ItemType]
                                   END,
                                   [ItemId]
                               ORDER BY [CreatedAt] DESC, [Id]
                           ) AS [RowNumber]
                    FROM [StarredItems]
                )
                DELETE FROM [StarredItems]
                WHERE [Id] IN
                (
                    SELECT [Id] FROM NormalizedStarred WHERE [RowNumber] > 1
                );

                UPDATE [StarredItems]
                SET [ItemType] =
                    CASE
                        WHEN LOWER([ItemType]) IN ('task', 'work-task', 'work_task', 'worktask') THEN 'WorkTask'
                        WHEN LOWER([ItemType]) = 'project' THEN 'Project'
                        WHEN LOWER([ItemType]) = 'goal' THEN 'Goal'
                        WHEN LOWER([ItemType]) = 'team' THEN 'Team'
                        WHEN LOWER([ItemType]) = 'user' THEN 'User'
                    END
                WHERE LOWER([ItemType]) IN
                    ('task', 'work-task', 'work_task', 'worktask', 'project', 'goal', 'team', 'user');

                DELETE FROM [StarredItems]
                WHERE [ItemType] NOT IN ('Goal', 'Project', 'Team', 'User', 'WorkTask');
                """);

            migrationBuilder.AddColumn<DateTime>(
                name: "UpdatedAt",
                table: "StarredItems",
                type: "datetime2",
                nullable: false,
                defaultValueSql: "SYSUTCDATETIME()");

            migrationBuilder.Sql(
                "UPDATE [StarredItems] SET [UpdatedAt] = [CreatedAt];");

            migrationBuilder.CreateIndex(
                name: "IX_StarredItems_UserId_WorkspaceId_CreatedAt_Id",
                table: "StarredItems",
                columns: new[] { "UserId", "WorkspaceId", "CreatedAt", "Id" },
                descending: new[] { false, false, true, false });

            migrationBuilder.AddCheckConstraint(
                name: "CK_StarredItems_ItemType",
                table: "StarredItems",
                sql: "[ItemType] IN ('Goal', 'Project', 'Team', 'User', 'WorkTask')");

            migrationBuilder.CreateIndex(
                name: "IX_RecentViews_UserId_ViewedAt_Id",
                table: "RecentViews",
                columns: new[] { "UserId", "ViewedAt", "Id" },
                descending: new[] { false, true, false });

            migrationBuilder.Sql(
                """
                ;WITH NormalizedRecent AS
                (
                    SELECT [Id],
                           ROW_NUMBER() OVER
                           (
                               PARTITION BY [UserId],
                                   CASE
                                       WHEN LOWER([EntityType]) IN ('task', 'work-task', 'work_task', 'worktask') THEN 'WorkTask'
                                       WHEN LOWER([EntityType]) = 'project' THEN 'Project'
                                       WHEN LOWER([EntityType]) = 'goal' THEN 'Goal'
                                       WHEN LOWER([EntityType]) = 'team' THEN 'Team'
                                       WHEN LOWER([EntityType]) = 'user' THEN 'User'
                                       ELSE [EntityType]
                                   END,
                                   [EntityId]
                               ORDER BY [ViewedAt] DESC, [Id]
                           ) AS [RowNumber]
                    FROM [RecentViews]
                )
                DELETE FROM [RecentViews]
                WHERE [Id] IN
                (
                    SELECT [Id] FROM NormalizedRecent WHERE [RowNumber] > 1
                );

                UPDATE [RecentViews]
                SET [EntityType] =
                    CASE
                        WHEN LOWER([EntityType]) IN ('task', 'work-task', 'work_task', 'worktask') THEN 'WorkTask'
                        WHEN LOWER([EntityType]) = 'project' THEN 'Project'
                        WHEN LOWER([EntityType]) = 'goal' THEN 'Goal'
                        WHEN LOWER([EntityType]) = 'team' THEN 'Team'
                        WHEN LOWER([EntityType]) = 'user' THEN 'User'
                    END
                WHERE LOWER([EntityType]) IN
                    ('task', 'work-task', 'work_task', 'worktask', 'project', 'goal', 'team', 'user');

                DELETE FROM [RecentViews]
                WHERE [EntityType] NOT IN ('Goal', 'Project', 'Team', 'User', 'WorkTask');
                """);

            migrationBuilder.AddCheckConstraint(
                name: "CK_RecentViews_EntityType",
                table: "RecentViews",
                sql: "[EntityType] IN ('Goal', 'Project', 'Team', 'User', 'WorkTask')");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_StarredItems_UserId_WorkspaceId_CreatedAt_Id",
                table: "StarredItems");

            migrationBuilder.DropCheckConstraint(
                name: "CK_StarredItems_ItemType",
                table: "StarredItems");

            migrationBuilder.DropIndex(
                name: "IX_RecentViews_UserId_ViewedAt_Id",
                table: "RecentViews");

            migrationBuilder.DropCheckConstraint(
                name: "CK_RecentViews_EntityType",
                table: "RecentViews");

            migrationBuilder.DropColumn(
                name: "UpdatedAt",
                table: "StarredItems");

            migrationBuilder.Sql(
                "DELETE FROM [StarredItems] WHERE [ItemType] = 'WorkTask';");

            migrationBuilder.AddCheckConstraint(
                name: "CK_StarredItems_ItemType",
                table: "StarredItems",
                sql: "[ItemType] IN ('Goal', 'Project', 'Team', 'User')");
        }
    }
}
