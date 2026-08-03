using System;
using System.Collections.Generic;
using System.Linq;

namespace WalkerMediaManager.UI.Models;

public sealed class CollectionSeriesProgress
{
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string Category { get; set; } = string.Empty;
    public string Studio { get; set; } = string.Empty;
    public string CollectionType { get; set; } = "Franchise";
    public IReadOnlyList<CollectionSeriesTitleStatus> Titles { get; set; } = [];

    public int TotalCount => Titles.Count;
    public int OwnedCount => Titles.Count(title => title.IsOwned);
    public int MissingCount => Math.Max(0, TotalCount - OwnedCount);
    public int TotalOwnedRuntime => Titles.Where(title => title.IsOwned).Sum(title => title.Runtime);
    public decimal TotalInvestment => Titles.Where(title => title.IsOwned).Sum(title => title.PurchasePriceTotal);
    public int DigitalCopyCount => Titles.Count(title => title.IsOwned && title.HasDigitalCopy);
    public string TotalInvestmentDisplay => TotalInvestment.ToString("C");
    public string AveragePurchasePriceDisplay
    {
        get
        {
            List<CollectionSeriesTitleStatus> priced = Titles
                .Where(title => title.IsOwned && title.PurchasePriceTotal > 0)
                .ToList();
            return priced.Count == 0 ? "Not recorded" : (priced.Sum(title => title.PurchasePriceTotal) / priced.Count).ToString("C");
        }
    }
    public string DigitalCoverageDisplay => OwnedCount == 0 ? "0 of 0" : $"{DigitalCopyCount} of {OwnedCount}";
    public string FormatBreakdownDisplay
    {
        get
        {
            List<string> formats = Titles
                .Where(title => title.IsOwned)
                .SelectMany(title => title.OwnedFormats)
                .Where(format => !string.IsNullOrWhiteSpace(format))
                .GroupBy(format => format.Trim(), StringComparer.OrdinalIgnoreCase)
                .OrderByDescending(group => group.Count())
                .ThenBy(group => group.Key, StringComparer.OrdinalIgnoreCase)
                .Select(group => $"{group.Key} x{group.Count()}")
                .ToList();
            return formats.Count == 0 ? "Not recorded" : string.Join(" • ", formats);
        }
    }
    public string StorageLocationsDisplay
    {
        get
        {
            List<string> locations = Titles
                .Where(title => title.IsOwned && !string.IsNullOrWhiteSpace(title.StorageLocation))
                .Select(title => title.StorageLocation.Trim())
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(location => location, StringComparer.OrdinalIgnoreCase)
                .ToList();
            return locations.Count == 0 ? "Not recorded" : string.Join(" • ", locations);
        }
    }
    public double CompletionPercent => TotalCount == 0 ? 0 : (double)OwnedCount / TotalCount * 100;
    public bool IsComplete => TotalCount > 0 && OwnedCount == TotalCount;
    public int WishlistCount => Titles.Count(title => !title.IsOwned && title.IsOnWishlist);

    public int WishlistMissingCount => Titles.Count(title => !title.IsOwned && title.IsOnWishlist);
    public int FirstReleaseYear => Titles.Where(title => title.Year > 0).Select(title => title.Year).DefaultIfEmpty(0).Min();
    public int LastReleaseYear => Titles.Where(title => title.Year > 0).Select(title => title.Year).DefaultIfEmpty(0).Max();
    public string ReleaseSpanDisplay => FirstReleaseYear <= 0
        ? "Years unavailable"
        : FirstReleaseYear == LastReleaseYear
            ? FirstReleaseYear.ToString()
            : $"{FirstReleaseYear}-{LastReleaseYear}";
    public string WishlistSummaryDisplay => WishlistMissingCount == 0
        ? "No missing titles planned"
        : WishlistMissingCount == 1
            ? "1 missing title on wishlist"
            : $"{WishlistMissingCount} missing titles on wishlist";
    public string EstimatedCompletionDisplay => IsComplete ? "$0.00" : EstimatedCompletionCost.ToString("C");
    public string PreferredFormatSummary
    {
        get
        {
            List<string> formats = Titles
                .Where(title => !title.IsOwned && title.IsOnWishlist && !string.IsNullOrWhiteSpace(title.PreferredFormat))
                .Select(title => title.PreferredFormat.Trim())
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(format => format, StringComparer.OrdinalIgnoreCase)
                .ToList();

            return formats.Count == 0 ? "DVD default" : string.Join(", ", formats);
        }
    }
    public int UnplannedMissingCount => Titles.Count(title => !title.IsOwned && !title.IsOnWishlist);
    public decimal EstimatedCompletionCost => Titles
        .Where(title => !title.IsOwned)
        .Sum(title => title.EstimatedPurchasePrice);
    public int SmartPriorityScore => IsComplete
        ? 0
        : (int)Math.Round(CompletionPercent) + WishlistCount * 8 - MissingCount * 2;
    public int HealthScore { get; set; }
    public string HealthDisplay => $"Health {HealthScore}";
    public string HealthCategory => HealthScore >= 85
        ? "Excellent"
        : HealthScore >= 65
            ? "Good"
            : HealthScore >= 40
                ? "Needs Attention"
                : "Low";
    public string CompleteNowDisplay => IsComplete
        ? "Complete"
        : MissingCount == 1
            ? "Only 1 movie left!"
            : CompletionPercent >= 75
                ? "Close to completion"
                : string.Empty;
    public bool CanAddMissingToWishlist => UnplannedMissingCount > 0;

