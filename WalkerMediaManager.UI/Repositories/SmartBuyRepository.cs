using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Data.Sqlite;
using WalkerMediaManager.UI.Data;
using WalkerMediaManager.UI.Models;
using WalkerMediaManager.UI.Services;

namespace WalkerMediaManager.UI.Repositories;

public sealed class SmartBuyRepository
{
    private static readonly UpgradeAdvisorService UpgradeAdvisor = new();

    public Task<List<SmartBuyResult>> SearchAsync(
        string searchText,
        string plannedFormat,
        decimal? plannedPrice)
    {
        return SearchAsync(searchText, plannedFormat, string.Empty, plannedPrice);
    }

    public async Task<List<SmartBuyResult>> SearchAsync(
        string searchText,
        string plannedFormat,
        string plannedEdition,
        decimal? plannedPrice)
    {
        string normalizedSearch = NormalizeSearchText(searchText);
        if (string.IsNullOrWhiteSpace(normalizedSearch)) return [];

        List<SmartBuyResult> results = [];

        await using SqliteConnection connection =
            new($"Data Source={DatabaseService.DatabasePath}");
        await connection.OpenAsync();

        HashSet<string> wishlistTitles = await LoadWishlistTitlesAsync(connection);

        await SearchMoviesAsync(
            connection,
            normalizedSearch,
            plannedFormat,
            plannedEdition,
            plannedPrice,
            wishlistTitles,
            results);

        await SearchTvShowsAsync(
            connection,
            normalizedSearch,
            plannedPrice,
            wishlistTitles,
            results);

        await SearchWishlistAsync(
            connection,
            normalizedSearch,
            plannedFormat,
            plannedEdition,
            plannedPrice,
            results);

        return results
            .GroupBy(result => $"{result.MediaType}|{result.Id}|{NormalizeSearchText(result.Title)}")
            .Select(group => group.OrderByDescending(item => item.MatchScore).First())
            .OrderByDescending(result => result.MatchScore)
            .ThenByDescending(result => result.IsOwned)
            .ThenBy(result => result.Title, StringComparer.OrdinalIgnoreCase)
            .ThenBy(result => result.Year)
            .Take(25)
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

    private static async Task<HashSet<string>> LoadWishlistTitlesAsync(SqliteConnection connection)
    {
        HashSet<string> titles = new(StringComparer.OrdinalIgnoreCase);
        await using SqliteCommand command = connection.CreateCommand();
        command.CommandText = "SELECT Title FROM Wishlist WHERE TRIM(Title) <> '';";
        await using SqliteDataReader reader = await command.ExecuteReaderAsync();
        while (await reader.ReadAsync())
            titles.Add(NormalizeSearchText(GetString(reader, 0)));
        return titles;
    }

    private static async Task SearchMoviesAsync(
        SqliteConnection connection,
        string normalizedSearch,
        string plannedFormat,
        string plannedEdition,
        decimal? plannedPrice,
        HashSet<string> wishlistTitles,
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
                GROUP_CONCAT(DISTINCT NULLIF(TRIM(c.Location), '')) AS Locations,
                GROUP_CONCAT(DISTINCT NULLIF(TRIM(c.Edition), '')) AS Editions,
                GROUP_CONCAT(DISTINCT NULLIF(TRIM(c.Packaging), '')) AS Packaging
            FROM Movies m
            LEFT JOIN OwnedCopies c ON c.MovieId = m.Id
            GROUP BY
                m.Id,
                m.Title,
                m.ReleaseYear,
                m.Rating,
                m.Genre,
                m.PosterPath,
                m.PlexRatingKey;
            """;

        await using SqliteDataReader reader = await command.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            string title = GetString(reader, 1);
            int matchScore = CalculateMatchScore(title, normalizedSearch);
            if (matchScore <= 0) continue;

            int id = reader.GetInt32(0);
            int releaseYear = reader.IsDBNull(2) ? 0 : reader.GetInt32(2);
            string rating = GetString(reader, 3);
            string genre = GetString(reader, 4);
            string posterPath = GetString(reader, 5);
            string ratingKey = GetString(reader, 6);
            int copyCount = reader.IsDBNull(7) ? 0 : reader.GetInt32(7);
            string formats = NormalizeList(GetString(reader, 8));
            string locations = NormalizeList(GetString(reader, 9));
            string editions = NormalizeList(GetString(reader, 10));
            string packaging = NormalizeList(GetString(reader, 11));

            SmartBuyResult result = new()
            {
                Id = id,
                MediaType = "Movie",
                Title = title,
                Year = releaseYear,
                Details = BuildMovieDetails(rating, genre),
                PosterPath = posterPath,
                CacheKey = string.IsNullOrWhiteSpace(ratingKey)
                    ? $"movie-{id}"
                    : $"movie-{ratingKey}",
                OwnedCopyCount = copyCount,
                OwnedFormats = formats,
                OwnedLocations = locations,
                OwnedEditions = editions,
                OwnedPackaging = packaging,
                PlannedFormat = plannedFormat,
                PlannedEdition = plannedEdition,
                PlannedPrice = plannedPrice,
                IsWishlist = wishlistTitles.Contains(NormalizeSearchText(title)),
                MatchScore = matchScore
            };

            UpgradeAdvisor.ApplyRecommendation(result, plannedFormat, plannedEdition);
            results.Add(result);
        }
    }

    private static async Task SearchTvShowsAsync(
        SqliteConnection connection,
        string normalizedSearch,
        decimal? plannedPrice,
        HashSet<string> wishlistTitles,
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
            FROM TVShows;
            """;

        await using SqliteDataReader reader = await command.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            string title = GetString(reader, 1);
            int matchScore = CalculateMatchScore(title, normalizedSearch);
            if (matchScore <= 0) continue;

            int id = reader.GetInt32(0);
            int seasons = reader.IsDBNull(3) ? 0 : reader.GetInt32(3);
            int episodes = reader.IsDBNull(4) ? 0 : reader.GetInt32(4);
            string studio = GetString(reader, 5);
            string ratingKey = GetString(reader, 7);

            results.Add(new SmartBuyResult
            {
                Id = id,
                MediaType = "TV Show",
                Title = title,
                Year = reader.IsDBNull(2) ? 0 : reader.GetInt32(2),
                Details = BuildTvDetails(seasons, episodes, studio),
                PosterPath = GetString(reader, 6),
                CacheKey = string.IsNullOrWhiteSpace(ratingKey)
                    ? $"tv-{id}"
                    : $"tv-{ratingKey}",
                OwnedCopyCount = 1,
                PlannedPrice = plannedPrice,
                IsWishlist = wishlistTitles.Contains(NormalizeSearchText(title)),
                MatchScore = matchScore,
                Recommendation = "Already in collection",
                RecommendationDetail =
                    "This television series is already in your library. Check the season or box-set details before buying.",
                RecommendationGlyph = "\uE73E",
                RecommendationColor = "#C42B1C"
            });
        }
    }

