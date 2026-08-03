using System;

namespace WalkerMediaManager.UI.Models;

public sealed class WishlistItem
{
    public int Id { get; set; }

    public string MediaType { get; set; } = "Movie";

    public string Title { get; set; } = string.Empty;

    public string NormalizedTitle { get; set; } = string.Empty;

    public int Year { get; set; }

    public int? TMDbId { get; set; }

    public string PreferredFormat { get; set; } = string.Empty;

    public decimal? TargetPrice { get; set; }

    public string PreferredStore { get; set; } = string.Empty;

    public int Priority { get; set; } = 2;

    public string Notes { get; set; } = string.Empty;

    public DateTime DateAdded { get; set; } = DateTime.UtcNow;

    public DateTime LastUpdated { get; set; } = DateTime.UtcNow;

    public bool IsPurchased { get; set; }

    public string YearDisplay => Year > 0 ? Year.ToString() : string.Empty;

    public string TargetPriceDisplay => TargetPrice.HasValue
        ? TargetPrice.Value.ToString("C")
        : string.Empty;
}

public enum WishlistSortOrder
{
    Priority,
    Title,
    DateAddedNewest,
    DateAddedOldest,
    TargetPriceLowToHigh,
    TargetPriceHighToLow
}
