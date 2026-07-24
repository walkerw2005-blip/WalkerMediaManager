using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Data.Sqlite;
using WalkerMediaManager.UI.Data;
using WalkerMediaManager.UI.Models;

namespace WalkerMediaManager.UI.Services;

public sealed class PlexMovieSyncService
{
    private readonly PlexService _plexService = new();

    public async Task<PlexSyncResult> SyncMoviesAsync(
        string serverUrl,
        string token,
        string librarySectionKey,
        IProgress<string>? progress = null,
        CancellationToken cancellationToken = default)
    {
        IReadOnlyList<PlexMovie> plexMovies =
            await _plexService.GetMoviesAsync(
                serverUrl,
                token,
                librarySectionKey,
                cancellationToken);

        PlexSyncResult result = new();

        await using SqliteConnection connection =
            new($"Data Source={DatabaseService.DatabasePath}");

        await connection.OpenAsync(cancellationToken);

        using SqliteTransaction transaction = connection.BeginTransaction();

        try
        {
            for (int index = 0; index < plexMovies.Count; index++)
            {
                cancellationToken.ThrowIfCancellationRequested();

                PlexMovie plexMovie = plexMovies[index];
                progress?.Report(
                    $"Syncing {index + 1} of {plexMovies.Count}: {plexMovie.Title}");

                try
                {
                    int? existingId = await FindByPlexGuidAsync(
                        connection,
                        transaction,
                        plexMovie.PlexGuid,
                        cancellationToken);

                    if (existingId.HasValue)
                    {
                        await UpdateFromPlexAsync(
                            connection,
                            transaction,
                            existingId.Value,
                            plexMovie,
                            cancellationToken);

                        result.UpdatedCount++;
                        continue;
                    }

                    existingId = await FindByTitleAndYearAsync(
                        connection,
                        transaction,
                        plexMovie.Title,
                        plexMovie.ReleaseYear,
                        cancellationToken);

                    if (existingId.HasValue)
                    {
                        await MatchExistingMovieAsync(
                            connection,
                            transaction,
                            existingId.Value,
                            plexMovie,
                            cancellationToken);

                        result.MatchedCount++;
                        continue;
                    }

                    await InsertMovieAsync(
                        connection,
                        transaction,
                        plexMovie,
                        cancellationToken);

                    result.AddedCount++;
                }
                catch
                {
                    result.FailedCount++;
                }
            }

            transaction.Commit();
        }
        catch
        {
            transaction.Rollback();
            throw;
        }

        return result;
    }

    private static async Task<int?> FindByPlexGuidAsync(
        SqliteConnection connection,
        SqliteTransaction transaction,
        string plexGuid,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(plexGuid))
        {
            return null;
        }

        await using SqliteCommand command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText =
            "SELECT Id FROM Movies WHERE PlexGuid = $plexGuid LIMIT 1;";
        command.Parameters.AddWithValue("$plexGuid", plexGuid);

        object? value = await command.ExecuteScalarAsync(cancellationToken);
        return value is null or DBNull ? null : Convert.ToInt32(value);
    }

    private static async Task<int?> FindByTitleAndYearAsync(
        SqliteConnection connection,
        SqliteTransaction transaction,
        string title,
        int releaseYear,
        CancellationToken cancellationToken)
    {
        await using SqliteCommand command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText =
            """
            SELECT Id
            FROM Movies
            WHERE LOWER(TRIM(Title)) = LOWER(TRIM($title))
              AND ($releaseYear = 0 OR ReleaseYear = 0 OR ReleaseYear = $releaseYear)
            ORDER BY CASE WHEN ReleaseYear = $releaseYear THEN 0 ELSE 1 END
            LIMIT 1;
            """;
        command.Parameters.AddWithValue("$title", title);
        command.Parameters.AddWithValue("$releaseYear", releaseYear);

        object? value = await command.ExecuteScalarAsync(cancellationToken);
        return value is null or DBNull ? null : Convert.ToInt32(value);
    }

    private static async Task UpdateFromPlexAsync(
        SqliteConnection connection,
        SqliteTransaction transaction,
        int movieId,
        PlexMovie movie,
        CancellationToken cancellationToken)
    {
        await using SqliteCommand command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText =
            """
            UPDATE Movies
            SET Title = $title,
                ReleaseYear = CASE WHEN $year > 0 THEN $year ELSE ReleaseYear END,
                Rating = CASE WHEN TRIM($rating) <> '' THEN $rating ELSE Rating END,
                Runtime = CASE WHEN $runtime > 0 THEN $runtime ELSE Runtime END,
                Genre = CASE WHEN TRIM($genre) <> '' THEN $genre ELSE Genre END,
                Director = CASE WHEN TRIM($director) <> '' THEN $director ELSE Director END,
                PlexGuid = $plexGuid
            WHERE Id = $id;
            """;
        AddParameters(command, movie);
        command.Parameters.AddWithValue("$id", movieId);
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    private static async Task MatchExistingMovieAsync(
        SqliteConnection connection,
        SqliteTransaction transaction,
        int movieId,
        PlexMovie movie,
        CancellationToken cancellationToken)
    {
        await UpdateFromPlexAsync(
            connection,
            transaction,
            movieId,
            movie,
            cancellationToken);
    }

    private static async Task InsertMovieAsync(
        SqliteConnection connection,
        SqliteTransaction transaction,
        PlexMovie movie,
        CancellationToken cancellationToken)
    {
        await using SqliteCommand command = connection.CreateCommand();
        command.Transaction = transaction;
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
                $year,
                $rating,
                $runtime,
                $genre,
                $director,
                $plexGuid,
                NULL,
                ''
            );
            """;
        AddParameters(command, movie);
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    private static void AddParameters(
        SqliteCommand command,
        PlexMovie movie)
    {
        command.Parameters.AddWithValue("$title", movie.Title);
        command.Parameters.AddWithValue("$year", movie.ReleaseYear);
        command.Parameters.AddWithValue("$rating", movie.Rating);
        command.Parameters.AddWithValue("$runtime", movie.RuntimeMinutes);
        command.Parameters.AddWithValue("$genre", movie.GenreDisplay);
        command.Parameters.AddWithValue("$director", movie.DirectorDisplay);
        command.Parameters.AddWithValue("$plexGuid", movie.PlexGuid);
    }
}
