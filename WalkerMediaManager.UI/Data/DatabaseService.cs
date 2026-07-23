using System;
using System.IO;
using Microsoft.Data.Sqlite;

namespace WalkerMediaManager.UI.Data;

public static class DatabaseService
{
    public static string DatabasePath =>
        Path.Combine(
            Environment.GetFolderPath(
                Environment.SpecialFolder.LocalApplicationData),
            "WalkerMediaManager",
            "walker.db");

    public static void Initialize()
    {
        string? databaseFolder =
            Path.GetDirectoryName(DatabasePath);

        if (string.IsNullOrWhiteSpace(databaseFolder))
        {
            throw new InvalidOperationException(
                "The database folder could not be determined.");
        }

        Directory.CreateDirectory(databaseFolder);

        using SqliteConnection connection =
            new($"Data Source={DatabasePath}");

        connection.Open();

        using SqliteCommand command =
            connection.CreateCommand();

        command.CommandText =
            """
            PRAGMA foreign_keys = ON;

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

            CREATE INDEX IF NOT EXISTS IX_Movies_Title
                ON Movies (Title);

            CREATE INDEX IF NOT EXISTS IX_TVShows_Title
                ON TVShows (Title);

            CREATE INDEX IF NOT EXISTS IX_Wishlist_Title
                ON Wishlist (Title);

            CREATE INDEX IF NOT EXISTS IX_Collections_Name
                ON Collections (Name);
            """;

        command.ExecuteNonQuery();
    }
}