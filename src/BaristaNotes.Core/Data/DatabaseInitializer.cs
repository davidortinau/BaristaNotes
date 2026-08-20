using System.Data;
using System.Data.Common;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace BaristaNotes.Core.Data;

public sealed class DatabaseInitializer(
    BaristaNotesContext context,
    ILogger<DatabaseInitializer> logger)
{
    private const string ProductVersion = "10.0.11";
    private const string InitialMigrationId = "20251206024345_InitialCreate";
    private const string BagMigrationId = "20251207202829_AddBagEntity";
    private const string TastingNotesMigrationId = "20251209215438_AddTastingNotesToShotRecord";
    private const string RecipeMigrationId = "20260419013842_AddRecipeEntity";
    private const string BrewMethodMigrationId = "20260419023825_AddBrewMethodToShotRecord";
    private const string GrinderMigrationId = "20260424031239_AddGrinderProfileAndGrindTranslationCache";
    private const string GrindMicronsMigrationId = "20260517192156_AddGrindMicronsDropGrindSetting";
    private const string RatingMigrationId = "20260527135350_ShiftRatingScaleTo04";
    private const string ProfileContextMigrationId = "20260527155808_AddContextToUserProfile";
    private const string RoasterUrlMigrationId = "20260527180505_AddRoasterUrlToBean";
    private const string WaterTempMigrationId = "20260624171622_AddWaterTempToShotRecord";

    public void Initialize()
    {
        if (context.Database.EnsureCreated())
        {
            logger.LogInformation("Created the BaristaNotes database");
            return;
        }

        var connection = context.Database.GetDbConnection();
        var closeConnection = connection.State != ConnectionState.Open;

        try
        {
            if (closeConnection)
            {
                connection.Open();
            }

            EnsureMigrationHistoryTable(connection);
            EnsureInitialMigrationRecord(connection);
            ApplyMigration(connection, BagMigrationId, HasBagSchema, AddBagSql, disableForeignKeys: true);
            ApplyMigration(
                connection,
                TastingNotesMigrationId,
                c => ColumnExists(c, "ShotRecords", "TastingNotes"),
                """ALTER TABLE "ShotRecords" ADD COLUMN "TastingNotes" TEXT NULL;""");
            ApplyMigration(connection, RecipeMigrationId, c => TableExists(c, "Recipes"), AddRecipeSql);
            ApplyMigration(
                connection,
                BrewMethodMigrationId,
                c => ColumnExists(c, "ShotRecords", "BrewMethod")
                     && ColumnExists(c, "ShotRecords", "ParametersJson"),
                """
                ALTER TABLE "ShotRecords" ADD COLUMN "BrewMethod" INTEGER NOT NULL DEFAULT 1;
                ALTER TABLE "ShotRecords" ADD COLUMN "ParametersJson" TEXT NULL;
                """);
            ApplyMigration(
                connection,
                GrinderMigrationId,
                c => TableExists(c, "GrinderProfiles") && TableExists(c, "GrindTranslationCache"),
                AddGrinderTablesSql);
            ApplyMigration(
                connection,
                GrindMicronsMigrationId,
                c => ColumnExists(c, "ShotRecords", "GrindMicrons")
                     && !ColumnExists(c, "ShotRecords", "GrindSetting"),
                AddGrindMicronsSql,
                disableForeignKeys: true);
            ApplyMigration(
                connection,
                RatingMigrationId,
                HasPostRatingSchema,
                """
                UPDATE "ShotRecords"
                SET "Rating" = CASE
                    WHEN "Rating" IS NULL THEN NULL
                    WHEN "Rating" BETWEEN 1 AND 5 THEN "Rating" - 1
                    WHEN "Rating" < 0 THEN 0
                    WHEN "Rating" > 5 THEN 4
                    ELSE "Rating"
                END
                WHERE "Rating" IS NOT NULL;
                """);
            ApplyMigration(
                connection,
                ProfileContextMigrationId,
                c => ColumnExists(c, "UserProfiles", "Context"),
                """ALTER TABLE "UserProfiles" ADD COLUMN "Context" TEXT NULL;""");
            ApplyMigration(
                connection,
                RoasterUrlMigrationId,
                c => ColumnExists(c, "Beans", "RoasterUrl"),
                """ALTER TABLE "Beans" ADD COLUMN "RoasterUrl" TEXT NULL;""");
            ApplyMigration(
                connection,
                WaterTempMigrationId,
                c => ColumnExists(c, "ShotRecords", "WaterTempC"),
                """ALTER TABLE "ShotRecords" ADD COLUMN "WaterTempC" TEXT NULL;""");

            RepairOrphanedShots(connection);
            ValidateCurrentSchema(connection);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to initialize the BaristaNotes database");
            throw;
        }
        finally
        {
            if (closeConnection)
            {
                connection.Close();
            }
        }
    }

    private void EnsureInitialMigrationRecord(DbConnection connection)
    {
        if (MigrationExists(connection, InitialMigrationId))
        {
            return;
        }

        var requiredTables = new[] { "Beans", "Equipment", "UserProfiles", "ShotRecords", "ShotEquipments" };
        var missingTables = requiredTables.Where(table => !TableExists(connection, table)).ToArray();

        if (missingTables.Length > 0)
        {
            throw new InvalidOperationException(
                $"The existing database has an unsupported partial schema. Missing: {string.Join(", ", missingTables)}.");
        }

        RecordMigration(connection, InitialMigrationId);
    }

    private void ApplyMigration(
        DbConnection connection,
        string migrationId,
        Func<DbConnection, bool> schemaAlreadyApplied,
        string sql,
        bool disableForeignKeys = false)
    {
        if (MigrationExists(connection, migrationId))
        {
            return;
        }

        if (schemaAlreadyApplied(connection))
        {
            RecordMigration(connection, migrationId);
            return;
        }

        if (disableForeignKeys)
        {
            ExecuteSql(connection, null, "PRAGMA foreign_keys = 0;");
        }

        try
        {
            using var transaction = connection.BeginTransaction();
            ExecuteSql(connection, transaction, sql);
            RecordMigration(connection, migrationId, transaction);
            transaction.Commit();
            logger.LogInformation("Applied database schema update {MigrationId}", migrationId);
        }
        finally
        {
            if (disableForeignKeys)
            {
                ExecuteSql(connection, null, "PRAGMA foreign_keys = 1;");
            }
        }
    }

    private static bool HasBagSchema(DbConnection connection) =>
        TableExists(connection, "Bags")
        && ColumnExists(connection, "ShotRecords", "BagId")
        && !ColumnExists(connection, "ShotRecords", "BeanId")
        && !ColumnExists(connection, "Beans", "RoastDate");

    private static bool HasPostRatingSchema(DbConnection connection) =>
        ColumnExists(connection, "UserProfiles", "Context")
        || ColumnExists(connection, "Beans", "RoasterUrl")
        || ColumnExists(connection, "ShotRecords", "WaterTempC");

    private void RepairOrphanedShots(DbConnection connection)
    {
        using var transaction = connection.BeginTransaction();
        ExecuteSql(
            connection,
            transaction,
            """
            INSERT INTO "Beans"
                ("Name", "Roaster", "Origin", "Notes", "RoasterUrl", "IsActive",
                 "CreatedAt", "SyncId", "LastModifiedAt", "IsDeleted")
            SELECT 'Recovered shots', NULL, NULL,
                   'Created automatically to preserve shots whose original bag is unavailable.',
                   NULL, 0, datetime('now'), '00000000-0000-0000-0000-00000000ba01',
                   datetime('now'), 0
            WHERE EXISTS (
                SELECT 1
                FROM "ShotRecords" AS "s"
                LEFT JOIN "Bags" AS "b" ON "s"."BagId" = "b"."Id"
                WHERE "b"."Id" IS NULL
            )
            AND NOT EXISTS (
                SELECT 1 FROM "Beans"
                WHERE "SyncId" = '00000000-0000-0000-0000-00000000ba01'
            );

            INSERT INTO "Bags"
                ("BeanId", "RoastDate", "Notes", "IsComplete", "IsActive",
                 "CreatedAt", "SyncId", "LastModifiedAt", "IsDeleted")
            SELECT "Id", datetime('now'), 'Recovered historical shots', 1, 0,
                   datetime('now'), '00000000-0000-0000-0000-00000000ba02',
                   datetime('now'), 0
            FROM "Beans"
            WHERE "SyncId" = '00000000-0000-0000-0000-00000000ba01'
              AND EXISTS (
                  SELECT 1
                  FROM "ShotRecords" AS "s"
                  LEFT JOIN "Bags" AS "b" ON "s"."BagId" = "b"."Id"
                  WHERE "b"."Id" IS NULL
              )
              AND NOT EXISTS (
                  SELECT 1 FROM "Bags"
                  WHERE "SyncId" = '00000000-0000-0000-0000-00000000ba02'
              );
            """);

        using var repairCommand = connection.CreateCommand();
        repairCommand.Transaction = transaction;
        repairCommand.CommandText = """
            UPDATE "ShotRecords"
            SET "BagId" = (
                SELECT "Id" FROM "Bags"
                WHERE "SyncId" = '00000000-0000-0000-0000-00000000ba02'
            )
            WHERE NOT EXISTS (
                SELECT 1 FROM "Bags"
                WHERE "Bags"."Id" = "ShotRecords"."BagId"
            );
            """;
        var repairedCount = repairCommand.ExecuteNonQuery();
        transaction.Commit();

        if (repairedCount > 0)
        {
            logger.LogWarning("Recovered {ShotCount} shots with missing bag references", repairedCount);
        }
    }

    private static void ValidateCurrentSchema(DbConnection connection)
    {
        var requiredTables = new[]
        {
            "Beans",
            "Equipment",
            "UserProfiles",
            "ShotRecords",
            "ShotEquipments",
            "Bags",
            "Recipes",
            "GrinderProfiles",
            "GrindTranslationCache"
        };

        foreach (var table in requiredTables)
        {
            if (!TableExists(connection, table))
            {
                throw new InvalidOperationException($"Database initialization did not create required table '{table}'.");
            }
        }

        var requiredColumns = new[]
        {
            ("Beans", "RoasterUrl"),
            ("UserProfiles", "Context"),
            ("ShotRecords", "BagId"),
            ("ShotRecords", "TastingNotes"),
            ("ShotRecords", "BrewMethod"),
            ("ShotRecords", "ParametersJson"),
            ("ShotRecords", "GrindMicrons"),
            ("ShotRecords", "WaterTempC")
        };

        foreach (var (table, column) in requiredColumns)
        {
            if (!ColumnExists(connection, table, column))
            {
                throw new InvalidOperationException(
                    $"Database initialization did not create required column '{table}.{column}'.");
            }
        }

        using var command = connection.CreateCommand();
        command.CommandText = "PRAGMA foreign_key_check;";
        using var reader = command.ExecuteReader();
        if (reader.Read())
        {
            throw new InvalidOperationException(
                $"Database foreign-key validation failed for table '{reader.GetString(0)}'.");
        }
    }

    private static void EnsureMigrationHistoryTable(DbConnection connection) =>
        ExecuteSql(
            connection,
            null,
            """
            CREATE TABLE IF NOT EXISTS "__EFMigrationsHistory" (
                "MigrationId" TEXT NOT NULL CONSTRAINT "PK___EFMigrationsHistory" PRIMARY KEY,
                "ProductVersion" TEXT NOT NULL
            );
            """);

    private static bool MigrationExists(DbConnection connection, string migrationId)
    {
        using var command = connection.CreateCommand();
        command.CommandText =
            """SELECT COUNT(*) FROM "__EFMigrationsHistory" WHERE "MigrationId" = $migrationId;""";
        AddParameter(command, "$migrationId", migrationId);
        return Convert.ToInt64(command.ExecuteScalar()) > 0;
    }

    private static void RecordMigration(
        DbConnection connection,
        string migrationId,
        DbTransaction? transaction = null)
    {
        using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = """
            INSERT OR IGNORE INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
            VALUES ($migrationId, $productVersion);
            """;
        AddParameter(command, "$migrationId", migrationId);
        AddParameter(command, "$productVersion", ProductVersion);
        command.ExecuteNonQuery();
    }

    private static bool ColumnExists(DbConnection connection, string tableName, string columnName)
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

    private static bool TableExists(DbConnection connection, string tableName)
    {
        using var command = connection.CreateCommand();
        command.CommandText =
            "SELECT COUNT(*) FROM \"sqlite_master\" WHERE \"type\" = 'table' AND \"name\" = $tableName;";
        AddParameter(command, "$tableName", tableName);
        return Convert.ToInt64(command.ExecuteScalar()) > 0;
    }

    private static void ExecuteSql(
        DbConnection connection,
        DbTransaction? transaction,
        string sql)
    {
        using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = sql;
        command.ExecuteNonQuery();
    }

    private static void AddParameter(DbCommand command, string name, object value)
    {
        var parameter = command.CreateParameter();
        parameter.ParameterName = name;
        parameter.Value = value;
        command.Parameters.Add(parameter);
    }

    private const string AddBagSql = """
        CREATE TABLE "Bags" (
            "Id" INTEGER NOT NULL CONSTRAINT "PK_Bags" PRIMARY KEY AUTOINCREMENT,
            "BeanId" INTEGER NOT NULL,
            "RoastDate" TEXT NOT NULL,
            "Notes" TEXT NULL,
            "IsComplete" INTEGER NOT NULL DEFAULT 0,
            "IsActive" INTEGER NOT NULL DEFAULT 1,
            "CreatedAt" TEXT NOT NULL,
            "SyncId" TEXT NOT NULL,
            "LastModifiedAt" TEXT NOT NULL,
            "IsDeleted" INTEGER NOT NULL DEFAULT 0,
            CONSTRAINT "FK_Bags_Beans_BeanId" FOREIGN KEY ("BeanId") REFERENCES "Beans" ("Id") ON DELETE CASCADE
        );

        ALTER TABLE "ShotRecords" ADD COLUMN "BagId" INTEGER NULL;

        INSERT INTO "Beans"
            ("Name", "Roaster", "RoastDate", "Origin", "Notes", "IsActive",
             "CreatedAt", "SyncId", "LastModifiedAt", "IsDeleted")
        SELECT 'Recovered shots', NULL, datetime('now'), NULL,
               'Created automatically to preserve shots whose original bean is unavailable.',
               0, datetime('now'), '00000000-0000-0000-0000-00000000ba01', datetime('now'), 0
        WHERE EXISTS (
            SELECT 1
            FROM "ShotRecords" AS "s"
            LEFT JOIN "Beans" AS "b" ON "s"."BeanId" = "b"."Id"
            WHERE "b"."Id" IS NULL
        )
        AND NOT EXISTS (
            SELECT 1 FROM "Beans"
            WHERE "SyncId" = '00000000-0000-0000-0000-00000000ba01'
        );

        INSERT INTO "Bags" ("BeanId", "RoastDate", "IsComplete", "IsActive", "CreatedAt", "SyncId", "LastModifiedAt", "IsDeleted")
        SELECT "Id", COALESCE("RoastDate", datetime('now')), 0, "IsActive", "CreatedAt",
               lower(hex(randomblob(16))), "LastModifiedAt", "IsDeleted"
        FROM "Beans";

        UPDATE "ShotRecords"
        SET "BagId" = (
            SELECT "Id" FROM "Bags"
            WHERE "Bags"."BeanId" = COALESCE(
                (
                    SELECT "Id" FROM "Beans"
                    WHERE "Id" = "ShotRecords"."BeanId"
                ),
                (
                    SELECT "Id" FROM "Beans"
                    WHERE "SyncId" = '00000000-0000-0000-0000-00000000ba01'
                )
            )
            ORDER BY "RoastDate" ASC
            LIMIT 1
        )
        WHERE "BagId" IS NULL;

        CREATE TABLE "ef_temp_ShotRecords" (
            "Id" INTEGER NOT NULL CONSTRAINT "PK_ShotRecords" PRIMARY KEY AUTOINCREMENT,
            "ActualOutput" TEXT NULL,
            "ActualTime" TEXT NULL,
            "BagId" INTEGER NOT NULL,
            "DoseIn" TEXT NOT NULL,
            "DrinkType" TEXT NOT NULL,
            "ExpectedOutput" TEXT NOT NULL,
            "ExpectedTime" TEXT NOT NULL,
            "GrindSetting" TEXT NOT NULL,
            "GrinderId" INTEGER NULL,
            "IsDeleted" INTEGER NOT NULL DEFAULT 0,
            "LastModifiedAt" TEXT NOT NULL,
            "MachineId" INTEGER NULL,
            "MadeById" INTEGER NULL,
            "MadeForId" INTEGER NULL,
            "PreinfusionTime" TEXT NULL,
            "Rating" INTEGER NULL,
            "SyncId" TEXT NOT NULL,
            "Timestamp" TEXT NOT NULL,
            CONSTRAINT "FK_ShotRecords_Bags_BagId" FOREIGN KEY ("BagId") REFERENCES "Bags" ("Id") ON DELETE CASCADE,
            CONSTRAINT "FK_ShotRecords_Equipment_GrinderId" FOREIGN KEY ("GrinderId") REFERENCES "Equipment" ("Id") ON DELETE SET NULL,
            CONSTRAINT "FK_ShotRecords_Equipment_MachineId" FOREIGN KEY ("MachineId") REFERENCES "Equipment" ("Id") ON DELETE SET NULL,
            CONSTRAINT "FK_ShotRecords_UserProfiles_MadeById" FOREIGN KEY ("MadeById") REFERENCES "UserProfiles" ("Id") ON DELETE SET NULL,
            CONSTRAINT "FK_ShotRecords_UserProfiles_MadeForId" FOREIGN KEY ("MadeForId") REFERENCES "UserProfiles" ("Id") ON DELETE SET NULL
        );

        INSERT INTO "ef_temp_ShotRecords"
            ("Id", "ActualOutput", "ActualTime", "BagId", "DoseIn", "DrinkType", "ExpectedOutput",
             "ExpectedTime", "GrindSetting", "GrinderId", "IsDeleted", "LastModifiedAt", "MachineId",
             "MadeById", "MadeForId", "PreinfusionTime", "Rating", "SyncId", "Timestamp")
        SELECT "Id", "ActualOutput", "ActualTime", IFNULL("BagId", 0), "DoseIn", "DrinkType",
               "ExpectedOutput", "ExpectedTime", "GrindSetting", "GrinderId", "IsDeleted",
               "LastModifiedAt", "MachineId", "MadeById", "MadeForId", "PreinfusionTime",
               "Rating", "SyncId", "Timestamp"
        FROM "ShotRecords";

        CREATE TABLE "ef_temp_Beans" (
            "Id" INTEGER NOT NULL CONSTRAINT "PK_Beans" PRIMARY KEY AUTOINCREMENT,
            "CreatedAt" TEXT NOT NULL,
            "IsActive" INTEGER NOT NULL DEFAULT 1,
            "IsDeleted" INTEGER NOT NULL DEFAULT 0,
            "LastModifiedAt" TEXT NOT NULL,
            "Name" TEXT NOT NULL,
            "Notes" TEXT NULL,
            "Origin" TEXT NULL,
            "Roaster" TEXT NULL,
            "SyncId" TEXT NOT NULL
        );

        INSERT INTO "ef_temp_Beans"
            ("Id", "CreatedAt", "IsActive", "IsDeleted", "LastModifiedAt", "Name", "Notes", "Origin", "Roaster", "SyncId")
        SELECT "Id", "CreatedAt", "IsActive", "IsDeleted", "LastModifiedAt", "Name", "Notes", "Origin", "Roaster", "SyncId"
        FROM "Beans";

        DROP TABLE "ShotRecords";
        ALTER TABLE "ef_temp_ShotRecords" RENAME TO "ShotRecords";
        DROP TABLE "Beans";
        ALTER TABLE "ef_temp_Beans" RENAME TO "Beans";

        CREATE INDEX "IX_ShotRecords_BagId" ON "ShotRecords" ("BagId");
        CREATE INDEX "IX_ShotRecords_BagId_Rating" ON "ShotRecords" ("BagId", "Rating");
        CREATE INDEX "IX_ShotRecords_GrinderId" ON "ShotRecords" ("GrinderId");
        CREATE INDEX "IX_ShotRecords_MachineId" ON "ShotRecords" ("MachineId");
        CREATE INDEX "IX_ShotRecords_MadeById" ON "ShotRecords" ("MadeById");
        CREATE INDEX "IX_ShotRecords_MadeForId" ON "ShotRecords" ("MadeForId");
        CREATE UNIQUE INDEX "IX_ShotRecords_SyncId" ON "ShotRecords" ("SyncId");
        CREATE INDEX "IX_ShotRecords_Timestamp" ON "ShotRecords" ("Timestamp" DESC);
        CREATE INDEX "IX_Beans_IsActive" ON "Beans" ("IsActive");
        CREATE INDEX "IX_Beans_Name_Roaster" ON "Beans" ("Name", "Roaster");
        CREATE UNIQUE INDEX "IX_Beans_SyncId" ON "Beans" ("SyncId");
        CREATE INDEX "IX_Bags_BeanId" ON "Bags" ("BeanId");
        CREATE INDEX "IX_Bags_BeanId_IsComplete_RoastDate" ON "Bags" ("BeanId", "IsComplete", "RoastDate" DESC);
        CREATE INDEX "IX_Bags_BeanId_RoastDate" ON "Bags" ("BeanId", "RoastDate" DESC);
        CREATE UNIQUE INDEX "IX_Bags_SyncId" ON "Bags" ("SyncId");
        """;

    private const string AddRecipeSql = """
        CREATE TABLE "Recipes" (
            "Id" INTEGER NOT NULL CONSTRAINT "PK_Recipes" PRIMARY KEY AUTOINCREMENT,
            "BeanId" INTEGER NOT NULL,
            "BrewMethod" INTEGER NOT NULL,
            "Source" INTEGER NOT NULL,
            "SourceUrl" TEXT NULL,
            "Title" TEXT NULL,
            "DoseIn" TEXT NULL,
            "OutputAmount" TEXT NULL,
            "GrindHint" TEXT NULL,
            "BrewTempC" TEXT NULL,
            "TotalTimeSeconds" TEXT NULL,
            "ParametersJson" TEXT NULL,
            "Notes" TEXT NULL,
            "FetchedAt" TEXT NOT NULL,
            "IsEditedByUser" INTEGER NOT NULL DEFAULT 0,
            "SyncId" TEXT NOT NULL,
            "LastModifiedAt" TEXT NOT NULL,
            "IsDeleted" INTEGER NOT NULL DEFAULT 0,
            CONSTRAINT "FK_Recipes_Beans_BeanId" FOREIGN KEY ("BeanId") REFERENCES "Beans" ("Id") ON DELETE CASCADE
        );
        CREATE INDEX "IX_Recipes_BeanId" ON "Recipes" ("BeanId");
        CREATE INDEX "IX_Recipes_BeanId_BrewMethod" ON "Recipes" ("BeanId", "BrewMethod");
        CREATE UNIQUE INDEX "IX_Recipes_SyncId" ON "Recipes" ("SyncId");
        """;

    private const string AddGrinderTablesSql = """
        CREATE TABLE "GrinderProfiles" (
            "Id" INTEGER NOT NULL CONSTRAINT "PK_GrinderProfiles" PRIMARY KEY AUTOINCREMENT,
            "EquipmentId" INTEGER NOT NULL,
            "MinSetting" TEXT NULL,
            "MaxSetting" TEXT NULL,
            "StepSize" TEXT NULL,
            "AnchorsJson" TEXT NULL,
            "CreatedAt" TEXT NOT NULL,
            "LastModifiedAt" TEXT NOT NULL,
            "SyncId" TEXT NOT NULL,
            "IsDeleted" INTEGER NOT NULL DEFAULT 0,
            CONSTRAINT "FK_GrinderProfiles_Equipment_EquipmentId" FOREIGN KEY ("EquipmentId") REFERENCES "Equipment" ("Id") ON DELETE CASCADE
        );
        CREATE TABLE "GrindTranslationCache" (
            "Id" INTEGER NOT NULL CONSTRAINT "PK_GrindTranslationCache" PRIMARY KEY AUTOINCREMENT,
            "GrinderModelNormalized" TEXT NOT NULL,
            "GrindHintNormalized" TEXT NOT NULL,
            "BrewMethod" INTEGER NOT NULL,
            "MinSetting" TEXT NULL,
            "MaxSetting" TEXT NULL,
            "SuggestedSetting" TEXT NULL,
            "Confidence" TEXT NOT NULL,
            "Source" TEXT NOT NULL,
            "Explanation" TEXT NULL,
            "CreatedAt" TEXT NOT NULL,
            "ExpiresAt" TEXT NOT NULL
        );
        CREATE UNIQUE INDEX "IX_GrinderProfiles_EquipmentId" ON "GrinderProfiles" ("EquipmentId");
        CREATE UNIQUE INDEX "IX_GrinderProfiles_SyncId" ON "GrinderProfiles" ("SyncId");
        CREATE INDEX "IX_GrindTranslationCache_ExpiresAt" ON "GrindTranslationCache" ("ExpiresAt");
        CREATE UNIQUE INDEX "IX_GrindTranslationCache_Key"
            ON "GrindTranslationCache" ("GrinderModelNormalized", "GrindHintNormalized", "BrewMethod");
        """;

    private const string AddGrindMicronsSql = """
        CREATE TABLE "LegacyShotGrindSettings" (
            "Id" INTEGER NOT NULL CONSTRAINT "PK_LegacyShotGrindSettings" PRIMARY KEY AUTOINCREMENT,
            "ShotRecordId" INTEGER NOT NULL,
            "BagId" INTEGER NOT NULL,
            "GrinderId" INTEGER NULL,
            "BrewMethod" INTEGER NOT NULL,
            "GrindSetting" TEXT NOT NULL,
            "CapturedAt" TEXT NOT NULL
        );

        INSERT INTO "LegacyShotGrindSettings"
            ("ShotRecordId", "BagId", "GrinderId", "BrewMethod", "GrindSetting", "CapturedAt")
        SELECT "Id", "BagId", "GrinderId", "BrewMethod", "GrindSetting", CURRENT_TIMESTAMP
        FROM "ShotRecords"
        WHERE "GrindSetting" IS NOT NULL AND "GrindSetting" <> '';

        ALTER TABLE "ShotRecords" ADD COLUMN "GrindMicrons" INTEGER NULL;

        CREATE TABLE "ef_temp_ShotRecords" (
            "Id" INTEGER NOT NULL CONSTRAINT "PK_ShotRecords" PRIMARY KEY AUTOINCREMENT,
            "ActualOutput" TEXT NULL,
            "ActualTime" TEXT NULL,
            "BagId" INTEGER NOT NULL,
            "BrewMethod" INTEGER NOT NULL DEFAULT 1,
            "DoseIn" TEXT NOT NULL,
            "DrinkType" TEXT NOT NULL,
            "ExpectedOutput" TEXT NOT NULL,
            "ExpectedTime" TEXT NOT NULL,
            "GrindMicrons" INTEGER NULL,
            "GrinderId" INTEGER NULL,
            "IsDeleted" INTEGER NOT NULL DEFAULT 0,
            "LastModifiedAt" TEXT NOT NULL,
            "MachineId" INTEGER NULL,
            "MadeById" INTEGER NULL,
            "MadeForId" INTEGER NULL,
            "ParametersJson" TEXT NULL,
            "PreinfusionTime" TEXT NULL,
            "Rating" INTEGER NULL,
            "SyncId" TEXT NOT NULL,
            "TastingNotes" TEXT NULL,
            "Timestamp" TEXT NOT NULL,
            CONSTRAINT "FK_ShotRecords_Bags_BagId" FOREIGN KEY ("BagId") REFERENCES "Bags" ("Id") ON DELETE CASCADE,
            CONSTRAINT "FK_ShotRecords_Equipment_GrinderId" FOREIGN KEY ("GrinderId") REFERENCES "Equipment" ("Id") ON DELETE SET NULL,
            CONSTRAINT "FK_ShotRecords_Equipment_MachineId" FOREIGN KEY ("MachineId") REFERENCES "Equipment" ("Id") ON DELETE SET NULL,
            CONSTRAINT "FK_ShotRecords_UserProfiles_MadeById" FOREIGN KEY ("MadeById") REFERENCES "UserProfiles" ("Id") ON DELETE SET NULL,
            CONSTRAINT "FK_ShotRecords_UserProfiles_MadeForId" FOREIGN KEY ("MadeForId") REFERENCES "UserProfiles" ("Id") ON DELETE SET NULL
        );

        INSERT INTO "ef_temp_ShotRecords"
            ("Id", "ActualOutput", "ActualTime", "BagId", "BrewMethod", "DoseIn", "DrinkType",
             "ExpectedOutput", "ExpectedTime", "GrindMicrons", "GrinderId", "IsDeleted",
             "LastModifiedAt", "MachineId", "MadeById", "MadeForId", "ParametersJson",
             "PreinfusionTime", "Rating", "SyncId", "TastingNotes", "Timestamp")
        SELECT "Id", "ActualOutput", "ActualTime", "BagId", "BrewMethod", "DoseIn", "DrinkType",
               "ExpectedOutput", "ExpectedTime", "GrindMicrons", "GrinderId", "IsDeleted",
               "LastModifiedAt", "MachineId", "MadeById", "MadeForId", "ParametersJson",
               "PreinfusionTime", "Rating", "SyncId", "TastingNotes", "Timestamp"
        FROM "ShotRecords";

        DROP TABLE "ShotRecords";
        ALTER TABLE "ef_temp_ShotRecords" RENAME TO "ShotRecords";
        CREATE INDEX "IX_ShotRecords_BagId" ON "ShotRecords" ("BagId");
        CREATE INDEX "IX_ShotRecords_BagId_Rating" ON "ShotRecords" ("BagId", "Rating");
        CREATE INDEX "IX_ShotRecords_GrinderId" ON "ShotRecords" ("GrinderId");
        CREATE INDEX "IX_ShotRecords_MachineId" ON "ShotRecords" ("MachineId");
        CREATE INDEX "IX_ShotRecords_MadeById" ON "ShotRecords" ("MadeById");
        CREATE INDEX "IX_ShotRecords_MadeForId" ON "ShotRecords" ("MadeForId");
        CREATE UNIQUE INDEX "IX_ShotRecords_SyncId" ON "ShotRecords" ("SyncId");
        CREATE INDEX "IX_ShotRecords_Timestamp" ON "ShotRecords" ("Timestamp" DESC);
        """;
}
