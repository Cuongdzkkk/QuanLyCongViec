using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TaskManagement.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class SyncModel : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
IF OBJECT_ID(N'[dbo].[LevelConfigs]', N'U') IS NULL
BEGIN
    CREATE TABLE [dbo].[LevelConfigs] (
        [Id] uniqueidentifier NOT NULL,
        [ProjectId] uniqueidentifier NOT NULL,
        [Level] int NOT NULL,
        [Title] nvarchar(100) NOT NULL,
        [RequiredXpPerLevel] int NOT NULL,
        [RewardId] uniqueidentifier NULL,
        CONSTRAINT [PK_LevelConfigs] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_LevelConfigs_Projects_ProjectId] FOREIGN KEY ([ProjectId]) REFERENCES [dbo].[Projects] ([Id]) ON DELETE CASCADE,
        CONSTRAINT [FK_LevelConfigs_RewardDefinitions_RewardId] FOREIGN KEY ([RewardId]) REFERENCES [dbo].[RewardDefinitions] ([Id])
    );
END

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_LevelConfigs_ProjectId' AND object_id = OBJECT_ID(N'[dbo].[LevelConfigs]'))
    CREATE INDEX [IX_LevelConfigs_ProjectId] ON [dbo].[LevelConfigs] ([ProjectId]);

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_LevelConfigs_RewardId' AND object_id = OBJECT_ID(N'[dbo].[LevelConfigs]'))
    CREATE INDEX [IX_LevelConfigs_RewardId] ON [dbo].[LevelConfigs] ([RewardId]);
");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "LevelConfigs");
        }
    }
}
