using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CoralLedger.Blue.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddTwoFactorAuthentication : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "TwoFactorEnabled",
                table: "tenant_users",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "TwoFactorSecretKey",
                table: "tenant_users",
                type: "text",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "TwoFactorEnabled",
                table: "tenant_users");

            migrationBuilder.DropColumn(
                name: "TwoFactorSecretKey",
                table: "tenant_users");
        }
    }
}
