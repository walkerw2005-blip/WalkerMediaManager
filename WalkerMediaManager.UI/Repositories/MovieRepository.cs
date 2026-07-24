using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.Data.Sqlite;
using WalkerMediaManager.UI.Data;
using WalkerMediaManager.UI.Models;

namespace WalkerMediaManager.UI.Repositories;

public sealed class MovieRepository
{
    public async Task<List<Movie>> GetAllAsync()
    {
        List<Movie> movies = [];
        await using SqliteConnection connection = new($"Data Source={DatabaseService.DatabasePath}");
        await connection.OpenAsync();
        await using SqliteCommand command = connection.CreateCommand();
        command.CommandText =
            """
            SELECT Id, Title, ReleaseYear, Rating, Runtime, Genre, Director,
                   PlexRatingKey, PlexGuid, TMDbId, IMDbId, SortTitle, Studio,
                   Summary, PosterPath, BackgroundPath, LastSynced
            FROM Movies
            ORDER BY CASE WHEN TRIM(SortTitle) <> '' THEN SortTitle ELSE Title END COLLATE NOCASE;
            """;

        await using SqliteDataReader reader = await command.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            movies.Add(new Movie
            {
                Id = reader.GetInt32(0),
                Title = GetString(reader, 1),
                ReleaseYear = GetInt32(reader, 2),
                Rating = GetString(reader, 3),
                Runtime = GetInt32(reader, 4),
                Genre = GetString(reader, 5),
                Director = GetString(reader, 6),
                PlexRatingKey = GetString(reader, 7),
                PlexGuid = GetString(reader, 8),
                TMDbId = reader.IsDBNull(9) ? null : reader.GetInt32(9),
                IMDbId = GetString(reader, 10),
                SortTitle = GetString(reader, 11),
                Studio = GetString(reader, 12),
                Summary = GetString(reader, 13),
                PosterPath = GetString(reader, 14),
                BackgroundPath = GetString(reader, 15),
                LastSynced = GetString(reader, 16),
                Owned = true
            });
        }
        return movies;
    }

    public async Task<Movie?> GetByIdAsync(int movieId)
    {
        await using SqliteConnection connection = new($"Data Source={DatabaseService.DatabasePath}");
        await connection.OpenAsync();
        await using SqliteCommand command = connection.CreateCommand();
        command.CommandText =
            """
            SELECT Id, Title, ReleaseYear, Rating, Runtime, Genre, Director,
                   PlexRatingKey, PlexGuid, TMDbId, IMDbId, SortTitle, Studio,
                   Summary, PosterPath, BackgroundPath, LastSynced
            FROM Movies
            WHERE Id = $id;
            """;
        command.Parameters.AddWithValue("$id", movieId);

        await using SqliteDataReader reader = await command.ExecuteReaderAsync();
        if (!await reader.ReadAsync()) return null;

        return new Movie
        {
            Id = reader.GetInt32(0),
            Title = GetString(reader, 1),
            ReleaseYear = GetInt32(reader, 2),
            Rating = GetString(reader, 3),
            Runtime = GetInt32(reader, 4),
            Genre = GetString(reader, 5),
            Director = GetString(reader, 6),
            PlexRatingKey = GetString(reader, 7),
            PlexGuid = GetString(reader, 8),
            TMDbId = reader.IsDBNull(9) ? null : reader.GetInt32(9),
            IMDbId = GetString(reader, 10),
            SortTitle = GetString(reader, 11),
            Studio = GetString(reader, 12),
            Summary = GetString(reader, 13),
            PosterPath = GetString(reader, 14),
            BackgroundPath = GetString(reader, 15),
            LastSynced = GetString(reader, 16),
            Owned = true
        };
    }

    public async Task<int> AddAsync(Movie movie)
    {
        await using SqliteConnection connection = new($"Data Source={DatabaseService.DatabasePath}");
        await connection.OpenAsync();
        await using SqliteCommand command = connection.CreateCommand();
        command.CommandText =
            """
            INSERT INTO Movies
            (Title, ReleaseYear, Rating, Runtime, Genre, Director, PlexRatingKey,
             PlexGuid, TMDbId, IMDbId, SortTitle, Studio, Summary, PosterPath,
             BackgroundPath, LastSynced)
            VALUES
            ($title, $year, $rating, $runtime, $genre, $director, $plexRatingKey,
             $plexGuid, $tmdbId, $imdbId, $sortTitle, $studio, $summary, $poster,
             $background, $lastSynced);
            SELECT last_insert_rowid();
            """;
        AddParameters(command, movie);
        return Convert.ToInt32(await command.ExecuteScalarAsync());
    }

    public async Task UpdateAsync(Movie movie)
    {
        await using SqliteConnection connection = new($"Data Source={DatabaseService.DatabasePath}");
        await connection.OpenAsync();
        await using SqliteCommand command = connection.CreateCommand();
        command.CommandText =
            """
            UPDATE Movies SET
                Title=$title, ReleaseYear=$year, Rating=$rating, Runtime=$runtime,
                Genre=$genre, Director=$director, PlexRatingKey=$plexRatingKey,
                PlexGuid=$plexGuid, TMDbId=$tmdbId, IMDbId=$imdbId,
                SortTitle=$sortTitle, Studio=$studio, Summary=$summary,
                PosterPath=$poster, BackgroundPath=$background, LastSynced=$lastSynced
            WHERE Id=$id;
            """;
        AddParameters(command, movie);
        command.Parameters.AddWithValue("$id", movie.Id);
        await command.ExecuteNonQueryAsync();
    }

    public async Task DeleteAsync(int movieId)
    {
        await using SqliteConnection connection = new($"Data Source={DatabaseService.DatabasePath}");
        await connection.OpenAsync();
        await using SqliteCommand command = connection.CreateCommand();
        command.CommandText = "DELETE FROM Movies WHERE Id=$id;";
        command.Parameters.AddWithValue("$id", movieId);
        await command.ExecuteNonQueryAsync();
    }

    public async Task<int> CountAsync()
    {
        await using SqliteConnection connection = new($"Data Source={DatabaseService.DatabasePath}");
        await connection.OpenAsync();
        await using SqliteCommand command = connection.CreateCommand();
        command.CommandText = "SELECT COUNT(*) FROM Movies;";
        return Convert.ToInt32(await command.ExecuteScalarAsync());
    }

    private static void AddParameters(SqliteCommand command, Movie movie)
    {
        command.Parameters.AddWithValue("$title", movie.Title);
        command.Parameters.AddWithValue("$year", movie.ReleaseYear);
        command.Parameters.AddWithValue("$rating", movie.Rating);
        command.Parameters.AddWithValue("$runtime", movie.Runtime);
        command.Parameters.AddWithValue("$genre", movie.Genre);
        command.Parameters.AddWithValue("$director", movie.Director);
        command.Parameters.AddWithValue("$plexRatingKey", movie.PlexRatingKey);
        command.Parameters.AddWithValue("$plexGuid", movie.PlexGuid);
        command.Parameters.AddWithValue("$tmdbId", movie.TMDbId.HasValue ? movie.TMDbId.Value : DBNull.Value);
        command.Parameters.AddWithValue("$imdbId", movie.IMDbId);
        command.Parameters.AddWithValue("$sortTitle", movie.SortTitle);
        command.Parameters.AddWithValue("$studio", movie.Studio);
        command.Parameters.AddWithValue("$summary", movie.Summary);
        command.Parameters.AddWithValue("$poster", movie.PosterPath);
        command.Parameters.AddWithValue("$background", movie.BackgroundPath);
        command.Parameters.AddWithValue("$lastSynced", movie.LastSynced);
    }

    private static string GetString(SqliteDataReader reader, int ordinal) =>
        reader.IsDBNull(ordinal) ? string.Empty : reader.GetString(ordinal);

    private static int GetInt32(SqliteDataReader reader, int ordinal) =>
        reader.IsDBNull(ordinal) ? 0 : reader.GetInt32(ordinal);
}
