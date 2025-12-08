using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BaristaNotes.Core.Migrations
{
    /// <inheritdoc />
    public partial class AddBagEntity : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // STEP 1: Create Bags table first (before modifying ShotRecords)
            migrationBuilder.CreateTable(
                name: "Bags",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    BeanId = table.Column<int>(type: "INTEGER", nullable: false),
                    RoastDate = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
                    Notes = table.Column<string>(type: "TEXT", maxLength: 500, nullable: true),
                    IsComplete = table.Column<bool>(type: "INTEGER", nullable: false, defaultValue: false),
                    IsActive = table.Column<bool>(type: "INTEGER", nullable: false, defaultValue: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
                    SyncId = table.Column<Guid>(type: "TEXT", nullable: false),
                    LastModifiedAt = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
                    IsDeleted = table.Column<bool>(type: "INTEGER", nullable: false, defaultValue: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Bags", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Bags_Beans_BeanId",
                        column: x => x.BeanId,
                        principalTable: "Beans",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            // STEP 2: Add nullable BagId column to ShotRecords (allows gradual data migration)
            migrationBuilder.AddColumn<int>(
                name: "BagId",
                table: "ShotRecords",
                type: "INTEGER",
                nullable: true); // Initially nullable to allow data migration

            // STEP 3: Seed Bags from existing Beans with RoastDate
            // This preserves all existing Bean data by creating corresponding Bags
            migrationBuilder.Sql(@"
                INSERT INTO Bags (BeanId, RoastDate, IsComplete, IsActive, CreatedAt, SyncId, LastModifiedAt, IsDeleted)
                SELECT 
                    Id, 
                    COALESCE(RoastDate, datetime('now')), 
                    0, 
                    IsActive, 
                    CreatedAt, 
                    lower(hex(randomblob(16))), 
                    LastModifiedAt, 
                    IsDeleted
                FROM Beans 
                WHERE RoastDate IS NOT NULL
            ");

            // STEP 3b: Handle edge case - Beans without RoastDate (create default bag with current date)
            // Ensures every Bean has at least one Bag
            migrationBuilder.Sql(@"
                INSERT INTO Bags (BeanId, RoastDate, IsComplete, IsActive, CreatedAt, SyncId, LastModifiedAt, IsDeleted)
                SELECT 
                    Id, 
                    datetime('now'), 
                    0, 
                    IsActive, 
                    CreatedAt, 
                    lower(hex(randomblob(16))), 
                    LastModifiedAt, 
                    IsDeleted
                FROM Beans 
                WHERE RoastDate IS NULL AND Id NOT IN (SELECT BeanId FROM Bags)
            ");

            // STEP 4: Migrate ShotRecords.BeanId to ShotRecords.BagId
            // Links each shot to the first Bag for its Bean (preserves all shot data)
            migrationBuilder.Sql(@"
                UPDATE ShotRecords 
                SET BagId = (
                    SELECT Id 
                    FROM Bags 
                    WHERE Bags.BeanId = ShotRecords.BeanId 
                    ORDER BY RoastDate ASC 
                    LIMIT 1
                )
                WHERE BeanId IS NOT NULL
            ");

            // STEP 5: Make BagId required now that all ShotRecords have valid BagId values
            migrationBuilder.AlterColumn<int>(
                name: "BagId",
                table: "ShotRecords",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0,
                oldClrType: typeof(int),
                oldType: "INTEGER",
                oldNullable: true);

            // STEP 6: Create indexes for performance (rating queries and bag lookups)
            migrationBuilder.CreateIndex(
                name: "IX_ShotRecords_BagId",
                table: "ShotRecords",
                column: "BagId");

            migrationBuilder.CreateIndex(
                name: "IX_ShotRecords_BagId_Rating",
                table: "ShotRecords",
                columns: new[] { "BagId", "Rating" });

            migrationBuilder.CreateIndex(
                name: "IX_Bags_BeanId",
                table: "Bags",
                column: "BeanId");

            migrationBuilder.CreateIndex(
                name: "IX_Bags_BeanId_IsComplete_RoastDate",
                table: "Bags",
                columns: new[] { "BeanId", "IsComplete", "RoastDate" },
                descending: new[] { false, false, true });

            migrationBuilder.CreateIndex(
                name: "IX_Bags_BeanId_RoastDate",
                table: "Bags",
                columns: new[] { "BeanId", "RoastDate" },
                descending: new[] { false, true });

            migrationBuilder.CreateIndex(
                name: "IX_Bags_SyncId",
                table: "Bags",
                column: "SyncId",
                unique: true);

            // STEP 7: Add FK constraint from ShotRecords to Bags
            migrationBuilder.AddForeignKey(
                name: "FK_ShotRecords_Bags_BagId",
                table: "ShotRecords",
                column: "BagId",
                principalTable: "Bags",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            // STEP 8: Drop old BeanId FK and index from ShotRecords (data already migrated)
            migrationBuilder.DropForeignKey(
                name: "FK_ShotRecords_Beans_BeanId",
                table: "ShotRecords");

            migrationBuilder.DropIndex(
                name: "IX_ShotRecords_BeanId",
                table: "ShotRecords");

            migrationBuilder.DropColumn(
                name: "BeanId",
                table: "ShotRecords");

            // STEP 9: Drop RoastDate from Beans (data preserved in Bags table)
            migrationBuilder.DropColumn(
                name: "RoastDate",
                table: "Beans");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // STEP 1: Add RoastDate back to Beans
            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "RoastDate",
                table: "Beans",
                type: "TEXT",
                nullable: true);

            // STEP 2: Add BeanId back to ShotRecords
            migrationBuilder.AddColumn<int>(
                name: "BeanId",
                table: "ShotRecords",
                type: "INTEGER",
                nullable: true);

            // STEP 3: Restore Bean.RoastDate from first Bag
            migrationBuilder.Sql(@"
                UPDATE Beans 
                SET RoastDate = (
                    SELECT RoastDate 
                    FROM Bags 
                    WHERE Bags.BeanId = Beans.Id 
                    ORDER BY RoastDate ASC 
                    LIMIT 1
                )
                WHERE Id IN (SELECT DISTINCT BeanId FROM Bags)
            ");

            // STEP 4: Restore ShotRecords.BeanId from Bag relationship
            migrationBuilder.Sql(@"
                UPDATE ShotRecords 
                SET BeanId = (
                    SELECT BeanId 
                    FROM Bags 
                    WHERE Bags.Id = ShotRecords.BagId
                )
                WHERE BagId IS NOT NULL
            ");

            // STEP 5: Drop FK from ShotRecords to Bags
            migrationBuilder.DropForeignKey(
                name: "FK_ShotRecords_Bags_BagId",
                table: "ShotRecords");

            // STEP 6: Drop Bags table
            migrationBuilder.DropTable(
                name: "Bags");

            // STEP 7: Drop BagId indexes
            migrationBuilder.DropIndex(
                name: "IX_ShotRecords_BagId",
                table: "ShotRecords");

            migrationBuilder.DropIndex(
                name: "IX_ShotRecords_BagId_Rating",
                table: "ShotRecords");

            // STEP 8: Drop BagId column
            migrationBuilder.DropColumn(
                name: "BagId",
                table: "ShotRecords");

            // STEP 9: Recreate BeanId index and FK
            migrationBuilder.CreateIndex(
                name: "IX_ShotRecords_BeanId",
                table: "ShotRecords",
                column: "BeanId");

            migrationBuilder.AddForeignKey(
                name: "FK_ShotRecords_Beans_BeanId",
                table: "ShotRecords",
                column: "BeanId",
                principalTable: "Beans",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }
    }
}
