using System;
using System.Collections.Generic;
using System.Linq;
using WalkerMediaManager.UI.Models;

namespace WalkerMediaManager.UI.Services;

public sealed class ShoppingPlannerService
{
    public ShoppingPlan BuildPlan(IEnumerable<CollectionSeriesProgress> collections, decimal budget)
    {
        decimal remaining = Math.Max(0, budget);
        List<ShoppingRecommendation> selected = [];

        List<ShoppingRecommendation> candidates = collections
            .Where(collection => !collection.IsComplete)
            .SelectMany(collection => collection.Titles
                .Where(title => !title.IsOwned)
                .Select(title => new ShoppingRecommendation
                {
                    Collection = collection,
                    Title = title,
                    ImpactScore = CalculateImpact(collection, title)
                }))
            .OrderByDescending(item => item.ImpactScore)
            .ThenBy(item => item.EstimatedPrice)
            .ThenBy(item => item.Title.Title, StringComparer.OrdinalIgnoreCase)
            .ToList();

        foreach (ShoppingRecommendation candidate in candidates)
        {
            if (candidate.EstimatedPrice <= remaining)
            {
                selected.Add(candidate);
                remaining -= candidate.EstimatedPrice;
            }
        }

        return new ShoppingPlan
        {
            Budget = budget,
            Recommendations = selected
        };
    }

    private static int CalculateImpact(CollectionSeriesProgress collection, CollectionSeriesTitleStatus title)
    {
        int score = collection.SmartPriorityScore;
        score += collection.MissingCount == 1 ? 100 : 0;
        score += title.IsOnWishlist ? 20 : 0;
        score += Math.Max(0, 30 - collection.MissingCount * 5);
        return score;
    }
}
