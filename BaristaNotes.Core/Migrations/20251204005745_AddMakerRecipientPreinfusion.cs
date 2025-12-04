using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BaristaNotes.Core.Migrations
{
    /// <inheritdoc />
    public partial class AddMakerRecipientPreinfusion : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Add MadeById column
            migrationBuilder.AddColumn<int>(
                name: "MadeById",
                table: "ShotRecords",
                type: "INTEGER",
                nullable: true);

            // Add MadeForId column
            migrationBuilder.AddColumn<int>(
                name: "MadeForId",
                table: "ShotRecords",
                type: "INTEGER",
                nullable: true);

            // Add PreinfusionTime column
            migrationBuilder.AddColumn<decimal>(
                name: "PreinfusionTime",
                table: "ShotRecords",
                type: "TEXT",
                precision: 5,
                scale: 2,
                nullable: true);

            // Create index for MadeById
            migrationBuilder.CreateIndex(
                name: "IX_ShotRecords_MadeById",
                table: "ShotRecords",
                column: "MadeById");

            // Create index for MadeForId
            migrationBuilder.CreateIndex(
                name: "IX_ShotRecords_MadeForId",
                table: "ShotRecords",
                column: "MadeForId");

            // Add foreign key for MadeById
            migrationBuilder.AddForeignKey(
                name: "FK_ShotRecords_UserProfiles_MadeById",
                table: "ShotRecords",
                column: "MadeById",
                principalTable: "UserProfiles",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);

            // Add foreign key for MadeForId
            migrationBuilder.AddForeignKey(
                name: "FK_ShotRecords_UserProfiles_MadeForId",
                table: "ShotRecords",
                column: "MadeForId",
                principalTable: "UserProfiles",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Drop foreign keys
            migrationBuilder.DropForeignKey(
                name: "FK_ShotRecords_UserProfiles_MadeById",
                table: "ShotRecords");

            migrationBuilder.DropForeignKey(
                name: "FK_ShotRecords_UserProfiles_MadeForId",
                table: "ShotRecords");

            // Drop indexes
            migrationBuilder.DropIndex(
                name: "IX_ShotRecords_MadeById",
                table: "ShotRecords");

            migrationBuilder.DropIndex(
                name: "IX_ShotRecords_MadeForId",
                table: "ShotRecords");

            // Drop columns
            migrationBuilder.DropColumn(
                name: "MadeById",
                table: "ShotRecords");

            migrationBuilder.DropColumn(
                name: "MadeForId",
                table: "ShotRecords");

            migrationBuilder.DropColumn(
                name: "PreinfusionTime",
                table: "ShotRecords");
        }
    }
}
