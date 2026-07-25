using System;
using System.Collections.Generic;
using System.IO;
using Microsoft.Data.Sqlite;

namespace WalkerMediaManager.UI.Data;

public static class DatabaseService
{
    private const int CurrentDatabaseVersion = 6;

    public static string DatabasePath =>
        Path.Combine(
            Environment.GetFolderPath(
                Environment.SpecialFolder.LocalApplicationData),
            "WalkerMediaManager",
            "walker.db");

    public static void Initialize()
    {
        string? databaseFolder = Path.GetDirectoryName(DatabasePath);

        if (string.IsNullOrWhiteSpace(databaseFolder))
        {
            throw new InvalidOperationException(
                "The database folder could not be determined.");
        }

        Directory.CreateDirectory(databaseFolder);

        using SqliteConnection connection =
            new($"Data Source={DatabasePath}");

        connection.Open();

        using SqliteTransaction transaction = connection.BeginTransaction();

        try
        {
            EnableForeignKeys(connection, transaction);
            CreateBaseSchema(connection, transaction);
            EnsureSchemaInfoTable(connection, transaction);

            int version = GetDatabaseVersion(connection, transaction);

            if (version < 2)
            {
                ApplyVersion2Migration(connection, transaction);
                SetDatabaseVersion(connection, transaction, 2);
                version = 2;
            }

            if (version < 3)
            {
                ApplyVersion3Migration(connection, transaction);
                SetDatabaseVersion(connection, transaction, 3);
                version = 3;
            }

            if (version < 4)
            {
                ApplyVersion4Migration(connection, transaction);
                SetDatabaseVersion(connection, transaction, 4);
                version = 4;
            }

            if (version < 5)
            {
                ApplyVersion5Migration(connection, transaction);
                SetDatabaseVersion(connection, transaction, 5);
                version = 5;
            }

            if (version < 6)
            {
                ApplyVersion6Migration(connection, transaction);
                SetDatabaseVersion(connection, transaction, 6);
            }

            EnsureIndexes(connection, transaction);

            if (GetDatabaseVersion(connection, transaction) < CurrentDatabaseVersion)
            {
                SetDatabaseVersion(
                    connection,
                    transaction,
                    CurrentDatabaseVersion);
            }

            transaction.Commit();
        }
        catch
        {
            transaction.Rollback();
            throw;
        }
    }

    private static void EnableForeignKeys(
        SqliteConnection connection,
        SqliteTransaction transaction)
    {
        ExecuteNonQuery(
            connection,
            transaction,
            "PRAGMA foreign_keys = ON;");
    }

    private static void CreateBaseSchema(
        SqliteConnection connection,
        SqliteTransaction transaction)
    {
        ExecuteNonQuery(
            connection,
            transaction,
            """
            CREATE TABLE IF NOT EXISTS Movies
            (
                Id INTEGER PRIMARY KEY AUTOINCREMENT,
                Title TEXT NOT NULL,
                ReleaseYear INTEGER NOT NULL DEFAULT 0,
                Rating TEXT NOT NULL DEFAULT '',
                Runtime INTEGER NOT NULL DEFAULT 0,
                Genre TEXT NOT NULL DEFAULT '',
                Director TEXT NOT NULL DEFAULT '',
                PlexGuid TEXT NOT NULL DEFAULT '',
                TMDbId INTEGER NULL,
                IMDbId TEXT NOT NULL DEFAULT ''
            );

            CREATE TABLE IF NOT EXISTS TVShows
            (
                Id INTEGER PRIMARY KEY AUTOINCREMENT,
                Title TEXT NOT NULL,
                Seasons INTEGER NOT NULL DEFAULT 0,
                Episodes INTEGER NOT NULL DEFAULT 0
            );

            CREATE TABLE IF NOT EXISTS Wishlist
            (
                Id INTEGER PRIMARY KEY AUTOINCREMENT,
                Title TEXT NOT NULL,
                Priority INTEGER NOT NULL DEFAULT 2
            );

            CREATE TABLE IF NOT EXISTS Collections
            (
                Id INTEGER PRIMARY KEY AUTOINCREMENT,
                Name TEXT NOT NULL,
                Description TEXT NOT NULL DEFAULT '',
                TargetCount INTEGER NOT NULL DEFAULT 0,
                OwnedCount INTEGER NOT NULL DEFAULT 0
            );
            """);
    }

