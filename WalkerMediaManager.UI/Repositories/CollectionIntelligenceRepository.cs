using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.Data.Sqlite;
using WalkerMediaManager.UI.Data;
using WalkerMediaManager.UI.Models;

namespace WalkerMediaManager.UI.Repositories;

public sealed class CollectionIntelligenceRepository
{
    public async Task<CollectionIntelligenceSummary> GetSummaryAsync()
    {
        await using SqliteConnection connection = new($"Data Source={DatabaseService.DatabasePath}");
        await connection.OpenAsync();

        await using SqliteCommand command = connection.CreateCommand();
        command.CommandText =
            """
            SELECT
                COUNT(DISTINCT oc.MovieId),
                COUNT(oc.Id),
                COUNT(DISTINCT CASE WHEN copies.CopyCount > 1 THEN oc.MovieId END),
                COUNT(DISTINCT CASE
                    WHEN LOWER(oc.Format) IN ('dvd', 'vhs', 'laserdisc')
                     AND NOT EXISTS (
                         SELECT 1 FROM OwnedCopies newer
                         WHERE newer.MovieId = oc.MovieId
                           AND LOWER(newer.Format) IN ('blu-ray', 'bluray', '4k', '4k uhd', 'ultra hd blu-ray'))
                    THEN oc.MovieId
                    WHEN LOWER(oc.Format) IN ('blu-ray', 'bluray')
                     AND NOT EXISTS (
                         SELECT 1 FROM OwnedCopies newer
                         WHERE newer.MovieId = oc.MovieId
                           AND LOWER(newer.Format) IN ('4k', '4k uhd', 'ultra hd blu-ray'))
                    THEN oc.MovieId
                END),
                (SELECT COUNT(*) FROM Loans WHERE ReturnedDate = ''),
                (SELECT COUNT(*) FROM Collections WHERE TargetCount > OwnedCount),
                COALESCE(SUM(
                    CASE
                        WHEN oc.PurchasePrice IS NOT NULL THEN oc.PurchasePrice
                        WHEN oc.IsDigital = 1 THEN 12.99
                        WHEN LOWER(oc.Format) IN ('4k', '4k uhd', 'ultra hd blu-ray') THEN 29.99
                        WHEN LOWER(oc.Format) IN ('blu-ray', 'bluray') THEN 19.99
                        WHEN LOWER(oc.Format) = 'dvd' THEN 12.99
                        ELSE 14.99
                    END), 0)
            FROM OwnedCopies oc
            LEFT JOIN (
                SELECT MovieId, COUNT(*) AS CopyCount
                FROM OwnedCopies
                GROUP BY MovieId
            ) copies ON copies.MovieId = oc.MovieId;
            """;

        await using SqliteDataReader reader = await command.ExecuteReaderAsync();
        if (!await reader.ReadAsync())
            return new CollectionIntelligenceSummary();

        return new CollectionIntelligenceSummary
        {
            UniqueOwnedMovies = reader.IsDBNull(0) ? 0 : reader.GetInt32(0),
            TotalCopies = reader.IsDBNull(1) ? 0 : reader.GetInt32(1),
            DuplicateTitleCount = reader.IsDBNull(2) ? 0 : reader.GetInt32(2),
            UpgradeOpportunityCount = reader.IsDBNull(3) ? 0 : reader.GetInt32(3),
            ActiveLoanCount = reader.IsDBNull(4) ? 0 : reader.GetInt32(4),
            IncompleteGoalCount = reader.IsDBNull(5) ? 0 : reader.GetInt32(5),
            EstimatedReplacementValue = reader.IsDBNull(6) ? 0 : Convert.ToDecimal(reader.GetDouble(6))
        };
    }