    public string MetadataDisplay
    {
        get
        {
            List<string> parts = [];
            if (!string.IsNullOrWhiteSpace(Category))
            {
                parts.Add(Category);
            }

            if (!string.IsNullOrWhiteSpace(Studio))
            {
                parts.Add(Studio);
            }

            return parts.Count == 0 ? CollectionType : string.Join(" • ", parts);
        }
    }

    public CollectionSeriesTitleStatus? RecommendedNextTitle => Titles
        .Where(title => !title.IsOwned)
        .OrderByDescending(title => title.IsOnWishlist)
        .ThenBy(title => title.Year <= 0 ? int.MaxValue : title.Year)
        .ThenBy(title => title.Title, StringComparer.OrdinalIgnoreCase)
        .FirstOrDefault();

    public string RecommendedNextDisplay
    {
        get
        {
            CollectionSeriesTitleStatus? title = RecommendedNextTitle;
            if (title is null)
            {
                return "Collection complete";
            }

            if (title.IsOnWishlist)
            {
                string format = string.IsNullOrWhiteSpace(title.PreferredFormat) ? "planned" : title.PreferredFormat;
                return $"Buy next: {title.Title} • Wishlist ({format})";
            }

            return $"Buy next: {title.Title}";
        }
    }

    public string ProgressDisplay => $"{OwnedCount} of {TotalCount} owned";
    public string DashboardProgressDisplay => $"{OwnedCount} / {TotalCount}";
    public string CompletionCategory => IsComplete
        ? "Complete"
        : CompletionPercent >= 75
            ? "Almost Complete"
            : CompletionPercent > 0
                ? "In Progress"
                : "Not Started";
    public string DashboardSubtitle => IsComplete
        ? "All movies owned"
        : MissingCount == 1
            ? "1 movie left"
            : $"{MissingCount} movies left";
    public string CompletionDisplay => $"{CompletionPercent:0}% complete";
    public string WishlistDisplay => WishlistCount == 0
        ? "Nothing on wishlist"
        : WishlistCount == 1
            ? "1 missing title on wishlist"
            : $"{WishlistCount} missing titles on wishlist";
    public string CompletionCostDisplay => IsComplete
        ? "Complete"
        : $"About {EstimatedCompletionCost:C} to complete";
    public string PlanningDisplay => IsComplete
        ? "No purchases needed"
        : UnplannedMissingCount == 0
            ? "Every missing title is planned"
            : UnplannedMissingCount == 1
                ? "1 missing title is not on the wishlist"
                : $"{UnplannedMissingCount} missing titles are not on the wishlist";
    public string StatusDisplay => IsComplete
        ? "Collection complete"
        : MissingCount == 1
            ? "1 movie missing"
            : $"{MissingCount} movies missing";

    public string RuntimeDisplay
    {
        get
        {
            if (TotalOwnedRuntime <= 0)
            {
                return "Runtime unavailable";
            }

            int hours = TotalOwnedRuntime / 60;
            int minutes = TotalOwnedRuntime % 60;
            return hours > 0 ? $"{hours}h {minutes}m owned" : $"{minutes}m owned";
        }
    }

