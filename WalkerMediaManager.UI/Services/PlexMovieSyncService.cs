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

    public async Task<PlexSyncResult> SyncMoviesAsync(string serverUrl, string token, string librarySectionKey,
        IProgress<string>? progress = null, CancellationToken cancellationToken = default)
    {
        IReadOnlyList<PlexMovie> plexMovies = await _plexService.GetMoviesAsync(serverUrl, token, librarySectionKey, cancellationToken);
        PlexSyncResult result = new();
        await using SqliteConnection connection = new($"Data Source={DatabaseService.DatabasePath}");
        await connection.OpenAsync(cancellationToken);
        using SqliteTransaction transaction = connection.BeginTransaction();

        try
        {
            for (int index = 0; index < plexMovies.Count; index++)
            {
                cancellationToken.ThrowIfCancellationRequested();
                PlexMovie movie = plexMovies[index];
                progress?.Report($"Syncing {index + 1} of {plexMovies.Count}: {movie.Title}");
                try
                {
                    int? id = await FindByPlexGuidAsync(connection, transaction, movie.PlexGuid, cancellationToken)
                              ?? await FindByTitleAndYearAsync(connection, transaction, movie.Title, movie.ReleaseYear, cancellationToken);
                    if (id.HasValue)
                    {
                        await UpdateAsync(connection, transaction, id.Value, movie, cancellationToken);
                        if (!string.IsNullOrWhiteSpace(movie.PlexGuid)) result.UpdatedCount++; else result.MatchedCount++;
                    }
                    else
                    {
                        await InsertAsync(connection, transaction, movie, cancellationToken);
                        result.AddedCount++;
                    }
                }
                catch { result.FailedCount++; }
            }
            transaction.Commit();
        }
        catch { transaction.Rollback(); throw; }

        return result;
    }

    private static async Task<int?> FindByPlexGuidAsync(SqliteConnection c, SqliteTransaction t, string guid, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(guid)) return null;
        await using SqliteCommand cmd = c.CreateCommand();
        cmd.Transaction = t;
        cmd.CommandText = "SELECT Id FROM Movies WHERE PlexGuid = $guid LIMIT 1;";
        cmd.Parameters.AddWithValue("$guid", guid);
        object? value = await cmd.ExecuteScalarAsync(ct);
        return value is null or DBNull ? null : Convert.ToInt32(value);
    }

    private static async Task<int?> FindByTitleAndYearAsync(SqliteConnection c, SqliteTransaction t, string title, int year, CancellationToken ct)
    {
        await using SqliteCommand cmd = c.CreateCommand();
        cmd.Transaction = t;
        cmd.CommandText = """
            SELECT Id FROM Movies
            WHERE LOWER(TRIM(Title)) = LOWER(TRIM($title))
              AND ($year = 0 OR ReleaseYear = 0 OR ReleaseYear = $year)
            ORDER BY CASE WHEN ReleaseYear = $year THEN 0 ELSE 1 END LIMIT 1;
            """;
        cmd.Parameters.AddWithValue("$title", title);
        cmd.Parameters.AddWithValue("$year", year);
        object? value = await cmd.ExecuteScalarAsync(ct);
        return value is null or DBNull ? null : Convert.ToInt32(value);
    }

    private static async Task UpdateAsync(SqliteConnection c, SqliteTransaction t, int id, PlexMovie movie, CancellationToken ct)
    {
        await using SqliteCommand cmd = c.CreateCommand();
        cmd.Transaction = t;
        cmd.CommandText = """
            UPDATE Movies SET
                Title=$title,
                ReleaseYear=CASE WHEN $year>0 THEN $year ELSE ReleaseYear END,
                Rating=CASE WHEN TRIM($rating)<>'' THEN $rating ELSE Rating END,
                Runtime=CASE WHEN $runtime>0 THEN $runtime ELSE Runtime END,
                Genre=CASE WHEN TRIM($genre)<>'' THEN $genre ELSE Genre END,
                Director=CASE WHEN TRIM($director)<>'' THEN $director ELSE Director END,
                PlexRatingKey=CASE WHEN TRIM($key)<>'' THEN $key ELSE PlexRatingKey END,
                PlexGuid=CASE WHEN TRIM($guid)<>'' THEN $guid ELSE PlexGuid END,
                Studio=CASE WHEN TRIM($studio)<>'' THEN $studio ELSE Studio END,
                Summary=CASE WHEN TRIM($summary)<>'' THEN $summary ELSE Summary END,
                PosterPath=CASE WHEN TRIM($poster)<>'' THEN $poster ELSE PosterPath END,
                BackgroundPath=CASE WHEN TRIM($background)<>'' THEN $background ELSE BackgroundPath END,
                LastSynced=$synced
            WHERE Id=$id;
            """;
        AddParameters(cmd, movie);
        cmd.Parameters.AddWithValue("$id", id);
        await cmd.ExecuteNonQueryAsync(ct);
    }

    private static async Task InsertAsync(SqliteConnection c, SqliteTransaction t, PlexMovie movie, CancellationToken ct)
    {
        await using SqliteCommand cmd = c.CreateCommand();
        cmd.Transaction = t;
        cmd.CommandText = """
            INSERT INTO Movies
            (Title,ReleaseYear,Rating,Runtime,Genre,Director,PlexRatingKey,PlexGuid,TMDbId,IMDbId,SortTitle,Studio,Summary,PosterPath,BackgroundPath,LastSynced)
            VALUES
            ($title,$year,$rating,$runtime,$genre,$director,$key,$guid,NULL,'','',$studio,$summary,$poster,$background,$synced);
            """;
        AddParameters(cmd, movie);
        await cmd.ExecuteNonQueryAsync(ct);
    }

    private static void AddParameters(SqliteCommand cmd, PlexMovie movie)
    {
        cmd.Parameters.AddWithValue("$title", movie.Title);
        cmd.Parameters.AddWithValue("$year", movie.ReleaseYear);
        cmd.Parameters.AddWithValue("$rating", movie.Rating);
        cmd.Parameters.AddWithValue("$runtime", movie.RuntimeMinutes);
        cmd.Parameters.AddWithValue("$genre", movie.GenreDisplay);
        cmd.Parameters.AddWithValue("$director", movie.DirectorDisplay);
        cmd.Parameters.AddWithValue("$key", movie.PlexKey);
        cmd.Parameters.AddWithValue("$guid", movie.PlexGuid);
        cmd.Parameters.AddWithValue("$studio", movie.Studio);
        cmd.Parameters.AddWithValue("$summary", movie.Summary);
        cmd.Parameters.AddWithValue("$poster", movie.ThumbPath);
        cmd.Parameters.AddWithValue("$background", movie.BackgroundPath);
        cmd.Parameters.AddWithValue("$synced", DateTimeOffset.UtcNow.ToString("O"));
    }
}
