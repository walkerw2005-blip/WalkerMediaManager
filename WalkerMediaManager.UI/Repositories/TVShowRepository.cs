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

        await using SqliteConnection connection =
            new($"Data Source={DatabaseService.DatabasePath}");

        await connection.OpenAsync();

        await using SqliteCommand command = connection.CreateCommand();

        command.CommandText =
            """
            SELECT
                Id,
                Title,
                Seasons,
                Episodes
            FROM TVShows
            ORDER BY Title;
            """;

        await using SqliteDataReader reader =
            await command.ExecuteReaderAsync();

        while (await reader.ReadAsync())
        {
            shows.Add(new TVShow
            {
                Id = reader.GetInt32(0),
                Title = reader.IsDBNull(1)
                    ? string.Empty
                    : reader.GetString(1),
                Seasons = reader.IsDBNull(2)
                    ? 0
                    : reader.GetInt32(2),
                Episodes = reader.IsDBNull(3)
                    ? 0
                    : reader.GetInt32(3),
                Owned = true,
                PlexGuid = string.Empty,
                TMDbId = null
            });
        }

        return shows;
    }

    public async Task<int> AddAsync(TVShow show)
    {
        await using SqliteConnection connection =
            new($"Data Source={DatabaseService.DatabasePath}");

        await connection.OpenAsync();

        await using SqliteCommand command = connection.CreateCommand();

        command.CommandText =
            """
            INSERT INTO TVShows
            (
                Title,
                Seasons,
                Episodes
            )
            VALUES
            (
                $title,
                $seasons,
                $episodes
            );

            SELECT last_insert_rowid();
            """;

        AddParameters(command, show);

        object? result = await command.ExecuteScalarAsync();

        return Convert.ToInt32(result);
    }

    public async Task UpdateAsync(TVShow show)
    {
        await using SqliteConnection connection =
            new($"Data Source={DatabaseService.DatabasePath}");

        await connection.OpenAsync();

        await using SqliteCommand command = connection.CreateCommand();

        command.CommandText =
            """
            UPDATE TVShows
            SET
                Title = $title,
                Seasons = $seasons,
                Episodes = $episodes
            WHERE Id = $id;
            """;

        AddParameters(command, show);
        command.Parameters.AddWithValue("$id", show.Id);

        await command.ExecuteNonQueryAsync();
    }

    public async Task DeleteAsync(int showId)
    {
        await using SqliteConnection connection =
            new($"Data Source={DatabaseService.DatabasePath}");

        await connection.OpenAsync();

        await using SqliteCommand command = connection.CreateCommand();

        command.CommandText =
            """
            DELETE FROM TVShows
            WHERE Id = $id;
            """;

        command.Parameters.AddWithValue("$id", showId);

        await command.ExecuteNonQueryAsync();
    }

    public async Task<int> CountAsync()
    {
        await using SqliteConnection connection =
            new($"Data Source={DatabaseService.DatabasePath}");

        await connection.OpenAsync();

        await using SqliteCommand command = connection.CreateCommand();

        command.CommandText =
            """
            SELECT COUNT(*)
            FROM TVShows;
            """;

        object? result = await command.ExecuteScalarAsync();

        return Convert.ToInt32(result);
    }

    private static void AddParameters(
        SqliteCommand command,
        TVShow show)
    {
        command.Parameters.AddWithValue("$title", show.Title);
        command.Parameters.AddWithValue("$seasons", show.Seasons);
        command.Parameters.AddWithValue("$episodes", show.Episodes);
    }
}