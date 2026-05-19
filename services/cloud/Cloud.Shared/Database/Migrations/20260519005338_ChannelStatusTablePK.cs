using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Cloud.Shared.Database.Migrations
{
    /// <inheritdoc />
    public partial class ChannelStatusTablePK : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_ChannelStatusTableColumnConfiguration_ChannelStatusTableCon~",
                table: "ChannelStatusTableColumnConfiguration");

            migrationBuilder.DropPrimaryKey(
                name: "PK_ChannelStatusTableColumnConfiguration",
                table: "ChannelStatusTableColumnConfiguration");

            migrationBuilder.DropIndex(
                name: "IX_ChannelStatusTableColumnConfiguration_ChannelStatusTableCon~",
                table: "ChannelStatusTableColumnConfiguration");

            migrationBuilder.DropColumn(
                name: "ChannelStatusTableConfigurationTeamId",
                table: "ChannelStatusTableColumnConfiguration");

            migrationBuilder.DropColumn(
                name: "ChannelStatusTableConfigurationUserId",
                table: "ChannelStatusTableColumnConfiguration");

            migrationBuilder.AddColumn<int>(
                name: "TeamId",
                table: "ChannelStatusTableColumnConfiguration",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "UserId",
                table: "ChannelStatusTableColumnConfiguration",
                type: "character varying(128)",
                maxLength: 128,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddPrimaryKey(
                name: "PK_ChannelStatusTableColumnConfiguration",
                table: "ChannelStatusTableColumnConfiguration",
                columns: new[] { "TeamId", "UserId", "ChannelDefinitionId" });

            migrationBuilder.AddForeignKey(
                name: "FK_ChannelStatusTableColumnConfiguration_ChannelStatusTableCon~",
                table: "ChannelStatusTableColumnConfiguration",
                columns: new[] { "TeamId", "UserId" },
                principalTable: "ChannelStatusTableConfigurations",
                principalColumns: new[] { "TeamId", "UserId" },
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_ChannelStatusTableColumnConfiguration_ChannelStatusTableCon~",
                table: "ChannelStatusTableColumnConfiguration");

            migrationBuilder.DropPrimaryKey(
                name: "PK_ChannelStatusTableColumnConfiguration",
                table: "ChannelStatusTableColumnConfiguration");

            migrationBuilder.DropColumn(
                name: "TeamId",
                table: "ChannelStatusTableColumnConfiguration");

            migrationBuilder.DropColumn(
                name: "UserId",
                table: "ChannelStatusTableColumnConfiguration");

            migrationBuilder.AddColumn<int>(
                name: "ChannelStatusTableConfigurationTeamId",
                table: "ChannelStatusTableColumnConfiguration",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ChannelStatusTableConfigurationUserId",
                table: "ChannelStatusTableColumnConfiguration",
                type: "character varying(128)",
                nullable: true);

            migrationBuilder.AddPrimaryKey(
                name: "PK_ChannelStatusTableColumnConfiguration",
                table: "ChannelStatusTableColumnConfiguration",
                column: "ChannelDefinitionId");

            migrationBuilder.CreateIndex(
                name: "IX_ChannelStatusTableColumnConfiguration_ChannelStatusTableCon~",
                table: "ChannelStatusTableColumnConfiguration",
                columns: new[] { "ChannelStatusTableConfigurationTeamId", "ChannelStatusTableConfigurationUserId" });

            migrationBuilder.AddForeignKey(
                name: "FK_ChannelStatusTableColumnConfiguration_ChannelStatusTableCon~",
                table: "ChannelStatusTableColumnConfiguration",
                columns: new[] { "ChannelStatusTableConfigurationTeamId", "ChannelStatusTableConfigurationUserId" },
                principalTable: "ChannelStatusTableConfigurations",
                principalColumns: new[] { "TeamId", "UserId" });
        }
    }
}
