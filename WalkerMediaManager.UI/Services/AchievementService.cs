using System;
using System.Collections.Generic;
using WalkerMediaManager.UI.Models;

namespace WalkerMediaManager.UI.Services;

public sealed class AchievementService
{
    public IReadOnlyList<Achievement> Build(DashboardStatistics statistics) =>
    [
        Create("Movie Collector", "Own 500 movies", "\uE714", statistics.MovieCount, 500),
        Create("Series Keeper", "Own 50 TV series", "\uE7F4", statistics.TvSeriesCount, 50),
        Create("Collection Curator", "Complete 25 collections", "\uE8D5", statistics.CompletedCollectionCount, 25),
        Create("A Thousand Hours", "Catalog 1,000 hours of movie runtime", "\uE823", statistics.TotalRuntimeMinutes / 60.0, 1000)
    ];

    private static Achievement Create(string title, string description, string glyph, double value, double target)
    {
        double progress = target <= 0 ? 0 : Math.Min(100, value / target * 100);
        return new Achievement
        {
            Title = title,
            Description = description,
            Glyph = glyph,
            Progress = progress,
            IsUnlocked = value >= target
        };
    }
}
