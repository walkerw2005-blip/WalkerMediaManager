using Microsoft.Data.Sqlite;
using WalkerMediaManager.Services;

namespace WalkerMediaManager.Data;

public sealed class DatabaseService
{
    public async Task InitializeAsync()
    {
        Directory.CreateDirectory(AppPaths.DataDirectory);
        await using var connection = new SqliteConnection($"Data Source={AppPaths.DatabasePath}");
        await connection.OpenAsync();

        var command = connection.CreateCommand();
        command.CommandText = """
        PRAGMA foreign_keys = ON;

        CREATE TABLE IF NOT EXISTS AppMetadata (
            Key TEXT PRIMARY KEY,
            Value TEXT NOT NULL
        );

        CREATE TABLE IF NOT EXISTS PlexLibraries (
            Id INTEGER PRIMARY KEY AUTOINCREMENT,
            PlexKey TEXT NOT NULL UNIQUE,
            Title TEXT NOT NULL,
            Type TEXT NOT NULL,
            IsSelected INTEGER NOT NULL DEFAULT 1,
            LastSeenUtc TEXT NOT NULL
        );

        CREATE TABLE IF NOT EXISTS Movies (
            Id INTEGER PRIMARY KEY AUTOINCREMENT,
            PlexRatingKey TEXT UNIQUE,
            Title TEXT NOT NULL,
            SortTitle TEXT,
            Year INTEGER,
            Edition TEXT,
            RuntimeMinutes INTEGER,
            ContentRating TEXT,
            LibraryTitle TEXT,
            FilePath TEXT,
            DateAddedUtc TEXT,
            CreatedUtc TEXT NOT NULL,
            UpdatedUtc TEXT NOT NULL
        );

        CREATE TABLE IF NOT EXISTS TvSeries (
            Id INTEGER PRIMARY KEY AUTOINCREMENT,
            PlexRatingKey TEXT UNIQUE,
            Title TEXT NOT NULL,
            SortTitle TEXT,
            StartYear INTEGER,
            EndYear INTEGER,
            ContentRating TEXT,
            Network TEXT,
            LibraryTitle TEXT,
            DateAddedUtc TEXT,
            CreatedUtc TEXT NOT NULL,
            UpdatedUtc TEXT NOT NULL
        );

        CREATE TABLE IF NOT EXISTS Seasons (
            Id INTEGER PRIMARY KEY AUTOINCREMENT,
            TvSeriesId INTEGER NOT NULL,
            PlexRatingKey TEXT UNIQUE,
            SeasonNumber INTEGER NOT NULL,
            Title TEXT,
            FOREIGN KEY (TvSeriesId) REFERENCES TvSeries(Id) ON DELETE CASCADE
        );

        CREATE TABLE IF NOT EXISTS Episodes (
            Id INTEGER PRIMARY KEY AUTOINCREMENT,
            SeasonId INTEGER NOT NULL,
            PlexRatingKey TEXT UNIQUE,
            EpisodeNumber INTEGER NOT NULL,
            Title TEXT,
            AirDate TEXT,
            RuntimeMinutes INTEGER,
            FOREIGN KEY (SeasonId) REFERENCES Seasons(Id) ON DELETE CASCADE
        );

        CREATE TABLE IF NOT EXISTS Purchases (
            Id INTEGER PRIMARY KEY AUTOINCREMENT,
            MediaType TEXT NOT NULL,
            MediaId INTEGER,
            DisplayTitle TEXT NOT NULL,
            PurchaseDate TEXT,
            Store TEXT,
            Price REAL,
            Format TEXT,
            Upc TEXT,
            Notes TEXT,
            CreatedUtc TEXT NOT NULL
        );
        """;
        await command.ExecuteNonQueryAsync();
    }

    public async Task SaveLibrariesAsync(IEnumerable<Models.PlexLibrary> libraries)
    {
        await using var connection = new SqliteConnection($"Data Source={AppPaths.DatabasePath}");
        await connection.OpenAsync();
        await using var transaction = await connection.BeginTransactionAsync();

        foreach (var library in libraries)
        {
            var command = connection.CreateCommand();
            command.Transaction = (SqliteTransaction)transaction;
            command.CommandText = """
                INSERT INTO PlexLibraries (PlexKey, Title, Type, IsSelected, LastSeenUtc)
                VALUES ($key, $title, $type, 1, $seen)
                ON CONFLICT(PlexKey) DO UPDATE SET
                    Title = excluded.Title,
                    Type = excluded.Type,
                    LastSeenUtc = excluded.LastSeenUtc;
                """;
            command.Parameters.AddWithValue("$key", library.Key);
            command.Parameters.AddWithValue("$title", library.Title);
            command.Parameters.AddWithValue("$type", library.Type);
            command.Parameters.AddWithValue("$seen", DateTimeOffset.UtcNow.ToString("O"));
            await command.ExecuteNonQueryAsync();
        }

        await transaction.CommitAsync();
    }

    public async Task<int> GetLibraryCountAsync()
    {
        await using var connection = new SqliteConnection($"Data Source={AppPaths.DatabasePath}");
        await connection.OpenAsync();
        var command = connection.CreateCommand();
        command.CommandText = "SELECT COUNT(*) FROM PlexLibraries";
        return Convert.ToInt32(await command.ExecuteScalarAsync());
    }
}
