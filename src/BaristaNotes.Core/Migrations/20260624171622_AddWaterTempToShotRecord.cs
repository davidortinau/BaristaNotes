using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BaristaNotes.Core.Migrations
{
    /// <inheritdoc />
    public partial class AddWaterTempToShotRecord : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(
                name: "WaterTempC",
                table: "ShotRecords",
                type: "TEXT",
                precision: 5,
                scale: 2,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "WaterTempC",
                table: "ShotRecords");
        }
    }
}