    private static void EnsureSchemaInfoTable(
        SqliteConnection connection,
        SqliteTransaction transaction)
    {
        ExecuteNonQuery(
            connection,
            transaction,
            """
            CREATE TABLE IF NOT EXISTS SchemaInfo
            (
                Key TEXT PRIMARY KEY,
                Value TEXT NOT NULL
            );
            """);
    }

    private static int GetDatabaseVersion(
        SqliteConnection connection,
        SqliteTransaction transaction)
    {
        using SqliteCommand command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText =
            "SELECT Value FROM SchemaInfo WHERE Key = 'DatabaseVersion' LIMIT 1;";

        object? value = command.ExecuteScalar();

        return value is not null &&
               int.TryParse(value.ToString(), out int version)
            ? version
            : 1;
    }

    private static void SetDatabaseVersion(
        SqliteConnection connection,
        SqliteTransaction transaction,
        int version)
    {
        using SqliteCommand command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText =
            """
            INSERT INTO SchemaInfo (Key, Value)
            VALUES ('DatabaseVersion', $version)
            ON CONFLICT(Key) DO UPDATE SET Value = excluded.Value;
            """;
        command.Parameters.AddWithValue("$version", version.ToString());
        command.ExecuteNonQuery();
    }

    private static void ApplyVersion2Migration(
        SqliteConnection connection,
        SqliteTransaction transaction)
    {
        EnsureColumn(connection, transaction, "Movies", "PlexRatingKey", "TEXT NOT NULL DEFAULT ''");
        EnsureColumn(connection, transaction, "Movies", "SortTitle", "TEXT NOT NULL DEFAULT ''");
        EnsureColumn(connection, transaction, "Movies", "Studio", "TEXT NOT NULL DEFAULT ''");
        EnsureColumn(connection, transaction, "Movies", "Summary", "TEXT NOT NULL DEFAULT ''");
        EnsureColumn(connection, transaction, "Movies", "PosterPath", "TEXT NOT NULL DEFAULT ''");
        EnsureColumn(connection, transaction, "Movies", "BackgroundPath", "TEXT NOT NULL DEFAULT ''");
        EnsureColumn(connection, transaction, "Movies", "LastSynced", "TEXT NOT NULL DEFAULT ''");

        EnsureColumn(connection, transaction, "TVShows", "PlexRatingKey", "TEXT NOT NULL DEFAULT ''");
        EnsureColumn(connection, transaction, "TVShows", "PlexGuid", "TEXT NOT NULL DEFAULT ''");
        EnsureColumn(connection, transaction, "TVShows", "TMDbId", "INTEGER NULL");
        EnsureColumn(connection, transaction, "TVShows", "IMDbId", "TEXT NOT NULL DEFAULT ''");
        EnsureColumn(connection, transaction, "TVShows", "Year", "INTEGER NOT NULL DEFAULT 0");
        EnsureColumn(connection, transaction, "TVShows", "Studio", "TEXT NOT NULL DEFAULT ''");
        EnsureColumn(connection, transaction, "TVShows", "Summary", "TEXT NOT NULL DEFAULT ''");
        EnsureColumn(connection, transaction, "TVShows", "PosterPath", "TEXT NOT NULL DEFAULT ''");
        EnsureColumn(connection, transaction, "TVShows", "BackgroundPath", "TEXT NOT NULL DEFAULT ''");
        EnsureColumn(connection, transaction, "TVShows", "LastSynced", "TEXT NOT NULL DEFAULT ''");
    }

