using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Cloud.Shared.Database.Migrations
{
    /// <inheritdoc />
    public partial class AddStintOriginType : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Backfill existing rows with "Auto" — every Stint that already exists was
            // created by telemetry-driven flows (StintLifecycle, SessionLifecycleHandler).
            // Manual is reserved for engineer-entered stints from the new Add/Edit modal.
            migrationBuilder.AddColumn<string>(
                name: "OriginType",
                table: "Stints",
                type: "character varying(16)",
                maxLength: 16,
                nullable: false,
                defaultValue: "Auto");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "OriginType",
                table: "Stints");
        }
    }
}
