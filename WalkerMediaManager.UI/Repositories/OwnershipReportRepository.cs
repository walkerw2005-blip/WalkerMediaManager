using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.Data.Sqlite;
using WalkerMediaManager.UI.Data;
using WalkerMediaManager.UI.Models;

namespace WalkerMediaManager.UI.Repositories;

public sealed class OwnershipReportRepository
{
    public async Task<OwnershipReportSummary> GetSummaryAsync()
    {
        await using SqliteConnection connection = new($"Data Source={DatabaseService.DatabasePath}");
        await connection.OpenAsync();

        await using SqliteCommand command = connection.CreateCommand();
        command.CommandText =
            """
            SELECT
                COUNT(*) AS OwnedCopyCount,
                COUNT(DISTINCT MovieId) AS MovieCountWithCopies,
                COALESCE(SUM(PurchasePrice), 0) AS RecordedCollectionValue,
                COALESCE(AVG(PurchasePrice), 0) AS AverageRecordedPrice,
                SUM(CASE WHEN IsDigital = 0 THEN 1 ELSE 0 END) AS PhysicalCopyCount,
                SUM(CASE WHEN IsDigital = 1 THEN 1 ELSE 0 END) AS DigitalCopyCount,
                SUM(CASE WHEN PurchasePrice IS NULL THEN 1 ELSE 0 END) AS MissingPriceCount,
                SUM(CASE WHEN PurchaseDate IS NULL OR TRIM(PurchaseDate) = '' THEN 1 ELSE 0 END) AS MissingPurchaseDateCount,
                SUM(CASE WHEN Location IS NULL OR TRIM(Location) = '' THEN 1 ELSE 0 END) AS MissingLocationCount
            FROM OwnedCopies;
            """;

        await using SqliteDataReader reader = await command.ExecuteReaderAsync();
        if (!await reader.ReadAsync())
            return new OwnershipReportSummary();

        return new OwnershipReportSummary
        {
            OwnedCopyCount = GetInt(reader, 0),
            MovieCountWithCopies = GetInt(reader, 1),
            RecordedCollectionValue = GetDecimal(reader, 2),
            AverageRecordedPrice = GetDecimal(reader, 3),
            PhysicalCopyCount = GetInt(reader, 4),
            DigitalCopyCount = GetInt(reader, 5),
            MissingPriceCount = GetInt(reader, 6),
            MissingPurchaseDateCount = GetInt(reader, 7),
            MissingLocationCount = GetInt(reader, 8)
        };
    }

    public async Task<List<ReportBreakdownItem>> GetFormatBreakdownAsync()
    {
        List<ReportBreakdownItem> items = [];

        await using SqliteConnection connection = new($"Data Source={DatabaseService.DatabasePath}");
        await connection.OpenAsync();

        await using SqliteCommand command = connection.CreateCommand();
        command.CommandText =
            """
            SELECT
                CASE WHEN Format IS NULL OR TRIM(Format) = '' THEN 'Unspecified' ELSE TRIM(Format) END AS FormatName,
                COUNT(*) AS CopyCount,
                COALESCE(SUM(PurchasePrice), 0) AS RecordedValue
            FROM OwnedCopies
            GROUP BY FormatName
            ORDER BY CopyCount DESC, FormatName COLLATE NOCASE;
            """;

        await using SqliteDataReader reader = await command.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            items.Add(new ReportBreakdownItem
            {
                Name = reader.IsDBNull(0) ? "Unspecified" : reader.GetString(0),
                Count = GetInt(reader, 1),
                Value = GetDecimal(reader, 2)
            });
        }

