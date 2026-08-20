using BaristaNotes.Core.Data;
using BaristaNotes.Tests.Helpers;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace BaristaNotes.Tests.Integration;

public sealed class DatabaseInitializerTests
{
    private const string InitialMigrationId = "20251206024345_InitialCreate";
    private const string BagMigrationId = "20251207202829_AddBagEntity";

    [Fact]
    public void Initialize_CreatesCurrentSchemaForNewDatabase()
    {
        using var connection = new SqliteConnection("Data Source=:memory:");
        connection.Open();
        using var context = CreateContext(connection);
        var initializer = CreateInitializer(context);

        initializer.Initialize();

        Assert.True(ColumnExists(connection, "ShotRecords", "WaterTempC"));
    }

    [Fact]
    public void Initialize_UpgradesInitialSchemaAndPreservesExistingRows()
    {
        using var connection = new SqliteConnection("Data Source=:memory:");
        connection.Open();
        using (var migrationContext = CreateContext(connection))
        {
            migrationContext.GetService<IMigrator>().Migrate(InitialMigrationId);
        }

        ExecuteNonQuery(connection, """
            INSERT INTO "Beans"
                ("Id", "Name", "Roaster", "RoastDate", "Origin", "Notes", "IsActive",
                 "CreatedAt", "SyncId", "LastModifiedAt", "IsDeleted")
            VALUES
                (1, 'Preserved bean', 'Test roaster', '2026-01-01T00:00:00+00:00', NULL, NULL, 1,
                 '2026-01-01T00:00:00+00:00', '00000000-0000-0000-0000-000000000001',
                 '2026-01-01T00:00:00+00:00', 0);

            INSERT INTO "ShotRecords"
                ("Id", "Timestamp", "BeanId", "MachineId", "GrinderId", "MadeById", "MadeForId",
                 "DoseIn", "GrindSetting", "ExpectedTime", "ExpectedOutput", "DrinkType",
                 "ActualTime", "ActualOutput", "PreinfusionTime", "Rating", "SyncId",
                 "LastModifiedAt", "IsDeleted")
            VALUES
                (1, '2026-01-02T00:00:00+00:00', 1, NULL, NULL, NULL, NULL,
                 '18.0', '12', '30.0', '36.0', 'Espresso',
                 '29.0', '36.0', NULL, 5, '00000000-0000-0000-0000-000000000002',
                 '2026-01-02T00:00:00+00:00', 0);

            INSERT INTO "ShotRecords"
                ("Id", "Timestamp", "BeanId", "MachineId", "GrinderId", "MadeById", "MadeForId",
                 "DoseIn", "GrindSetting", "ExpectedTime", "ExpectedOutput", "DrinkType",
                 "ActualTime", "ActualOutput", "PreinfusionTime", "Rating", "SyncId",
                 "LastModifiedAt", "IsDeleted")
            VALUES
                (2, '2026-01-03T00:00:00+00:00', NULL, NULL, NULL, NULL, NULL,
                 '18.0', '14', '30.0', '36.0', 'Espresso',
                 '31.0', '35.0', NULL, 3, '00000000-0000-0000-0000-000000000003',
                 '2026-01-03T00:00:00+00:00', 0);
            """);
        using var context = CreateContext(connection);
        var initializer = CreateInitializer(context);

        initializer.Initialize();
        initializer.Initialize();

        Assert.True(ColumnExists(connection, "ShotRecords", "WaterTempC"));
        Assert.Equal(1L, ExecuteScalar(
            connection,
            "SELECT COUNT(*) FROM \"Beans\" WHERE \"Name\" = 'Preserved bean';"));
        Assert.Equal(1L, ExecuteScalar(
            connection,
            "SELECT COUNT(*) FROM \"Bags\" WHERE \"BeanId\" = 1;"));
        Assert.Equal(1L, ExecuteScalar(
            connection,
            "SELECT COUNT(*) FROM \"ShotRecords\" WHERE \"BagId\" = 1 AND \"Rating\" = 4;"));
        Assert.Equal(2L, ExecuteScalar(
            connection,
            "SELECT COUNT(*) FROM \"LegacyShotGrindSettings\";"));
        Assert.Equal(1L, ExecuteScalar(
            connection,
            """
            SELECT COUNT(*)
            FROM "ShotRecords" AS "s"
            INNER JOIN "Bags" AS "b" ON "s"."BagId" = "b"."Id"
            INNER JOIN "Beans" AS "bean" ON "b"."BeanId" = "bean"."Id"
            WHERE "s"."Id" = 2 AND "bean"."Name" = 'Recovered shots';
            """));
        Assert.Equal(1L, ExecuteScalar(
            connection,
            """
            SELECT COUNT(*) FROM "__EFMigrationsHistory"
            WHERE "MigrationId" = '20260624171622_AddWaterTempToShotRecord';
            """));
        Assert.Equal(0L, ExecuteScalar(connection, "SELECT COUNT(*) FROM pragma_foreign_key_check;"));
    }

