using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Data.Sqlite;
using WalkerMediaManager.UI.Data;
using WalkerMediaManager.UI.Models;

namespace WalkerMediaManager.UI.Repositories;

public sealed class SmartBuyRepository
{
    private static readonly Dictionary<string, int> FormatRanks =
        new(StringComparer.OrdinalIgnoreCase)
        {
            ["VHS"] = 10,
            ["LaserDisc"] = 20,
            ["DVD"] = 30,
            ["Digital"] = 35,
            ["Blu-ray"] = 40,
            ["3D Blu-ray"] = 45,
            ["4K UHD"] = 50,
            ["4K UHD Blu-ray"] = 50
        };

    public async Task<List<SmartBuyResult>> SearchAsync(
        string searchText,
        string plannedFormat,
        decimal? plannedPrice)
    {
        List<SmartBuyResult> results = [];
        string normalizedSearch = $"%{searchText.Trim()}%";

        await using SqliteConnection connection =
            new($"Data Source={DatabaseService.DatabasePath}");
        await connection.OpenAsync();

        await SearchMoviesAsync(
            connection,
            normalizedSearch,
            plannedFormat,
            plannedPrice,
            results);

        await SearchTvShowsAsync(
            connection,
            normalizedSearch,
            plannedPrice,
            results);

        return results
            .OrderBy(result => result.Title, StringComparer.OrdinalIgnoreCase)
            .ThenBy(result => result.Year)
            .ToList();
    }

    public async Task<bool> ExactMovieExistsAsync(string title, int releaseYear)
    {
        await using SqliteConnection connection =
            new($"Data Source={DatabaseService.DatabasePath}");
        await connection.OpenAsync();

        await using SqliteCommand command = connection.CreateCommand();
        command.CommandText =
            """
            SELECT COUNT(*)
            FROM Movies
            WHERE LOWER(Title) = LOWER($title)
              AND ($releaseYear = 0 OR ReleaseYear = $releaseYear);
            """;
        command.Parameters.AddWithValue("$title", title.Trim());
        command.Parameters.AddWithValue("$releaseYear", releaseYear);

        return Convert.ToInt32(await command.ExecuteScalarAsync()) > 0;
    }

    public async Task<bool> ExactTvShowExistsAsync(string title)
    {
        await using SqliteConnection connection =
            new($"Data Source={DatabaseService.DatabasePath}");
        await connection.OpenAsync();

        await using SqliteCommand command = connection.CreateCommand();
        command.CommandText =
            """
            SELECT COUNT(*)
            FROM TVShows
            WHERE LOWER(Title) = LOWER($title);
            """;
        command.Parameters.AddWithValue("$title", title.Trim());

        return Convert.ToInt32(await command.ExecuteScalarAsync()) > 0;
    }

