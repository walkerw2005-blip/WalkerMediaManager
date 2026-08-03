using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.Data.Sqlite;
using WalkerMediaManager.UI.Data;
using WalkerMediaManager.UI.Models;

namespace WalkerMediaManager.UI.Repositories;

public sealed class TVSeasonRepository
{
    public async Task<List<TVSeason>> GetForShowAsync(int tvShowId)
    {
        List<TVSeason> seasons = [];
        await using SqliteConnection connection = new($"Data Source={DatabaseService.DatabasePath}");
        await connection.OpenAsync();
        await using SqliteCommand command = connection.CreateCommand();
        command.CommandText =
            """
            SELECT Id, TVShowId, SeasonNumber, Name, EpisodeCount, IsOwned,
                   Format, HasDigitalCopy, PurchasePrice, PurchaseDate,
                   StorageLocation, Notes
            FROM TVSeasons
            WHERE TVShowId = $tvShowId
            ORDER BY SeasonNumber;
            """;
        command.Parameters.AddWithValue("$tvShowId", tvShowId);

        await using SqliteDataReader reader = await command.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            seasons.Add(new TVSeason
            {
                Id = reader.GetInt32(0),
                TVShowId = reader.GetInt32(1),
                SeasonNumber = reader.GetInt32(2),
                Name = GetString(reader, 3),
                EpisodeCount = GetInt32(reader, 4),
                IsOwned = GetBool(reader, 5),
                Format = GetString(reader, 6, "DVD"),
                HasDigitalCopy = GetBool(reader, 7),
                PurchasePrice = reader.IsDBNull(8) ? null : Convert.ToDecimal(reader.GetDouble(8)),
                PurchaseDate = GetString(reader, 9),
                StorageLocation = GetString(reader, 10),
                Notes = GetString(reader, 11)
            });
        }

        return seasons;
    }

    public async Task UpdateAsync(TVSeason season)
    {
        await using SqliteConnection connection = new($"Data Source={DatabaseService.DatabasePath}");
        await connection.OpenAsync();
        await using SqliteCommand command = connection.CreateCommand();
        command.CommandText =
            """
            UPDATE TVSeasons
            SET Name = $name,
                EpisodeCount = $episodeCount,
                IsOwned = $isOwned,
                Format = $format,
                HasDigitalCopy = $hasDigitalCopy,
                PurchasePrice = $purchasePrice,
                PurchaseDate = $purchaseDate,
                StorageLocation = $storageLocation,
                Notes = $notes,
                UpdatedAt = datetime('now')
            WHERE Id = $id;
            """;
        command.Parameters.AddWithValue("$id", season.Id);
        command.Parameters.AddWithValue("$name", season.Name);
        command.Parameters.AddWithValue("$episodeCount", season.EpisodeCount);
        command.Parameters.AddWithValue("$isOwned", season.IsOwned ? 1 : 0);
        command.Parameters.AddWithValue("$format", string.IsNullOrWhiteSpace(season.Format) ? "DVD" : season.Format);
        command.Parameters.AddWithValue("$hasDigitalCopy", season.HasDigitalCopy ? 1 : 0);
        command.Parameters.AddWithValue("$purchasePrice", season.PurchasePrice.HasValue ? Convert.ToDouble(season.PurchasePrice.Value) : DBNull.Value);
        command.Parameters.AddWithValue("$purchaseDate", season.PurchaseDate);
        command.Parameters.AddWithValue("$storageLocation", season.StorageLocation);
        command.Parameters.AddWithValue("$notes", season.Notes);
        await command.ExecuteNonQueryAsync();
    }

    public async Task EnsureRowsAsync(int tvShowId, int seasonCount, int ownedSeasonCount)
    {
        if (seasonCount <= 0) return;
        await using SqliteConnection connection = new($"Data Source={DatabaseService.DatabasePath}");
        await connection.OpenAsync();
        await using SqliteTransaction transaction = connection.BeginTransaction();
        for (int seasonNumber = 1; seasonNumber <= seasonCount; seasonNumber++)
        {
            await using SqliteCommand command = connection.CreateCommand();
            command.Transaction = transaction;
            command.CommandText =
                """
                INSERT OR IGNORE INTO TVSeasons
                (TVShowId, SeasonNumber, Name, IsOwned, Format, HasDigitalCopy)
                VALUES ($tvShowId, $seasonNumber, $name, $isOwned, 'DVD', $hasDigitalCopy);
                """;
            command.Parameters.AddWithValue("$tvShowId", tvShowId);
            command.Parameters.AddWithValue("$seasonNumber", seasonNumber);
            command.Parameters.AddWithValue("$name", $"Season {seasonNumber}");
            bool isOwned = seasonNumber <= ownedSeasonCount;
            command.Parameters.AddWithValue("$isOwned", isOwned ? 1 : 0);
            command.Parameters.AddWithValue("$hasDigitalCopy", isOwned ? 1 : 0);
            await command.ExecuteNonQueryAsync();
        }
        await transaction.CommitAsync();
    }

    private static string GetString(SqliteDataReader reader, int ordinal, string fallback = "") =>
        reader.IsDBNull(ordinal) ? fallback : reader.GetString(ordinal);
    private static int GetInt32(SqliteDataReader reader, int ordinal) => reader.IsDBNull(ordinal) ? 0 : reader.GetInt32(ordinal);
    private static bool GetBool(SqliteDataReader reader, int ordinal) => !reader.IsDBNull(ordinal) && reader.GetInt32(ordinal) != 0;
}
