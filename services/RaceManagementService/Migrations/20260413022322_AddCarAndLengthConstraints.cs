using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RaceManagementService.Migrations
{
    /// <inheritdoc />
    public partial class AddCarAndLengthConstraints : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Car",
                table: "CarConfigurations",
                type: "TEXT",
                maxLength: 6,
                nullable: false,
                defaultValue: "");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Car",
                table: "CarConfigurations");
        }
    }
}
