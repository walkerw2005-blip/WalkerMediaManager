using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.Data.Sqlite;
using WalkerMediaManager.UI.Data;
using WalkerMediaManager.UI.Models;

namespace WalkerMediaManager.UI.Repositories;

public sealed class OwnedCopyRepository
{
    public async Task<List<OwnedCopy>> GetForMovieAsync(int movieId)
    {
        List<OwnedCopy> copies = [];

        await using SqliteConnection connection = new($"Data Source={DatabaseService.DatabasePath}");
        await connection.OpenAsync();

        await using SqliteCommand command = connection.CreateCommand();
        command.CommandText =
            """
            SELECT Id, MovieId, Format, Edition, Packaging, Condition, Store,
                   PurchasePrice, PurchaseDate, Location, Notes, IsDigital, IsFavorite
            FROM OwnedCopies
            WHERE MovieId = $movieId
            ORDER BY IsFavorite DESC, Format COLLATE NOCASE, Edition COLLATE NOCASE, Id;
            """;
        command.Parameters.AddWithValue("$movieId", movieId);

        await using SqliteDataReader reader = await command.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            copies.Add(ReadCopy(reader));
        }

        return copies;
    }

    public async Task<int> AddAsync(OwnedCopy copy)
    {
        await using SqliteConnection connection = new($"Data Source={DatabaseService.DatabasePath}");
        await connection.OpenAsync();

        await using SqliteCommand command = connection.CreateCommand();
        command.CommandText =
            """
            INSERT INTO OwnedCopies
            (MovieId, Format, Edition, Packaging, Condition, Store, PurchasePrice,
             PurchaseDate, Location, Notes, IsDigital, IsFavorite)
            VALUES
            ($movieId, $format, $edition, $packaging, $condition, $store, $price,
             $date, $location, $notes, $isDigital, $isFavorite);
            SELECT last_insert_rowid();
            """;
        AddParameters(command, copy);
        return Convert.ToInt32(await command.ExecuteScalarAsync());
    }

    public async Task UpdateAsync(OwnedCopy copy)
    {
        await using SqliteConnection connection = new($"Data Source={DatabaseService.DatabasePath}");
        await connection.OpenAsync();

        await using SqliteCommand command = connection.CreateCommand();
        command.CommandText =
            """
            UPDATE OwnedCopies SET
                MovieId = $movieId,
                Format = $format,
                Edition = $edition,
                Packaging = $packaging,
                Condition = $condition,
                Store = $store,
                PurchasePrice = $price,
                PurchaseDate = $date,
                Location = $location,
                Notes = $notes,
                IsDigital = $isDigital,
                IsFavorite = $isFavorite
            WHERE Id = $id;
            """;
        AddParameters(command, copy);
        command.Parameters.AddWithValue("$id", copy.Id);
        await command.ExecuteNonQueryAsync();
    }

    public async Task DeleteAsync(int copyId)
    {
        await using SqliteConnection connection = new($"Data Source={DatabaseService.DatabasePath}");
        await connection.OpenAsync();

        await using SqliteCommand command = connection.CreateCommand();
        command.CommandText = "DELETE FROM OwnedCopies WHERE Id = $id;";
        command.Parameters.AddWithValue("$id", copyId);
        await command.ExecuteNonQueryAsync();
    }

    private static OwnedCopy ReadCopy(SqliteDataReader reader)
    {
        return new OwnedCopy
        {
            Id = reader.GetInt32(0),
            MovieId = reader.GetInt32(1),
            Format = GetString(reader, 2),
            Edition = GetString(reader, 3),
            Packaging = GetString(reader, 4),
            Condition = GetString(reader, 5),
            Store = GetString(reader, 6),
            PurchasePrice = reader.IsDBNull(7) ? null : Convert.ToDecimal(reader.GetDouble(7)),
            PurchaseDate = GetString(reader, 8),
            Location = GetString(reader, 9),
            Notes = GetString(reader, 10),
            IsDigital = !reader.IsDBNull(11) && reader.GetInt32(11) != 0,
            IsFavorite = !reader.IsDBNull(12) && reader.GetInt32(12) != 0
        };
    }

    private static void AddParameters(SqliteCommand command, OwnedCopy copy)
    {
        command.Parameters.AddWithValue("$movieId", copy.MovieId);
        command.Parameters.AddWithValue("$format", copy.Format.Trim());
        command.Parameters.AddWithValue("$edition", copy.Edition.Trim());
        command.Parameters.AddWithValue("$packaging", copy.Packaging.Trim());
        command.Parameters.AddWithValue("$condition", copy.Condition.Trim());
        command.Parameters.AddWithValue("$store", copy.Store.Trim());
        command.Parameters.AddWithValue("$price", copy.PurchasePrice.HasValue ? copy.PurchasePrice.Value : DBNull.Value);
        command.Parameters.AddWithValue("$date", copy.PurchaseDate.Trim());
        command.Parameters.AddWithValue("$location", copy.Location.Trim());
        command.Parameters.AddWithValue("$notes", copy.Notes.Trim());
        command.Parameters.AddWithValue("$isDigital", copy.IsDigital ? 1 : 0);
        command.Parameters.AddWithValue("$isFavorite", copy.IsFavorite ? 1 : 0);
    }

    private static string GetString(SqliteDataReader reader, int ordinal) =>
        reader.IsDBNull(ordinal) ? string.Empty : reader.GetString(ordinal);
}
