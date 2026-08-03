using System.Collections.Generic;

namespace WalkerMediaManager.UI.Models;

public sealed class DashboardStatistics
{
    public int MovieCount { get; set; }
    public int TvSeriesCount { get; set; }
    public int WishlistCount { get; set; }
    public int CollectionCount { get; set; }
    public int CompletedCollectionCount { get; set; }
    public int TotalRuntimeMinutes { get; set; }
    public double AverageCollectionCompletion { get; set; }
    public IReadOnlyList<DashboardBreakdownItem> TopGenres { get; set; } = [];
    public IReadOnlyList<DashboardBreakdownItem> Decades { get; set; } = [];
    public IReadOnlyList<DashboardBreakdownItem> Formats { get; set; } = [];
    public IReadOnlyList<DashboardCollectionItem> ClosestCollections { get; set; } = [];
    public IReadOnlyList<DashboardRecentMovieItem> RecentMovies { get; set; } = [];
    public IReadOnlyList<DashboardInsight> Insights { get; set; } = [];
    public IReadOnlyList<Achievement> Achievements { get; set; } = [];
    public IReadOnlyList<ActivityEvent> Activity { get; set; } = [];
    public DashboardCollectionItem? LargestCollection { get; set; }
    public DashboardCollectionItem? BestCollection { get; set; }

    public int OwnedTitleCount => MovieCount + TvSeriesCount;
    public string RuntimeDisplay
    {
        get
        {
            if (TotalRuntimeMinutes <= 0) return "Unavailable";
            int days = TotalRuntimeMinutes / 1440;
            int hours = (TotalRuntimeMinutes % 1440) / 60;
            return days > 0 ? $"{days}d {hours}h" : $"{hours}h";
        }
    }

    public string CollectionSummaryDisplay => $"{CompletedCollectionCount} of {CollectionCount} complete";
    public string AverageCompletionDisplay => $"{AverageCollectionCompletion:0}% average completion";
}

public sealed class DashboardBreakdownItem
{
    public string Label { get; set; } = string.Empty;
    public int Count { get; set; }
    public double Percent { get; set; }
    public string CountDisplay => Count == 1 ? "1 movie" : $"{Count} movies";
}

public sealed class DashboardCollectionItem
{
    public string Name { get; set; } = string.Empty;
    public int OwnedCount { get; set; }
    public int TotalCount { get; set; }
    public int MissingCount { get; set; }
    public double CompletionPercent { get; set; }
    public string ProgressDisplay => $"{OwnedCount} of {TotalCount} owned";
    public string MissingDisplay => MissingCount == 1 ? "1 title left" : $"{MissingCount} titles left";
    public string CompletionDisplay => $"{CompletionPercent:0}%";
}

public sealed class DashboardRecentMovieItem
{
    public int MovieId { get; set; }
    public string Title { get; set; } = string.Empty;
    public int ReleaseYear { get; set; }
    public string LastSynced { get; set; } = string.Empty;
    public string DisplayTitle => ReleaseYear > 0 ? $"{Title} ({ReleaseYear})" : Title;
    public string AddedDisplay => System.DateTimeOffset.TryParse(LastSynced, out System.DateTimeOffset value)
        ? value.ToLocalTime().ToString("g")
        : "Recently synced";
}
