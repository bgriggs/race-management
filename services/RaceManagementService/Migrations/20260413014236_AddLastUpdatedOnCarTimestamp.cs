using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RaceManagementService.Migrations
{
    /// <inheritdoc />
    public partial class AddLastUpdatedOnCarTimestamp : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "LastUpdatedOnCarTimestamp",
                table: "CarConfigurations",
                type: "TEXT",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "LastUpdatedOnCarTimestamp",
                table: "CarConfigurations");
        }
    }
}