    private static void ApplyVersion3Migration(
        SqliteConnection connection,
        SqliteTransaction transaction)
    {
        ExecuteNonQuery(
            connection,
            transaction,
            """
            CREATE TABLE IF NOT EXISTS OwnedCopies
            (
                Id INTEGER PRIMARY KEY AUTOINCREMENT,
                MovieId INTEGER NOT NULL,
                Format TEXT NOT NULL DEFAULT '',
                Edition TEXT NOT NULL DEFAULT '',
                Packaging TEXT NOT NULL DEFAULT '',
                Condition TEXT NOT NULL DEFAULT '',
                Store TEXT NOT NULL DEFAULT '',
                PurchasePrice REAL NULL,
                PurchaseDate TEXT NOT NULL DEFAULT '',
                Location TEXT NOT NULL DEFAULT '',
                Notes TEXT NOT NULL DEFAULT '',
                IsDigital INTEGER NOT NULL DEFAULT 0,
                IsFavorite INTEGER NOT NULL DEFAULT 0,
                CreatedAt TEXT NOT NULL DEFAULT (datetime('now')),
                FOREIGN KEY (MovieId) REFERENCES Movies(Id) ON DELETE CASCADE
            );
            """);
    }


    private static void ApplyVersion4Migration(
        SqliteConnection connection,
        SqliteTransaction transaction)
    {
        ExecuteNonQuery(
            connection,
            transaction,
            """
            CREATE TABLE IF NOT EXISTS StorageLocations
            (
                Id INTEGER PRIMARY KEY AUTOINCREMENT,
                Name TEXT NOT NULL COLLATE NOCASE UNIQUE,
                Room TEXT NOT NULL DEFAULT '',
                Area TEXT NOT NULL DEFAULT '',
                Shelf TEXT NOT NULL DEFAULT '',
                Bin TEXT NOT NULL DEFAULT '',
                Notes TEXT NOT NULL DEFAULT '',
                IsActive INTEGER NOT NULL DEFAULT 1,
                CreatedAt TEXT NOT NULL DEFAULT (datetime('now'))
            );

            INSERT OR IGNORE INTO StorageLocations (Name, IsActive)
            SELECT DISTINCT TRIM(Location), 1
            FROM OwnedCopies
            WHERE Location IS NOT NULL
              AND TRIM(Location) <> '';
            """);
    }

    private static void ApplyVersion5Migration(
        SqliteConnection connection,
        SqliteTransaction transaction)
    {
        ExecuteNonQuery(
            connection,
            transaction,
            """
            CREATE TABLE IF NOT EXISTS Barcodes
            (
                Id INTEGER PRIMARY KEY AUTOINCREMENT,
                Code TEXT NOT NULL COLLATE NOCASE UNIQUE,
                MovieId INTEGER NOT NULL,
                Format TEXT NOT NULL DEFAULT '',
                Edition TEXT NOT NULL DEFAULT '',
                Notes TEXT NOT NULL DEFAULT '',
                CreatedAt TEXT NOT NULL DEFAULT (datetime('now')),
                FOREIGN KEY (MovieId) REFERENCES Movies(Id) ON DELETE CASCADE
            );

            CREATE TABLE IF NOT EXISTS ShoppingHistory
            (
                Id INTEGER PRIMARY KEY AUTOINCREMENT,
                SearchText TEXT NOT NULL DEFAULT '',
                Barcode TEXT NOT NULL DEFAULT '',
                MovieId INTEGER NULL,
                Title TEXT NOT NULL DEFAULT '',
                Store TEXT NOT NULL DEFAULT '',
                PlannedFormat TEXT NOT NULL DEFAULT '',
                Price REAL NULL,
                Decision TEXT NOT NULL DEFAULT '',
                Notes TEXT NOT NULL DEFAULT '',
                SearchedAt TEXT NOT NULL DEFAULT (datetime('now')),
                FOREIGN KEY (MovieId) REFERENCES Movies(Id) ON DELETE SET NULL
            );
            """);
    }


