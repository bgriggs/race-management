using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Cloud.Shared.Database.Migrations
{
    /// <inheritdoc />
    public partial class AddRaceRedMistAccessCode : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Optional access code for RedMist private events. Nullable so public-event Races
            // are unaffected; 6-char max matches the model-level StringLength constraint and
            // RedMist's code format.
            migrationBuilder.AddColumn<string>(
                name: "RedMistAccessCode",
                table: "Races",
                type: "character varying(6)",
                maxLength: 6,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "RedMistAccessCode",
                table: "Races");
        }
    }
}
