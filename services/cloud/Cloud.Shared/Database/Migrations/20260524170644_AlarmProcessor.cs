using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Cloud.Shared.Database.Migrations
{
    /// <inheritdoc />
    public partial class AlarmProcessor : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "AlarmDefinitions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TeamId = table.Column<int>(type: "integer", nullable: false),
                    CarNumber = table.Column<string>(type: "character varying(6)", maxLength: 6, nullable: true),
                    Name = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    Message = table.Column<string>(type: "text", nullable: false),
                    DisplayChannelSourceColorHex = table.Column<string>(type: "character varying(7)", maxLength: 7, nullable: false),
                    TimeAfterAckToDisplaySecs = table.Column<int>(type: "integer", nullable: false),
                    AlarmStatusChannelId = table.Column<Guid>(type: "uuid", nullable: true),
                    StatementJson = table.Column<string>(type: "jsonb", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AlarmDefinitions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AlarmDefinitions_Teams_TeamId",
                        column: x => x.TeamId,
                        principalTable: "Teams",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ActiveAlarms",
                columns: table => new
                {
                    TeamId = table.Column<int>(type: "integer", nullable: false),
                    CarNumber = table.Column<string>(type: "character varying(6)", maxLength: 6, nullable: false),
                    AlarmDefinitionId = table.Column<Guid>(type: "uuid", nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    IsAcknowledged = table.Column<bool>(type: "boolean", nullable: false),
                    LastActivatedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    LastAcknowledgedTimestamp = table.Column<DateTime>(type: "timestamp without time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ActiveAlarms", x => new { x.TeamId, x.CarNumber, x.AlarmDefinitionId });
                    table.ForeignKey(
                        name: "FK_ActiveAlarms_AlarmDefinitions_AlarmDefinitionId",
                        column: x => x.AlarmDefinitionId,
                        principalTable: "AlarmDefinitions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_ActiveAlarms_Cars_TeamId_CarNumber",
                        columns: x => new { x.TeamId, x.CarNumber },
                        principalTable: "Cars",
                        principalColumns: new[] { "TeamId", "Number" },
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "AlarmEvents",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    TeamId = table.Column<int>(type: "integer", nullable: false),
                    CarNumber = table.Column<string>(type: "character varying(6)", maxLength: 6, nullable: false),
                    AlarmDefinitionId = table.Column<Guid>(type: "uuid", nullable: false),
                    EventType = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    Timestamp = table.Column<DateTime>(type: "timestamp without time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AlarmEvents", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AlarmEvents_AlarmDefinitions_AlarmDefinitionId",
                        column: x => x.AlarmDefinitionId,
                        principalTable: "AlarmDefinitions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_AlarmEvents_Cars_TeamId_CarNumber",
                        columns: x => new { x.TeamId, x.CarNumber },
                        principalTable: "Cars",
                        principalColumns: new[] { "TeamId", "Number" },
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ActiveAlarms_AlarmDefinitionId",
                table: "ActiveAlarms",
                column: "AlarmDefinitionId");

            migrationBuilder.CreateIndex(
                name: "IX_ActiveAlarms_TeamId_IsActive",
                table: "ActiveAlarms",
                columns: new[] { "TeamId", "IsActive" });

            migrationBuilder.CreateIndex(
                name: "IX_AlarmDefinitions_TeamId_CarNumber",
                table: "AlarmDefinitions",
                columns: new[] { "TeamId", "CarNumber" });

            migrationBuilder.CreateIndex(
                name: "IX_AlarmEvents_AlarmDefinitionId",
                table: "AlarmEvents",
                column: "AlarmDefinitionId");

            migrationBuilder.CreateIndex(
                name: "IX_AlarmEvents_TeamId_CarNumber_AlarmDefinitionId_Timestamp",
                table: "AlarmEvents",
                columns: new[] { "TeamId", "CarNumber", "AlarmDefinitionId", "Timestamp" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ActiveAlarms");

            migrationBuilder.DropTable(
                name: "AlarmEvents");

            migrationBuilder.DropTable(
                name: "AlarmDefinitions");
        }
    }
}
