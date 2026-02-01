using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CoralLedger.Blue.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddGamificationSystem : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "PointsAwarded",
                table: "citizen_observations",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<bool>(
                name: "PointsProcessed",
                table: "citizen_observations",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.CreateTable(
                name: "user_achievements",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    CitizenEmail = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    AchievementKey = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Title = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Description = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    CurrentProgress = table.Column<int>(type: "integer", nullable: false),
                    TargetProgress = table.Column<int>(type: "integer", nullable: false),
                    IsCompleted = table.Column<bool>(type: "boolean", nullable: false),
                    CompletedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    PointsAwarded = table.Column<int>(type: "integer", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatedBy = table.Column<string>(type: "text", nullable: true),
                    ModifiedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    ModifiedBy = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_user_achievements", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "user_badges",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    CitizenEmail = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    BadgeType = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    EarnedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    Description = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatedBy = table.Column<string>(type: "text", nullable: true),
                    ModifiedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    ModifiedBy = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_user_badges", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "user_points",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    CitizenEmail = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    TotalPoints = table.Column<int>(type: "integer", nullable: false),
                    WeeklyPoints = table.Column<int>(type: "integer", nullable: false),
                    MonthlyPoints = table.Column<int>(type: "integer", nullable: false),
                    LastPointsEarned = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    WeeklyResetAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    MonthlyResetAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatedBy = table.Column<string>(type: "text", nullable: true),
                    ModifiedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    ModifiedBy = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_user_points", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "user_profiles",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    CitizenEmail = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    CitizenName = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    Tier = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    TotalObservations = table.Column<int>(type: "integer", nullable: false),
                    VerifiedObservations = table.Column<int>(type: "integer", nullable: false),
                    RejectedObservations = table.Column<int>(type: "integer", nullable: false),
                    AccuracyRate = table.Column<double>(type: "double precision", nullable: false),
                    LastObservationAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatedBy = table.Column<string>(type: "text", nullable: true),
                    ModifiedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    ModifiedBy = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_user_profiles", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_user_achievements_AchievementKey",
                table: "user_achievements",
                column: "AchievementKey");

            migrationBuilder.CreateIndex(
                name: "IX_user_achievements_CitizenEmail",
                table: "user_achievements",
                column: "CitizenEmail");

            migrationBuilder.CreateIndex(
                name: "IX_user_achievements_CitizenEmail_AchievementKey",
                table: "user_achievements",
                columns: new[] { "CitizenEmail", "AchievementKey" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_user_achievements_IsCompleted",
                table: "user_achievements",
                column: "IsCompleted");

            migrationBuilder.CreateIndex(
                name: "IX_user_badges_BadgeType",
                table: "user_badges",
                column: "BadgeType");

            migrationBuilder.CreateIndex(
                name: "IX_user_badges_CitizenEmail",
                table: "user_badges",
                column: "CitizenEmail");

            migrationBuilder.CreateIndex(
                name: "IX_user_badges_CitizenEmail_BadgeType",
                table: "user_badges",
                columns: new[] { "CitizenEmail", "BadgeType" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_user_badges_EarnedAt",
                table: "user_badges",
                column: "EarnedAt");

            migrationBuilder.CreateIndex(
                name: "IX_user_points_CitizenEmail",
                table: "user_points",
                column: "CitizenEmail",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_user_points_MonthlyPoints",
                table: "user_points",
                column: "MonthlyPoints");

            migrationBuilder.CreateIndex(
                name: "IX_user_points_TotalPoints",
                table: "user_points",
                column: "TotalPoints");

            migrationBuilder.CreateIndex(
                name: "IX_user_points_WeeklyPoints",
                table: "user_points",
                column: "WeeklyPoints");

            migrationBuilder.CreateIndex(
                name: "IX_user_profiles_CitizenEmail",
                table: "user_profiles",
                column: "CitizenEmail",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_user_profiles_Tier",
                table: "user_profiles",
                column: "Tier");

            migrationBuilder.CreateIndex(
                name: "IX_user_profiles_TotalObservations",
                table: "user_profiles",
                column: "TotalObservations");

            migrationBuilder.CreateIndex(
                name: "IX_user_profiles_VerifiedObservations",
                table: "user_profiles",
                column: "VerifiedObservations");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "user_achievements");

            migrationBuilder.DropTable(
                name: "user_badges");

            migrationBuilder.DropTable(
                name: "user_points");

            migrationBuilder.DropTable(
                name: "user_profiles");

            migrationBuilder.DropColumn(
                name: "PointsAwarded",
                table: "citizen_observations");

            migrationBuilder.DropColumn(
                name: "PointsProcessed",
                table: "citizen_observations");
        }
    }
}
