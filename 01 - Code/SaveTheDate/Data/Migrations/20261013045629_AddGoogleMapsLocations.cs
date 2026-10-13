using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SaveTheDate.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddGoogleMapsLocations : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "GooglePlaceId",
                table: "Modules",
                type: "TEXT",
                maxLength: 500,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "LocationAddress",
                table: "Modules",
                type: "TEXT",
                maxLength: 500,
                nullable: false,
                defaultValue: "");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "GooglePlaceId",
                table: "Modules");

            migrationBuilder.DropColumn(
                name: "LocationAddress",
                table: "Modules");
        }
    }
}