    public async Task<List<DuplicateOwnershipItem>> GetDuplicatesAsync()
    {
        List<DuplicateOwnershipItem> items = [];
        await using SqliteConnection connection = new($"Data Source={DatabaseService.DatabasePath}");
        await connection.OpenAsync();
        await using SqliteCommand command = connection.CreateCommand();
        command.CommandText =
            """
            SELECT m.Id, m.Title, m.ReleaseYear, COUNT(oc.Id),
                   GROUP_CONCAT(DISTINCT CASE WHEN TRIM(oc.Format) = '' THEN 'Unspecified' ELSE oc.Format END),
                   COALESCE(SUM(oc.PurchasePrice), 0)
            FROM Movies m
            JOIN OwnedCopies oc ON oc.MovieId = m.Id
            GROUP BY m.Id, m.Title, m.ReleaseYear
            HAVING COUNT(oc.Id) > 1
            ORDER BY COUNT(oc.Id) DESC, m.Title
            LIMIT 100;
            """;
        await using SqliteDataReader reader = await command.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            items.Add(new DuplicateOwnershipItem
            {
                MovieId = reader.GetInt32(0),
                Title = reader.IsDBNull(1) ? string.Empty : reader.GetString(1),
                ReleaseYear = reader.IsDBNull(2) ? 0 : reader.GetInt32(2),
                CopyCount = reader.IsDBNull(3) ? 0 : reader.GetInt32(3),
                Formats = reader.IsDBNull(4) ? string.Empty : reader.GetString(4),
                RecordedValue = reader.IsDBNull(5) ? 0 : Convert.ToDecimal(reader.GetDouble(5))
            });
        }
        return items;
    }

    public async Task<List<UpgradeOpportunityItem>> GetUpgradeOpportunitiesAsync()
    {
        List<UpgradeOpportunityItem> items = [];
        await using SqliteConnection connection = new($"Data Source={DatabaseService.DatabasePath}");
        await connection.OpenAsync();
        await using SqliteCommand command = connection.CreateCommand();
        command.CommandText =
            """
            SELECT m.Id, m.Title, m.ReleaseYear,
                   GROUP_CONCAT(DISTINCT CASE WHEN TRIM(oc.Format) = '' THEN 'Unspecified' ELSE oc.Format END) AS Formats,
                   CASE
                       WHEN MAX(CASE WHEN LOWER(oc.Format) IN ('blu-ray', 'bluray') THEN 1 ELSE 0 END) = 1
                        AND MAX(CASE WHEN LOWER(oc.Format) IN ('4k', '4k uhd', 'ultra hd blu-ray') THEN 1 ELSE 0 END) = 0
                       THEN 'Consider a 4K UHD upgrade'
                       WHEN MAX(CASE WHEN LOWER(oc.Format) IN ('dvd', 'vhs', 'laserdisc') THEN 1 ELSE 0 END) = 1
                        AND MAX(CASE WHEN LOWER(oc.Format) IN ('blu-ray', 'bluray', '4k', '4k uhd', 'ultra hd blu-ray') THEN 1 ELSE 0 END) = 0
                       THEN 'Consider a Blu-ray or 4K UHD upgrade'
                       ELSE ''
                   END AS Recommendation
            FROM Movies m
            JOIN OwnedCopies oc ON oc.MovieId = m.Id
            GROUP BY m.Id, m.Title, m.ReleaseYear
            HAVING Recommendation <> ''
            ORDER BY m.Title
            LIMIT 150;
            """;
        await using SqliteDataReader reader = await command.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            items.Add(new UpgradeOpportunityItem
            {
                MovieId = reader.GetInt32(0),
                Title = reader.IsDBNull(1) ? string.Empty : reader.GetString(1),
                ReleaseYear = reader.IsDBNull(2) ? 0 : reader.GetInt32(2),
                CurrentFormats = reader.IsDBNull(3) ? string.Empty : reader.GetString(3),
                Recommendation = reader.IsDBNull(4) ? string.Empty : reader.GetString(4)
            });
        }
        return items;
    }

    public async Task<List<MediaCollection>> GetIncompleteGoalsAsync()
    {
        List<MediaCollection> items = [];
        await using SqliteConnection connection = new($"Data Source={DatabaseService.DatabasePath}");
        await connection.OpenAsync();
        await using SqliteCommand command = connection.CreateCommand();
        command.CommandText =
            """
            SELECT Id, Name, Description, TargetCount, OwnedCount
            FROM Collections
            WHERE TargetCount > OwnedCount
            ORDER BY (CAST(OwnedCount AS REAL) / NULLIF(TargetCount, 0)) DESC, Name;
            """;
        await using SqliteDataReader reader = await command.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            items.Add(new MediaCollection
            {
                Id = reader.GetInt32(0),
                Name = reader.IsDBNull(1) ? string.Empty : reader.GetString(1),
                Description = reader.IsDBNull(2) ? string.Empty : reader.GetString(2),
                TargetCount = reader.IsDBNull(3) ? 0 : reader.GetInt32(3),
                OwnedCount = reader.IsDBNull(4) ? 0 : reader.GetInt32(4)
            });
        }
        return items;
    }

    public async Task<List<LoanRecord>> GetLoansAsync(bool includeReturned = false)
    {
        List<LoanRecord> items = [];
        await using SqliteConnection connection = new($"Data Source={DatabaseService.DatabasePath}");
        await connection.OpenAsync();
        await using SqliteCommand command = connection.CreateCommand();
        command.CommandText =
            """
            SELECT l.Id, l.OwnedCopyId, m.Id, m.Title, m.ReleaseYear,
                   oc.Format, oc.Edition, l.Borrower, l.LoanedDate, l.DueDate,
                   l.ReturnedDate, l.Notes
            FROM Loans l
            JOIN OwnedCopies oc ON oc.Id = l.OwnedCopyId
            JOIN Movies m ON m.Id = oc.MovieId
            WHERE ($includeReturned = 1 OR l.ReturnedDate = '')
            ORDER BY CASE WHEN l.ReturnedDate = '' THEN 0 ELSE 1 END,
                     CASE WHEN l.DueDate = '' THEN '9999-12-31' ELSE l.DueDate END,
                     m.Title;
            """;
        command.Parameters.AddWithValue("$includeReturned", includeReturned ? 1 : 0);
        await using SqliteDataReader reader = await command.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            items.Add(new LoanRecord
            {
                Id = reader.GetInt32(0),
                OwnedCopyId = reader.GetInt32(1),
                MovieId = reader.GetInt32(2),
                Title = reader.IsDBNull(3) ? string.Empty : reader.GetString(3),
                ReleaseYear = reader.IsDBNull(4) ? 0 : reader.GetInt32(4),
                Format = reader.IsDBNull(5) ? string.Empty : reader.GetString(5),
                Edition = reader.IsDBNull(6) ? string.Empty : reader.GetString(6),
                Borrower = reader.IsDBNull(7) ? string.Empty : reader.GetString(7),
                LoanedDate = reader.IsDBNull(8) ? string.Empty : reader.GetString(8),
                DueDate = reader.IsDBNull(9) ? string.Empty : reader.GetString(9),
                ReturnedDate = reader.IsDBNull(10) ? string.Empty : reader.GetString(10),
                Notes = reader.IsDBNull(11) ? string.Empty : reader.GetString(11)
            });
        }
        return items;
    }

    public async Task<List<OwnedCopyOption>> GetAvailableCopiesAsync()
    {
        List<OwnedCopyOption> items = [];
        await using SqliteConnection connection = new($"Data Source={DatabaseService.DatabasePath}");
        await connection.OpenAsync();
        await using SqliteCommand command = connection.CreateCommand();
        command.CommandText =
            """
            SELECT oc.Id, m.Id, m.Title, m.ReleaseYear, oc.Format, oc.Edition
            FROM OwnedCopies oc
            JOIN Movies m ON m.Id = oc.MovieId
            WHERE NOT EXISTS (
                SELECT 1 FROM Loans l
                WHERE l.OwnedCopyId = oc.Id AND l.ReturnedDate = '')
            ORDER BY m.Title, m.ReleaseYear, oc.Format, oc.Edition;
            """;
        await using SqliteDataReader reader = await command.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            items.Add(new OwnedCopyOption
            {
                OwnedCopyId = reader.GetInt32(0),
                MovieId = reader.GetInt32(1),
                Title = reader.IsDBNull(2) ? string.Empty : reader.GetString(2),
                ReleaseYear = reader.IsDBNull(3) ? 0 : reader.GetInt32(3),
                Format = reader.IsDBNull(4) ? string.Empty : reader.GetString(4),
                Edition = reader.IsDBNull(5) ? string.Empty : reader.GetString(5)
            });
        }
        return items;
    }

    public async Task AddLoanAsync(int ownedCopyId, string borrower, DateTime loanedDate, DateTime? dueDate, string notes)
    {
        await using SqliteConnection connection = new($"Data Source={DatabaseService.DatabasePath}");
        await connection.OpenAsync();
        await using SqliteCommand command = connection.CreateCommand();
        command.CommandText =
            """
            INSERT INTO Loans (OwnedCopyId, Borrower, LoanedDate, DueDate, ReturnedDate, Notes)
            VALUES ($ownedCopyId, $borrower, $loanedDate, $dueDate, '', $notes);
            """;
        command.Parameters.AddWithValue("$ownedCopyId", ownedCopyId);
        command.Parameters.AddWithValue("$borrower", borrower.Trim());
        command.Parameters.AddWithValue("$loanedDate", loanedDate.ToString("yyyy-MM-dd"));
        command.Parameters.AddWithValue("$dueDate", dueDate?.ToString("yyyy-MM-dd") ?? string.Empty);
        command.Parameters.AddWithValue("$notes", notes.Trim());
        await command.ExecuteNonQueryAsync();
    }

    public async Task MarkReturnedAsync(int loanId, DateTime returnedDate)
    {
        await using SqliteConnection connection = new($"Data Source={DatabaseService.DatabasePath}");
        await connection.OpenAsync();
        await using SqliteCommand command = connection.CreateCommand();
        command.CommandText = "UPDATE Loans SET ReturnedDate = $date WHERE Id = $id;";
        command.Parameters.AddWithValue("$date", returnedDate.ToString("yyyy-MM-dd"));
        command.Parameters.AddWithValue("$id", loanId);
        await command.ExecuteNonQueryAsync();
    }
}
