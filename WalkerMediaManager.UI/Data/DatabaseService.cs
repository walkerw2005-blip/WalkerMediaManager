using System;
using System.Collections.Generic;
using System.IO;
using Microsoft.Data.Sqlite;

namespace WalkerMediaManager.UI.Data;

public static class DatabaseService
{
    private const int CurrentDatabaseVersion = 2;

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