    private static async Task SearchMoviesAsync(
        SqliteConnection connection,
        string searchText,
        string plannedFormat,
        decimal? plannedPrice,
        List<SmartBuyResult> results)
    {
        await using SqliteCommand command = connection.CreateCommand();
        command.CommandText =
            """
            SELECT
                m.Id,
                m.Title,
                m.ReleaseYear,
                m.Rating,
                m.Genre,
                m.PosterPath,
                m.PlexRatingKey,
                COUNT(c.Id) AS CopyCount,
                GROUP_CONCAT(DISTINCT NULLIF(TRIM(c.Format), '')) AS Formats,
                GROUP_CONCAT(DISTINCT NULLIF(TRIM(c.Location), '')) AS Locations
            FROM Movies m
            LEFT JOIN OwnedCopies c ON c.MovieId = m.Id
            WHERE m.Title LIKE $searchText COLLATE NOCASE
            GROUP BY
                m.Id,
                m.Title,
                m.ReleaseYear,
                m.Rating,
                m.Genre,
                m.PosterPath,
                m.PlexRatingKey
            ORDER BY m.Title COLLATE NOCASE, m.ReleaseYear;
            """;
        command.Parameters.AddWithValue("$searchText", searchText);

        await using SqliteDataReader reader = await command.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            int id = reader.GetInt32(0);
            int releaseYear = reader.IsDBNull(2) ? 0 : reader.GetInt32(2);
            string rating = GetString(reader, 3);
            string genre = GetString(reader, 4);
            string posterPath = GetString(reader, 5);
            string ratingKey = GetString(reader, 6);
            int copyCount = reader.IsDBNull(7) ? 0 : reader.GetInt32(7);
            string formats = NormalizeList(GetString(reader, 8));
            string locations = NormalizeList(GetString(reader, 9));

            SmartBuyResult result = new()
            {
                Id = id,
                MediaType = "Movie",
                Title = GetString(reader, 1),
                Year = releaseYear,
                Details = BuildMovieDetails(rating, genre),
                PosterPath = posterPath,
                CacheKey = string.IsNullOrWhiteSpace(ratingKey)
                    ? $"movie-{id}"
                    : $"movie-{ratingKey}",
                OwnedCopyCount = copyCount,
                OwnedFormats = formats,
                OwnedLocations = locations,
                PlannedFormat = plannedFormat,
                PlannedPrice = plannedPrice
            };

            ApplyMovieRecommendation(result);
            results.Add(result);
        }
    }

    private static async Task SearchTvShowsAsync(
        SqliteConnection connection,
        string searchText,
        decimal? plannedPrice,
        List<SmartBuyResult> results)
    {
        await using SqliteCommand command = connection.CreateCommand();
        command.CommandText =
            """
            SELECT
                Id,
                Title,
                Year,
                Seasons,
                Episodes,
                Studio,
                PosterPath,
                PlexRatingKey
            FROM TVShows
            WHERE Title LIKE $searchText COLLATE NOCASE
            ORDER BY Title COLLATE NOCASE, Year;
            """;
        command.Parameters.AddWithValue("$searchText", searchText);

        await using SqliteDataReader reader = await command.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            int id = reader.GetInt32(0);
            int seasons = reader.IsDBNull(3) ? 0 : reader.GetInt32(3);
            int episodes = reader.IsDBNull(4) ? 0 : reader.GetInt32(4);
            string studio = GetString(reader, 5);
            string ratingKey = GetString(reader, 7);

            results.Add(new SmartBuyResult
            {
                Id = id,
                MediaType = "TV Show",
                Title = GetString(reader, 1),
                Year = reader.IsDBNull(2) ? 0 : reader.GetInt32(2),
                Details = BuildTvDetails(seasons, episodes, studio),
                PosterPath = GetString(reader, 6),
                CacheKey = string.IsNullOrWhiteSpace(ratingKey)
                    ? $"tv-{id}"
                    : $"tv-{ratingKey}",
                OwnedCopyCount = 1,
                PlannedPrice = plannedPrice,
                Recommendation = "Already in collection",
                RecommendationDetail =
                    "This television series is already in your library. Check the season or box-set details before buying.",
                RecommendationGlyph = "\uE73E",
                RecommendationColor = "#C42B1C"
            });
        }
    }

    private static void ApplyMovieRecommendation(SmartBuyResult result)
    {
        string plannedFormat = result.PlannedFormat.Trim();
        List<string> ownedFormats = SplitValues(result.OwnedFormats);

        if (result.OwnedCopyCount == 0)
        {
            result.Recommendation = "Ownership details missing";
            result.RecommendationDetail =
                "The title is in your movie library, but no physical or digital copy has been recorded. Verify the copy before buying another one.";
            result.RecommendationGlyph = "\uE7BA";
            result.RecommendationColor = "#9D5D00";
            return;
        }

        if (string.IsNullOrWhiteSpace(plannedFormat))
        {
            result.Recommendation = "Already owned";
            result.RecommendationDetail =
                $"You already own {result.OwnedCopyCount} " +
                (result.OwnedCopyCount == 1 ? "copy" : "copies") +
                ". Select a planned format to check whether the purchase would be an upgrade.";
            result.RecommendationGlyph = "\uE73E";
            result.RecommendationColor = "#C42B1C";
            return;
        }

        if (ownedFormats.Any(format =>
                string.Equals(format, plannedFormat, StringComparison.OrdinalIgnoreCase)))
        {
            result.Recommendation = "Duplicate format";
            result.RecommendationDetail =
                $"You already own this title on {plannedFormat}. Do not buy unless this is a special edition or replacement copy.";
            result.RecommendationGlyph = "\uEA39";
            result.RecommendationColor = "#C42B1C";
            return;
        }

        int plannedRank = GetFormatRank(plannedFormat);
        int bestOwnedRank = ownedFormats.Count == 0
            ? 0
            : ownedFormats.Max(GetFormatRank);

        if (plannedRank > bestOwnedRank && plannedRank > 0)
        {
            string bestOwned = ownedFormats
                .OrderByDescending(GetFormatRank)
                .FirstOrDefault() ?? "an older format";

            result.Recommendation = "Upgrade available";
            result.RecommendationDetail =
                $"You own {bestOwned}; {plannedFormat} appears to be a format upgrade. Confirm the transfer and edition quality before buying.";
            result.RecommendationGlyph = "\uE74A";
            result.RecommendationColor = "#107C10";
            return;
        }

        result.Recommendation = "Possible duplicate";
        result.RecommendationDetail =
            $"You already own this title on {result.FormatSummary}. The planned {plannedFormat} copy is not a clear format upgrade.";
        result.RecommendationGlyph = "\uE7BA";
        result.RecommendationColor = "#9D5D00";
    }

    private static int GetFormatRank(string format) =>
        FormatRanks.TryGetValue(format.Trim(), out int rank) ? rank : 0;

    private static List<string> SplitValues(string value) =>
        string.IsNullOrWhiteSpace(value)
            ? []
            : value.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();

    private static string NormalizeList(string value) =>
        string.Join(", ", SplitValues(value));

    private static string BuildMovieDetails(string rating, string genre)
    {
        if (!string.IsNullOrWhiteSpace(rating) && !string.IsNullOrWhiteSpace(genre))
            return $"{rating} • {genre}";
        if (!string.IsNullOrWhiteSpace(rating))
            return rating;
        if (!string.IsNullOrWhiteSpace(genre))
            return genre;
        return "Movie";
    }

    private static string BuildTvDetails(int seasons, int episodes, string studio)
    {
        string counts = $"{seasons} seasons • {episodes} episodes";
        return string.IsNullOrWhiteSpace(studio) ? counts : $"{counts} • {studio}";
    }

    private static string GetString(SqliteDataReader reader, int ordinal) =>
        reader.IsDBNull(ordinal) ? string.Empty : reader.GetString(ordinal);
}
