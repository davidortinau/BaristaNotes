using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BaristaNotes.Core.Migrations
{
    /// <inheritdoc />
    public partial class AddGrinderProfileAndGrindTranslationCache : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "GrinderProfiles",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    EquipmentId = table.Column<int>(type: "INTEGER", nullable: false),
                    MinSetting = table.Column<decimal>(type: "TEXT", precision: 6, scale: 2, nullable: true),
                    MaxSetting = table.Column<decimal>(type: "TEXT", precision: 6, scale: 2, nullable: true),
                    StepSize = table.Column<decimal>(type: "TEXT", precision: 6, scale: 3, nullable: true),
                    AnchorsJson = table.Column<string>(type: "TEXT", maxLength: 4000, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    LastModifiedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    SyncId = table.Column<Guid>(type: "TEXT", nullable: false),
                    IsDeleted = table.Column<bool>(type: "INTEGER", nullable: false, defaultValue: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_GrinderProfiles", x => x.Id);
                    table.ForeignKey(
                        name: "FK_GrinderProfiles_Equipment_EquipmentId",
                        column: x => x.EquipmentId,
                        principalTable: "Equipment",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "GrindTranslationCache",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    GrinderModelNormalized = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    GrindHintNormalized = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    BrewMethod = table.Column<int>(type: "INTEGER", nullable: false),
                    MinSetting = table.Column<decimal>(type: "TEXT", precision: 6, scale: 2, nullable: true),
                    MaxSetting = table.Column<decimal>(type: "TEXT", precision: 6, scale: 2, nullable: true),
                    SuggestedSetting = table.Column<decimal>(type: "TEXT", precision: 6, scale: 2, nullable: true),
                    Confidence = table.Column<string>(type: "TEXT", maxLength: 10, nullable: false),
                    Source = table.Column<string>(type: "TEXT", maxLength: 20, nullable: false),
                    Explanation = table.Column<string>(type: "TEXT", maxLength: 500, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    ExpiresAt = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_GrindTranslationCache", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_GrinderProfiles_EquipmentId",
                table: "GrinderProfiles",
                column: "EquipmentId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_GrinderProfiles_SyncId",
                table: "GrinderProfiles",
                column: "SyncId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_GrindTranslationCache_ExpiresAt",
                table: "GrindTranslationCache",
                column: "ExpiresAt");

            migrationBuilder.CreateIndex(
                name: "IX_GrindTranslationCache_Key",
                table: "GrindTranslationCache",
                columns: new[] { "GrinderModelNormalized", "GrindHintNormalized", "BrewMethod" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "GrinderProfiles");

            migrationBuilder.DropTable(
                name: "GrindTranslationCache");
        }
    }
}
