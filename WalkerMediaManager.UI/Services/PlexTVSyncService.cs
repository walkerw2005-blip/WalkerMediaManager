using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Data.Sqlite;
using WalkerMediaManager.UI.Data;
using WalkerMediaManager.UI.Models;

namespace WalkerMediaManager.UI.Services;

public sealed class PlexTVSyncService
{
    private readonly PlexService _plexService = new();

    public async Task<PlexSyncResult> SyncTVShowsAsync(
        string serverUrl,
        string token,
        string librarySectionKey,
        IProgress<string>? progress = null,
        CancellationToken cancellationToken = default)
    {
        IReadOnlyList<PlexTVShow> plexShows =
            await _plexService.GetTVShowsAsync(
                serverUrl,
                token,
                librarySectionKey,
                cancellationToken);

        PlexSyncResult result = new();
        string syncedAt = DateTimeOffset.UtcNow.ToString("O");

        await using SqliteConnection connection =
            new($"Data Source={DatabaseService.DatabasePath}");

        await connection.OpenAsync(cancellationToken);

        using SqliteTransaction transaction = connection.BeginTransaction();

        try
        {
            for (int index = 0; index < plexShows.Count; index++)
            {
                cancellationToken.ThrowIfCancellationRequested();

                PlexTVShow plexShow = plexShows[index];
                progress?.Report(
                    $"Syncing {index + 1} of {plexShows.Count}: {plexShow.Title}");

                try
                {
                    int? existingId = await FindByPlexGuidAsync(
                        connection,
                        transaction,
                        plexShow.PlexGuid,
                        plexShow.PlexRatingKey,
                        cancellationToken);

                    if (existingId.HasValue)
                    {
                        await UpdateFromPlexAsync(
                            connection,
                            transaction,
                            existingId.Value,
                            plexShow,
                            syncedAt,
                            cancellationToken);

                        result.UpdatedCount++;
                        continue;
                    }

                    existingId = await FindByTitleAndYearAsync(
                        connection,
                        transaction,
                        plexShow.Title,
                        plexShow.Year,
                        cancellationToken);

                    if (existingId.HasValue)
                    {
                        await UpdateFromPlexAsync(
                            connection,
                            transaction,
                            existingId.Value,
                            plexShow,
                            syncedAt,
                            cancellationToken);

                        result.MatchedCount++;
                        continue;
                    }

                    await InsertShowAsync(
                        connection,
                        transaction,
                        plexShow,
                        syncedAt,
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
        string plexRatingKey,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(plexGuid) &&
            string.IsNullOrWhiteSpace(plexRatingKey))
        {
            return null;
        }

        await using SqliteCommand command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText =
            """
            SELECT Id
            FROM TVShows
            WHERE (TRIM($plexGuid) <> '' AND PlexGuid = $plexGuid)
               OR (TRIM($plexRatingKey) <> '' AND PlexRatingKey = $plexRatingKey)
            LIMIT 1;
            """;
        command.Parameters.AddWithValue("$plexGuid", plexGuid);
        command.Parameters.AddWithValue("$plexRatingKey", plexRatingKey);

        object? value = await command.ExecuteScalarAsync(cancellationToken);
        return value is null or DBNull ? null : Convert.ToInt32(value);
    }

    private static async Task<int?> FindByTitleAndYearAsync(
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
            FROM TVShows
            WHERE LOWER(TRIM(Title)) = LOWER(TRIM($title))
              AND ($year = 0 OR Year = 0 OR Year = $year)
            ORDER BY CASE WHEN Year = $year THEN 0 ELSE 1 END
            LIMIT 1;
            """;
        command.Parameters.AddWithValue("$title", title);
        command.Parameters.AddWithValue("$year", year);

        object? value = await command.ExecuteScalarAsync(cancellationToken);
        return value is null or DBNull ? null : Convert.ToInt32(value);
    }

    private static async Task UpdateFromPlexAsync(
        SqliteConnection connection,
        SqliteTransaction transaction,
        int showId,
        PlexTVShow show,
        string syncedAt,
        CancellationToken cancellationToken)
    {
        await using SqliteCommand command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText =
            """
            UPDATE TVShows
            SET Title = $title,
                Year = CASE WHEN $year > 0 THEN $year ELSE Year END,
                Seasons = CASE WHEN $seasons > 0 THEN $seasons ELSE Seasons END,
                Episodes = CASE WHEN $episodes > 0 THEN $episodes ELSE Episodes END,
                PlexRatingKey = CASE WHEN TRIM($plexRatingKey) <> '' THEN $plexRatingKey ELSE PlexRatingKey END,
                PlexGuid = CASE WHEN TRIM($plexGuid) <> '' THEN $plexGuid ELSE PlexGuid END,
                Studio = CASE WHEN TRIM($studio) <> '' THEN $studio ELSE Studio END,
                Summary = CASE WHEN TRIM($summary) <> '' THEN $summary ELSE Summary END,
                PosterPath = CASE WHEN TRIM($posterPath) <> '' THEN $posterPath ELSE PosterPath END,
                BackgroundPath = CASE WHEN TRIM($backgroundPath) <> '' THEN $backgroundPath ELSE BackgroundPath END,
                LastSynced = $lastSynced
            WHERE Id = $id;
            """;
        AddParameters(command, show, syncedAt);
        command.Parameters.AddWithValue("$id", showId);
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    private static async Task InsertShowAsync(
        SqliteConnection connection,
        SqliteTransaction transaction,
        PlexTVShow show,
        string syncedAt,
        CancellationToken cancellationToken)
    {
        await using SqliteCommand command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText =
            """
            INSERT INTO TVShows
            (
                Title,
                Year,
                Seasons,
                Episodes,
                PlexRatingKey,
                PlexGuid,
                TMDbId,
                IMDbId,
                Studio,
                Summary,
                PosterPath,
                BackgroundPath,
                LastSynced
            )
            VALUES
            (
                $title,
                $year,
                $seasons,
                $episodes,
                $plexRatingKey,
                $plexGuid,
                NULL,
                '',
                $studio,
                $summary,
                $posterPath,
                $backgroundPath,
                $lastSynced
            );
            """;
        AddParameters(command, show, syncedAt);
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    private static void AddParameters(
        SqliteCommand command,
        PlexTVShow show,
        string syncedAt)
    {
        command.Parameters.AddWithValue("$title", show.Title);
        command.Parameters.AddWithValue("$year", show.Year);
        command.Parameters.AddWithValue("$seasons", show.Seasons);
        command.Parameters.AddWithValue("$episodes", show.Episodes);
        command.Parameters.AddWithValue("$plexRatingKey", show.PlexRatingKey);
        command.Parameters.AddWithValue("$plexGuid", show.PlexGuid);
        command.Parameters.AddWithValue("$studio", show.Studio);
        command.Parameters.AddWithValue("$summary", show.Summary);
        command.Parameters.AddWithValue("$posterPath", show.PosterPath);
        command.Parameters.AddWithValue("$backgroundPath", show.BackgroundPath);
        command.Parameters.AddWithValue("$lastSynced", syncedAt);
    }
}
