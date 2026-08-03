using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using WalkerMediaManager.UI.Models;
using WalkerMediaManager.UI.Repositories;

namespace WalkerMediaManager.UI.Services;

public sealed class DashboardStatisticsService
{
    private readonly MovieRepository _movieRepository = new();
    private readonly TVShowRepository _tvShowRepository = new();
    private readonly WishlistRepository _wishlistRepository = new();
    private readonly CollectionSeriesService _collectionSeriesService = new();
    private readonly DashboardInsightsService _insightsService = new();
    private readonly AchievementService _achievementService = new();

    public async Task<DashboardStatistics> GetAsync()
    {
        Task<List<Movie>> moviesTask = _movieRepository.GetAllAsync();
        Task<List<TVShow>> showsTask = _tvShowRepository.GetAllAsync();
        Task<List<WishlistItem>> wishlistTask = _wishlistRepository.GetAllAsync();
        Task<List<CollectionSeriesProgress>> collectionsTask = _collectionSeriesService.GetProgressAsync();

        await Task.WhenAll(moviesTask, showsTask, wishlistTask, collectionsTask);

        List<Movie> movies = await moviesTask;
        List<TVShow> shows = await showsTask;
        List<WishlistItem> wishlist = await wishlistTask;
        List<CollectionSeriesProgress> collections = await collectionsTask;

        DashboardStatistics statistics = new()
        {
            MovieCount = movies.Count,
            TvSeriesCount = shows.Count,
            WishlistCount = wishlist.Count(item => !item.IsPurchased),
            CollectionCount = collections.Count,
            CompletedCollectionCount = collections.Count(collection => collection.IsComplete),
            TotalRuntimeMinutes = movies.Sum(movie => Math.Max(0, movie.Runtime)),
            AverageCollectionCompletion = collections.Count == 0 ? 0 : collections.Average(c => c.CompletionPercent),
            TopGenres = BuildBreakdown(movies.SelectMany(movie => SplitGenres(movie.Genre))),
            Decades = BuildDecadeBreakdown(movies),
            Formats = BuildBreakdown(movies.Select(movie => NormalizeFormat(movie.Format))),
            ClosestCollections = BuildClosestCollections(collections),
            RecentMovies = BuildRecentMovies(movies),
            Activity = BuildActivity(movies, wishlist),
            LargestCollection = ToDashboardCollection(collections.OrderByDescending(c => c.TotalCount).FirstOrDefault()),
            BestCollection = ToDashboardCollection(collections.Where(c => c.OwnedCount > 0).OrderByDescending(c => c.CompletionPercent).ThenByDescending(c => c.TotalCount).FirstOrDefault())
        };

        statistics.Insights = _insightsService.Build(collections, wishlist);
        statistics.Achievements = _achievementService.Build(statistics);
        return statistics;
    }

    private static IReadOnlyList<DashboardBreakdownItem> BuildBreakdown(IEnumerable<string> values)
    {
        List<string> clean = values.Where(value => !string.IsNullOrWhiteSpace(value)).ToList();
        int total = clean.Count;
        return clean.GroupBy(value => value, StringComparer.OrdinalIgnoreCase)
            .Select(group => new DashboardBreakdownItem { Label = group.Key, Count = group.Count(), Percent = total == 0 ? 0 : (double)group.Count() / total * 100 })
            .OrderByDescending(item => item.Count).ThenBy(item => item.Label, StringComparer.OrdinalIgnoreCase).Take(6).ToList();
    }

    private static IEnumerable<string> SplitGenres(string genre) => string.IsNullOrWhiteSpace(genre)
        ? []
        : genre.Split([',', ';', '/', '|'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

    private static string NormalizeFormat(string format) => string.IsNullOrWhiteSpace(format) ? "Not recorded" : format.Trim();

    private static IReadOnlyList<DashboardBreakdownItem> BuildDecadeBreakdown(IEnumerable<Movie> movies)
    {
        List<Movie> dated = movies.Where(movie => movie.ReleaseYear > 0).ToList();
        int total = dated.Count;
        return dated.GroupBy(movie => movie.ReleaseYear / 10 * 10)
            .Select(group => new DashboardBreakdownItem { Label = $"{group.Key}s", Count = group.Count(), Percent = total == 0 ? 0 : (double)group.Count() / total * 100 })
            .OrderByDescending(item => item.Count).ThenByDescending(item => item.Label).Take(6).ToList();
    }

    private static IReadOnlyList<DashboardCollectionItem> BuildClosestCollections(IEnumerable<CollectionSeriesProgress> collections) =>
        collections.Where(c => !c.IsComplete && c.OwnedCount > 0).OrderBy(c => c.MissingCount).ThenByDescending(c => c.CompletionPercent).Take(5).Select(ToDashboardCollection).Where(c => c is not null).Cast<DashboardCollectionItem>().ToList();

    private static DashboardCollectionItem? ToDashboardCollection(CollectionSeriesProgress? collection) => collection is null ? null : new DashboardCollectionItem
    {
        Name = collection.Name,
        OwnedCount = collection.OwnedCount,
        TotalCount = collection.TotalCount,
        MissingCount = collection.MissingCount,
        CompletionPercent = collection.CompletionPercent
    };

    private static IReadOnlyList<DashboardRecentMovieItem> BuildRecentMovies(IEnumerable<Movie> movies) =>
        movies.Where(movie => !string.IsNullOrWhiteSpace(movie.LastSynced))
            .Select(movie => new { Movie = movie, Parsed = DateTimeOffset.TryParse(movie.LastSynced, out DateTimeOffset value) ? value : DateTimeOffset.MinValue })
            .OrderByDescending(item => item.Parsed).Take(5)
            .Select(item => new DashboardRecentMovieItem { MovieId = item.Movie.Id, Title = item.Movie.Title, ReleaseYear = item.Movie.ReleaseYear, LastSynced = item.Movie.LastSynced }).ToList();

    private static IReadOnlyList<ActivityEvent> BuildActivity(IEnumerable<Movie> movies, IEnumerable<WishlistItem> wishlist)
    {
        IEnumerable<(DateTimeOffset Date, ActivityEvent Event)> movieEvents = movies
            .Where(movie => DateTimeOffset.TryParse(movie.LastSynced, out _))
            .Select(movie =>
            {
                DateTimeOffset.TryParse(movie.LastSynced, out DateTimeOffset date);
                return (date, new ActivityEvent { Title = movie.Title, Detail = "Movie synced from Plex", DateDisplay = date.ToLocalTime().ToString("g"), Glyph = "\uE714" });
            });
        IEnumerable<(DateTimeOffset Date, ActivityEvent Event)> wishlistEvents = wishlist
            .Where(item => item.DateAdded != default)
            .Select(item =>
            {
                DateTimeOffset date = new(item.DateAdded, TimeSpan.Zero);
                return (date, new ActivityEvent { Title = item.Title, Detail = item.IsPurchased ? "Wishlist item purchased" : "Added to wishlist", DateDisplay = date.ToLocalTime().ToString("g"), Glyph = "\uE8B7" });
            });
        return movieEvents.Concat(wishlistEvents).OrderByDescending(item => item.Date).Take(8).Select(item => item.Event).ToList();
    }
}
