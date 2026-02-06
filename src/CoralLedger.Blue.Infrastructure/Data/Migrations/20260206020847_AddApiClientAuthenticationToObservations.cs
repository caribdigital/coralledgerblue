using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CoralLedger.Blue.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddApiClientAuthenticationToObservations : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "ApiClientId",
                table: "citizen_observations",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsEmailVerified",
                table: "citizen_observations",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.CreateIndex(
                name: "IX_citizen_observations_ApiClientId",
                table: "citizen_observations",
                column: "ApiClientId");

            migrationBuilder.CreateIndex(
                name: "IX_citizen_observations_IsEmailVerified",
                table: "citizen_observations",
                column: "IsEmailVerified");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_citizen_observations_ApiClientId",
                table: "citizen_observations");

            migrationBuilder.DropIndex(
                name: "IX_citizen_observations_IsEmailVerified",
                table: "citizen_observations");

            migrationBuilder.DropColumn(
                name: "ApiClientId",
                table: "citizen_observations");

            migrationBuilder.DropColumn(
                name: "IsEmailVerified",
                table: "citizen_observations");
        }
    }
}
