using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BaristaNotes.Core.Migrations
{
    /// <inheritdoc />
    public partial class AddContextToUserProfile : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Context",
                table: "UserProfiles",
                type: "TEXT",
                maxLength: 2000,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Context",
                table: "UserProfiles");
        }
    }
}
