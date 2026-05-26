using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Cloud.Shared.Database.Migrations
{
    /// <inheritdoc />
    public partial class AddRaceTimeZone : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Backfill existing rows with America/Chicago so cloud-side TZ lookups don't
            // fail on rows created before the column existed. The model-level default
            // ("America/Chicago") covers new rows; the column has no DB-level default.
            migrationBuilder.AddColumn<string>(
                name: "TimeZone",
                table: "Races",
                type: "character varying(50)",
                maxLength: 50,
                nullable: false,
                defaultValue: "America/Chicago");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "TimeZone",
                table: "Races");
        }
    }
}
