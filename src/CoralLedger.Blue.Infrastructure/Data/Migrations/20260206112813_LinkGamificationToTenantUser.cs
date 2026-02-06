using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CoralLedger.Blue.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class LinkGamificationToTenantUser : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "TenantUserId",
                table: "user_profiles",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "TenantUserId",
                table: "user_points",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "TenantUserId",
                table: "user_badges",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "TenantUserId",
                table: "user_achievements",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_user_profiles_TenantUserId",
                table: "user_profiles",
                column: "TenantUserId");

            migrationBuilder.CreateIndex(
                name: "IX_user_points_TenantUserId",
                table: "user_points",
                column: "TenantUserId");

            migrationBuilder.CreateIndex(
                name: "IX_user_badges_TenantUserId",
                table: "user_badges",
                column: "TenantUserId");

            migrationBuilder.CreateIndex(
                name: "IX_user_achievements_TenantUserId",
                table: "user_achievements",
                column: "TenantUserId");

            migrationBuilder.AddForeignKey(
                name: "FK_user_achievements_tenant_users_TenantUserId",
                table: "user_achievements",
                column: "TenantUserId",
                principalTable: "tenant_users",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_user_badges_tenant_users_TenantUserId",
                table: "user_badges",
                column: "TenantUserId",
                principalTable: "tenant_users",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_user_points_tenant_users_TenantUserId",
                table: "user_points",
                column: "TenantUserId",
                principalTable: "tenant_users",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_user_profiles_tenant_users_TenantUserId",
                table: "user_profiles",
                column: "TenantUserId",
                principalTable: "tenant_users",
                principalColumn: "Id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_user_achievements_tenant_users_TenantUserId",
                table: "user_achievements");

            migrationBuilder.DropForeignKey(
                name: "FK_user_badges_tenant_users_TenantUserId",
                table: "user_badges");

            migrationBuilder.DropForeignKey(
                name: "FK_user_points_tenant_users_TenantUserId",
                table: "user_points");

            migrationBuilder.DropForeignKey(
                name: "FK_user_profiles_tenant_users_TenantUserId",
                table: "user_profiles");

            migrationBuilder.DropIndex(
                name: "IX_user_profiles_TenantUserId",
                table: "user_profiles");

            migrationBuilder.DropIndex(
                name: "IX_user_points_TenantUserId",
                table: "user_points");

            migrationBuilder.DropIndex(
                name: "IX_user_badges_TenantUserId",
                table: "user_badges");

            migrationBuilder.DropIndex(
                name: "IX_user_achievements_TenantUserId",
                table: "user_achievements");

            migrationBuilder.DropColumn(
                name: "TenantUserId",
                table: "user_profiles");

            migrationBuilder.DropColumn(
                name: "TenantUserId",
                table: "user_points");

            migrationBuilder.DropColumn(
                name: "TenantUserId",
                table: "user_badges");

            migrationBuilder.DropColumn(
                name: "TenantUserId",
                table: "user_achievements");
        }
    }
}
