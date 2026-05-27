using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Cloud.Shared.Database.Migrations
{
    /// <inheritdoc />
    public partial class AddTeamSelectedRaceId : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // The Race the team is currently monitoring. Drives the ChannelProcessor's RedMist
            // subscription; null means "fall back to time-window auto-pick". Nullable so
            // existing teams start with no explicit selection.
            migrationBuilder.AddColumn<int>(
                name: "SelectedRaceId",
                table: "Teams",
                type: "integer",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "SelectedRaceId",
                table: "Teams");
        }
    }
}
