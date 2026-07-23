using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.Data.Sqlite;
using WalkerMediaManager.UI.Data;
using WalkerMediaManager.UI.Models;

namespace WalkerMediaManager.UI.Repositories;

public sealed class WishlistRepository
{
    public async Task<List<WishlistItem>> GetAllAsync()
    {
        List<WishlistItem> items = [];

        await using SqliteConnection connection =
            new($"Data Source={DatabaseService.DatabasePath}");

        await connection.OpenAsync();

        await using SqliteCommand command = connection.CreateCommand();

        command.CommandText =
            """
            SELECT
                Id,
                Title,
                Priority
            FROM Wishlist
            ORDER BY
                Priority DESC,
                Title;
            """;

        await using SqliteDataReader reader =
            await command.ExecuteReaderAsync();

        while (await reader.ReadAsync())
        {
            items.Add(
                new WishlistItem
                {
                    Id = reader.GetInt32(0),
                    Title = reader.IsDBNull(1)
                        ? string.Empty
                        : reader.GetString(1),
                    Priority = reader.IsDBNull(2)
                        ? 0
                        : reader.GetInt32(2)
                });
        }

        return items;
    }

    public async Task<int> AddAsync(
        WishlistItem item)
    {
        await using SqliteConnection connection =
            new($"Data Source={DatabaseService.DatabasePath}");

        await connection.OpenAsync();

        await using SqliteCommand command = connection.CreateCommand();

        command.CommandText =
            """
            INSERT INTO Wishlist
            (
                Title,
                Priority
            )
            VALUES
            (
                $title,
                $priority
            );

            SELECT last_insert_rowid();
            """;

        command.Parameters.AddWithValue(
            "$title",
            item.Title.Trim());

        command.Parameters.AddWithValue(
            "$priority",
            item.Priority);

        object? result = await command.ExecuteScalarAsync();

        return Convert.ToInt32(result);
    }

    public async Task UpdateAsync(
        WishlistItem item)
    {
        await using SqliteConnection connection =
            new($"Data Source={DatabaseService.DatabasePath}");

        await connection.OpenAsync();

        await using SqliteCommand command = connection.CreateCommand();

        command.CommandText =
            """
            UPDATE Wishlist
            SET
                Title = $title,
                Priority = $priority
            WHERE Id = $id;
            """;

        command.Parameters.AddWithValue(
            "$title",
            item.Title.Trim());

        command.Parameters.AddWithValue(
            "$priority",
            item.Priority);

        command.Parameters.AddWithValue(
            "$id",
            item.Id);

        await command.ExecuteNonQueryAsync();
    }

    public async Task DeleteAsync(
        int wishlistItemId)
    {
        await using SqliteConnection connection =
            new($"Data Source={DatabaseService.DatabasePath}");

        await connection.OpenAsync();

        await using SqliteCommand command = connection.CreateCommand();

        command.CommandText =
            """
            DELETE FROM Wishlist
            WHERE Id = $id;
            """;

        command.Parameters.AddWithValue(
            "$id",
            wishlistItemId);

        await command.ExecuteNonQueryAsync();
    }

    public async Task<bool> ExistsAsync(
        string title)
    {
        await using SqliteConnection connection =
            new($"Data Source={DatabaseService.DatabasePath}");

        await connection.OpenAsync();

        await using SqliteCommand command = connection.CreateCommand();

        command.CommandText =
            """
            SELECT COUNT(*)
            FROM Wishlist
            WHERE LOWER(Title) = LOWER($title);
            """;

        command.Parameters.AddWithValue(
            "$title",
            title.Trim());

        object? result = await command.ExecuteScalarAsync();

        return Convert.ToInt32(result) > 0;
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
            FROM Wishlist;
            """;

        object? result = await command.ExecuteScalarAsync();

        return Convert.ToInt32(result);
    }
}