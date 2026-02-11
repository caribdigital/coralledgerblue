using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CoralLedger.Blue.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddTwoFactorPendingSecretFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "TwoFactorPendingSecretExpiry",
                table: "tenant_users",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "TwoFactorPendingSecretKey",
                table: "tenant_users",
                type: "text",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "TwoFactorPendingSecretExpiry",
                table: "tenant_users");

            migrationBuilder.DropColumn(
                name: "TwoFactorPendingSecretKey",
                table: "tenant_users");
        }
    }
}
