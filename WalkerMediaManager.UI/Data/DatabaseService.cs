using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Microsoft.Data.Sqlite;
using WalkerMediaManager.UI.Services;

namespace WalkerMediaManager.UI.Data;

public static class DatabaseService
{
    private const int CurrentDatabaseVersion = 11;

    public static string DatabasePath => ApplicationPaths.DatabasePath;

    public static void Initialize()
    {
        string? databaseFolder = Path.GetDirectoryName(DatabasePath);

        if (string.IsNullOrWhiteSpace(databaseFolder))
        {
            throw new InvalidOperationException(
                "The database folder could not be determined.");
        }

        Directory.CreateDirectory(databaseFolder);
        ImportLegacyPackagedDatabaseIfNeeded();

        if (File.Exists(DatabasePath))
        {
            ValidateDatabaseIntegrity(DatabasePath, "active database");
            DatabaseBackupService.CreateDailyBackupAsync()
                .GetAwaiter()
                .GetResult();
        }

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
                version = 6;
            }

            if (version < 7)
            {
                ApplyVersion7Migration(connection, transaction);
                SetDatabaseVersion(connection, transaction, 7);
                version = 7;
            }

            if (version < 8)
            {
                ApplyVersion8Migration(connection, transaction);
                SetDatabaseVersion(connection, transaction, 8);
                version = 8;
            }

            if (version < 9)
            {
                ApplyVersion9Migration(connection, transaction);
                SetDatabaseVersion(connection, transaction, 9);
                version = 9;
            }

            if (version < 10)
            {
                ApplyVersion10Migration(connection, transaction);
                SetDatabaseVersion(connection, transaction, 10);
                version = 10;
            }

            if (version < 11)
            {
                ApplyVersion11Migration(connection, transaction);
                SetDatabaseVersion(connection, transaction, 11);
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


    private static void ImportLegacyPackagedDatabaseIfNeeded()
    {
        if (File.Exists(DatabasePath))
        {
            DiagnosticsService.Log(
                "Canonical database already exists; legacy database import skipped.");
            return;
        }

        try
        {
            string packagesFolder = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
                "AppData",
                "Local",
                "Packages");

            if (!Directory.Exists(packagesFolder))
            {
                DiagnosticsService.Log(
                    "Windows Packages folder was not found; legacy database import skipped.");
                return;
            }

            string? newestCandidate = Directory
                .EnumerateFiles(packagesFolder, "walker.db", SearchOption.AllDirectories)
                .Where(path => path.Contains(
                    Path.Combine("LocalCache", "Local", "WalkerMediaManager"),
                    StringComparison.OrdinalIgnoreCase))
                .OrderByDescending(File.GetLastWriteTimeUtc)
                .FirstOrDefault();

            if (string.IsNullOrWhiteSpace(newestCandidate))
            {
                DiagnosticsService.Log(
                    "No legacy packaged database was found; a new database will be created.");
                return;
            }

            ValidateDatabaseIntegrity(newestCandidate, "legacy database candidate");

            string temporaryImportPath = DatabasePath + ".importing";
            TryDeleteFile(temporaryImportPath);
            File.Copy(newestCandidate, temporaryImportPath, true);
            ValidateDatabaseIntegrity(temporaryImportPath, "copied legacy database");
            File.Move(temporaryImportPath, DatabasePath, true);

            DiagnosticsService.Log(
                $"Legacy database imported from '{newestCandidate}' to '{DatabasePath}'.");
        }
        catch (Exception exception)
        {
            DiagnosticsService.LogException(
                "Legacy database import failed. The existing source database was not modified.",
                exception);
            throw new InvalidOperationException(
                "Walker Media Manager found an older database but could not import it safely. " +
                "No source data was changed. See the diagnostic log for details.",
                exception);
        }
    }

