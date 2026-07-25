using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.Data.Sqlite;
using WalkerMediaManager.UI.Data;
using WalkerMediaManager.UI.Models;

namespace WalkerMediaManager.UI.Repositories;

public sealed class ShoppingRepository
{
    public async Task<BarcodeRecord?> FindBarcodeAsync(string code)
    {
        await using SqliteConnection connection = new($"Data Source={DatabaseService.DatabasePath}");
        await connection.OpenAsync();
        await using SqliteCommand command = connection.CreateCommand();
        command.CommandText =
            """
            SELECT b.Id, b.Code, b.MovieId, m.Title, m.ReleaseYear,
                   b.Format, b.Edition, b.Notes, b.CreatedAt
            FROM Barcodes b
            INNER JOIN Movies m ON m.Id = b.MovieId
            WHERE b.Code = $code
            LIMIT 1;
            """;
        command.Parameters.AddWithValue("$code", NormalizeCode(code));
        await using SqliteDataReader reader = await command.ExecuteReaderAsync();
        if (!await reader.ReadAsync()) return null;
        return new BarcodeRecord
        {
            Id = reader.GetInt32(0),
            Code = reader.GetString(1),
            MovieId = reader.GetInt32(2),
            Title = reader.GetString(3),
            Year = reader.IsDBNull(4) ? 0 : reader.GetInt32(4),
            Format = reader.IsDBNull(5) ? string.Empty : reader.GetString(5),
            Edition = reader.IsDBNull(6) ? string.Empty : reader.GetString(6),
            Notes = reader.IsDBNull(7) ? string.Empty : reader.GetString(7),
            CreatedAt = DateTime.TryParse(reader.IsDBNull(8) ? string.Empty : reader.GetString(8), out DateTime created) ? created : DateTime.MinValue
        };
    }

    public async Task SaveBarcodeAsync(string code, int movieId, string format, string edition, string notes)
    {
        await using SqliteConnection connection = new($"Data Source={DatabaseService.DatabasePath}");
        await connection.OpenAsync();
        await using SqliteCommand command = connection.CreateCommand();
        command.CommandText =
            """
            INSERT INTO Barcodes (Code, MovieId, Format, Edition, Notes)
            VALUES ($code, $movieId, $format, $edition, $notes)
            ON CONFLICT(Code) DO UPDATE SET
                MovieId = excluded.MovieId,
                Format = excluded.Format,
                Edition = excluded.Edition,
                Notes = excluded.Notes;
            """;
        command.Parameters.AddWithValue("$code", NormalizeCode(code));
        command.Parameters.AddWithValue("$movieId", movieId);
        command.Parameters.AddWithValue("$format", format.Trim());
        command.Parameters.AddWithValue("$edition", edition.Trim());
        command.Parameters.AddWithValue("$notes", notes.Trim());
        await command.ExecuteNonQueryAsync();
    }

    public async Task AddHistoryAsync(ShoppingHistoryItem item)
    {
        await using SqliteConnection connection = new($"Data Source={DatabaseService.DatabasePath}");
        await connection.OpenAsync();
        await using SqliteCommand command = connection.CreateCommand();
        command.CommandText =
            """
            INSERT INTO ShoppingHistory
                (SearchText, Barcode, MovieId, Title, Store, PlannedFormat, Price, Decision, Notes, SearchedAt)
            VALUES
                ($searchText, $barcode, $movieId, $title, $store, $format, $price, $decision, $notes, $searchedAt);
            """;
        command.Parameters.AddWithValue("$searchText", item.SearchText.Trim());
        command.Parameters.AddWithValue("$barcode", NormalizeCode(item.Barcode));
        command.Parameters.AddWithValue("$movieId", item.MovieId.HasValue ? item.MovieId.Value : DBNull.Value);
        command.Parameters.AddWithValue("$title", item.Title.Trim());
        command.Parameters.AddWithValue("$store", item.Store.Trim());
        command.Parameters.AddWithValue("$format", item.PlannedFormat.Trim());
        command.Parameters.AddWithValue("$price", item.Price.HasValue ? item.Price.Value : DBNull.Value);
        command.Parameters.AddWithValue("$decision", item.Decision.Trim());
        command.Parameters.AddWithValue("$notes", item.Notes.Trim());
        command.Parameters.AddWithValue("$searchedAt", DateTime.UtcNow.ToString("O"));
        await command.ExecuteNonQueryAsync();
    }

    public async Task<List<ShoppingHistoryItem>> GetRecentHistoryAsync(int limit = 50)
    {
        List<ShoppingHistoryItem> items = [];
        await using SqliteConnection connection = new($"Data Source={DatabaseService.DatabasePath}");
        await connection.OpenAsync();
        await using SqliteCommand command = connection.CreateCommand();
        command.CommandText =
            """
            SELECT Id, SearchText, Barcode, MovieId, Title, Store, PlannedFormat,
                   Price, Decision, Notes, SearchedAt
            FROM ShoppingHistory
            ORDER BY datetime(SearchedAt) DESC, Id DESC
            LIMIT $limit;
            """;
        command.Parameters.AddWithValue("$limit", Math.Max(1, limit));
        await using SqliteDataReader reader = await command.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            items.Add(new ShoppingHistoryItem
            {
                Id = reader.GetInt32(0),
                SearchText = GetString(reader, 1),
                Barcode = GetString(reader, 2),
                MovieId = reader.IsDBNull(3) ? null : reader.GetInt32(3),
                Title = GetString(reader, 4),
                Store = GetString(reader, 5),
                PlannedFormat = GetString(reader, 6),
                Price = reader.IsDBNull(7) ? null : Convert.ToDecimal(reader.GetValue(7)),
                Decision = GetString(reader, 8),
                Notes = GetString(reader, 9),
                SearchedAt = DateTime.TryParse(GetString(reader, 10), out DateTime searched) ? searched : DateTime.MinValue
            });
        }
        return items;
    }

    private static string NormalizeCode(string code)
    {
        Span<char> buffer = stackalloc char[code.Length];
        int count = 0;
        foreach (char character in code)
            if (char.IsLetterOrDigit(character)) buffer[count++] = character;
        return new string(buffer[..count]);
    }

    private static string GetString(SqliteDataReader reader, int ordinal) =>
        reader.IsDBNull(ordinal) ? string.Empty : reader.GetString(ordinal);
}
