using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.Data.Sqlite;
using WalkerMediaManager.UI.Data;
using WalkerMediaManager.UI.Models;

namespace WalkerMediaManager.UI.Repositories;

public sealed class CollectionRepository
{
    public async Task<List<MediaCollection>> GetAllAsync()
    {
        List<MediaCollection> collections = [];

        await using SqliteConnection connection =
            new($"Data Source={DatabaseService.DatabasePath}");

        await connection.OpenAsync();

        await using SqliteCommand command =
            connection.CreateCommand();

        command.CommandText =
            """
            SELECT
                Id,
                Name,
                Description,
                TargetCount,
                OwnedCount
            FROM Collections
            ORDER BY Name;
            """;

        await using SqliteDataReader reader =
            await command.ExecuteReaderAsync();

        while (await reader.ReadAsync())
        {
            collections.Add(
                new MediaCollection
                {
                    Id = reader.GetInt32(0),

                    Name = reader.IsDBNull(1)
                        ? string.Empty
                        : reader.GetString(1),

                    Description = reader.IsDBNull(2)
                        ? string.Empty
                        : reader.GetString(2),

                    TargetCount = reader.IsDBNull(3)
                        ? 0
                        : reader.GetInt32(3),

                    OwnedCount = reader.IsDBNull(4)
                        ? 0
                        : reader.GetInt32(4)
                });
        }

        return collections;
    }

    public async Task<int> AddAsync(
        MediaCollection collection)
    {
        await using SqliteConnection connection =
            new($"Data Source={DatabaseService.DatabasePath}");

        await connection.OpenAsync();

        await using SqliteCommand command =
            connection.CreateCommand();

        command.CommandText =
            """
            INSERT INTO Collections
            (
                Name,
                Description,
                TargetCount,
                OwnedCount
            )
            VALUES
            (
                $name,
                $description,
                $targetCount,
                $ownedCount
            );

            SELECT last_insert_rowid();
            """;

        AddParameters(command, collection);

        object? result =
            await command.ExecuteScalarAsync();

        return Convert.ToInt32(result);
    }

    public async Task UpdateAsync(
        MediaCollection collection)
    {
        await using SqliteConnection connection =
            new($"Data Source={DatabaseService.DatabasePath}");

        await connection.OpenAsync();

        await using SqliteCommand command =
            connection.CreateCommand();

        command.CommandText =
            """
            UPDATE Collections
            SET
                Name = $name,
                Description = $description,
                TargetCount = $targetCount,
                OwnedCount = $ownedCount
            WHERE Id = $id;
            """;

        AddParameters(command, collection);

        command.Parameters.AddWithValue(
            "$id",
            collection.Id);

        await command.ExecuteNonQueryAsync();
    }

    public async Task DeleteAsync(
        int collectionId)
    {
        await using SqliteConnection connection =
            new($"Data Source={DatabaseService.DatabasePath}");

        await connection.OpenAsync();

        await using SqliteCommand command =
            connection.CreateCommand();

        command.CommandText =
            """
            DELETE FROM Collections
            WHERE Id = $id;
            """;

        command.Parameters.AddWithValue(
            "$id",
            collectionId);

        await command.ExecuteNonQueryAsync();
    }

    public async Task<bool> ExistsAsync(
        string name)
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
            WHERE LOWER(Name) = LOWER($name);
            """;

        command.Parameters.AddWithValue(
            "$name",
            name.Trim());

        object? result =
            await command.ExecuteScalarAsync();

        return Convert.ToInt32(result) > 0;
    }

    public async Task<int> CountIncompleteAsync()
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

        object? result =
            await command.ExecuteScalarAsync();

        return Convert.ToInt32(result);
    }

    private static void AddParameters(
        SqliteCommand command,
        MediaCollection collection)
    {
        command.Parameters.AddWithValue(
            "$name",
            collection.Name.Trim());

        command.Parameters.AddWithValue(
            "$description",
            collection.Description.Trim());

        command.Parameters.AddWithValue(
            "$targetCount",
            collection.TargetCount);

        command.Parameters.AddWithValue(
            "$ownedCount",
            collection.OwnedCount);
    }
}