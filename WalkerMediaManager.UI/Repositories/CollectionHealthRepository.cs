using System;
using System.Threading.Tasks;
using Microsoft.Data.Sqlite;
using WalkerMediaManager.UI.Data;

namespace WalkerMediaManager.UI.Repositories;

public sealed class CollectionHealthRepository
{
    public async Task<int> CountPossibleDuplicateMoviesAsync()
    {
        await using SqliteConnection connection =
            new($"Data Source={DatabaseService.DatabasePath}");

        await connection.OpenAsync();

        await using SqliteCommand command =
            connection.CreateCommand();

        command.CommandText =
            """
            SELECT COALESCE(SUM(DuplicateCount - 1), 0)
            FROM
            (
                SELECT COUNT(*) AS DuplicateCount
                FROM Movies
                GROUP BY
                    LOWER(TRIM(Title)),
                    ReleaseYear
                HAVING COUNT(*) > 1
            );
            """;

        object? result = await command.ExecuteScalarAsync();

        return Convert.ToInt32(result);
    }

    public async Task<int> CountIncompleteCollectionsAsync()
    {
        await using SqliteConnection connection =
            new($"Data Source={DatabaseService.DatabasePath}");

        await connection.OpenAsync();

        await using SqliteCommand command =
            connection.CreateCommand();

        command.CommandText =
            """
            SELECT COUNT(*)
            FROM Collections
            WHERE TargetCount > OwnedCount;
            """;

        object? result = await command.ExecuteScalarAsync();

        return Convert.ToInt32(result);
    }

    public async Task<int> CountMoviesWithMissingMetadataAsync()
    {
        await using SqliteConnection connection =
            new($"Data Source={DatabaseService.DatabasePath}");

        await connection.OpenAsync();

        await using SqliteCommand command =
            connection.CreateCommand();

        command.CommandText =
            """
            SELECT COUNT(*)
            FROM Movies
            WHERE
                ReleaseYear <= 0
                OR TRIM(Rating) = ''
                OR TRIM(Genre) = ''
                OR TRIM(Director) = '';
            """;

        object? result = await command.ExecuteScalarAsync();

        return Convert.ToInt32(result);
    }
}