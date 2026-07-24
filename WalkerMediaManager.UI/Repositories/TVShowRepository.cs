using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.Data.Sqlite;
using WalkerMediaManager.UI.Data;
using WalkerMediaManager.UI.Models;

namespace WalkerMediaManager.UI.Repositories;

public sealed class TVShowRepository
{
    public async Task<List<TVShow>> GetAllAsync()
    {
        List<TVShow> shows = [];

        await using SqliteConnection connection = new($"Data Source={DatabaseService.DatabasePath}");
        await connection.OpenAsync();

        await using SqliteCommand command = connection.CreateCommand();
        command.CommandText = SelectColumns + " ORDER BY Title;";

        await using SqliteDataReader reader = await command.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            shows.Add(ReadShow(reader));
        }

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
                Title, Year, Seasons, Episodes, PlexRatingKey, PlexGuid,
                TMDbId, IMDbId, Studio, Summary, PosterPath,
                BackgroundPath, LastSynced
            )
            VALUES
            (
                $title, $year, $seasons, $episodes, $plexRatingKey, $plexGuid,
                $tmdbId, $imdbId, $studio, $summary, $posterPath,
                $backgroundPath, $lastSynced
            );
            SELECT last_insert_rowid();
            """;

        AddParameters(command, show);
        object? result = await command.ExecuteScalarAsync();
        return Convert.ToInt32(result);
    }

    public async Task UpdateAsync(TVShow show)
    {
        await using SqliteConnection connection = new($"Data Source={DatabaseService.DatabasePath}");
        await connection.OpenAsync();

        await using SqliteCommand command = connection.CreateCommand();
        command.CommandText =
            """
            UPDATE TVShows
            SET Title = $title,
                Year = $year,
                Seasons = $seasons,
                Episodes = $episodes,
                PlexRatingKey = $plexRatingKey,
                PlexGuid = $plexGuid,
                TMDbId = $tmdbId,
                IMDbId = $imdbId,
                Studio = $studio,
                Summary = $summary,
                PosterPath = $posterPath,
                BackgroundPath = $backgroundPath,
                LastSynced = $lastSynced
            WHERE Id = $id;
            """;

        AddParameters(command, show);
        command.Parameters.AddWithValue("$id", show.Id);
        await command.ExecuteNonQueryAsync();
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
        object? result = await command.ExecuteScalarAsync();
        return Convert.ToInt32(result);
    }

    private const string SelectColumns =
        """
        SELECT Id, Title, Year, Seasons, Episodes, PlexRatingKey, PlexGuid,
               TMDbId, IMDbId, Studio, Summary, PosterPath, BackgroundPath,
               LastSynced
        FROM TVShows
        """;

    private static TVShow ReadShow(SqliteDataReader reader)
    {
        return new TVShow
        {
            Id = reader.GetInt32(0),
            Title = GetString(reader, 1),
            Year = GetInt32(reader, 2),
            Seasons = GetInt32(reader, 3),
            Episodes = GetInt32(reader, 4),
            Owned = true,
            PlexRatingKey = GetString(reader, 5),
            PlexGuid = GetString(reader, 6),
            TMDbId = reader.IsDBNull(7) ? null : reader.GetInt32(7),
            IMDbId = GetString(reader, 8),
            Studio = GetString(reader, 9),
            Summary = GetString(reader, 10),
            PosterPath = GetString(reader, 11),
            BackgroundPath = GetString(reader, 12),
            LastSynced = GetString(reader, 13)
        };
    }

    private static void AddParameters(SqliteCommand command, TVShow show)
    {
        command.Parameters.AddWithValue("$title", show.Title);
        command.Parameters.AddWithValue("$year", show.Year);
        command.Parameters.AddWithValue("$seasons", show.Seasons);
        command.Parameters.AddWithValue("$episodes", show.Episodes);
        command.Parameters.AddWithValue("$plexRatingKey", show.PlexRatingKey);
        command.Parameters.AddWithValue("$plexGuid", show.PlexGuid);
        command.Parameters.AddWithValue("$tmdbId", show.TMDbId.HasValue ? show.TMDbId.Value : DBNull.Value);
        command.Parameters.AddWithValue("$imdbId", show.IMDbId);
        command.Parameters.AddWithValue("$studio", show.Studio);
        command.Parameters.AddWithValue("$summary", show.Summary);
        command.Parameters.AddWithValue("$posterPath", show.PosterPath);
        command.Parameters.AddWithValue("$backgroundPath", show.BackgroundPath);
        command.Parameters.AddWithValue("$lastSynced", show.LastSynced);
    }

    private static string GetString(SqliteDataReader reader, int ordinal) =>
        reader.IsDBNull(ordinal) ? string.Empty : reader.GetString(ordinal);

    private static int GetInt32(SqliteDataReader reader, int ordinal) =>
        reader.IsDBNull(ordinal) ? 0 : reader.GetInt32(ordinal);
}
