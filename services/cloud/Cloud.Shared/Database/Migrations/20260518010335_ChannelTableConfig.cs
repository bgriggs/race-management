using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Cloud.Shared.Database.Migrations
{
    /// <inheritdoc />
    public partial class ChannelTableConfig : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "CarConfiguration",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TeamId = table.Column<int>(type: "integer", nullable: false),
                    Car = table.Column<string>(type: "text", nullable: false),
                    LastUpdated = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    LastUpdatedOnCarTimestamp = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    ConfigurationSchemaVersion = table.Column<int>(type: "integer", nullable: false),
                    Name = table.Column<string>(type: "text", nullable: false),
                    Notes = table.Column<string>(type: "character varying(1024)", maxLength: 1024, nullable: false),
                    ConfigurationJson = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CarConfiguration", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CarConfiguration_Teams_TeamId",
                        column: x => x.TeamId,
                        principalTable: "Teams",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ChannelStatusTableConfigurations",
                columns: table => new
                {
                    TeamId = table.Column<int>(type: "integer", nullable: false),
                    UserId = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ChannelStatusTableConfigurations", x => new { x.TeamId, x.UserId });
                    table.ForeignKey(
                        name: "FK_ChannelStatusTableConfigurations_Teams_TeamId",
                        column: x => x.TeamId,
                        principalTable: "Teams",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ChannelStatusTableColumnConfiguration",
                columns: table => new
                {
                    ChannelDefinitionId = table.Column<Guid>(type: "uuid", nullable: false),
                    Order = table.Column<int>(type: "integer", nullable: false),
                    NameOverride = table.Column<string>(type: "text", nullable: true),
                    ChannelStatusTableConfigurationTeamId = table.Column<int>(type: "integer", nullable: true),
                    ChannelStatusTableConfigurationUserId = table.Column<string>(type: "character varying(128)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ChannelStatusTableColumnConfiguration", x => x.ChannelDefinitionId);
                    table.ForeignKey(
                        name: "FK_ChannelStatusTableColumnConfiguration_ChannelStatusTableCon~",
                        columns: x => new { x.ChannelStatusTableConfigurationTeamId, x.ChannelStatusTableConfigurationUserId },
                        principalTable: "ChannelStatusTableConfigurations",
                        principalColumns: new[] { "TeamId", "UserId" });
                });

            migrationBuilder.CreateIndex(
                name: "IX_CarConfiguration_TeamId_Car",
                table: "CarConfiguration",
                columns: new[] { "TeamId", "Car" });

            migrationBuilder.CreateIndex(
                name: "IX_ChannelStatusTableColumnConfiguration_ChannelStatusTableCon~",
                table: "ChannelStatusTableColumnConfiguration",
                columns: new[] { "ChannelStatusTableConfigurationTeamId", "ChannelStatusTableConfigurationUserId" });

            migrationBuilder.CreateIndex(
                name: "IX_ChannelStatusTableConfigurations_TeamId_UserId",
                table: "ChannelStatusTableConfigurations",
                columns: new[] { "TeamId", "UserId" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "CarConfiguration");

            migrationBuilder.DropTable(
                name: "ChannelStatusTableColumnConfiguration");

            migrationBuilder.DropTable(
                name: "ChannelStatusTableConfigurations");
        }
    }
}
