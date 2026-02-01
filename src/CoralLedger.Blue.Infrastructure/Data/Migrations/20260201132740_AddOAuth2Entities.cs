using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CoralLedger.Blue.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddOAuth2Entities : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "api_clients",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Description = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    OrganizationName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    ContactEmail = table.Column<string>(type: "character varying(254)", maxLength: 254, nullable: true),
                    ClientId = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    DeactivatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    DeactivationReason = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    RateLimitPerMinute = table.Column<int>(type: "integer", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatedBy = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    ModifiedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    ModifiedBy = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_api_clients", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "api_keys",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ApiClientId = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    KeyHash = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    KeyPrefix = table.Column<string>(type: "character varying(12)", maxLength: 12, nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    ExpiresAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    LastUsedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    RevokedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    RevocationReason = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    Scopes = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatedBy = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    ModifiedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    ModifiedBy = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_api_keys", x => x.Id);
                    table.ForeignKey(
                        name: "FK_api_keys_api_clients_ApiClientId",
                        column: x => x.ApiClientId,
                        principalTable: "api_clients",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "api_usage_logs",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ApiClientId = table.Column<Guid>(type: "uuid", nullable: false),
                    ApiKeyId = table.Column<Guid>(type: "uuid", nullable: true),
                    Timestamp = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    Endpoint = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    HttpMethod = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    StatusCode = table.Column<int>(type: "integer", nullable: false),
                    ResponseTimeMs = table.Column<int>(type: "integer", nullable: false),
                    IpAddress = table.Column<string>(type: "character varying(45)", maxLength: 45, nullable: true),
                    UserAgent = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    ErrorMessage = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_api_usage_logs", x => x.Id);
                    table.ForeignKey(
                        name: "FK_api_usage_logs_api_clients_ApiClientId",
                        column: x => x.ApiClientId,
                        principalTable: "api_clients",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_api_usage_logs_api_keys_ApiKeyId",
                        column: x => x.ApiKeyId,
                        principalTable: "api_keys",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateIndex(
                name: "IX_api_clients_ClientId",
                table: "api_clients",
                column: "ClientId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_api_clients_CreatedAt",
                table: "api_clients",
                column: "CreatedAt");

            migrationBuilder.CreateIndex(
                name: "IX_api_clients_IsActive",
                table: "api_clients",
                column: "IsActive");

            migrationBuilder.CreateIndex(
                name: "IX_api_keys_ApiClientId",
                table: "api_keys",
                column: "ApiClientId");

            migrationBuilder.CreateIndex(
                name: "IX_api_keys_CreatedAt",
                table: "api_keys",
                column: "CreatedAt");

            migrationBuilder.CreateIndex(
                name: "IX_api_keys_ExpiresAt",
                table: "api_keys",
                column: "ExpiresAt");

            migrationBuilder.CreateIndex(
                name: "IX_api_keys_IsActive",
                table: "api_keys",
                column: "IsActive");

            migrationBuilder.CreateIndex(
                name: "IX_api_keys_KeyHash",
                table: "api_keys",
                column: "KeyHash");

            migrationBuilder.CreateIndex(
                name: "IX_api_keys_LastUsedAt",
                table: "api_keys",
                column: "LastUsedAt");

            migrationBuilder.CreateIndex(
                name: "IX_api_usage_logs_ApiClientId",
                table: "api_usage_logs",
                column: "ApiClientId");

            migrationBuilder.CreateIndex(
                name: "IX_api_usage_logs_ApiClientId_Timestamp",
                table: "api_usage_logs",
                columns: new[] { "ApiClientId", "Timestamp" });

            migrationBuilder.CreateIndex(
                name: "IX_api_usage_logs_ApiKeyId",
                table: "api_usage_logs",
                column: "ApiKeyId");

            migrationBuilder.CreateIndex(
                name: "IX_api_usage_logs_Endpoint_Timestamp",
                table: "api_usage_logs",
                columns: new[] { "Endpoint", "Timestamp" });

            migrationBuilder.CreateIndex(
                name: "IX_api_usage_logs_StatusCode",
                table: "api_usage_logs",
                column: "StatusCode");

            migrationBuilder.CreateIndex(
                name: "IX_api_usage_logs_Timestamp",
                table: "api_usage_logs",
                column: "Timestamp");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "api_usage_logs");

            migrationBuilder.DropTable(
                name: "api_keys");

            migrationBuilder.DropTable(
                name: "api_clients");
        }
    }
}
