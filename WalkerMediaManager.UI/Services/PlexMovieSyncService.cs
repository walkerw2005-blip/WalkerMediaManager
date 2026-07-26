using System;
using System.Collections.Generic;
using System.Linq;
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
        string librarySectionTitle,
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

                PlexMovie movie = plexMovies[index];

                progress?.Report(
                    $"Syncing {index + 1} of {plexMovies.Count}: {movie.Title}");

                try
                {
                    int? id =
                        await FindByPlexGuidInLibraryAsync(
                            connection,
                            transaction,
                            movie.PlexGuid,
                            librarySectionKey,
                            cancellationToken)
                        ?? await FindByPlexRatingKeyInLibraryAsync(
                            connection,
                            transaction,
                            movie.PlexKey,
                            librarySectionKey,
                            cancellationToken)
                        ?? await FindLegacyUnassignedMatchAsync(
                            connection,
                            transaction,
                            movie.Title,
                            movie.ReleaseYear,
                            cancellationToken)
                        ?? await FindByTitleAndYearInLibraryAsync(
                            connection,
                            transaction,
                            movie.Title,
                            movie.ReleaseYear,
                            librarySectionKey,
                            cancellationToken);

                    if (id.HasValue)
                    {
                        await UpdateAsync(
                            connection,
                            transaction,
                            id.Value,
                            movie,
                            librarySectionKey,
                            librarySectionTitle,
                            cancellationToken);

                        result.UpdatedCount++;
                    }
                    else
                    {
                        await InsertAsync(
                            connection,
                            transaction,
                            movie,
                            librarySectionKey,
                            librarySectionTitle,
                            cancellationToken);

                        result.AddedCount++;
                    }
                }
                catch (OperationCanceledException)
                {
                    throw;
                }
                catch
                {
                    result.FailedCount++;
                }
            }

            if (plexMovies.Count > 0)
            {
                progress?.Report(
                    $"Cleaning stale records for {librarySectionTitle}...");

                await UnassignStaleLibraryRecordsAsync(
                    connection,
                    transaction,
                    librarySectionKey,
                    plexMovies,
                    cancellationToken);
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

    private static async Task<int?> FindByPlexGuidInLibraryAsync(
        SqliteConnection connection,
        SqliteTransaction transaction,
        string plexGuid,
        string libraryKey,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(plexGuid))
        {
            return null;
        }

        await using SqliteCommand command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText =
            """
            SELECT Id
            FROM Movies
            WHERE PlexGuid = $guid
              AND PlexLibraryKey = $libraryKey
            LIMIT 1;
            """;

        command.Parameters.AddWithValue("$guid", plexGuid);
        command.Parameters.AddWithValue("$libraryKey", libraryKey);

        object? value = await command.ExecuteScalarAsync(cancellationToken);

        return value is null or DBNull
            ? null
            : Convert.ToInt32(value);
    }

    private static async Task<int?> FindByPlexRatingKeyInLibraryAsync(
        SqliteConnection connection,
        SqliteTransaction transaction,
        string plexRatingKey,
        string libraryKey,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(plexRatingKey))
        {
            return null;
        }

        await using SqliteCommand command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText =
            """
            SELECT Id
            FROM Movies
            WHERE PlexRatingKey = $ratingKey
              AND PlexLibraryKey = $libraryKey
            LIMIT 1;
            """;

        command.Parameters.AddWithValue("$ratingKey", plexRatingKey);
        command.Parameters.AddWithValue("$libraryKey", libraryKey);

        object? value = await command.ExecuteScalarAsync(cancellationToken);

        return value is null or DBNull
            ? null
            : Convert.ToInt32(value);
    }

    private static async Task<int?> FindLegacyUnassignedMatchAsync(
        SqliteConnection connection,
        SqliteTransaction transaction,
        string title,
        int year,
        CancellationToken cancellationToken)
    {
        await using SqliteCommand command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText =
            """
            SELECT Id
            FROM Movies
            WHERE TRIM(PlexLibraryKey) = ''
              AND TRIM(PlexLibraryTitle) = ''
              AND LOWER(TRIM(Title)) = LOWER(TRIM($title))
              AND ($year = 0 OR ReleaseYear = 0 OR ReleaseYear = $year)
            ORDER BY
                CASE WHEN ReleaseYear = $year THEN 0 ELSE 1 END,
                Id
            LIMIT 1;
            """;

        command.Parameters.AddWithValue("$title", title);
        command.Parameters.AddWithValue("$year", year);

        object? value = await command.ExecuteScalarAsync(cancellationToken);

        return value is null or DBNull
            ? null
            : Convert.ToInt32(value);
    }

    private static async Task<int?> FindByTitleAndYearInLibraryAsync(
        SqliteConnection connection,
        SqliteTransaction transaction,
        string title,
        int year,
        string libraryKey,
        CancellationToken cancellationToken)
    {
        await using SqliteCommand command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText =
            """
            SELECT Id
            FROM Movies
            WHERE PlexLibraryKey = $libraryKey
              AND LOWER(TRIM(Title)) = LOWER(TRIM($title))
              AND ($year = 0 OR ReleaseYear = 0 OR ReleaseYear = $year)
            ORDER BY
                CASE WHEN ReleaseYear = $year THEN 0 ELSE 1 END,
                Id
            LIMIT 1;
            """;

        command.Parameters.AddWithValue("$title", title);
        command.Parameters.AddWithValue("$year", year);
        command.Parameters.AddWithValue("$libraryKey", libraryKey);

        object? value = await command.ExecuteScalarAsync(cancellationToken);

        return value is null or DBNull
            ? null
            : Convert.ToInt32(value);
    }

    private static async Task UnassignStaleLibraryRecordsAsync(
        SqliteConnection connection,
        SqliteTransaction transaction,
        string libraryKey,
        IReadOnlyList<PlexMovie> plexMovies,
        CancellationToken cancellationToken)
    {
        HashSet<string> activeRatingKeys = plexMovies
            .Select(movie => movie.PlexKey?.Trim() ?? string.Empty)
            .Where(key => !string.IsNullOrWhiteSpace(key))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        HashSet<string> activeGuids = plexMovies
            .Select(movie => movie.PlexGuid?.Trim() ?? string.Empty)
            .Where(guid => !string.IsNullOrWhiteSpace(guid))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        await using SqliteCommand query = connection.CreateCommand();
        query.Transaction = transaction;
        query.CommandText =
            """
            SELECT Id, PlexRatingKey, PlexGuid
            FROM Movies
            WHERE PlexLibraryKey = $libraryKey;
            """;
        query.Parameters.AddWithValue("$libraryKey", libraryKey);

        List<int> staleIds = [];

        await using (SqliteDataReader reader =
                     await query.ExecuteReaderAsync(cancellationToken))
        {
            while (await reader.ReadAsync(cancellationToken))
            {
                int id = reader.GetInt32(0);
                string ratingKey = reader.IsDBNull(1)
                    ? string.Empty
                    : reader.GetString(1);
                string guid = reader.IsDBNull(2)
                    ? string.Empty
                    : reader.GetString(2);

                bool stillExists =
                    (!string.IsNullOrWhiteSpace(ratingKey) &&
                     activeRatingKeys.Contains(ratingKey))
                    ||
                    (!string.IsNullOrWhiteSpace(guid) &&
                     activeGuids.Contains(guid));

                if (!stillExists)
                {
                    staleIds.Add(id);
                }
            }
        }

        foreach (int id in staleIds)
        {
            await using SqliteCommand update = connection.CreateCommand();
            update.Transaction = transaction;
            update.CommandText =
                """
                UPDATE Movies
                SET PlexLibraryKey = '',
                    PlexLibraryTitle = ''
                WHERE Id = $id;
                """;
            update.Parameters.AddWithValue("$id", id);
            await update.ExecuteNonQueryAsync(cancellationToken);
        }
    }

    private static async Task UpdateAsync(
        SqliteConnection connection,
        SqliteTransaction transaction,
        int id,
        PlexMovie movie,
        string libraryKey,
        string libraryTitle,
        CancellationToken cancellationToken)
    {
        await using SqliteCommand command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText =
            """
            UPDATE Movies
            SET
                Title = $title,
                ReleaseYear =
                    CASE WHEN $year > 0 THEN $year ELSE ReleaseYear END,
                Rating =
                    CASE WHEN TRIM($rating) <> '' THEN $rating ELSE Rating END,
                Runtime =
                    CASE WHEN $runtime > 0 THEN $runtime ELSE Runtime END,
                Genre =
                    CASE WHEN TRIM($genre) <> '' THEN $genre ELSE Genre END,
                Director =
                    CASE WHEN TRIM($director) <> '' THEN $director ELSE Director END,
                PlexRatingKey =
                    CASE WHEN TRIM($key) <> '' THEN $key ELSE PlexRatingKey END,
                PlexGuid =
                    CASE WHEN TRIM($guid) <> '' THEN $guid ELSE PlexGuid END,
                Studio =
                    CASE WHEN TRIM($studio) <> '' THEN $studio ELSE Studio END,
                Summary =
                    CASE WHEN TRIM($summary) <> '' THEN $summary ELSE Summary END,
                PosterPath =
                    CASE WHEN TRIM($poster) <> '' THEN $poster ELSE PosterPath END,
                BackgroundPath =
                    CASE WHEN TRIM($background) <> '' THEN $background ELSE BackgroundPath END,
                LastSynced = $synced,
                PlexLibraryKey = $libraryKey,
                PlexLibraryTitle = $libraryTitle
            WHERE Id = $id;
            """;

        AddParameters(command, movie);
        command.Parameters.AddWithValue("$libraryKey", libraryKey);
        command.Parameters.AddWithValue("$libraryTitle", libraryTitle);
        command.Parameters.AddWithValue("$id", id);

        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    private static async Task InsertAsync(
        SqliteConnection connection,
        SqliteTransaction transaction,
        PlexMovie movie,
        string libraryKey,
        string libraryTitle,
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
                $key,
                $guid,
                NULL,
                '',
                '',
                $studio,
                $summary,
                $poster,
                $background,
                $synced,
                $libraryKey,
                $libraryTitle
            );
            """;

        AddParameters(command, movie);
        command.Parameters.AddWithValue("$libraryKey", libraryKey);
        command.Parameters.AddWithValue("$libraryTitle", libraryTitle);

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
        command.Parameters.AddWithValue("$key", movie.PlexKey);
        command.Parameters.AddWithValue("$guid", movie.PlexGuid);
        command.Parameters.AddWithValue("$studio", movie.Studio);
        command.Parameters.AddWithValue("$summary", movie.Summary);
        command.Parameters.AddWithValue("$poster", movie.ThumbPath);
        command.Parameters.AddWithValue("$background", movie.BackgroundPath);
        command.Parameters.AddWithValue(
            "$synced",
            DateTimeOffset.UtcNow.ToString("O"));
    }
}
