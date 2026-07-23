using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.Data.Sqlite;
using WalkerMediaManager.UI.Data;
using WalkerMediaManager.UI.Models;

namespace WalkerMediaManager.UI.Repositories;

public sealed class SmartBuyRepository
{
    public async Task<List<SmartBuyResult>> SearchAsync(
        string searchText)
    {
        List<SmartBuyResult> results = [];

        string normalizedSearch =
            $"%{searchText.Trim()}%";

        await using SqliteConnection connection =
            new($"Data Source={DatabaseService.DatabasePath}");

        await connection.OpenAsync();

        await SearchMoviesAsync(
            connection,
            normalizedSearch,
            results);

        await SearchTvShowsAsync(
            connection,
            normalizedSearch,
            results);

        results.Sort(
            static (left, right) =>
                string.Compare(
                    left.Title,
                    right.Title,
                    System.StringComparison.OrdinalIgnoreCase));

        return results;
    }

    public async Task<bool> ExactMovieExistsAsync(
        string title,
        int releaseYear)
    {
        await using SqliteConnection connection =
            new($"Data Source={DatabaseService.DatabasePath}");

        await connection.OpenAsync();

        await using SqliteCommand command =
            connection.CreateCommand();

        command.CommandText =
            """
            SELECT COUNT(*)
            FROM Movies
            WHERE LOWER(Title) = LOWER($title)
              AND
              (
                  $releaseYear = 0
                  OR ReleaseYear = $releaseYear
              );
            """;

        command.Parameters.AddWithValue(
            "$title",
            title.Trim());

        command.Parameters.AddWithValue(
            "$releaseYear",
            releaseYear);

        object? result =
            await command.ExecuteScalarAsync();

        return System.Convert.ToInt32(result) > 0;
    }

    public async Task<bool> ExactTvShowExistsAsync(
        string title)
    {
        await using SqliteConnection connection =
            new($"Data Source={DatabaseService.DatabasePath}");

        await connection.OpenAsync();

        await using SqliteCommand command =
            connection.CreateCommand();

        command.CommandText =
            """
            SELECT COUNT(*)
            FROM TVShows
            WHERE LOWER(Title) = LOWER($title);
            """;

        command.Parameters.AddWithValue(
            "$title",
            title.Trim());

        object? result =
            await command.ExecuteScalarAsync();

        return System.Convert.ToInt32(result) > 0;
    }

    private static async Task SearchMoviesAsync(
        SqliteConnection connection,
        string searchText,
        List<SmartBuyResult> results)
    {
        await using SqliteCommand command =
            connection.CreateCommand();

        command.CommandText =
            """
            SELECT
                Id,
                Title,
                ReleaseYear,
                Rating,
                Genre
            FROM Movies
            WHERE Title LIKE $searchText
                  COLLATE NOCASE
            ORDER BY Title;
            """;

        command.Parameters.AddWithValue(
            "$searchText",
            searchText);

        await using SqliteDataReader reader =
            await command.ExecuteReaderAsync();

        while (await reader.ReadAsync())
        {
            int releaseYear =
                reader.IsDBNull(2)
                    ? 0
                    : reader.GetInt32(2);

            string rating =
                reader.IsDBNull(3)
                    ? string.Empty
                    : reader.GetString(3);

            string genre =
                reader.IsDBNull(4)
                    ? string.Empty
                    : reader.GetString(4);

            string details = BuildMovieDetails(
                rating,
                genre);

            results.Add(
                new SmartBuyResult
                {
                    Id = reader.GetInt32(0),
                    MediaType = "Movie",
                    Title = reader.IsDBNull(1)
                        ? string.Empty
                        : reader.GetString(1),
                    Year = releaseYear,
                    Details = details,
                    IsOwned = true
                });
        }
    }

    private static async Task SearchTvShowsAsync(
        SqliteConnection connection,
        string searchText,
        List<SmartBuyResult> results)
    {
        await using SqliteCommand command =
            connection.CreateCommand();

        command.CommandText =
            """
            SELECT
                Id,
                Title,
                Seasons,
                Episodes
            FROM TVShows
            WHERE Title LIKE $searchText
                  COLLATE NOCASE
            ORDER BY Title;
            """;

        command.Parameters.AddWithValue(
            "$searchText",
            searchText);

        await using SqliteDataReader reader =
            await command.ExecuteReaderAsync();

        while (await reader.ReadAsync())
        {
            int seasons =
                reader.IsDBNull(2)
                    ? 0
                    : reader.GetInt32(2);

            int episodes =
                reader.IsDBNull(3)
                    ? 0
                    : reader.GetInt32(3);

            results.Add(
                new SmartBuyResult
                {
                    Id = reader.GetInt32(0),
                    MediaType = "TV Show",
                    Title = reader.IsDBNull(1)
                        ? string.Empty
                        : reader.GetString(1),
                    Year = 0,
                    Details =
                        $"{seasons} seasons owned • " +
                        $"{episodes} episodes owned",
                    IsOwned = true
                });
        }
    }

    private static string BuildMovieDetails(
        string rating,
        string genre)
    {
        if (!string.IsNullOrWhiteSpace(rating) &&
            !string.IsNullOrWhiteSpace(genre))
        {
            return $"{rating} • {genre}";
        }

        if (!string.IsNullOrWhiteSpace(rating))
        {
            return rating;
        }

        if (!string.IsNullOrWhiteSpace(genre))
        {
            return genre;
        }

        return "Movie";
    }
}