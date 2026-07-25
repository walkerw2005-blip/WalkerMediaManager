using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.Data.Sqlite;
using WalkerMediaManager.UI.Data;
using WalkerMediaManager.UI.Models;

namespace WalkerMediaManager.UI.Repositories;

public sealed class PurchaseHistoryRepository
{
    public async Task<PurchaseHistorySummary> GetSummaryAsync()
    {
        await using SqliteConnection connection = new($"Data Source={DatabaseService.DatabasePath}");
        await connection.OpenAsync();

        await using SqliteCommand command = connection.CreateCommand();
        command.CommandText =
            """
            SELECT
                COUNT(*) AS PurchaseCount,
                COALESCE(SUM(PurchasePrice), 0) AS RecordedSpending,
                COALESCE(AVG(PurchasePrice), 0) AS AveragePrice,
                COUNT(DISTINCT CASE
                    WHEN Store IS NOT NULL AND TRIM(Store) <> '' THEN TRIM(Store)
                END) AS StoreCount,
                SUM(CASE
                    WHEN PurchaseDate IS NULL OR TRIM(PurchaseDate) = '' THEN 1 ELSE 0
                END) AS MissingDateCount
            FROM OwnedCopies;
            """;

        await using SqliteDataReader reader = await command.ExecuteReaderAsync();
        if (!await reader.ReadAsync())
            return new PurchaseHistorySummary();

        return new PurchaseHistorySummary
        {
            PurchaseCount = GetInt(reader, 0),
            RecordedSpending = GetDecimal(reader, 1),
            AveragePrice = GetDecimal(reader, 2),
            StoreCount = GetInt(reader, 3),
            MissingDateCount = GetInt(reader, 4)
        };
    }

    public async Task<List<string>> GetStoresAsync()
    {
        List<string> stores = [];

        await using SqliteConnection connection = new($"Data Source={DatabaseService.DatabasePath}");
        await connection.OpenAsync();

        await using SqliteCommand command = connection.CreateCommand();
        command.CommandText =
            """
            SELECT DISTINCT TRIM(Store)
            FROM OwnedCopies
            WHERE Store IS NOT NULL AND TRIM(Store) <> ''
            ORDER BY TRIM(Store) COLLATE NOCASE;
            """;

        await using SqliteDataReader reader = await command.ExecuteReaderAsync();
        while (await reader.ReadAsync())
            stores.Add(reader.GetString(0));

        return stores;
    }

    public async Task<List<string>> GetFormatsAsync()
    {
        List<string> formats = [];

        await using SqliteConnection connection = new($"Data Source={DatabaseService.DatabasePath}");
        await connection.OpenAsync();

        await using SqliteCommand command = connection.CreateCommand();
        command.CommandText =
            """
            SELECT DISTINCT TRIM(Format)
            FROM OwnedCopies
            WHERE Format IS NOT NULL AND TRIM(Format) <> ''
            ORDER BY TRIM(Format) COLLATE NOCASE;
            """;

        await using SqliteDataReader reader = await command.ExecuteReaderAsync();
        while (await reader.ReadAsync())
            formats.Add(reader.GetString(0));

        return formats;
    }

