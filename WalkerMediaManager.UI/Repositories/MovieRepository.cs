using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading.Tasks;
using Microsoft.Data.Sqlite;
using WalkerMediaManager.UI.Data;
using WalkerMediaManager.UI.Models;
using WalkerMediaManager.UI.Services;

namespace WalkerMediaManager.UI.Repositories;

public sealed class MovieRepository
{
    private const string MovieLibraryKeySettingKey = "PlexMovieLibraryKey";
    private const string SlideshowLibraryKeySettingKey = "PlexSlideshowLibraryKey";

    public Task<List<Movie>> GetAllAsync() =>
        GetBySelectedLibraryAsync(
            MovieLibraryKeySettingKey,
            "Movies",
            "movies");

    public Task<List<Movie>> GetSlideshowsAsync() =>
        GetBySelectedLibraryAsync(
            SlideshowLibraryKeySettingKey,
            "Slide Shows",
            "slideshows");

    private static async Task<List<Movie>> GetBySelectedLibraryAsync(
        string settingsKey,
        string fallbackLibraryTitle,
        string category)
    {
        List<Movie> movies = [];
        Stopwatch stopwatch = Stopwatch.StartNew();

        string selectedLibraryKey = SettingsService.GetString(settingsKey);

        DiagnosticsService.Log(
            $"MovieRepository loading {category}. " +
            $"LibraryKey='{selectedLibraryKey}'. " +
            $"Database: {DatabaseService.DatabasePath}");

        await using SqliteConnection connection =
            new($"Data Source={DatabaseService.DatabasePath}");

        await connection.OpenAsync();

        await using SqliteCommand command = connection.CreateCommand();

        command.CommandText =
            """
            SELECT Id, Title, ReleaseYear, Rating, Runtime, Genre, Director,
                   PlexRatingKey, PlexGuid, TMDbId, IMDbId, SortTitle, Studio,
                   Summary, PosterPath, BackgroundPath, LastSynced,
                   PlexLibraryKey, PlexLibraryTitle
            FROM Movies
            WHERE
                (
                    TRIM($libraryKey) <> ''
                    AND PlexLibraryKey = $libraryKey
                )
                OR
                (
                    TRIM($libraryKey) = ''
                    AND LOWER(TRIM(PlexLibraryTitle)) = LOWER(TRIM($libraryTitle))
                )
            ORDER BY
                CASE
                    WHEN TRIM(SortTitle) <> '' THEN SortTitle
                    ELSE Title
                END COLLATE NOCASE;
            """;

        command.Parameters.AddWithValue(
            "$libraryKey",
            selectedLibraryKey);

        command.Parameters.AddWithValue(
            "$libraryTitle",
            fallbackLibraryTitle);

        try
        {
            await using SqliteDataReader reader =
                await command.ExecuteReaderAsync();

            while (await reader.ReadAsync())
            {
                movies.Add(ReadMovie(reader));
            }

            stopwatch.Stop();

            DiagnosticsService.Log(
                $"MovieRepository returned {movies.Count} {category} " +
                $"in {stopwatch.ElapsedMilliseconds} ms.");

            return movies;
        }
        catch (Exception exception)
        {
            stopwatch.Stop();

            DiagnosticsService.LogException(
                $"MovieRepository failed to load {category} " +
                $"after {stopwatch.ElapsedMilliseconds} ms.",
                exception);

            throw;
        }
    }

    public async Task<Movie?> GetByIdAsync(int movieId)
    {
        await using SqliteConnection connection =
            new($"Data Source={DatabaseService.DatabasePath}");

        await connection.OpenAsync();

        await using SqliteCommand command = connection.CreateCommand();

        command.CommandText =
            """
            SELECT Id, Title, ReleaseYear, Rating, Runtime, Genre, Director,
                   PlexRatingKey, PlexGuid, TMDbId, IMDbId, SortTitle, Studio,
                   Summary, PosterPath, BackgroundPath, LastSynced,
                   PlexLibraryKey, PlexLibraryTitle
            FROM Movies
            WHERE Id = $id;
            """;

        command.Parameters.AddWithValue("$id", movieId);

        await using SqliteDataReader reader =
            await command.ExecuteReaderAsync();

        return await reader.ReadAsync()
            ? ReadMovie(reader)
            : null;
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
                PlexRatingKey,
                PlexGuid,
                TMDbId,
                IMDbId,
                SortTitle,
                Studio,
                Summary,
                PosterPath,
                BackgroundPath,
                LastSynced,
                PlexLibraryKey,
                PlexLibraryTitle
            )
            VALUES
            (
                $title,
                $year,
                $rating,
                $runtime,
                $genre,
                $director,
                $plexRatingKey,
                $plexGuid,
                $tmdbId,
                $imdbId,
                $sortTitle,
                $studio,
                $summary,
                $poster,
                $background,
                $lastSynced,
                $plexLibraryKey,
                $plexLibraryTitle
            );

