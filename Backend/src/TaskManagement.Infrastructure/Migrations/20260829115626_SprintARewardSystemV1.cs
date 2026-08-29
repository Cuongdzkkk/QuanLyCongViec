using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TaskManagement.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class SprintARewardSystemV1 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "RewardSeasons",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ProjectId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(160)", maxLength: 160, nullable: false),
                    Type = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    SprintId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    StartAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    EndAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    TimeZone = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: false),
                    Status = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    AllowSelfApproval = table.Column<bool>(type: "bit", nullable: false),
                    CreatedBy = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    ClosedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RewardSeasons", x => x.Id);
                    table.ForeignKey(
                        name: "FK_RewardSeasons_Projects_ProjectId",
                        column: x => x.ProjectId,
                        principalTable: "Projects",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_RewardSeasons_Sprints_SprintId",
                        column: x => x.SprintId,
                        principalTable: "Sprints",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.NoAction);
                });

            migrationBuilder.CreateTable(
                name: "RewardDefinitions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ProjectId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    SeasonId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(160)", maxLength: 160, nullable: false),
                    Description = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    RewardType = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    DisplayValue = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: true),
                    Currency = table.Column<string>(type: "nvarchar(8)", maxLength: 8, nullable: true),
                    ConditionType = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    ConditionMetric = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                    Threshold = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    RankFrom = table.Column<int>(type: "int", nullable: true),
                    RankTo = table.Column<int>(type: "int", nullable: true),
                    RequireActiveMemberAtSettlement = table.Column<bool>(type: "bit", nullable: false),
                    IsEnabled = table.Column<bool>(type: "bit", nullable: false),
                    CreatedBy = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RewardDefinitions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_RewardDefinitions_RewardSeasons_SeasonId",
                        column: x => x.SeasonId,
                        principalTable: "RewardSeasons",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "RewardPointEvents",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ProjectId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    SeasonId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    WorkTaskId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    UserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Points = table.Column<int>(type: "int", nullable: false),
                    Xp = table.Column<int>(type: "int", nullable: false),
                    Status = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    EventType = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                    ScoreSource = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                    DifficultySnapshot = table.Column<string>(type: "nvarchar(8)", maxLength: 8, nullable: false),
                    CompletedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    FinalizedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    FinalizedBy = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    CancelledAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    CancelledBy = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    CancellationReason = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    IdempotencyKey = table.Column<string>(type: "nvarchar(180)", maxLength: 180, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RewardPointEvents", x => x.Id);
                    table.ForeignKey(
                        name: "FK_RewardPointEvents_RewardSeasons_SeasonId",
                        column: x => x.SeasonId,
                        principalTable: "RewardSeasons",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_RewardPointEvents_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_RewardPointEvents_WorkTasks_WorkTaskId",
                        column: x => x.WorkTaskId,
                        principalTable: "WorkTasks",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "RewardGrants",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ProjectId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    SeasonId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    RewardDefinitionId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    RecipientUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Status = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    RequiresManagerResolution = table.Column<bool>(type: "bit", nullable: false),
                    ManagerNote = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    EarnedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    FulfilledAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    FulfilledBy = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RewardGrants", x => x.Id);
                    table.ForeignKey(
                        name: "FK_RewardGrants_RewardDefinitions_RewardDefinitionId",
                        column: x => x.RewardDefinitionId,
                        principalTable: "RewardDefinitions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_RewardGrants_RewardSeasons_SeasonId",
                        column: x => x.SeasonId,
                        principalTable: "RewardSeasons",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_RewardGrants_Users_RecipientUserId",
                        column: x => x.RecipientUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_RewardDefinitions_SeasonId_IsEnabled",
                table: "RewardDefinitions",
                columns: new[] { "SeasonId", "IsEnabled" });

            migrationBuilder.CreateIndex(
                name: "IX_RewardGrants_RecipientUserId_Status",
                table: "RewardGrants",
                columns: new[] { "RecipientUserId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_RewardGrants_RewardDefinitionId_SeasonId_RecipientUserId",
                table: "RewardGrants",
                columns: new[] { "RewardDefinitionId", "SeasonId", "RecipientUserId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_RewardGrants_SeasonId",
                table: "RewardGrants",
                column: "SeasonId");

            migrationBuilder.CreateIndex(
                name: "IX_RewardPointEvents_IdempotencyKey",
                table: "RewardPointEvents",
                column: "IdempotencyKey",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_RewardPointEvents_SeasonId_UserId_Status",
                table: "RewardPointEvents",
                columns: new[] { "SeasonId", "UserId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_RewardPointEvents_UserId",
                table: "RewardPointEvents",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_RewardPointEvents_WorkTaskId_UserId",
                table: "RewardPointEvents",
                columns: new[] { "WorkTaskId", "UserId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_RewardSeasons_ProjectId_StartAt",
                table: "RewardSeasons",
                columns: new[] { "ProjectId", "StartAt" });

            migrationBuilder.CreateIndex(
                name: "IX_RewardSeasons_ProjectId_Status",
                table: "RewardSeasons",
                columns: new[] { "ProjectId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_RewardSeasons_SprintId",
                table: "RewardSeasons",
                column: "SprintId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "RewardGrants");

            migrationBuilder.DropTable(
                name: "RewardPointEvents");

            migrationBuilder.DropTable(
                name: "RewardDefinitions");

            migrationBuilder.DropTable(
                name: "RewardSeasons");
        }
    }
}
