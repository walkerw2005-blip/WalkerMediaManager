using System;
using System.Linq;

namespace WalkerMediaManager.UI.Models;

public sealed class SmartBuyResult
{
    public int Id { get; set; }
    public string MediaType { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public int Year { get; set; }
    public string Details { get; set; } = string.Empty;
    public string PosterPath { get; set; } = string.Empty;
    public string CacheKey { get; set; } = string.Empty;
    public int OwnedCopyCount { get; set; }
    public string OwnedFormats { get; set; } = string.Empty;
    public string OwnedLocations { get; set; } = string.Empty;
    public string OwnedEditions { get; set; } = string.Empty;
    public string OwnedPackaging { get; set; } = string.Empty;
    public string PlannedFormat { get; set; } = string.Empty;
    public string PlannedEdition { get; set; } = string.Empty;
    public decimal? PlannedPrice { get; set; }
    public string Recommendation { get; set; } = string.Empty;
    public string RecommendationDetail { get; set; } = string.Empty;
    public string RecommendationGlyph { get; set; } = "\uE946";
    public string RecommendationColor { get; set; } = "Gray";
    public bool IsWishlist { get; set; }
    public int MatchScore { get; set; }

    public bool IsOwned => OwnedCopyCount > 0 || MediaType == "TV Show";
    public bool CanOpenRecord => MediaType is "Movie" or "TV Show";

    public string YearDisplay => Year > 0 ? Year.ToString() : "Year unknown";

    public string OwnershipState
    {
        get
        {
            if (IsOwned) return "OWNED";
            if (IsWishlist) return "WISHLIST";
            return "NOT OWNED";
        }
    }

    public string OwnershipSummary
    {
        get
        {
            if (MediaType == "TV Show")
                return "In your television collection";

            if (IsWishlist && OwnedCopyCount == 0)
                return "On your wishlist";

            if (OwnedCopyCount == 0)
                return "No owned-copy record";

            return OwnedCopyCount == 1
                ? "1 owned copy"
                : $"{OwnedCopyCount} owned copies";
        }
    }

    public string FormatSummary =>
        string.IsNullOrWhiteSpace(OwnedFormats)
            ? "Formats not recorded"
            : OwnedFormats;

    public string EditionSummary
    {
        get
        {
            string[] values = new[] { OwnedEditions, OwnedPackaging }
                .Where(value => !string.IsNullOrWhiteSpace(value))
                .ToArray();
            return values.Length == 0 ? "Edition not recorded" : string.Join(", ", values);
        }
    }

    public string LocationSummary =>
        string.IsNullOrWhiteSpace(OwnedLocations)
            ? "Location not recorded"
            : OwnedLocations;

    public string PlannedPriceDisplay =>
        PlannedPrice.HasValue
            ? PlannedPrice.Value.ToString("C")
            : string.Empty;

    public string MatchDisplay =>
        Year > 0 ? $"{MediaType} • {Year}" : MediaType;
}
