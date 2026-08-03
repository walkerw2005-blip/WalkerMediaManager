using System.Collections.Generic;
using System.Linq;
using WalkerMediaManager.UI.Models;

namespace WalkerMediaManager.UI.Services;

public sealed class DashboardInsightsService
{
    public IReadOnlyList<DashboardInsight> Build(
        IReadOnlyList<CollectionSeriesProgress> collections,
        IReadOnlyList<WishlistItem> wishlist)
    {
        List<DashboardInsight> insights = [];

        List<CollectionSeriesProgress> affordable = collections
            .Where(c => !c.IsComplete && c.EstimatedCompletionCost > 0 && c.EstimatedCompletionCost <= 40)
            .OrderBy(c => c.EstimatedCompletionCost)
            .ToList();
        if (affordable.Count > 0)
        {
            insights.Add(new DashboardInsight
            {
                Title = "Affordable collection wins",
                Message = $"You can complete {affordable.Count} collection{(affordable.Count == 1 ? string.Empty : "s")} for $40 or less each.",
                Glyph = "\uE8C7",
                ActionLabel = "Open Collections",
                ActionKey = "collections"
            });
        }

        int wishlistFinishers = collections.Sum(c => c.Titles.Count(t => !t.IsOwned && t.IsOnWishlist && c.MissingCount == 1));
        if (wishlistFinishers > 0)
        {
            insights.Add(new DashboardInsight
            {
                Title = "Wishlist completion opportunities",
                Message = $"{wishlistFinishers} wishlist title{(wishlistFinishers == 1 ? string.Empty : "s")} would finish a collection.",
                Glyph = "\uE8B7",
                ActionLabel = "Open Wishlist",
                ActionKey = "wishlist"
            });
        }

        CollectionSeriesProgress? closest = collections
            .Where(c => !c.IsComplete && c.OwnedCount > 0)
            .OrderBy(c => c.MissingCount)
            .ThenByDescending(c => c.CompletionPercent)
            .FirstOrDefault();
        if (closest is not null)
        {
            insights.Add(new DashboardInsight
            {
                Title = "Closest to completion",
                Message = $"{closest.Name} is {closest.CompletionPercent:0}% complete with {closest.MissingCount} title{(closest.MissingCount == 1 ? string.Empty : "s")} left.",
                Glyph = "\uE9D9",
                ActionLabel = "View Recommendations",
                ActionKey = "recommendations"
            });
        }

        int unplanned = collections.Sum(c => c.UnplannedMissingCount);
        if (unplanned > 0)
        {
            insights.Add(new DashboardInsight
            {
                Title = "Missing titles need a plan",
                Message = $"{unplanned} missing title{(unplanned == 1 ? string.Empty : "s")} are not yet on your wishlist.",
                Glyph = "\uE8A5",
                ActionLabel = "Plan Purchases",
                ActionKey = "collections"
            });
        }

        return insights.Take(4).ToList();
    }
}
