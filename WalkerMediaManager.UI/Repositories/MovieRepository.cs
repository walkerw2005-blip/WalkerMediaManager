using Microsoft.Data.Sqlite;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using WalkerMediaManager.UI.Data;
using WalkerMediaManager.UI.Models;

namespace WalkerMediaManager.UI.Repositories;

public sealed class MovieRepository
{
    public async Task<List<Movie>> GetAllAsync()
    {
        List<Movie> movies = [];

        await using SqliteConnection connection =
            new($"Data Source={DatabaseService.DatabasePath}");

        await connection.OpenAsync();

        await using SqliteCommand command = connection.CreateCommand();

        command.CommandText =
            """
            SELECT
                Id,
                Title,
                ReleaseYear,
                Rating,
                Runtime,
                Genre,
                Director,
                PlexGuid,
                TMDbId,
                IMDbId
            FROM Movies
            ORDER BY Title;
            """;

        await using SqliteDataReader reader =
            await command.ExecuteReaderAsync();

        while (await reader.ReadAsync())
        {
            movies.Add(new Movie
            {
                Id = reader.GetInt32(0),
                Title = reader.IsDBNull(1) ? string.Empty : reader.GetString(1),
                ReleaseYear = reader.IsDBNull(2) ? 0 : reader.GetInt32(2),
                Rating = reader.IsDBNull(3) ? string.Empty : reader.GetString(3),
                Runtime = reader.IsDBNull(4) ? 0 : reader.GetInt32(4),
                Genre = reader.IsDBNull(5) ? string.Empty : reader.GetString(5),
                Director = reader.IsDBNull(6) ? string.Empty : reader.GetString(6),
                PlexGuid = reader.IsDBNull(7) ? string.Empty : reader.GetString(7),
                TMDbId = reader.IsDBNull(8) ? null : reader.GetInt32(8),
                IMDbId = reader.IsDBNull(9) ? string.Empty : reader.GetString(9)
            });
        }

        return movies;
    }

    public async Task<int> AddAsync(Movie movie)
    {
        await using SqliteConnection connection =
            new($"Data Source={DatabaseService.DatabasePath}");

        await connection.OpenAsync();

        await using SqliteCommand command = connection.CreateCommand();

        command.CommandText =
            """
            INSERT INTO Movies
            (
                Title,
                ReleaseYear,
                Rating,
                Runtime,
                Genre,
                Director,
                PlexGuid,
                TMDbId,
                IMDbId
            )
            VALUES
            (
                $title,
                $releaseYear,
                $rating,
                $runtime,
                $genre,
                $director,
                $plexGuid,
                $tmdbId,
                $imdbId
            );

            SELECT last_insert_rowid();
            """;

        command.Parameters.AddWithValue("$title", movie.Title);
        command.Parameters.AddWithValue("$releaseYear", movie.ReleaseYear);
        command.Parameters.AddWithValue("$rating", movie.Rating);
        command.Parameters.AddWithValue("$runtime", movie.Runtime);
        command.Parameters.AddWithValue("$genre", movie.Genre);
        command.Parameters.AddWithValue("$director", movie.Director);
        command.Parameters.AddWithValue("$plexGuid", movie.PlexGuid);
        command.Parameters.AddWithValue(
            "$tmdbId",
            movie.TMDbId is null ? DBNull.Value : movie.TMDbId.Value);
        command.Parameters.AddWithValue("$imdbId", movie.IMDbId);

        object? result = await command.ExecuteScalarAsync();

        return Convert.ToInt32(result);
    }

    public async Task<int> CountAsync()
    {
        await using SqliteConnection connection =
            new($"Data Source={DatabaseService.DatabasePath}");

        await connection.OpenAsync();

        await using SqliteCommand command = connection.CreateCommand();

        command.CommandText = "SELECT COUNT(*) FROM Movies;";

        object? result = await command.ExecuteScalarAsync();

        return Convert.ToInt32(result);
    }
}