        return items;
    }

    public async Task<List<ReportBreakdownItem>> GetStoreBreakdownAsync()
    {
        List<ReportBreakdownItem> items = [];

        await using SqliteConnection connection = new($"Data Source={DatabaseService.DatabasePath}");
        await connection.OpenAsync();

        await using SqliteCommand command = connection.CreateCommand();
        command.CommandText =
            """
            SELECT
                CASE WHEN Store IS NULL OR TRIM(Store) = '' THEN 'Store not recorded' ELSE TRIM(Store) END AS StoreName,
                COUNT(*) AS CopyCount,
                COALESCE(SUM(PurchasePrice), 0) AS RecordedValue
            FROM OwnedCopies
            GROUP BY StoreName
            ORDER BY RecordedValue DESC, CopyCount DESC, StoreName COLLATE NOCASE
            LIMIT 12;
            """;

        await using SqliteDataReader reader = await command.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            items.Add(new ReportBreakdownItem
            {
                Name = reader.IsDBNull(0) ? "Store not recorded" : reader.GetString(0),
                Count = GetInt(reader, 1),
                Value = GetDecimal(reader, 2)
            });
        }

        return items;
    }

    public async Task<List<OwnedCopyReportRow>> GetRecentPurchasesAsync(int limit = 25)
    {
        List<OwnedCopyReportRow> items = [];

        await using SqliteConnection connection = new($"Data Source={DatabaseService.DatabasePath}");
        await connection.OpenAsync();

        await using SqliteCommand command = connection.CreateCommand();
        command.CommandText =
            """
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
                c.IsDigital,
                c.IsFavorite
            FROM OwnedCopies c
            INNER JOIN Movies m ON m.Id = c.MovieId
            ORDER BY
                CASE WHEN c.PurchaseDate IS NULL OR TRIM(c.PurchaseDate) = '' THEN 1 ELSE 0 END,
                date(c.PurchaseDate) DESC,
                c.Id DESC
            LIMIT $limit;
            """;
        command.Parameters.AddWithValue("$limit", Math.Max(1, limit));

        await using SqliteDataReader reader = await command.ExecuteReaderAsync();
        while (await reader.ReadAsync())
            items.Add(ReadRow(reader));

        return items;
    }

    public async Task<List<OwnedCopyReportRow>> GetMissingInformationAsync(int limit = 100)
    {
        List<OwnedCopyReportRow> items = [];

        await using SqliteConnection connection = new($"Data Source={DatabaseService.DatabasePath}");
        await connection.OpenAsync();

        await using SqliteCommand command = connection.CreateCommand();
        command.CommandText =
            """
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
                c.IsDigital,
                c.IsFavorite
            FROM OwnedCopies c
            INNER JOIN Movies m ON m.Id = c.MovieId
            WHERE c.PurchasePrice IS NULL
               OR c.PurchaseDate IS NULL OR TRIM(c.PurchaseDate) = ''
               OR c.Location IS NULL OR TRIM(c.Location) = ''
            ORDER BY m.Title COLLATE NOCASE, m.ReleaseYear, c.Id
            LIMIT $limit;
            """;
        command.Parameters.AddWithValue("$limit", Math.Max(1, limit));

        await using SqliteDataReader reader = await command.ExecuteReaderAsync();
        while (await reader.ReadAsync())
            items.Add(ReadRow(reader));

        return items;
    }

    private static OwnedCopyReportRow ReadRow(SqliteDataReader reader)
    {
        return new OwnedCopyReportRow
        {
            CopyId = reader.GetInt32(0),
            MovieId = reader.GetInt32(1),
            MovieTitle = reader.IsDBNull(2) ? string.Empty : reader.GetString(2),
            MovieYear = reader.IsDBNull(3) ? null : reader.GetInt32(3),
            Format = GetString(reader, 4),
            Edition = GetString(reader, 5),
            Packaging = GetString(reader, 6),
            Store = GetString(reader, 7),
            PurchasePrice = reader.IsDBNull(8) ? null : Convert.ToDecimal(reader.GetDouble(8)),
            PurchaseDate = GetString(reader, 9),
            Location = GetString(reader, 10),
            IsDigital = !reader.IsDBNull(11) && reader.GetInt32(11) != 0,
            IsFavorite = !reader.IsDBNull(12) && reader.GetInt32(12) != 0
        };
    }

    private static string GetString(SqliteDataReader reader, int ordinal) =>
        reader.IsDBNull(ordinal) ? string.Empty : reader.GetString(ordinal);

    private static int GetInt(SqliteDataReader reader, int ordinal) =>
        reader.IsDBNull(ordinal) ? 0 : Convert.ToInt32(reader.GetValue(ordinal));

    private static decimal GetDecimal(SqliteDataReader reader, int ordinal) =>
        reader.IsDBNull(ordinal) ? 0m : Convert.ToDecimal(reader.GetValue(ordinal));
}