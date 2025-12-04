using System;
using Microsoft.EntityFrameworkCore.Migrations;
using NetTopologySuite.Geometries;

#nullable disable

namespace CoralLedger.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddCitizenObservations : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "citizen_observations",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Location = table.Column<Point>(type: "geometry(Point, 4326)", nullable: false),
                    ObservationTime = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    Title = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Description = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    Type = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    Severity = table.Column<int>(type: "integer", nullable: false),
                    CitizenEmail = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true),
                    CitizenName = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    IsInMpa = table.Column<bool>(type: "boolean", nullable: true),
                    MarineProtectedAreaId = table.Column<Guid>(type: "uuid", nullable: true),
                    ReefId = table.Column<Guid>(type: "uuid", nullable: true),
                    Status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    ModerationNotes = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    ModeratedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatedBy = table.Column<string>(type: "text", nullable: true),
                    ModifiedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    ModifiedBy = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_citizen_observations", x => x.Id);
                    table.ForeignKey(
                        name: "FK_citizen_observations_marine_protected_areas_MarineProtected~",
                        column: x => x.MarineProtectedAreaId,
                        principalTable: "marine_protected_areas",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_citizen_observations_reefs_ReefId",
                        column: x => x.ReefId,
                        principalTable: "reefs",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "observation_photos",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    BlobName = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    BlobUri = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: false),
                    ContentType = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    FileSizeBytes = table.Column<long>(type: "bigint", nullable: false),
                    Caption = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    DisplayOrder = table.Column<int>(type: "integer", nullable: false),
                    UploadedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CitizenObservationId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_observation_photos", x => x.Id);
                    table.ForeignKey(
                        name: "FK_observation_photos_citizen_observations_CitizenObservationId",
                        column: x => x.CitizenObservationId,
                        principalTable: "citizen_observations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_citizen_observations_CreatedAt",
                table: "citizen_observations",
                column: "CreatedAt");

            migrationBuilder.CreateIndex(
                name: "IX_citizen_observations_Location",
                table: "citizen_observations",
                column: "Location")
                .Annotation("Npgsql:IndexMethod", "GIST");

            migrationBuilder.CreateIndex(
                name: "IX_citizen_observations_MarineProtectedAreaId",
                table: "citizen_observations",
                column: "MarineProtectedAreaId");

            migrationBuilder.CreateIndex(
                name: "IX_citizen_observations_ObservationTime",
                table: "citizen_observations",
                column: "ObservationTime");

            migrationBuilder.CreateIndex(
                name: "IX_citizen_observations_ReefId",
                table: "citizen_observations",
                column: "ReefId");

            migrationBuilder.CreateIndex(
                name: "IX_citizen_observations_Status",
                table: "citizen_observations",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_citizen_observations_Type",
                table: "citizen_observations",
                column: "Type");

            migrationBuilder.CreateIndex(
                name: "IX_observation_photos_CitizenObservationId",
                table: "observation_photos",
                column: "CitizenObservationId");

            migrationBuilder.CreateIndex(
                name: "IX_observation_photos_UploadedAt",
                table: "observation_photos",
                column: "UploadedAt");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "observation_photos");

            migrationBuilder.DropTable(
                name: "citizen_observations");
        }
    }
}
