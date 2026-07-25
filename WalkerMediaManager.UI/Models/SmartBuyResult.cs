using System;

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
    public string PlannedFormat { get; set; } = string.Empty;
    public decimal? PlannedPrice { get; set; }
    public string Recommendation { get; set; } = string.Empty;
    public string RecommendationDetail { get; set; } = string.Empty;
    public string RecommendationGlyph { get; set; } = "\uE946";
    public string RecommendationColor { get; set; } = "Gray";

    public bool IsOwned => OwnedCopyCount > 0 || MediaType == "TV Show";

    public string YearDisplay => Year > 0 ? Year.ToString() : "Year unknown";

    public string OwnershipSummary
    {
        get
        {
            if (MediaType == "TV Show")
                return "In your television collection";

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

    public string LocationSummary =>
        string.IsNullOrWhiteSpace(OwnedLocations)
            ? "Location not recorded"
            : OwnedLocations;

    public string PlannedPriceDisplay =>
        PlannedPrice.HasValue
            ? PlannedPrice.Value.ToString("C")
            : string.Empty;
}
