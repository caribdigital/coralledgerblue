using System;
using Microsoft.EntityFrameworkCore.Migrations;
using NetTopologySuite.Geometries;

#nullable disable

namespace CoralLedger.Blue.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddTenantIdColumns : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "TenantId",
                table: "reefs",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<Guid>(
                name: "TenantId",
                table: "marine_protected_areas",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<Guid>(
                name: "TenantId",
                table: "bleaching_alerts",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<Guid>(
                name: "TenantId",
                table: "api_clients",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.CreateTable(
                name: "tenants",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Slug = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Description = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    DeactivatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    RegionCode = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: true),
                    EezBoundary = table.Column<Geometry>(type: "geometry", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatedBy = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    ModifiedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    ModifiedBy = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_tenants", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "tenant_brandings",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    CustomDomain = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true),
                    UseCustomDomain = table.Column<bool>(type: "boolean", nullable: false),
                    LogoUrl = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    FaviconUrl = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    PrimaryColor = table.Column<string>(type: "character varying(7)", maxLength: 7, nullable: true),
                    SecondaryColor = table.Column<string>(type: "character varying(7)", maxLength: 7, nullable: true),
                    AccentColor = table.Column<string>(type: "character varying(7)", maxLength: 7, nullable: true),
                    ApplicationTitle = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    Tagline = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    WelcomeMessage = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatedBy = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    ModifiedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    ModifiedBy = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_tenant_brandings", x => x.Id);
                    table.ForeignKey(
                        name: "FK_tenant_brandings_tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "tenant_configurations",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    WdpaApiToken = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    CustomMpaSourceUrl = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    EnableAutomaticMpaSync = table.Column<bool>(type: "boolean", nullable: false),
                    AllowCrossTenantDataSharing = table.Column<bool>(type: "boolean", nullable: false),
                    SharedDataTenantIds = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    EnableVesselTracking = table.Column<bool>(type: "boolean", nullable: false),
                    EnableBleachingAlerts = table.Column<bool>(type: "boolean", nullable: false),
                    EnableCitizenScience = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatedBy = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    ModifiedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    ModifiedBy = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_tenant_configurations", x => x.Id);
                    table.ForeignKey(
                        name: "FK_tenant_configurations_tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "tenant_users",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    Email = table.Column<string>(type: "character varying(254)", maxLength: 254, nullable: false),
                    FullName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    Role = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    LastLoginAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatedBy = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    ModifiedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    ModifiedBy = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_tenant_users", x => x.Id);
                    table.ForeignKey(
                        name: "FK_tenant_users_tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_reefs_TenantId",
                table: "reefs",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_marine_protected_areas_TenantId",
                table: "marine_protected_areas",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_bleaching_alerts_TenantId",
                table: "bleaching_alerts",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_api_clients_TenantId",
                table: "api_clients",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_tenant_brandings_CustomDomain",
                table: "tenant_brandings",
                column: "CustomDomain");

            migrationBuilder.CreateIndex(
                name: "IX_tenant_brandings_TenantId",
                table: "tenant_brandings",
                column: "TenantId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_tenant_configurations_TenantId",
                table: "tenant_configurations",
                column: "TenantId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_tenant_users_Email",
                table: "tenant_users",
                column: "Email");

            migrationBuilder.CreateIndex(
                name: "IX_tenant_users_IsActive",
                table: "tenant_users",
                column: "IsActive");

            migrationBuilder.CreateIndex(
                name: "IX_tenant_users_TenantId",
                table: "tenant_users",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_tenant_users_TenantId_Email",
                table: "tenant_users",
                columns: new[] { "TenantId", "Email" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_tenants_IsActive",
                table: "tenants",
                column: "IsActive");

            migrationBuilder.CreateIndex(
                name: "IX_tenants_RegionCode",
                table: "tenants",
                column: "RegionCode");

            migrationBuilder.CreateIndex(
                name: "IX_tenants_Slug",
                table: "tenants",
                column: "Slug",
                unique: true);

            // Insert default tenant for existing data
            migrationBuilder.Sql(@"
                INSERT INTO tenants (""Id"", ""Name"", ""Slug"", ""Description"", ""IsActive"", ""RegionCode"", ""CreatedAt"")
                VALUES ('00000000-0000-0000-0000-000000000000', 'Bahamas', 'bahamas', 'Default tenant for Bahamas marine data', true, 'BS', NOW())
                ON CONFLICT (""Id"") DO NOTHING;
            ");

            migrationBuilder.AddForeignKey(
                name: "FK_api_clients_tenants_TenantId",
                table: "api_clients",
                column: "TenantId",
                principalTable: "tenants",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_marine_protected_areas_tenants_TenantId",
                table: "marine_protected_areas",
                column: "TenantId",
                principalTable: "tenants",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_api_clients_tenants_TenantId",
                table: "api_clients");

            migrationBuilder.DropForeignKey(
                name: "FK_marine_protected_areas_tenants_TenantId",
                table: "marine_protected_areas");

            migrationBuilder.DropTable(
                name: "tenant_brandings");

            migrationBuilder.DropTable(
                name: "tenant_configurations");

            migrationBuilder.DropTable(
                name: "tenant_users");

            migrationBuilder.DropTable(
                name: "tenants");

            migrationBuilder.DropIndex(
                name: "IX_reefs_TenantId",
                table: "reefs");

            migrationBuilder.DropIndex(
                name: "IX_marine_protected_areas_TenantId",
                table: "marine_protected_areas");

            migrationBuilder.DropIndex(
                name: "IX_bleaching_alerts_TenantId",
                table: "bleaching_alerts");

            migrationBuilder.DropIndex(
                name: "IX_api_clients_TenantId",
                table: "api_clients");

            migrationBuilder.DropColumn(
                name: "TenantId",
                table: "reefs");

            migrationBuilder.DropColumn(
                name: "TenantId",
                table: "marine_protected_areas");

            migrationBuilder.DropColumn(
                name: "TenantId",
                table: "bleaching_alerts");

            migrationBuilder.DropColumn(
                name: "TenantId",
                table: "api_clients");
        }
    }
}