    private static async Task SearchWishlistAsync(
        SqliteConnection connection,
        string normalizedSearch,
        string plannedFormat,
        string plannedEdition,
        decimal? plannedPrice,
        List<SmartBuyResult> results)
    {
        await using SqliteCommand command = connection.CreateCommand();
        command.CommandText = "SELECT Id, Title FROM Wishlist WHERE TRIM(Title) <> '';";
        await using SqliteDataReader reader = await command.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            string title = GetString(reader, 1);
            int matchScore = CalculateMatchScore(title, normalizedSearch);
            if (matchScore <= 0) continue;

            results.Add(new SmartBuyResult
            {
                Id = reader.GetInt32(0),
                MediaType = "Wishlist",
                Title = title,
                Details = "Wishlist item",
                PlannedFormat = plannedFormat,
                PlannedEdition = plannedEdition,
                PlannedPrice = plannedPrice,
                IsWishlist = true,
                MatchScore = matchScore - 5,
                Recommendation = "On your wishlist",
                RecommendationDetail = "You do not have a matching owned record, but this title is already on your wishlist.",
                RecommendationGlyph = "\uE734",
                RecommendationColor = "#9D5D00"
            });
        }
    }

    private static int CalculateMatchScore(string title, string normalizedSearch)
    {
        string normalizedTitle = NormalizeSearchText(title);
        if (normalizedTitle.Length == 0) return 0;
        if (normalizedTitle == normalizedSearch) return 1000;
        if (normalizedTitle.StartsWith(normalizedSearch, StringComparison.Ordinal)) return 800;
        if (normalizedTitle.Contains(normalizedSearch, StringComparison.Ordinal)) return 600;

        string[] terms = normalizedSearch.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (terms.Length > 0 && terms.All(term => normalizedTitle.Contains(term, StringComparison.Ordinal)))
            return 400 + terms.Length;

        return 0;
    }

    private static string NormalizeSearchText(string value)
    {
        if (string.IsNullOrWhiteSpace(value)) return string.Empty;

        string decomposed = value.Normalize(NormalizationForm.FormD);
        StringBuilder builder = new(decomposed.Length);
        bool previousWasSpace = true;

        foreach (char character in decomposed)
        {
            UnicodeCategory category = CharUnicodeInfo.GetUnicodeCategory(character);
            if (category == UnicodeCategory.NonSpacingMark) continue;

            if (char.IsLetterOrDigit(character))
            {
                builder.Append(char.ToUpperInvariant(character));
                previousWasSpace = false;
            }
            else if (!previousWasSpace)
            {
                builder.Append(' ');
                previousWasSpace = true;
            }
        }

        return builder.ToString().Trim();
    }


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
