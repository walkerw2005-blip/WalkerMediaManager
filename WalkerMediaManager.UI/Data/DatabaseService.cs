using Microsoft.Data.Sqlite;
using System;
using System.IO;

namespace WalkerMediaManager.UI.Data;

public static class DatabaseService
{
    public static string DatabasePath =>
        Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "WalkerMediaManager",
            "walker.db");

    public static void Initialize()
    {
        Directory.CreateDirectory(
            Path.GetDirectoryName(DatabasePath)!);

        using var connection =
            new SqliteConnection($"Data Source={DatabasePath}");

        connection.Open();

        var command = connection.CreateCommand();

        command.CommandText =
        @"
        CREATE TABLE IF NOT EXISTS Movies
        (
            Id INTEGER PRIMARY KEY AUTOINCREMENT,
            Title TEXT,
            ReleaseYear INTEGER,
            Rating TEXT,
            Runtime INTEGER,
            Genre TEXT,
            Director TEXT,
            PlexGuid TEXT,
            TMDbId INTEGER,
            IMDbId TEXT
        );

        CREATE TABLE IF NOT EXISTS TVShows
        (
            Id INTEGER PRIMARY KEY AUTOINCREMENT,
            Title TEXT,
            Seasons INTEGER,
            Episodes INTEGER
        );

        CREATE TABLE IF NOT EXISTS Wishlist
        (
            Id INTEGER PRIMARY KEY AUTOINCREMENT,
            Title TEXT,
            Priority INTEGER
        );
        ";

        command.ExecuteNonQuery();
    }
}