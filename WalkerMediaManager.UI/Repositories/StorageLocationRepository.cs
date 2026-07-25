using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.Data.Sqlite;
using WalkerMediaManager.UI.Data;
using WalkerMediaManager.UI.Models;

namespace WalkerMediaManager.UI.Repositories;

public sealed class StorageLocationRepository
{
    public async Task<List<StorageLocation>> GetAllAsync(bool includeInactive = true)
    {
        List<StorageLocation> items = [];

        await using SqliteConnection connection = new($"Data Source={DatabaseService.DatabasePath}");
        await connection.OpenAsync();

        await using SqliteCommand command = connection.CreateCommand();
        command.CommandText =
            """
            SELECT
                l.Id,
                l.Name,
                l.Room,
                l.Area,
                l.Shelf,
                l.Bin,
                l.Notes,
                l.IsActive,
                COUNT(c.Id) AS CopyCount
            FROM StorageLocations l
            LEFT JOIN OwnedCopies c
                ON TRIM(c.Location) = TRIM(l.Name)
               AND TRIM(c.Location) <> ''
            WHERE $includeInactive = 1 OR l.IsActive = 1
            GROUP BY l.Id, l.Name, l.Room, l.Area, l.Shelf, l.Bin, l.Notes, l.IsActive
            ORDER BY l.IsActive DESC, l.Name COLLATE NOCASE;
            """;
        command.Parameters.AddWithValue("$includeInactive", includeInactive ? 1 : 0);

        await using SqliteDataReader reader = await command.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            items.Add(new StorageLocation
            {
                Id = reader.GetInt32(0),
                Name = GetString(reader, 1),
                Room = GetString(reader, 2),
                Area = GetString(reader, 3),
                Shelf = GetString(reader, 4),
                Bin = GetString(reader, 5),
                Notes = GetString(reader, 6),
                IsActive = !reader.IsDBNull(7) && reader.GetInt32(7) != 0,
                CopyCount = reader.IsDBNull(8) ? 0 : Convert.ToInt32(reader.GetValue(8))
            });
        }

        return items;
    }

    public async Task<int> AddAsync(StorageLocation location)
    {
        await using SqliteConnection connection = new($"Data Source={DatabaseService.DatabasePath}");
        await connection.OpenAsync();

        await using SqliteCommand command = connection.CreateCommand();
        command.CommandText =
            """
            INSERT INTO StorageLocations
                (Name, Room, Area, Shelf, Bin, Notes, IsActive)
            VALUES
                ($name, $room, $area, $shelf, $bin, $notes, $isActive);
            SELECT last_insert_rowid();
            """;
        AddParameters(command, location);
        return Convert.ToInt32(await command.ExecuteScalarAsync());
    }

    public async Task UpdateAsync(StorageLocation location)
    {
        await using SqliteConnection connection = new($"Data Source={DatabaseService.DatabasePath}");
        await connection.OpenAsync();

        string oldName = string.Empty;
        await using (SqliteCommand readCommand = connection.CreateCommand())
        {
            readCommand.CommandText = "SELECT Name FROM StorageLocations WHERE Id = $id LIMIT 1;";
            readCommand.Parameters.AddWithValue("$id", location.Id);
            oldName = (await readCommand.ExecuteScalarAsync())?.ToString() ?? string.Empty;
        }

        using SqliteTransaction transaction = connection.BeginTransaction();

        await using (SqliteCommand command = connection.CreateCommand())
        {
            command.Transaction = transaction;
            command.CommandText =
                """
                UPDATE StorageLocations SET
                    Name = $name,
                    Room = $room,
                    Area = $area,
                    Shelf = $shelf,
                    Bin = $bin,
                    Notes = $notes,
                    IsActive = $isActive
                WHERE Id = $id;
                """;
            AddParameters(command, location);
            command.Parameters.AddWithValue("$id", location.Id);
            await command.ExecuteNonQueryAsync();
        }

        if (!string.IsNullOrWhiteSpace(oldName) &&
            !string.Equals(oldName.Trim(), location.Name.Trim(), StringComparison.Ordinal))
        {
            await using SqliteCommand renameCommand = connection.CreateCommand();
            renameCommand.Transaction = transaction;
            renameCommand.CommandText =
                """
                UPDATE OwnedCopies
                SET Location = $newName
                WHERE TRIM(Location) = TRIM($oldName);
                """;
            renameCommand.Parameters.AddWithValue("$newName", location.Name.Trim());
            renameCommand.Parameters.AddWithValue("$oldName", oldName.Trim());
            await renameCommand.ExecuteNonQueryAsync();
        }

        await transaction.CommitAsync();
    }

    public async Task DeleteAsync(int locationId)
    {
        await using SqliteConnection connection = new($"Data Source={DatabaseService.DatabasePath}");
        await connection.OpenAsync();

        await using SqliteCommand command = connection.CreateCommand();
        command.CommandText = "DELETE FROM StorageLocations WHERE Id = $id;";
        command.Parameters.AddWithValue("$id", locationId);
        await command.ExecuteNonQueryAsync();
    }

    public async Task<int> ImportExistingLocationsAsync()
    {
        await using SqliteConnection connection = new($"Data Source={DatabaseService.DatabasePath}");
        await connection.OpenAsync();

        await using SqliteCommand command = connection.CreateCommand();
        command.CommandText =
            """
            INSERT INTO StorageLocations (Name, IsActive)
            SELECT DISTINCT TRIM(c.Location), 1
            FROM OwnedCopies c
            WHERE c.Location IS NOT NULL
              AND TRIM(c.Location) <> ''
              AND NOT EXISTS
              (
                  SELECT 1
                  FROM StorageLocations l
                  WHERE TRIM(l.Name) = TRIM(c.Location)
              );
            SELECT changes();
            """;

        return Convert.ToInt32(await command.ExecuteScalarAsync());
    }

    private static void AddParameters(SqliteCommand command, StorageLocation location)
    {
        command.Parameters.AddWithValue("$name", location.Name.Trim());
        command.Parameters.AddWithValue("$room", location.Room.Trim());
        command.Parameters.AddWithValue("$area", location.Area.Trim());
        command.Parameters.AddWithValue("$shelf", location.Shelf.Trim());
        command.Parameters.AddWithValue("$bin", location.Bin.Trim());
        command.Parameters.AddWithValue("$notes", location.Notes.Trim());
        command.Parameters.AddWithValue("$isActive", location.IsActive ? 1 : 0);
    }

    private static string GetString(SqliteDataReader reader, int ordinal) =>
        reader.IsDBNull(ordinal) ? string.Empty : reader.GetString(ordinal);
}
