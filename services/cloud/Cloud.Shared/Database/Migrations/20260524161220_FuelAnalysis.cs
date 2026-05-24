using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Cloud.Shared.Database.Migrations
{
    /// <inheritdoc />
    public partial class FuelAnalysis : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "CalibrationFactors",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    TeamId = table.Column<int>(type: "integer", nullable: false),
                    CarNumber = table.Column<string>(type: "character varying(6)", maxLength: 6, nullable: false),
                    Value = table.Column<double>(type: "double precision", nullable: false),
                    Source = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    EffectiveAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    RaceId = table.Column<int>(type: "integer", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CalibrationFactors", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CalibrationFactors_Cars_TeamId_CarNumber",
                        columns: x => new { x.TeamId, x.CarNumber },
                        principalTable: "Cars",
                        principalColumns: new[] { "TeamId", "Number" },
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_CalibrationFactors_Races_RaceId",
                        column: x => x.RaceId,
                        principalTable: "Races",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "RefuelEvents",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    TeamId = table.Column<int>(type: "integer", nullable: false),
                    CarNumber = table.Column<string>(type: "character varying(6)", maxLength: 6, nullable: false),
                    RaceId = table.Column<int>(type: "integer", nullable: false),
                    DetectedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    EnteredFuelGallons = table.Column<double>(type: "double precision", nullable: true),
                    EnteredAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    ConfidenceTier = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    AnchorFlags = table.Column<int>(type: "integer", nullable: false),
                    Source = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    EcuResetState = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    Status = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RefuelEvents", x => x.Id);
                    table.ForeignKey(
                        name: "FK_RefuelEvents_Cars_TeamId_CarNumber",
                        columns: x => new { x.TeamId, x.CarNumber },
                        principalTable: "Cars",
                        principalColumns: new[] { "TeamId", "Number" },
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_RefuelEvents_Races_RaceId",
                        column: x => x.RaceId,
                        principalTable: "Races",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "TeamFuelDefaults",
                columns: table => new
                {
                    TeamId = table.Column<int>(type: "integer", nullable: false),
                    GreenMultiplier = table.Column<double>(type: "double precision", nullable: false),
                    YellowMultiplier = table.Column<double>(type: "double precision", nullable: false),
                    Code60Multiplier = table.Column<double>(type: "double precision", nullable: false),
                    Code35Multiplier = table.Column<double>(type: "double precision", nullable: false),
                    RedMultiplier = table.Column<double>(type: "double precision", nullable: false),
                    HighConfidenceThreshold = table.Column<double>(type: "double precision", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TeamFuelDefaults", x => x.TeamId);
                    table.ForeignKey(
                        name: "FK_TeamFuelDefaults_Teams_TeamId",
                        column: x => x.TeamId,
                        principalTable: "Teams",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "FuelWindows",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    TeamId = table.Column<int>(type: "integer", nullable: false),
                    CarNumber = table.Column<string>(type: "character varying(6)", maxLength: 6, nullable: false),
                    RaceId = table.Column<int>(type: "integer", nullable: false),
                    StartRefuelEventId = table.Column<int>(type: "integer", nullable: false),
                    EndRefuelEventId = table.Column<int>(type: "integer", nullable: true),
                    OpenedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    ClosedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    ObservedConsumptionGalPerMin = table.Column<double>(type: "double precision", nullable: true),
                    ObservedDurationSeconds = table.Column<double>(type: "double precision", nullable: true),
                    ClosedBySessionEnd = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FuelWindows", x => x.Id);
                    table.ForeignKey(
                        name: "FK_FuelWindows_Cars_TeamId_CarNumber",
                        columns: x => new { x.TeamId, x.CarNumber },
                        principalTable: "Cars",
                        principalColumns: new[] { "TeamId", "Number" },
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_FuelWindows_Races_RaceId",
                        column: x => x.RaceId,
                        principalTable: "Races",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_FuelWindows_RefuelEvents_EndRefuelEventId",
                        column: x => x.EndRefuelEventId,
                        principalTable: "RefuelEvents",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_FuelWindows_RefuelEvents_StartRefuelEventId",
                        column: x => x.StartRefuelEventId,
                        principalTable: "RefuelEvents",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "Stints",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    TeamId = table.Column<int>(type: "integer", nullable: false),
                    CarNumber = table.Column<string>(type: "character varying(6)", maxLength: 6, nullable: false),
                    RaceId = table.Column<int>(type: "integer", nullable: false),
                    FuelWindowId = table.Column<int>(type: "integer", nullable: false),
                    StartAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    EndAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    DriverId = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Stints", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Stints_Cars_TeamId_CarNumber",
                        columns: x => new { x.TeamId, x.CarNumber },
                        principalTable: "Cars",
                        principalColumns: new[] { "TeamId", "Number" },
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_Stints_FuelWindows_FuelWindowId",
                        column: x => x.FuelWindowId,
                        principalTable: "FuelWindows",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_Stints_Races_RaceId",
                        column: x => x.RaceId,
                        principalTable: "Races",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_CalibrationFactors_RaceId",
                table: "CalibrationFactors",
                column: "RaceId");

            migrationBuilder.CreateIndex(
                name: "IX_CalibrationFactors_TeamId_CarNumber_EffectiveAt",
                table: "CalibrationFactors",
                columns: new[] { "TeamId", "CarNumber", "EffectiveAt" });

            migrationBuilder.CreateIndex(
                name: "IX_FuelWindows_EndRefuelEventId",
                table: "FuelWindows",
                column: "EndRefuelEventId");

            migrationBuilder.CreateIndex(
                name: "IX_FuelWindows_RaceId",
                table: "FuelWindows",
                column: "RaceId");

            migrationBuilder.CreateIndex(
                name: "IX_FuelWindows_StartRefuelEventId",
                table: "FuelWindows",
                column: "StartRefuelEventId");

            migrationBuilder.CreateIndex(
                name: "IX_FuelWindows_TeamId_CarNumber_RaceId",
                table: "FuelWindows",
                columns: new[] { "TeamId", "CarNumber", "RaceId" },
                unique: true,
                filter: "\"ClosedAt\" IS NULL");

            migrationBuilder.CreateIndex(
                name: "IX_FuelWindows_TeamId_CarNumber_RaceId_OpenedAt",
                table: "FuelWindows",
                columns: new[] { "TeamId", "CarNumber", "RaceId", "OpenedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_RefuelEvents_RaceId",
                table: "RefuelEvents",
                column: "RaceId");

            migrationBuilder.CreateIndex(
                name: "IX_RefuelEvents_TeamId_CarNumber_RaceId_DetectedAt",
                table: "RefuelEvents",
                columns: new[] { "TeamId", "CarNumber", "RaceId", "DetectedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_Stints_FuelWindowId",
                table: "Stints",
                column: "FuelWindowId");

            migrationBuilder.CreateIndex(
                name: "IX_Stints_RaceId",
                table: "Stints",
                column: "RaceId");

            migrationBuilder.CreateIndex(
                name: "IX_Stints_TeamId_CarNumber_RaceId_StartAt",
                table: "Stints",
                columns: new[] { "TeamId", "CarNumber", "RaceId", "StartAt" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "CalibrationFactors");

            migrationBuilder.DropTable(
                name: "Stints");

            migrationBuilder.DropTable(
                name: "TeamFuelDefaults");

            migrationBuilder.DropTable(
                name: "FuelWindows");

            migrationBuilder.DropTable(
                name: "RefuelEvents");
        }
    }
}