            SELECT last_insert_rowid();
            """;

        AddParameters(command, movie);

        return Convert.ToInt32(
            await command.ExecuteScalarAsync());
    }

    public async Task UpdateAsync(Movie movie)
    {
        await using SqliteConnection connection =
            new($"Data Source={DatabaseService.DatabasePath}");

        await connection.OpenAsync();

        await using SqliteCommand command = connection.CreateCommand();

        command.CommandText =
            """
            UPDATE Movies
            SET
                Title = $title,
                ReleaseYear = $year,
                Rating = $rating,
                Runtime = $runtime,
                Genre = $genre,
                Director = $director,
                PlexRatingKey = $plexRatingKey,
                PlexGuid = $plexGuid,
                TMDbId = $tmdbId,
                IMDbId = $imdbId,
                SortTitle = $sortTitle,
                Studio = $studio,
                Summary = $summary,
                PosterPath = $poster,
                BackgroundPath = $background,
                LastSynced = $lastSynced,
                PlexLibraryKey = $plexLibraryKey,
                PlexLibraryTitle = $plexLibraryTitle
            WHERE Id = $id;
            """;

        AddParameters(command, movie);
        command.Parameters.AddWithValue("$id", movie.Id);

        await command.ExecuteNonQueryAsync();
    }

    public async Task DeleteAsync(int movieId)
    {
        await using SqliteConnection connection =
            new($"Data Source={DatabaseService.DatabasePath}");

        await connection.OpenAsync();

        await using SqliteCommand command = connection.CreateCommand();

        command.CommandText =
            """
            DELETE FROM Movies
            WHERE Id = $id;
            """;

        command.Parameters.AddWithValue("$id", movieId);

        await command.ExecuteNonQueryAsync();
    }

    public Task<int> CountAsync() =>
        CountBySelectedLibraryAsync(
            MovieLibraryKeySettingKey,
            "Movies");

    public Task<int> CountSlideshowsAsync() =>
        CountBySelectedLibraryAsync(
            SlideshowLibraryKeySettingKey,
            "Slide Shows");

    private static async Task<int> CountBySelectedLibraryAsync(
        string settingsKey,
        string fallbackLibraryTitle)
    {
        string selectedLibraryKey = SettingsService.GetString(settingsKey);

        await using SqliteConnection connection =
            new($"Data Source={DatabaseService.DatabasePath}");

        await connection.OpenAsync();

        await using SqliteCommand command = connection.CreateCommand();

        command.CommandText =
            """
            SELECT COUNT(*)
            FROM Movies
            WHERE
                (
                    TRIM($libraryKey) <> ''
                    AND PlexLibraryKey = $libraryKey
                )
                OR
                (
                    TRIM($libraryKey) = ''
                    AND LOWER(TRIM(PlexLibraryTitle)) = LOWER(TRIM($libraryTitle))
                );
            """;

        command.Parameters.AddWithValue(
            "$libraryKey",
            selectedLibraryKey);

        command.Parameters.AddWithValue(
            "$libraryTitle",
            fallbackLibraryTitle);

        return Convert.ToInt32(
            await command.ExecuteScalarAsync());
    }

    private static Movie ReadMovie(SqliteDataReader reader) =>
        new()
        {
            Id = GetInt32(reader, 0),
            Title = GetString(reader, 1),
            ReleaseYear = GetInt32(reader, 2),
            Rating = GetString(reader, 3),
            Runtime = GetInt32(reader, 4),
            Genre = GetString(reader, 5),
            Director = GetString(reader, 6),
            PlexRatingKey = GetString(reader, 7),
            PlexGuid = GetString(reader, 8),
            TMDbId = GetNullableInt32(reader, 9),
            IMDbId = GetString(reader, 10),
            SortTitle = GetString(reader, 11),
            Studio = GetString(reader, 12),
            Summary = GetString(reader, 13),
            PosterPath = GetString(reader, 14),
            BackgroundPath = GetString(reader, 15),
            LastSynced = GetString(reader, 16),
            PlexLibraryKey = GetString(reader, 17),
            PlexLibraryTitle = GetString(reader, 18),
            Owned = true
        };

    private static void AddParameters(
        SqliteCommand command,
        Movie movie)
    {
        command.Parameters.AddWithValue("$title", movie.Title);
        command.Parameters.AddWithValue("$year", movie.ReleaseYear);
        command.Parameters.AddWithValue("$rating", movie.Rating);
        command.Parameters.AddWithValue("$runtime", movie.Runtime);
        command.Parameters.AddWithValue("$genre", movie.Genre);
        command.Parameters.AddWithValue("$director", movie.Director);
        command.Parameters.AddWithValue("$plexRatingKey", movie.PlexRatingKey);
        command.Parameters.AddWithValue("$plexGuid", movie.PlexGuid);
        command.Parameters.AddWithValue(
            "$tmdbId",
            movie.TMDbId.HasValue
                ? movie.TMDbId.Value
                : DBNull.Value);
        command.Parameters.AddWithValue("$imdbId", movie.IMDbId);
        command.Parameters.AddWithValue("$sortTitle", movie.SortTitle);
        command.Parameters.AddWithValue("$studio", movie.Studio);
        command.Parameters.AddWithValue("$summary", movie.Summary);
        command.Parameters.AddWithValue("$poster", movie.PosterPath);
        command.Parameters.AddWithValue("$background", movie.BackgroundPath);
        command.Parameters.AddWithValue("$lastSynced", movie.LastSynced);
        command.Parameters.AddWithValue("$plexLibraryKey", movie.PlexLibraryKey);
        command.Parameters.AddWithValue("$plexLibraryTitle", movie.PlexLibraryTitle);
    }

    private static string GetString(
        SqliteDataReader reader,
        int ordinal)
    {
        if (reader.IsDBNull(ordinal))
        {
            return string.Empty;
        }

        return Convert.ToString(reader.GetValue(ordinal))
            ?? string.Empty;
    }

    private static int GetInt32(
        SqliteDataReader reader,
        int ordinal)
    {
        if (reader.IsDBNull(ordinal))
        {
            return 0;
        }

        object value = reader.GetValue(ordinal);

        return int.TryParse(
            Convert.ToString(value),
            out int result)
            ? result
            : 0;
    }

    private static int? GetNullableInt32(
        SqliteDataReader reader,
        int ordinal)
    {
        if (reader.IsDBNull(ordinal))
        {
            return null;
        }

        object value = reader.GetValue(ordinal);

        return int.TryParse(
            Convert.ToString(value),
            out int result)
            ? result
            : null;
    }
}
