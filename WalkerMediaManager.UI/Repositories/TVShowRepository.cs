using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.Data.Sqlite;
using WalkerMediaManager.UI.Data;
using WalkerMediaManager.UI.Models;

namespace WalkerMediaManager.UI.Repositories;

public sealed class TVShowRepository
{
    private readonly TVSeasonRepository _seasonRepository = new();

    public async Task<List<TVShow>> GetAllAsync()
    {
        List<TVShow> shows = [];
        await using SqliteConnection connection = new($"Data Source={DatabaseService.DatabasePath}");
        await connection.OpenAsync();
        await using SqliteCommand command = connection.CreateCommand();
        command.CommandText = SelectColumns + " ORDER BY Title;";
        await using SqliteDataReader reader = await command.ExecuteReaderAsync();
        while (await reader.ReadAsync()) shows.Add(ReadShow(reader));
        return shows;
    }

    public async Task<TVShow?> GetByIdAsync(int id)
    {
        await using SqliteConnection connection = new($"Data Source={DatabaseService.DatabasePath}");
        await connection.OpenAsync();
        await using SqliteCommand command = connection.CreateCommand();
        command.CommandText = SelectColumns + " WHERE Id = $id LIMIT 1;";
        command.Parameters.AddWithValue("$id", id);
        await using SqliteDataReader reader = await command.ExecuteReaderAsync();
        return await reader.ReadAsync() ? ReadShow(reader) : null;
    }

    public async Task<int> AddAsync(TVShow show)
    {
        await using SqliteConnection connection = new($"Data Source={DatabaseService.DatabasePath}");
        await connection.OpenAsync();
        await using SqliteCommand command = connection.CreateCommand();
        command.CommandText =
            """
            INSERT INTO TVShows
            (
                Title, Year, Seasons, TotalSeasons, Episodes, PlexRatingKey, PlexGuid,
                TMDbId, TVMazeId, IMDbId, Studio, Summary, PosterPath, BackgroundPath,
                LastSynced, Status, FirstAirDate, LastAirDate, Network, MetadataLastSynced
            )
            VALUES
            (
                $title, $year, $seasons, $totalSeasons, $episodes, $plexRatingKey, $plexGuid,
                $tmdbId, $tvMazeId, $imdbId, $studio, $summary, $posterPath, $backgroundPath,
                $lastSynced, $status, $firstAirDate, $lastAirDate, $network, $metadataLastSynced
            );
            SELECT last_insert_rowid();
            """;
        AddParameters(command, show, true);
        int id = Convert.ToInt32(await command.ExecuteScalarAsync());
        await _seasonRepository.EnsureRowsAsync(id, Math.Max(show.Seasons, show.TotalSeasons), show.Seasons);
        return id;
    }

    public async Task UpdateAsync(TVShow show)
    {
        await using SqliteConnection connection = new($"Data Source={DatabaseService.DatabasePath}");
        await connection.OpenAsync();
        await using SqliteCommand command = connection.CreateCommand();
        command.CommandText =
            """
            UPDATE TVShows
            SET Title = $title, Year = $year, Seasons = $seasons, Episodes = $episodes,
                PlexRatingKey = $plexRatingKey, PlexGuid = $plexGuid, TMDbId = $tmdbId,
                TVMazeId = $tvMazeId, IMDbId = $imdbId, Studio = $studio, Summary = $summary,
                PosterPath = $posterPath, BackgroundPath = $backgroundPath, LastSynced = $lastSynced,
                Status = $status, FirstAirDate = $firstAirDate, LastAirDate = $lastAirDate,
                Network = $network, MetadataLastSynced = $metadataLastSynced
            WHERE Id = $id;
            """;
        AddParameters(command, show, false);
        command.Parameters.AddWithValue("$id", show.Id);
        await command.ExecuteNonQueryAsync();
        await _seasonRepository.EnsureRowsAsync(show.Id, Math.Max(show.Seasons, show.TotalSeasons), show.Seasons);
    }

