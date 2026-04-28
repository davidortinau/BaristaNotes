using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BaristaNotes.Core.Migrations
{
    /// <inheritdoc />
    public partial class AddRecipeEntity : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Recipes",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    BeanId = table.Column<int>(type: "INTEGER", nullable: false),
                    BrewMethod = table.Column<int>(type: "INTEGER", nullable: false),
                    Source = table.Column<int>(type: "INTEGER", nullable: false),
                    SourceUrl = table.Column<string>(type: "TEXT", maxLength: 500, nullable: true),
                    Title = table.Column<string>(type: "TEXT", maxLength: 200, nullable: true),
                    DoseIn = table.Column<decimal>(type: "TEXT", precision: 5, scale: 2, nullable: true),
                    OutputAmount = table.Column<decimal>(type: "TEXT", precision: 6, scale: 2, nullable: true),
                    GrindHint = table.Column<string>(type: "TEXT", maxLength: 100, nullable: true),
                    BrewTempC = table.Column<decimal>(type: "TEXT", precision: 5, scale: 2, nullable: true),
                    TotalTimeSeconds = table.Column<decimal>(type: "TEXT", precision: 6, scale: 2, nullable: true),
                    ParametersJson = table.Column<string>(type: "TEXT", maxLength: 4000, nullable: true),
                    Notes = table.Column<string>(type: "TEXT", maxLength: 2000, nullable: true),
                    FetchedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    IsEditedByUser = table.Column<bool>(type: "INTEGER", nullable: false, defaultValue: false),
                    SyncId = table.Column<Guid>(type: "TEXT", nullable: false),
                    LastModifiedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    IsDeleted = table.Column<bool>(type: "INTEGER", nullable: false, defaultValue: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Recipes", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Recipes_Beans_BeanId",
                        column: x => x.BeanId,
                        principalTable: "Beans",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Recipes_BeanId",
                table: "Recipes",
                column: "BeanId");

            migrationBuilder.CreateIndex(
                name: "IX_Recipes_BeanId_BrewMethod",
                table: "Recipes",
                columns: new[] { "BeanId", "BrewMethod" });

            migrationBuilder.CreateIndex(
                name: "IX_Recipes_SyncId",
                table: "Recipes",
                column: "SyncId",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Recipes");
        }
    }
}
