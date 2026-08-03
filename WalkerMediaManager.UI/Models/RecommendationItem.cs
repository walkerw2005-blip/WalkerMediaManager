using System;
using System.Collections.Generic;
using System.Text;

namespace WalkerMediaManager.UI.Models;

public sealed class RecommendationItem
{
    public string Title { get; set; } = string.Empty;

    public int Year { get; set; }

    public string CollectionName { get; set; } = string.Empty;

    public RecommendationType Type { get; set; }

    public RecommendationPriority Priority { get; set; }

    public int Score { get; set; }

    public string Reason { get; set; } = string.Empty;

    public string PosterPath { get; set; } = string.Empty;

    public int? MovieId { get; set; }

    public int? WishlistItemId { get; set; }

    public bool IsOnWishlist { get; set; }

    public int CollectionOwnedCount { get; set; }

    public int CollectionTotalCount { get; set; }

    public int CollectionMissingCount =>
        Math.Max(0, CollectionTotalCount - CollectionOwnedCount);

    public double CollectionCompletionPercent => CollectionTotalCount <= 0
        ? 0
        : (double)CollectionOwnedCount / CollectionTotalCount * 100;

    public double CompletionAfterPurchasePercent => CollectionTotalCount <= 0
        ? 0
        : (double)Math.Min(CollectionOwnedCount + 1, CollectionTotalCount) /
          CollectionTotalCount * 100;

    public string YearDisplay => Year > 0 ? Year.ToString() : string.Empty;

    public string DisplayTitle => Year > 0 ? $"{Title} ({Year})" : Title;

    public string RecommendationKey =>
        $"{NormalizeKeyPart(Title)}|{Math.Max(0, Year)}|{NormalizeKeyPart(CollectionName)}";

    private static string NormalizeKeyPart(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return string.Empty;
        }

        StringBuilder normalized = new(value.Length);

        foreach (char character in value)
        {
            if (char.IsLetterOrDigit(character))
            {
                normalized.Append(char.ToLowerInvariant(character));
            }
        }

        return normalized.ToString();
    }

    public string PriorityDisplay => Priority switch
    {
        RecommendationPriority.Critical => "Must Buy",
        RecommendationPriority.VeryHigh => "Strong Recommendation",
        RecommendationPriority.High => "Strong Recommendation",
        RecommendationPriority.Good => "Good Addition",
        _ => "Optional"
    };

    public string PriorityBadge => Priority switch
    {
        RecommendationPriority.Critical => "● MUST BUY",
        RecommendationPriority.VeryHigh => "● STRONG RECOMMENDATION",
        RecommendationPriority.High => "● STRONG RECOMMENDATION",
        RecommendationPriority.Good => "● GOOD ADDITION",
        _ => "○ OPTIONAL"
    };

    public string CollectionProgressDisplay => CollectionTotalCount > 0
        ? $"{CollectionOwnedCount} of {CollectionTotalCount} owned"
        : string.Empty;

    public string ProgressImpactDisplay => CollectionTotalCount > 0
        ? $"{Math.Round(CollectionCompletionPercent):0}% → {Math.Round(CompletionAfterPurchasePercent):0}%"
        : string.Empty;

    public IReadOnlyList<string> ExplanationItems
    {
        get
        {
            List<string> items = [];

            if (!string.IsNullOrWhiteSpace(Reason))
            {
                items.Add(Reason);
            }

            if (CollectionTotalCount > 0)
            {
                if (CollectionMissingCount == 1)
                {
                    items.Add($"This one purchase completes {CollectionName}.");
                }
                else
                {
                    items.Add($"Moves {CollectionName} from {CollectionOwnedCount} of {CollectionTotalCount} to {Math.Min(CollectionOwnedCount + 1, CollectionTotalCount)} of {CollectionTotalCount} owned.");
                }
            }

            if (Type == RecommendationType.ContinueWatchOrder)
            {
                items.Add("Fills the next gap in the recommended watch order.");
            }

            if (IsOnWishlist)
            {
                items.Add("Already saved on your wishlist.");
            }

            if (Score >= 95)
            {
                items.Add("One of the highest-impact additions currently available.");
            }

            return items;
        }
    }
}
