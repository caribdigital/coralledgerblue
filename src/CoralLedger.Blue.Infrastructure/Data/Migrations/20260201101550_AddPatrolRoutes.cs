using System;
using Microsoft.EntityFrameworkCore.Migrations;
using NetTopologySuite.Geometries;

#nullable disable

namespace CoralLedger.Blue.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddPatrolRoutes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "patrol_routes",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    OfficerName = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    OfficerId = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    StartTime = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    EndTime = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    Status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    Notes = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    RecordingIntervalSeconds = table.Column<int>(type: "integer", nullable: false),
                    TotalDistanceMeters = table.Column<double>(type: "double precision", nullable: true),
                    DurationSeconds = table.Column<int>(type: "integer", nullable: true),
                    RouteGeometry = table.Column<LineString>(type: "geometry(LineString, 4326)", nullable: true),
                    MarineProtectedAreaId = table.Column<Guid>(type: "uuid", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatedBy = table.Column<string>(type: "text", nullable: true),
                    ModifiedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    ModifiedBy = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_patrol_routes", x => x.Id);
                    table.ForeignKey(
                        name: "FK_patrol_routes_marine_protected_areas_MarineProtectedAreaId",
                        column: x => x.MarineProtectedAreaId,
                        principalTable: "marine_protected_areas",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "patrol_route_points",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Location = table.Column<Point>(type: "geometry(Point, 4326)", nullable: false),
                    Timestamp = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    Accuracy = table.Column<double>(type: "double precision", nullable: true),
                    Altitude = table.Column<double>(type: "double precision", nullable: true),
                    Speed = table.Column<double>(type: "double precision", nullable: true),
                    Heading = table.Column<double>(type: "double precision", nullable: true),
                    PatrolRouteId = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatedBy = table.Column<string>(type: "text", nullable: true),
                    ModifiedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    ModifiedBy = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_patrol_route_points", x => x.Id);
                    table.ForeignKey(
                        name: "FK_patrol_route_points_patrol_routes_PatrolRouteId",
                        column: x => x.PatrolRouteId,
                        principalTable: "patrol_routes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "patrol_waypoints",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Location = table.Column<Point>(type: "geometry(Point, 4326)", nullable: false),
                    Timestamp = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    Title = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Notes = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    WaypointType = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    PatrolRouteId = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatedBy = table.Column<string>(type: "text", nullable: true),
                    ModifiedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    ModifiedBy = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_patrol_waypoints", x => x.Id);
                    table.ForeignKey(
                        name: "FK_patrol_waypoints_patrol_routes_PatrolRouteId",
                        column: x => x.PatrolRouteId,
                        principalTable: "patrol_routes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_patrol_route_points_Location",
                table: "patrol_route_points",
                column: "Location")
                .Annotation("Npgsql:IndexMethod", "GIST");

            migrationBuilder.CreateIndex(
                name: "IX_patrol_route_points_PatrolRouteId",
                table: "patrol_route_points",
                column: "PatrolRouteId");

            migrationBuilder.CreateIndex(
                name: "IX_patrol_route_points_Timestamp",
                table: "patrol_route_points",
                column: "Timestamp");

            migrationBuilder.CreateIndex(
                name: "IX_patrol_routes_CreatedAt",
                table: "patrol_routes",
                column: "CreatedAt");

            migrationBuilder.CreateIndex(
                name: "IX_patrol_routes_MarineProtectedAreaId",
                table: "patrol_routes",
                column: "MarineProtectedAreaId");

            migrationBuilder.CreateIndex(
                name: "IX_patrol_routes_OfficerId",
                table: "patrol_routes",
                column: "OfficerId");

            migrationBuilder.CreateIndex(
                name: "IX_patrol_routes_RouteGeometry",
                table: "patrol_routes",
                column: "RouteGeometry")
                .Annotation("Npgsql:IndexMethod", "GIST");

            migrationBuilder.CreateIndex(
                name: "IX_patrol_routes_StartTime",
                table: "patrol_routes",
                column: "StartTime");

            migrationBuilder.CreateIndex(
                name: "IX_patrol_routes_Status",
                table: "patrol_routes",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_patrol_waypoints_Location",
                table: "patrol_waypoints",
                column: "Location")
                .Annotation("Npgsql:IndexMethod", "GIST");

            migrationBuilder.CreateIndex(
                name: "IX_patrol_waypoints_PatrolRouteId",
                table: "patrol_waypoints",
                column: "PatrolRouteId");

            migrationBuilder.CreateIndex(
                name: "IX_patrol_waypoints_Timestamp",
                table: "patrol_waypoints",
                column: "Timestamp");

            migrationBuilder.CreateIndex(
                name: "IX_patrol_waypoints_WaypointType",
                table: "patrol_waypoints",
                column: "WaypointType");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "patrol_route_points");

            migrationBuilder.DropTable(
                name: "patrol_waypoints");

            migrationBuilder.DropTable(
                name: "patrol_routes");
        }
    }
}