    public async Task UpdateMetadataAsync(TVShow show)
    {
        await using SqliteConnection connection = new($"Data Source={DatabaseService.DatabasePath}");
        await connection.OpenAsync();
        await using SqliteCommand command = connection.CreateCommand();
        command.CommandText =
            """
            UPDATE TVShows
            SET TotalSeasons = $totalSeasons,
                Episodes = $episodes,
                TVMazeId = $tvMazeId,
                IMDbId = CASE WHEN TRIM($imdbId) <> '' THEN $imdbId ELSE IMDbId END,
                Studio = CASE WHEN TRIM($studio) <> '' THEN $studio ELSE Studio END,
                Summary = CASE WHEN TRIM($summary) <> '' THEN $summary ELSE Summary END,
                PosterPath = CASE WHEN TRIM($posterPath) <> '' THEN $posterPath ELSE PosterPath END,
                BackgroundPath = CASE WHEN TRIM($backgroundPath) <> '' THEN $backgroundPath ELSE BackgroundPath END,
                Status = $status,
                FirstAirDate = $firstAirDate,
                LastAirDate = $lastAirDate,
                Network = $network,
                MetadataLastSynced = $metadataLastSynced
            WHERE Id = $id;
            """;
        command.Parameters.AddWithValue("$totalSeasons", show.TotalSeasons);
        command.Parameters.AddWithValue("$episodes", show.Episodes);
        command.Parameters.AddWithValue("$tvMazeId", show.TVMazeId.HasValue ? show.TVMazeId.Value : DBNull.Value);
        command.Parameters.AddWithValue("$imdbId", show.IMDbId);
        command.Parameters.AddWithValue("$studio", show.Studio);
        command.Parameters.AddWithValue("$summary", show.Summary);
        command.Parameters.AddWithValue("$posterPath", show.PosterPath);
        command.Parameters.AddWithValue("$backgroundPath", show.BackgroundPath);
        command.Parameters.AddWithValue("$status", show.Status);
        command.Parameters.AddWithValue("$firstAirDate", show.FirstAirDate);
        command.Parameters.AddWithValue("$lastAirDate", show.LastAirDate);
        command.Parameters.AddWithValue("$network", show.Network);
        command.Parameters.AddWithValue("$metadataLastSynced", show.MetadataLastSynced);
        command.Parameters.AddWithValue("$id", show.Id);
        await command.ExecuteNonQueryAsync();
        await _seasonRepository.EnsureRowsAsync(show.Id, Math.Max(show.Seasons, show.TotalSeasons), show.Seasons);
    }

    public async Task SetTotalSeasonsAsync(int showId, int totalSeasons, int ownedSeasonCount)
    {
        await using SqliteConnection connection = new($"Data Source={DatabaseService.DatabasePath}");
        await connection.OpenAsync();
        await using SqliteCommand command = connection.CreateCommand();
        command.CommandText = "UPDATE TVShows SET TotalSeasons = $totalSeasons WHERE Id = $id;";
        command.Parameters.AddWithValue("$totalSeasons", totalSeasons);
        command.Parameters.AddWithValue("$id", showId);
        await command.ExecuteNonQueryAsync();
        await _seasonRepository.EnsureRowsAsync(showId, Math.Max(totalSeasons, ownedSeasonCount), ownedSeasonCount);
    }

    public async Task DeleteAsync(int showId)
    {
        await using SqliteConnection connection = new($"Data Source={DatabaseService.DatabasePath}");
        await connection.OpenAsync();
        await using SqliteCommand command = connection.CreateCommand();
        command.CommandText = "DELETE FROM TVShows WHERE Id = $id;";
        command.Parameters.AddWithValue("$id", showId);
        await command.ExecuteNonQueryAsync();
    }

    public async Task<int> CountAsync()
    {
        await using SqliteConnection connection = new($"Data Source={DatabaseService.DatabasePath}");
        await connection.OpenAsync();
        await using SqliteCommand command = connection.CreateCommand();
        command.CommandText = "SELECT COUNT(*) FROM TVShows;";
        return Convert.ToInt32(await command.ExecuteScalarAsync());
    }