    public string MissingPreview
    {
        get
        {
            List<string> missing = Titles
                .Where(title => !title.IsOwned)
                .Select(title => title.DisplayTitle)
                .Take(3)
                .ToList();

            if (missing.Count == 0)
            {
                return "You own every movie in this series.";
            }

            string preview = "Missing: " + string.Join(", ", missing);
            return MissingCount > missing.Count ? preview + ", and more" : preview;
        }
    }
}

public sealed class CollectionSeriesTitleStatus
{
    public string Title { get; set; } = string.Empty;
    public int Year { get; set; }
    public bool IsOwned { get; set; }
    public int? MovieId { get; set; }
    public string OwnedFormat { get; set; } = string.Empty;
    public int Runtime { get; set; }
    public string Rating { get; set; } = string.Empty;
    public string PosterPath { get; set; } = string.Empty;
    public string PlexRatingKey { get; set; } = string.Empty;
    public bool IsOnWishlist { get; set; }
    public int? WishlistItemId { get; set; }
    public string PreferredFormat { get; set; } = string.Empty;
    public decimal? WishlistTargetPrice { get; set; }
    public decimal EstimatedPurchasePrice { get; set; }
    public List<string> OwnedFormats { get; set; } = [];
    public decimal PurchasePriceTotal { get; set; }
    public bool HasDigitalCopy { get; set; }
    public string PurchaseDate { get; set; } = string.Empty;
    public string StorageLocation { get; set; } = string.Empty;

    public string DisplayTitle => Year > 0 ? $"{Title} ({Year})" : Title;
    public string StatusDisplay => IsOwned
        ? string.IsNullOrWhiteSpace(OwnedFormat) ? "Owned" : $"Owned - {OwnedFormat}"
        : "Missing";
    public string RuntimeDisplay => Runtime > 0 ? $"{Runtime} min" : string.Empty;
    public string WishlistStatusDisplay => IsOwned
        ? string.Empty
        : IsOnWishlist
            ? WishlistTargetPrice.HasValue
                ? $"Wishlist - target {WishlistTargetPrice.Value:C}"
                : "On wishlist"
            : "Not on wishlist";
    public string WishlistActionText => IsOnWishlist ? "On Wishlist" : "Add to Wishlist";
    public bool CanAddToWishlist => !IsOwned && !IsOnWishlist;
    public string EstimatedPriceDisplay => IsOwned
        ? string.Empty
        : WishlistTargetPrice.HasValue
            ? WishlistTargetPrice.Value.ToString("C")
            : $"Est. {EstimatedPurchasePrice:C}";
    public string RatingDisplay => string.IsNullOrWhiteSpace(Rating) ? string.Empty : Rating;

    public string OwnedDetailsDisplay
    {
        get
        {
            List<string> details = [];
            if (!string.IsNullOrWhiteSpace(OwnedFormat))
            {
                details.Add(OwnedFormat);
            }
            if (Runtime > 0)
            {
                details.Add($"{Runtime} min");
            }
            if (!string.IsNullOrWhiteSpace(Rating))
            {
                details.Add(Rating);
            }
            return details.Count == 0 ? "Owned" : string.Join(" • ", details);
        }
    }
    public string OwnedCopyDetailsDisplay
    {
        get
        {
            List<string> details = [];
            string formats = OwnedFormats.Count > 0
                ? string.Join(", ", OwnedFormats.Distinct(StringComparer.OrdinalIgnoreCase))
                : OwnedFormat;
            if (!string.IsNullOrWhiteSpace(formats))
            {
                details.Add(formats);
            }
            if (HasDigitalCopy)
            {
                details.Add("Digital copy");
            }
            if (Runtime > 0)
            {
                details.Add($"{Runtime} min");
            }
            if (!string.IsNullOrWhiteSpace(Rating))
            {
                details.Add(Rating);
            }
            if (PurchasePriceTotal > 0)
            {
                details.Add(PurchasePriceTotal.ToString("C"));
            }
            if (!string.IsNullOrWhiteSpace(StorageLocation))
            {
                details.Add(StorageLocation);
            }
            return details.Count == 0 ? "Owned" : string.Join(" • ", details);
        }
    }
    public string PurchaseImpactDisplay => IsOwned
        ? string.Empty
        : IsOnWishlist
            ? $"Planned in {PreferredFormatSummary}"
            : "Advances collection progress";
    private string PreferredFormatSummary => string.IsNullOrWhiteSpace(PreferredFormat) ? "DVD" : PreferredFormat;
    public double CardOpacity => IsOwned ? 1.0 : 0.55;
}