    private static void ApplyVersion6Migration(
        SqliteConnection connection,
        SqliteTransaction transaction)
    {
        ExecuteNonQuery(
            connection,
            transaction,
            """
            CREATE TABLE IF NOT EXISTS Loans
            (
                Id INTEGER PRIMARY KEY AUTOINCREMENT,
                OwnedCopyId INTEGER NOT NULL,
                Borrower TEXT NOT NULL DEFAULT '',
                LoanedDate TEXT NOT NULL DEFAULT '',
                DueDate TEXT NOT NULL DEFAULT '',
                ReturnedDate TEXT NOT NULL DEFAULT '',
                Notes TEXT NOT NULL DEFAULT '',
                CreatedAt TEXT NOT NULL DEFAULT (datetime('now')),
                FOREIGN KEY (OwnedCopyId) REFERENCES OwnedCopies(Id) ON DELETE CASCADE
            );
            """);
    }

    private static void EnsureIndexes(
        SqliteConnection connection,
        SqliteTransaction transaction)
    {
        ExecuteNonQuery(
            connection,
            transaction,
            """
            CREATE INDEX IF NOT EXISTS IX_Movies_Title
                ON Movies (Title);

            CREATE INDEX IF NOT EXISTS IX_Movies_PlexGuid
                ON Movies (PlexGuid);

            CREATE INDEX IF NOT EXISTS IX_TVShows_Title
                ON TVShows (Title);

            CREATE INDEX IF NOT EXISTS IX_TVShows_PlexGuid
                ON TVShows (PlexGuid);

            CREATE INDEX IF NOT EXISTS IX_Wishlist_Title
                ON Wishlist (Title);

            CREATE INDEX IF NOT EXISTS IX_Collections_Name
                ON Collections (Name);

            CREATE INDEX IF NOT EXISTS IX_OwnedCopies_MovieId
                ON OwnedCopies (MovieId);

            CREATE INDEX IF NOT EXISTS IX_OwnedCopies_Format
                ON OwnedCopies (Format);

            CREATE INDEX IF NOT EXISTS IX_StorageLocations_Name
                ON StorageLocations (Name);

            CREATE INDEX IF NOT EXISTS IX_StorageLocations_IsActive
                ON StorageLocations (IsActive);

            CREATE INDEX IF NOT EXISTS IX_Barcodes_Code
                ON Barcodes (Code);

            CREATE INDEX IF NOT EXISTS IX_Barcodes_MovieId
                ON Barcodes (MovieId);

            CREATE INDEX IF NOT EXISTS IX_ShoppingHistory_SearchedAt
                ON ShoppingHistory (SearchedAt);

            CREATE INDEX IF NOT EXISTS IX_Loans_OwnedCopyId
                ON Loans (OwnedCopyId);

            CREATE INDEX IF NOT EXISTS IX_Loans_ReturnedDate
                ON Loans (ReturnedDate);
            """);
    }

    private static void EnsureColumn(
        SqliteConnection connection,
        SqliteTransaction transaction,
        string tableName,
        string columnName,
        string columnDefinition)
    {
        if (ColumnExists(connection, transaction, tableName, columnName))
        {
            return;
        }

        ExecuteNonQuery(
            connection,
            transaction,
            $"ALTER TABLE {QuoteIdentifier(tableName)} " +
            $"ADD COLUMN {QuoteIdentifier(columnName)} {columnDefinition};");
    }

    private static bool ColumnExists(
        SqliteConnection connection,
        SqliteTransaction transaction,
        string tableName,
        string columnName)
    {
        using SqliteCommand command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText =
            $"PRAGMA table_info({QuoteIdentifier(tableName)});";

        using SqliteDataReader reader = command.ExecuteReader();

        while (reader.Read())
        {
            string existingName = reader.IsDBNull(1)
                ? string.Empty
                : reader.GetString(1);

            if (string.Equals(
                    existingName,
                    columnName,
                    StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }

    private static string QuoteIdentifier(string identifier)
    {
        return $"\"{identifier.Replace("\"", "\"\"")}\"";
    }

    private static void ExecuteNonQuery(
        SqliteConnection connection,
        SqliteTransaction transaction,
        string commandText)
    {
        using SqliteCommand command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = commandText;
        command.ExecuteNonQuery();
    }
}