    private static void ValidateDatabaseIntegrity(
        string databasePath,
        string description)
    {
        if (!File.Exists(databasePath))
        {
            throw new FileNotFoundException(
                $"The {description} was not found at '{databasePath}'.",
                databasePath);
        }

        using SqliteConnection connection =
            new($"Data Source={databasePath};Mode=ReadOnly");
        connection.Open();

        using SqliteCommand command = connection.CreateCommand();
        command.CommandText = "PRAGMA integrity_check;";

        string result = command.ExecuteScalar()?.ToString() ?? string.Empty;

        if (!string.Equals(result, "ok", StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidDataException(
                $"SQLite integrity validation failed for the {description} at " +
                $"'{databasePath}'. Result: '{result}'.");
        }

        DiagnosticsService.Log(
            $"SQLite integrity validation passed for the {description}: {databasePath}");
    }

    private static void TryDeleteFile(string path)
    {
        try
        {
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }
        catch (Exception exception)
        {
            DiagnosticsService.LogException(
                $"Could not remove temporary database file '{path}'.",
                exception);
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
                TotalSeasons INTEGER NOT NULL DEFAULT 0,
                Episodes INTEGER NOT NULL DEFAULT 0
            );

            CREATE TABLE IF NOT EXISTS Wishlist
            (
                Id INTEGER PRIMARY KEY AUTOINCREMENT,
                MediaType TEXT NOT NULL DEFAULT 'Movie',
                Title TEXT NOT NULL,
                NormalizedTitle TEXT NOT NULL DEFAULT '',
                Year INTEGER NOT NULL DEFAULT 0,
                TMDbId INTEGER NULL,
                PreferredFormat TEXT NOT NULL DEFAULT '',
                TargetPrice REAL NULL,
                PreferredStore TEXT NOT NULL DEFAULT '',
                Priority INTEGER NOT NULL DEFAULT 2,
                Notes TEXT NOT NULL DEFAULT '',
                DateAdded TEXT NOT NULL DEFAULT '',
                LastUpdated TEXT NOT NULL DEFAULT '',
                IsPurchased INTEGER NOT NULL DEFAULT 0
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


    private static void ApplyVersion7Migration(
        SqliteConnection connection,
        SqliteTransaction transaction)
    {
        EnsureColumn(connection, transaction, "Movies", "PlexLibraryKey", "TEXT NOT NULL DEFAULT ''");
        EnsureColumn(connection, transaction, "Movies", "PlexLibraryTitle", "TEXT NOT NULL DEFAULT ''");
    }


    private static void ApplyVersion10Migration(
        SqliteConnection connection,
        SqliteTransaction transaction)
    {
        EnsureColumn(connection, transaction, "TVShows", "TotalSeasons", "INTEGER NOT NULL DEFAULT 0");
    }

    private static void ApplyVersion11Migration(
        SqliteConnection connection,
        SqliteTransaction transaction)
    {
        EnsureColumn(connection, transaction, "TVShows", "TVMazeId", "INTEGER NULL");
        EnsureColumn(connection, transaction, "TVShows", "Status", "TEXT NOT NULL DEFAULT ''");
        EnsureColumn(connection, transaction, "TVShows", "FirstAirDate", "TEXT NOT NULL DEFAULT ''");
        EnsureColumn(connection, transaction, "TVShows", "LastAirDate", "TEXT NOT NULL DEFAULT ''");
        EnsureColumn(connection, transaction, "TVShows", "Network", "TEXT NOT NULL DEFAULT ''");
        EnsureColumn(connection, transaction, "TVShows", "MetadataLastSynced", "TEXT NOT NULL DEFAULT ''");
    }


    private static void ApplyVersion9Migration(
        SqliteConnection connection,
        SqliteTransaction transaction)
    {
        ExecuteNonQuery(
            connection,
            transaction,
            """
            CREATE TABLE IF NOT EXISTS TVSeasons
            (
                Id INTEGER PRIMARY KEY AUTOINCREMENT,
                TVShowId INTEGER NOT NULL,
                SeasonNumber INTEGER NOT NULL,
                Name TEXT NOT NULL DEFAULT '',
                EpisodeCount INTEGER NOT NULL DEFAULT 0,
                IsOwned INTEGER NOT NULL DEFAULT 1,
                Format TEXT NOT NULL DEFAULT 'DVD',
                HasDigitalCopy INTEGER NOT NULL DEFAULT 1,
                PurchasePrice REAL NULL,
                PurchaseDate TEXT NOT NULL DEFAULT '',
                StorageLocation TEXT NOT NULL DEFAULT '',
                Notes TEXT NOT NULL DEFAULT '',
                CreatedAt TEXT NOT NULL DEFAULT (datetime('now')),
                UpdatedAt TEXT NOT NULL DEFAULT (datetime('now')),
                FOREIGN KEY (TVShowId) REFERENCES TVShows(Id) ON DELETE CASCADE,
                UNIQUE (TVShowId, SeasonNumber)
            );

            INSERT OR IGNORE INTO TVSeasons
                (TVShowId, SeasonNumber, Name, IsOwned, Format, HasDigitalCopy)
            WITH RECURSIVE SeasonNumbers(TVShowId, SeasonNumber, MaxSeason) AS
            (
                SELECT Id, 1, Seasons FROM TVShows WHERE Seasons > 0
                UNION ALL
                SELECT TVShowId, SeasonNumber + 1, MaxSeason
                FROM SeasonNumbers
                WHERE SeasonNumber < MaxSeason
            )
            SELECT TVShowId, SeasonNumber, 'Season ' || SeasonNumber, 1, 'DVD', 1
            FROM SeasonNumbers;
            """);
    }

    private static void ApplyVersion8Migration(
        SqliteConnection connection,
        SqliteTransaction transaction)
    {
        EnsureColumn(connection, transaction, "Wishlist", "MediaType", "TEXT NOT NULL DEFAULT 'Movie'");
        EnsureColumn(connection, transaction, "Wishlist", "NormalizedTitle", "TEXT NOT NULL DEFAULT ''");
        EnsureColumn(connection, transaction, "Wishlist", "Year", "INTEGER NOT NULL DEFAULT 0");
        EnsureColumn(connection, transaction, "Wishlist", "TMDbId", "INTEGER NULL");
        EnsureColumn(connection, transaction, "Wishlist", "PreferredFormat", "TEXT NOT NULL DEFAULT ''");
        EnsureColumn(connection, transaction, "Wishlist", "TargetPrice", "REAL NULL");
        EnsureColumn(connection, transaction, "Wishlist", "PreferredStore", "TEXT NOT NULL DEFAULT ''");
        EnsureColumn(connection, transaction, "Wishlist", "Notes", "TEXT NOT NULL DEFAULT ''");
        EnsureColumn(connection, transaction, "Wishlist", "DateAdded", "TEXT NOT NULL DEFAULT ''");
        EnsureColumn(connection, transaction, "Wishlist", "LastUpdated", "TEXT NOT NULL DEFAULT ''");
        EnsureColumn(connection, transaction, "Wishlist", "IsPurchased", "INTEGER NOT NULL DEFAULT 0");

        ExecuteNonQuery(
            connection,
            transaction,
            """
            UPDATE Wishlist
            SET MediaType = 'Movie'
            WHERE MediaType IS NULL OR TRIM(MediaType) = '';

            UPDATE Wishlist
            SET DateAdded = datetime('now')
            WHERE DateAdded IS NULL OR TRIM(DateAdded) = '';

            UPDATE Wishlist
            SET LastUpdated = DateAdded
            WHERE LastUpdated IS NULL OR TRIM(LastUpdated) = '';
            """);

        BackfillWishlistNormalizedTitles(connection, transaction);
    }

    private static void BackfillWishlistNormalizedTitles(
        SqliteConnection connection,
        SqliteTransaction transaction)
    {
        List<(int Id, string Title)> rows = [];

        using (SqliteCommand selectCommand = connection.CreateCommand())
        {
            selectCommand.Transaction = transaction;
            selectCommand.CommandText =
                """
                SELECT Id, Title
                FROM Wishlist
                WHERE NormalizedTitle IS NULL
                   OR TRIM(NormalizedTitle) = '';
                """;

            using SqliteDataReader reader = selectCommand.ExecuteReader();

            while (reader.Read())
            {
                rows.Add((
                    reader.GetInt32(0),
                    reader.IsDBNull(1) ? string.Empty : reader.GetString(1)));
            }
        }

        foreach ((int id, string title) in rows)
        {
            using SqliteCommand updateCommand = connection.CreateCommand();
            updateCommand.Transaction = transaction;
            updateCommand.CommandText =
                """
                UPDATE Wishlist
                SET NormalizedTitle = $normalizedTitle
                WHERE Id = $id;
                """;
            updateCommand.Parameters.AddWithValue(
                "$normalizedTitle",
                MediaDuplicateService.NormalizeTitle(title));
            updateCommand.Parameters.AddWithValue("$id", id);
            updateCommand.ExecuteNonQuery();
        }
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

            CREATE INDEX IF NOT EXISTS IX_TVSeasons_TVShowId
                ON TVSeasons (TVShowId);

            CREATE UNIQUE INDEX IF NOT EXISTS IX_TVSeasons_ShowSeason
                ON TVSeasons (TVShowId, SeasonNumber);

            CREATE INDEX IF NOT EXISTS IX_Wishlist_Title
                ON Wishlist (Title);

            CREATE INDEX IF NOT EXISTS IX_Wishlist_NormalizedTitle
                ON Wishlist (NormalizedTitle);

            CREATE INDEX IF NOT EXISTS IX_Wishlist_NormalizedTitle_Year
                ON Wishlist (NormalizedTitle, Year);

            CREATE INDEX IF NOT EXISTS IX_Wishlist_IsPurchased
                ON Wishlist (IsPurchased);

            CREATE INDEX IF NOT EXISTS IX_Wishlist_Priority
                ON Wishlist (Priority);

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