    public async Task<List<PurchaseHistoryRow>> SearchAsync(
        string search,
        string store,
        string format,
        string mediaType,
        string sort)
    {
        List<PurchaseHistoryRow> items = [];

        await using SqliteConnection connection = new($"Data Source={DatabaseService.DatabasePath}");
        await connection.OpenAsync();

        await using SqliteCommand command = connection.CreateCommand();
        command.CommandText =
            $"""
            SELECT
                c.Id,
                c.MovieId,
                m.Title,
                m.ReleaseYear,
                c.Format,
                c.Edition,
                c.Packaging,
                c.Store,
                c.PurchasePrice,
                c.PurchaseDate,
                c.Location,
                c.IsDigital
            FROM OwnedCopies c
            INNER JOIN Movies m ON m.Id = c.MovieId
            WHERE ($search = ''
                   OR m.Title LIKE $searchPattern COLLATE NOCASE
                   OR c.Format LIKE $searchPattern COLLATE NOCASE
                   OR c.Edition LIKE $searchPattern COLLATE NOCASE
                   OR c.Store LIKE $searchPattern COLLATE NOCASE
                   OR c.Location LIKE $searchPattern COLLATE NOCASE)
              AND ($store = '' OR TRIM(c.Store) = $store COLLATE NOCASE)
              AND ($format = '' OR TRIM(c.Format) = $format COLLATE NOCASE)
              AND ($mediaType = ''
                   OR ($mediaType = 'Digital' AND c.IsDigital = 1)
                   OR ($mediaType = 'Physical' AND c.IsDigital = 0))
            ORDER BY {GetOrderBy(sort)};
            """;

        string cleanedSearch = search.Trim();
        command.Parameters.AddWithValue("$search", cleanedSearch);
        command.Parameters.AddWithValue("$searchPattern", $"%{cleanedSearch}%");
        command.Parameters.AddWithValue("$store", store.Trim());
        command.Parameters.AddWithValue("$format", format.Trim());
        command.Parameters.AddWithValue("$mediaType", mediaType.Trim());

        await using SqliteDataReader reader = await command.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            items.Add(new PurchaseHistoryRow
            {
                CopyId = reader.GetInt32(0),
                MovieId = reader.GetInt32(1),
                MovieTitle = GetString(reader, 2),
                MovieYear = reader.IsDBNull(3) ? null : Convert.ToInt32(reader.GetValue(3)),
                Format = GetString(reader, 4),
                Edition = GetString(reader, 5),
                Packaging = GetString(reader, 6),
                Store = GetString(reader, 7),
                PurchasePrice = reader.IsDBNull(8) ? null : Convert.ToDecimal(reader.GetValue(8)),
                PurchaseDate = GetString(reader, 9),
                Location = GetString(reader, 10),
                IsDigital = !reader.IsDBNull(11) && reader.GetInt32(11) != 0
            });
        }

        return items;
    }

    private static string GetOrderBy(string sort) => sort switch
    {
        "Date oldest" =>
            "CASE WHEN c.PurchaseDate IS NULL OR TRIM(c.PurchaseDate) = '' THEN 1 ELSE 0 END, date(c.PurchaseDate), c.Id",
        "Price high-low" =>
            "CASE WHEN c.PurchasePrice IS NULL THEN 1 ELSE 0 END, c.PurchasePrice DESC, m.Title COLLATE NOCASE",
        "Price low-high" =>
            "CASE WHEN c.PurchasePrice IS NULL THEN 1 ELSE 0 END, c.PurchasePrice, m.Title COLLATE NOCASE",
        "Title A-Z" => "m.Title COLLATE NOCASE, m.ReleaseYear, c.Id",
        "Store A-Z" =>
            "CASE WHEN c.Store IS NULL OR TRIM(c.Store) = '' THEN 1 ELSE 0 END, c.Store COLLATE NOCASE, m.Title COLLATE NOCASE",
        _ =>
            "CASE WHEN c.PurchaseDate IS NULL OR TRIM(c.PurchaseDate) = '' THEN 1 ELSE 0 END, date(c.PurchaseDate) DESC, c.Id DESC"
    };

    private static string GetString(SqliteDataReader reader, int ordinal) =>
        reader.IsDBNull(ordinal) ? string.Empty : reader.GetString(ordinal);

    private static int GetInt(SqliteDataReader reader, int ordinal) =>
        reader.IsDBNull(ordinal) ? 0 : Convert.ToInt32(reader.GetValue(ordinal));

    private static decimal GetDecimal(SqliteDataReader reader, int ordinal) =>
        reader.IsDBNull(ordinal) ? 0m : Convert.ToDecimal(reader.GetValue(ordinal));
}
