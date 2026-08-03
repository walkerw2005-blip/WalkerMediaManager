using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Data.Sqlite;
using WalkerMediaManager.UI.Data;
using WalkerMediaManager.UI.Models;
using WalkerMediaManager.UI.Services;

namespace WalkerMediaManager.UI.Repositories;

public sealed class WishlistRepository
{
    public Task<List<WishlistItem>> GetAllAsync() =>
        SearchAsync(includePurchased: false);

    public async Task<WishlistItem?> GetByIdAsync(int id)
    {
        await using SqliteConnection connection = CreateConnection();
        await connection.OpenAsync();
        await using SqliteCommand command = connection.CreateCommand();
        command.CommandText = $"SELECT {SelectColumns} FROM Wishlist WHERE Id = $id LIMIT 1;";
        command.Parameters.AddWithValue("$id", id);

        await using SqliteDataReader reader = await command.ExecuteReaderAsync();
        return await reader.ReadAsync() ? ReadItem(reader) : null;
    }

    public async Task<WishlistItem?> FindMatchAsync(
        string title,
        int year = 0,
        string mediaType = "")
    {
        string normalizedTitle = MediaIdentityService.NormalizeTitle(title);
        if (string.IsNullOrWhiteSpace(normalizedTitle))
        {
            return null;
        }

        await using SqliteConnection connection = CreateConnection();
        await connection.OpenAsync();
        await using SqliteCommand command = connection.CreateCommand();
        command.CommandText =
            $"""
            SELECT {SelectColumns}
            FROM Wishlist
            WHERE NormalizedTitle = $normalizedTitle
              AND ($year = 0 OR Year = 0 OR Year = $year)
              AND ($mediaType = '' OR MediaType = $mediaType COLLATE NOCASE)
              AND IsPurchased = 0
            ORDER BY
                CASE WHEN Year = $year AND $year > 0 THEN 0 ELSE 1 END,
                Id
            LIMIT 1;
            """;
        command.Parameters.AddWithValue("$normalizedTitle", normalizedTitle);
        command.Parameters.AddWithValue("$year", Math.Max(0, year));
        command.Parameters.AddWithValue("$mediaType", mediaType.Trim());

        await using SqliteDataReader reader = await command.ExecuteReaderAsync();
        return await reader.ReadAsync() ? ReadItem(reader) : null;
    }

    public async Task<List<WishlistItem>> SearchAsync(
        string searchText = "",
        string mediaType = "",
        int? priority = null,
        bool includePurchased = false,
        WishlistSortOrder sortOrder = WishlistSortOrder.Priority)
    {
        List<WishlistItem> items = [];
        await using SqliteConnection connection = CreateConnection();
        await connection.OpenAsync();
        await using SqliteCommand command = connection.CreateCommand();

        StringBuilder sql = new();
        sql.Append($"SELECT {SelectColumns} FROM Wishlist WHERE 1 = 1");

        if (!includePurchased)
        {
            sql.Append(" AND IsPurchased = 0");
        }

        string normalizedSearch = MediaIdentityService.NormalizeTitle(searchText);
        if (!string.IsNullOrEmpty(normalizedSearch))
        {
            sql.Append(" AND (NormalizedTitle LIKE $search OR UPPER(Notes) LIKE $notes OR UPPER(PreferredStore) LIKE $notes OR UPPER(PreferredFormat) LIKE $notes)");
            command.Parameters.AddWithValue("$search", $"%{normalizedSearch}%");
            command.Parameters.AddWithValue("$notes", $"%{searchText.Trim().ToUpperInvariant()}%");
        }

        if (!string.IsNullOrWhiteSpace(mediaType))
        {
            sql.Append(" AND MediaType = $mediaType COLLATE NOCASE");
            command.Parameters.AddWithValue("$mediaType", mediaType.Trim());
        }

        if (priority.HasValue)
        {
            sql.Append(" AND Priority = $priority");
            command.Parameters.AddWithValue("$priority", Math.Clamp(priority.Value, 1, 5));
        }

        sql.Append(" ORDER BY ");
        sql.Append(sortOrder switch
        {
            WishlistSortOrder.Title => "Title COLLATE NOCASE, Year, Id",
            WishlistSortOrder.DateAddedNewest => "datetime(DateAdded) DESC, Id DESC",
            WishlistSortOrder.DateAddedOldest => "datetime(DateAdded), Id",
            WishlistSortOrder.TargetPriceLowToHigh => "TargetPrice IS NULL, TargetPrice, Title COLLATE NOCASE",
            WishlistSortOrder.TargetPriceHighToLow => "TargetPrice IS NULL, TargetPrice DESC, Title COLLATE NOCASE",
            _ => "Priority DESC, Title COLLATE NOCASE, Year, Id"
        });
        sql.Append(';');
        command.CommandText = sql.ToString();

        await using SqliteDataReader reader = await command.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            items.Add(ReadItem(reader));
        }

