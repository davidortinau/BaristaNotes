using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BaristaNotes.Core.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Beans",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Name = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    Roaster = table.Column<string>(type: "TEXT", maxLength: 100, nullable: true),
                    RoastDate = table.Column<DateTimeOffset>(type: "TEXT", nullable: true),
                    Origin = table.Column<string>(type: "TEXT", maxLength: 100, nullable: true),
                    Notes = table.Column<string>(type: "TEXT", maxLength: 500, nullable: true),
                    IsActive = table.Column<bool>(type: "INTEGER", nullable: false, defaultValue: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
                    SyncId = table.Column<Guid>(type: "TEXT", nullable: false),
                    LastModifiedAt = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
                    IsDeleted = table.Column<bool>(type: "INTEGER", nullable: false, defaultValue: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Beans", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Equipment",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Name = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    Type = table.Column<int>(type: "INTEGER", nullable: false),
                    Notes = table.Column<string>(type: "TEXT", maxLength: 500, nullable: true),
                    IsActive = table.Column<bool>(type: "INTEGER", nullable: false, defaultValue: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
                    SyncId = table.Column<Guid>(type: "TEXT", nullable: false),
                    LastModifiedAt = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
                    IsDeleted = table.Column<bool>(type: "INTEGER", nullable: false, defaultValue: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Equipment", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "UserProfiles",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Name = table.Column<string>(type: "TEXT", maxLength: 50, nullable: false),
                    AvatarPath = table.Column<string>(type: "TEXT", maxLength: 500, nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
                    SyncId = table.Column<Guid>(type: "TEXT", nullable: false),
                    LastModifiedAt = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
                    IsDeleted = table.Column<bool>(type: "INTEGER", nullable: false, defaultValue: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UserProfiles", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ShotRecords",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Timestamp = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
                    BeanId = table.Column<int>(type: "INTEGER", nullable: true),
                    MachineId = table.Column<int>(type: "INTEGER", nullable: true),
                    GrinderId = table.Column<int>(type: "INTEGER", nullable: true),
                    MadeById = table.Column<int>(type: "INTEGER", nullable: true),
                    MadeForId = table.Column<int>(type: "INTEGER", nullable: true),
                    DoseIn = table.Column<decimal>(type: "TEXT", precision: 5, scale: 2, nullable: false),
                    GrindSetting = table.Column<string>(type: "TEXT", maxLength: 50, nullable: false),
                    ExpectedTime = table.Column<decimal>(type: "TEXT", precision: 5, scale: 2, nullable: false),
                    ExpectedOutput = table.Column<decimal>(type: "TEXT", precision: 5, scale: 2, nullable: false),
                    DrinkType = table.Column<string>(type: "TEXT", maxLength: 50, nullable: false),
                    ActualTime = table.Column<decimal>(type: "TEXT", precision: 5, scale: 2, nullable: true),
                    ActualOutput = table.Column<decimal>(type: "TEXT", precision: 5, scale: 2, nullable: true),
                    PreinfusionTime = table.Column<decimal>(type: "TEXT", precision: 5, scale: 2, nullable: true),
                    Rating = table.Column<int>(type: "INTEGER", nullable: true),
                    SyncId = table.Column<Guid>(type: "TEXT", nullable: false),
                    LastModifiedAt = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
                    IsDeleted = table.Column<bool>(type: "INTEGER", nullable: false, defaultValue: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ShotRecords", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ShotRecords_Beans_BeanId",
                        column: x => x.BeanId,
                        principalTable: "Beans",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_ShotRecords_Equipment_GrinderId",
                        column: x => x.GrinderId,
                        principalTable: "Equipment",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_ShotRecords_Equipment_MachineId",
                        column: x => x.MachineId,
                        principalTable: "Equipment",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_ShotRecords_UserProfiles_MadeById",
                        column: x => x.MadeById,
                        principalTable: "UserProfiles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_ShotRecords_UserProfiles_MadeForId",
                        column: x => x.MadeForId,
                        principalTable: "UserProfiles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "ShotEquipments",
                columns: table => new
                {
                    ShotRecordId = table.Column<int>(type: "INTEGER", nullable: false),
                    EquipmentId = table.Column<int>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ShotEquipments", x => new { x.ShotRecordId, x.EquipmentId });
                    table.ForeignKey(
                        name: "FK_ShotEquipments_Equipment_EquipmentId",
                        column: x => x.EquipmentId,
                        principalTable: "Equipment",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_ShotEquipments_ShotRecords_ShotRecordId",
                        column: x => x.ShotRecordId,
                        principalTable: "ShotRecords",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Beans_IsActive",
                table: "Beans",
                column: "IsActive");

            migrationBuilder.CreateIndex(
                name: "IX_Beans_Name_Roaster",
                table: "Beans",
                columns: new[] { "Name", "Roaster" });

            migrationBuilder.CreateIndex(
                name: "IX_Beans_SyncId",
                table: "Beans",
                column: "SyncId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Equipment_IsActive",
                table: "Equipment",
                column: "IsActive");

            migrationBuilder.CreateIndex(
                name: "IX_Equipment_Name_Type",
                table: "Equipment",
                columns: new[] { "Name", "Type" });

            migrationBuilder.CreateIndex(
                name: "IX_Equipment_SyncId",
                table: "Equipment",
                column: "SyncId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ShotEquipments_EquipmentId",
                table: "ShotEquipments",
                column: "EquipmentId");

            migrationBuilder.CreateIndex(
                name: "IX_ShotRecords_BeanId",
                table: "ShotRecords",
                column: "BeanId");

            migrationBuilder.CreateIndex(
                name: "IX_ShotRecords_GrinderId",
                table: "ShotRecords",
                column: "GrinderId");

            migrationBuilder.CreateIndex(
                name: "IX_ShotRecords_MachineId",
                table: "ShotRecords",
                column: "MachineId");

            migrationBuilder.CreateIndex(
                name: "IX_ShotRecords_MadeById",
                table: "ShotRecords",
                column: "MadeById");

            migrationBuilder.CreateIndex(
                name: "IX_ShotRecords_MadeForId",
                table: "ShotRecords",
                column: "MadeForId");

            migrationBuilder.CreateIndex(
                name: "IX_ShotRecords_SyncId",
                table: "ShotRecords",
                column: "SyncId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ShotRecords_Timestamp",
                table: "ShotRecords",
                column: "Timestamp",
                descending: new bool[0]);

            migrationBuilder.CreateIndex(
                name: "IX_UserProfiles_Name",
                table: "UserProfiles",
                column: "Name");

            migrationBuilder.CreateIndex(
                name: "IX_UserProfiles_SyncId",
                table: "UserProfiles",
                column: "SyncId",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ShotEquipments");

            migrationBuilder.DropTable(
                name: "ShotRecords");

            migrationBuilder.DropTable(
                name: "Beans");

            migrationBuilder.DropTable(
                name: "Equipment");

            migrationBuilder.DropTable(
                name: "UserProfiles");
        }
    }
}
