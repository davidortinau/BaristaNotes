using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BaristaNotes.Core.Migrations
{
    /// <inheritdoc />
    public partial class AddGrindMicronsDropGrindSetting : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Safety net: snapshot existing non-empty grinder-native strings into
            // a sidecar table before we drop the column. The new GrindMicrons
            // column is canonical (grinder-agnostic) and we cannot reliably
            // back-translate every prior string without per-row anchors, so we
            // intentionally do NOT auto-populate GrindMicrons. The snapshot is
            // there as a one-way recovery path if a user ever needs to reconcile
            // their history manually.
            migrationBuilder.CreateTable(
                name: "LegacyShotGrindSettings",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    ShotRecordId = table.Column<int>(type: "INTEGER", nullable: false),
                    BagId = table.Column<int>(type: "INTEGER", nullable: false),
                    GrinderId = table.Column<int>(type: "INTEGER", nullable: true),
                    BrewMethod = table.Column<int>(type: "INTEGER", nullable: false),
                    GrindSetting = table.Column<string>(type: "TEXT", maxLength: 50, nullable: false),
                    CapturedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LegacyShotGrindSettings", x => x.Id);
                });

            migrationBuilder.Sql(@"
                INSERT INTO LegacyShotGrindSettings
                    (ShotRecordId, BagId, GrinderId, BrewMethod, GrindSetting, CapturedAt)
                SELECT Id, BagId, GrinderId, BrewMethod, GrindSetting, CURRENT_TIMESTAMP
                FROM ShotRecords
                WHERE GrindSetting IS NOT NULL AND GrindSetting <> '';
            ");

            migrationBuilder.DropColumn(
                name: "GrindSetting",
                table: "ShotRecords");

            migrationBuilder.AddColumn<int>(
                name: "GrindMicrons",
                table: "ShotRecords",
                type: "INTEGER",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "GrindMicrons",
                table: "ShotRecords");

            migrationBuilder.AddColumn<string>(
                name: "GrindSetting",
                table: "ShotRecords",
                type: "TEXT",
                maxLength: 50,
                nullable: false,
                defaultValue: "");

            // Best-effort restore from the snapshot.
            migrationBuilder.Sql(@"
                UPDATE ShotRecords
                SET GrindSetting = (
                    SELECT l.GrindSetting
                    FROM LegacyShotGrindSettings l
                    WHERE l.ShotRecordId = ShotRecords.Id
                    ORDER BY l.CapturedAt DESC
                    LIMIT 1
                )
                WHERE EXISTS (
                    SELECT 1 FROM LegacyShotGrindSettings l WHERE l.ShotRecordId = ShotRecords.Id
                );
            ");

            migrationBuilder.DropTable(
                name: "LegacyShotGrindSettings");
        }
    }
}