        return items;
    }

    public async Task<int> AddAsync(WishlistItem item)
    {
        Validate(item);
        MediaIdentityService.PrepareWishlistItem(item);

        if (await ExistsAsync(item.Title, item.Year, item.MediaType))
        {
            throw new InvalidOperationException(
                $"{item.Title}{(item.Year > 0 ? $" ({item.Year})" : string.Empty)} is already on the wishlist.");
        }

        DateTime now = DateTime.UtcNow;
        item.DateAdded = item.DateAdded == default ? now : item.DateAdded.ToUniversalTime();
        item.LastUpdated = now;

        await using SqliteConnection connection = CreateConnection();
        await connection.OpenAsync();
        await using SqliteCommand command = connection.CreateCommand();
        command.CommandText =
            """
            INSERT INTO Wishlist
            (
                MediaType, Title, NormalizedTitle, Year, TMDbId,
                PreferredFormat, TargetPrice, PreferredStore, Priority,
                Notes, DateAdded, LastUpdated, IsPurchased
            )
            VALUES
            (
                $mediaType, $title, $normalizedTitle, $year, $tmdbId,
                $preferredFormat, $targetPrice, $preferredStore, $priority,
                $notes, $dateAdded, $lastUpdated, $isPurchased
            );
            SELECT last_insert_rowid();
            """;
        AddItemParameters(command, item);
        object? result = await command.ExecuteScalarAsync();
        item.Id = Convert.ToInt32(result);
        return item.Id;
    }

    public async Task UpdateAsync(WishlistItem item)
    {
        if (item.Id <= 0)
        {
            throw new ArgumentException("A valid wishlist item ID is required.", nameof(item));
        }

        Validate(item);
        MediaIdentityService.PrepareWishlistItem(item);

        if (await ExistsAsync(item.Title, item.Year, item.MediaType, item.Id))
        {
            throw new InvalidOperationException(
                $"{item.Title}{(item.Year > 0 ? $" ({item.Year})" : string.Empty)} is already on the wishlist.");
        }

        item.LastUpdated = DateTime.UtcNow;
        if (item.DateAdded == default)
        {
            item.DateAdded = item.LastUpdated;
        }

        await using SqliteConnection connection = CreateConnection();
        await connection.OpenAsync();
        await using SqliteCommand command = connection.CreateCommand();
        command.CommandText =
            """
            UPDATE Wishlist
            SET MediaType = $mediaType,
                Title = $title,
                NormalizedTitle = $normalizedTitle,
                Year = $year,
                TMDbId = $tmdbId,
                PreferredFormat = $preferredFormat,
                TargetPrice = $targetPrice,
                PreferredStore = $preferredStore,
                Priority = $priority,
                Notes = $notes,
                DateAdded = $dateAdded,
                LastUpdated = $lastUpdated,
                IsPurchased = $isPurchased
            WHERE Id = $id;
            """;
        AddItemParameters(command, item);
        command.Parameters.AddWithValue("$id", item.Id);

        if (await command.ExecuteNonQueryAsync() == 0)
        {
            throw new InvalidOperationException("The wishlist item no longer exists.");
        }
    }

    public async Task MarkPurchasedAsync(int wishlistItemId) =>
        await SetPurchasedAsync(wishlistItemId, true);

    public async Task RestoreAsync(int wishlistItemId) =>
        await SetPurchasedAsync(wishlistItemId, false);

    public async Task DeleteAsync(int wishlistItemId)
    {
        await using SqliteConnection connection = CreateConnection();
        await connection.OpenAsync();
        await using SqliteCommand command = connection.CreateCommand();
        command.CommandText = "DELETE FROM Wishlist WHERE Id = $id;";
        command.Parameters.AddWithValue("$id", wishlistItemId);
        await command.ExecuteNonQueryAsync();
    }

    public Task<bool> ExistsAsync(string title) =>
        ExistsAsync(title, 0, string.Empty);

    public async Task<bool> ExistsAsync(
        string title,
        int year,
        string mediaType = "",
        int? excludedId = null)
    {
        string normalizedTitle = MediaIdentityService.NormalizeTitle(title);
        if (string.IsNullOrEmpty(normalizedTitle))
        {
            return false;
        }

        await using SqliteConnection connection = CreateConnection();
        await connection.OpenAsync();
        await using SqliteCommand command = connection.CreateCommand();
        command.CommandText =
            """
            SELECT 1
            FROM Wishlist
            WHERE NormalizedTitle = $normalizedTitle
              AND ($year = 0 OR Year = 0 OR Year = $year)
              AND ($mediaType = '' OR MediaType = $mediaType COLLATE NOCASE)
              AND ($excludedId IS NULL OR Id <> $excludedId)
            LIMIT 1;
            """;
        command.Parameters.AddWithValue("$normalizedTitle", normalizedTitle);
        command.Parameters.AddWithValue("$year", Math.Max(0, year));
        command.Parameters.AddWithValue("$mediaType", mediaType.Trim());
        command.Parameters.AddWithValue("$excludedId", excludedId.HasValue ? excludedId.Value : DBNull.Value);
        return await command.ExecuteScalarAsync() is not null;
    }

    public async Task<int> CountAsync(bool includePurchased = false)
    {
        await using SqliteConnection connection = CreateConnection();
        await connection.OpenAsync();
        await using SqliteCommand command = connection.CreateCommand();
        command.CommandText = includePurchased
            ? "SELECT COUNT(*) FROM Wishlist;"
            : "SELECT COUNT(*) FROM Wishlist WHERE IsPurchased = 0;";
        return Convert.ToInt32(await command.ExecuteScalarAsync());
    }

    private async Task SetPurchasedAsync(int wishlistItemId, bool isPurchased)
    {
        await using SqliteConnection connection = CreateConnection();
        await connection.OpenAsync();
        await using SqliteCommand command = connection.CreateCommand();
        command.CommandText =
            """
            UPDATE Wishlist
            SET IsPurchased = $isPurchased,
                LastUpdated = $lastUpdated
            WHERE Id = $id;
            """;
        command.Parameters.AddWithValue("$isPurchased", isPurchased ? 1 : 0);
        command.Parameters.AddWithValue("$lastUpdated", DateTime.UtcNow.ToString("O"));
        command.Parameters.AddWithValue("$id", wishlistItemId);

        if (await command.ExecuteNonQueryAsync() == 0)
        {
            throw new InvalidOperationException("The wishlist item no longer exists.");
        }
    }

    private static void Validate(WishlistItem item)
    {
        ArgumentNullException.ThrowIfNull(item);
        if (string.IsNullOrWhiteSpace(item.Title))
        {
            throw new ArgumentException("A title is required.", nameof(item));
        }
    }

    private static SqliteConnection CreateConnection() =>
        new($"Data Source={DatabaseService.DatabasePath}");

    private static void AddItemParameters(SqliteCommand command, WishlistItem item)
    {
        command.Parameters.AddWithValue("$mediaType", item.MediaType);
        command.Parameters.AddWithValue("$title", item.Title);
        command.Parameters.AddWithValue("$normalizedTitle", item.NormalizedTitle);
        command.Parameters.AddWithValue("$year", item.Year);
        command.Parameters.AddWithValue("$tmdbId", item.TMDbId.HasValue ? item.TMDbId.Value : DBNull.Value);
        command.Parameters.AddWithValue("$preferredFormat", item.PreferredFormat);
        command.Parameters.AddWithValue("$targetPrice", item.TargetPrice.HasValue ? item.TargetPrice.Value : DBNull.Value);
        command.Parameters.AddWithValue("$preferredStore", item.PreferredStore);
        command.Parameters.AddWithValue("$priority", item.Priority);
        command.Parameters.AddWithValue("$notes", item.Notes);
        command.Parameters.AddWithValue("$dateAdded", item.DateAdded.ToUniversalTime().ToString("O"));
        command.Parameters.AddWithValue("$lastUpdated", item.LastUpdated.ToUniversalTime().ToString("O"));
        command.Parameters.AddWithValue("$isPurchased", item.IsPurchased ? 1 : 0);
    }

    private static WishlistItem ReadItem(SqliteDataReader reader) =>
        new()
        {
            Id = reader.GetInt32(0),
            MediaType = GetString(reader, 1, "Movie"),
            Title = GetString(reader, 2),
            NormalizedTitle = GetString(reader, 3),
            Year = reader.IsDBNull(4) ? 0 : reader.GetInt32(4),
            TMDbId = reader.IsDBNull(5) ? null : reader.GetInt32(5),
            PreferredFormat = GetString(reader, 6),
            TargetPrice = reader.IsDBNull(7) ? null : Convert.ToDecimal(reader.GetValue(7)),
            PreferredStore = GetString(reader, 8),
            Priority = reader.IsDBNull(9) ? 2 : reader.GetInt32(9),
            Notes = GetString(reader, 10),
            DateAdded = ParseDate(GetString(reader, 11)),
            LastUpdated = ParseDate(GetString(reader, 12)),
            IsPurchased = !reader.IsDBNull(13) && reader.GetInt32(13) != 0
        };

    private static DateTime ParseDate(string value) =>
        DateTime.TryParse(value, null, System.Globalization.DateTimeStyles.RoundtripKind, out DateTime parsed)
            ? parsed
            : DateTime.MinValue;

    private static string GetString(SqliteDataReader reader, int ordinal, string fallback = "") =>
        reader.IsDBNull(ordinal) ? fallback : reader.GetString(ordinal);

    private const string SelectColumns =
        "Id, MediaType, Title, NormalizedTitle, Year, TMDbId, PreferredFormat, " +
        "TargetPrice, PreferredStore, Priority, Notes, DateAdded, LastUpdated, IsPurchased";
}