    private const string SelectColumns =
        """
        SELECT Id, Title, Year, Seasons, TotalSeasons, Episodes, PlexRatingKey, PlexGuid,
               TMDbId, TVMazeId, IMDbId, Studio, Summary, PosterPath, BackgroundPath,
               LastSynced, Status, FirstAirDate, LastAirDate, Network, MetadataLastSynced,
               (SELECT COUNT(*) FROM TVSeasons s WHERE s.TVShowId = TVShows.Id) AS TrackedSeasons,
               (SELECT COUNT(*) FROM TVSeasons s WHERE s.TVShowId = TVShows.Id AND s.IsOwned = 1) AS OwnedSeasons,
               (SELECT COUNT(*) FROM TVSeasons s WHERE s.TVShowId = TVShows.Id AND s.IsOwned = 1 AND s.HasDigitalCopy = 1) AS DigitalSeasons
        FROM TVShows
        """;

    private static TVShow ReadShow(SqliteDataReader reader) => new()
    {
        Id = reader.GetInt32(0), Title = GetString(reader, 1), Year = GetInt32(reader, 2),
        Seasons = GetInt32(reader, 3), TotalSeasons = GetInt32(reader, 4), Episodes = GetInt32(reader, 5),
        Owned = true, PlexRatingKey = GetString(reader, 6), PlexGuid = GetString(reader, 7),
        TMDbId = reader.IsDBNull(8) ? null : reader.GetInt32(8),
        TVMazeId = reader.IsDBNull(9) ? null : reader.GetInt32(9),
        IMDbId = GetString(reader, 10), Studio = GetString(reader, 11), Summary = GetString(reader, 12),
        PosterPath = GetString(reader, 13), BackgroundPath = GetString(reader, 14), LastSynced = GetString(reader, 15),
        Status = GetString(reader, 16), FirstAirDate = GetString(reader, 17), LastAirDate = GetString(reader, 18),
        Network = GetString(reader, 19), MetadataLastSynced = GetString(reader, 20),
        TrackedSeasons = GetInt32(reader, 21), OwnedSeasons = GetInt32(reader, 22), DigitalSeasons = GetInt32(reader, 23)
    };

    private static void AddParameters(SqliteCommand command, TVShow show, bool includeTotalSeasons)
    {
        command.Parameters.AddWithValue("$title", show.Title);
        command.Parameters.AddWithValue("$year", show.Year);
        command.Parameters.AddWithValue("$seasons", show.Seasons);
        if (includeTotalSeasons) command.Parameters.AddWithValue("$totalSeasons", show.TotalSeasons);
        command.Parameters.AddWithValue("$episodes", show.Episodes);
        command.Parameters.AddWithValue("$plexRatingKey", show.PlexRatingKey);
        command.Parameters.AddWithValue("$plexGuid", show.PlexGuid);
        command.Parameters.AddWithValue("$tmdbId", show.TMDbId.HasValue ? show.TMDbId.Value : DBNull.Value);
        command.Parameters.AddWithValue("$tvMazeId", show.TVMazeId.HasValue ? show.TVMazeId.Value : DBNull.Value);
        command.Parameters.AddWithValue("$imdbId", show.IMDbId);
        command.Parameters.AddWithValue("$studio", show.Studio);
        command.Parameters.AddWithValue("$summary", show.Summary);
        command.Parameters.AddWithValue("$posterPath", show.PosterPath);
        command.Parameters.AddWithValue("$backgroundPath", show.BackgroundPath);
        command.Parameters.AddWithValue("$lastSynced", show.LastSynced);
        command.Parameters.AddWithValue("$status", show.Status);
        command.Parameters.AddWithValue("$firstAirDate", show.FirstAirDate);
        command.Parameters.AddWithValue("$lastAirDate", show.LastAirDate);
        command.Parameters.AddWithValue("$network", show.Network);
        command.Parameters.AddWithValue("$metadataLastSynced", show.MetadataLastSynced);
    }

    private static string GetString(SqliteDataReader reader, int ordinal) => reader.IsDBNull(ordinal) ? string.Empty : reader.GetString(ordinal);
    private static int GetInt32(SqliteDataReader reader, int ordinal) => reader.IsDBNull(ordinal) ? 0 : reader.GetInt32(ordinal);
}