    [Theory]
    [InlineData("20251206024345_InitialCreate")]
    [InlineData("20251207202829_AddBagEntity")]
    [InlineData("20251209215438_AddTastingNotesToShotRecord")]
    [InlineData("20260419013842_AddRecipeEntity")]
    [InlineData("20260419023825_AddBrewMethodToShotRecord")]
    [InlineData("20260424031239_AddGrinderProfileAndGrindTranslationCache")]
    [InlineData("20260517192156_AddGrindMicronsDropGrindSetting")]
    [InlineData("20260527135350_ShiftRatingScaleTo04")]
    [InlineData("20260527155808_AddContextToUserProfile")]
    [InlineData("20260527180505_AddRoasterUrlToBean")]
    public void Initialize_UpgradesEveryHistoricalSchema(string targetMigration)
    {
        using var connection = new SqliteConnection("Data Source=:memory:");
        connection.Open();
        using (var migrationContext = CreateContext(connection))
        {
            migrationContext.GetService<IMigrator>().Migrate(targetMigration);
        }

        using var context = CreateContext(connection);
        CreateInitializer(context).Initialize();

        Assert.True(ColumnExists(connection, "Beans", "RoasterUrl"));
        Assert.True(ColumnExists(connection, "UserProfiles", "Context"));
        Assert.True(ColumnExists(connection, "ShotRecords", "GrindMicrons"));
        Assert.True(ColumnExists(connection, "ShotRecords", "WaterTempC"));
        Assert.Equal(1L, ExecuteScalar(
            connection,
            """
            SELECT COUNT(*) FROM "__EFMigrationsHistory"
            WHERE "MigrationId" = '20260624171622_AddWaterTempToShotRecord';
            """));
        Assert.Equal(0L, ExecuteScalar(connection, "SELECT COUNT(*) FROM pragma_foreign_key_check;"));
    }

    [Fact]
    public void Initialize_RepairsOrphanedShotAfterBagMigrationWasRecorded()
    {
        using var connection = new SqliteConnection("Data Source=:memory:");
        connection.Open();
        using (var migrationContext = CreateContext(connection))
        {
            migrationContext.GetService<IMigrator>().Migrate(BagMigrationId);
        }

        ExecuteNonQuery(connection, "PRAGMA foreign_keys = 0;");
        ExecuteNonQuery(connection, """
            INSERT INTO "ShotRecords"
                ("Timestamp", "BagId", "MachineId", "GrinderId", "MadeById", "MadeForId",
                 "DoseIn", "GrindSetting", "ExpectedTime", "ExpectedOutput", "DrinkType",
                 "ActualTime", "ActualOutput", "PreinfusionTime", "Rating", "SyncId",
                 "LastModifiedAt", "IsDeleted")
            VALUES
                ('2026-01-03T00:00:00+00:00', 0, NULL, NULL, NULL, NULL,
                 '18.0', '14', '30.0', '36.0', 'Espresso',
                 '31.0', '35.0', NULL, 3, '00000000-0000-0000-0000-000000000004',
                 '2026-01-03T00:00:00+00:00', 0);
            """);
        ExecuteNonQuery(connection, "PRAGMA foreign_keys = 1;");

        using var context = CreateContext(connection);
        CreateInitializer(context).Initialize();

        Assert.Equal(1L, ExecuteScalar(
            connection,
            """
            SELECT COUNT(*)
            FROM "ShotRecords" AS "s"
            INNER JOIN "Bags" AS "b" ON "s"."BagId" = "b"."Id"
            INNER JOIN "Beans" AS "bean" ON "b"."BeanId" = "bean"."Id"
            WHERE "bean"."Name" = 'Recovered shots';
            """));
        Assert.Equal(0L, ExecuteScalar(connection, "SELECT COUNT(*) FROM pragma_foreign_key_check;"));
    }

    private static BaristaNotesContext CreateContext(SqliteConnection connection)
    {
        var options = new DbContextOptionsBuilder<BaristaNotesContext>()
            .UseSqlite(connection)
            .Options;
        return new BaristaNotesContext(options);
    }

    private static DatabaseInitializer CreateInitializer(BaristaNotesContext context) =>
        new(context, MockLoggerFactory.Create<DatabaseInitializer>());

    private static bool ColumnExists(
        SqliteConnection connection,
        string tableName,
        string columnName)
    {
        using var command = connection.CreateCommand();
        command.CommandText = $"PRAGMA table_info(\"{tableName}\");";
        using var reader = command.ExecuteReader();

        while (reader.Read())
        {
            if (string.Equals(reader.GetString(1), columnName, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }

    private static void ExecuteNonQuery(SqliteConnection connection, string sql)
    {
        using var command = connection.CreateCommand();
        command.CommandText = sql;
        command.ExecuteNonQuery();
    }

    private static long ExecuteScalar(SqliteConnection connection, string sql)
    {
        using var command = connection.CreateCommand();
        command.CommandText = sql;
        return Convert.ToInt64(command.ExecuteScalar());
    }
}
