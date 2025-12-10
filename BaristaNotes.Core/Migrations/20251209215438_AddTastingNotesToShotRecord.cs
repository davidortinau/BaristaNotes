using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BaristaNotes.Core.Migrations
{
    /// <inheritdoc />
    public partial class AddTastingNotesToShotRecord : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "TastingNotes",
                table: "ShotRecords",
                type: "TEXT",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "TastingNotes",
                table: "ShotRecords");
        }
    }
}
