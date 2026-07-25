using System;

namespace WalkerMediaManager.UI.Models;

public sealed class OwnedCopyReportRow
{
    public int CopyId { get; set; }
    public int MovieId { get; set; }
    public string MovieTitle { get; set; } = string.Empty;
    public int? MovieYear { get; set; }
    public string Format { get; set; } = string.Empty;
    public string Edition { get; set; } = string.Empty;
    public string Packaging { get; set; } = string.Empty;
    public string Store { get; set; } = string.Empty;
    public decimal? PurchasePrice { get; set; }
    public string PurchaseDate { get; set; } = string.Empty;
    public string Location { get; set; } = string.Empty;
    public bool IsDigital { get; set; }
    public bool IsFavorite { get; set; }

    public string TitleDisplay => MovieYear.HasValue && MovieYear.Value > 0
        ? $"{MovieTitle} ({MovieYear.Value})"
        : MovieTitle;

    public string EditionDisplay
    {
        get
        {
            string format = string.IsNullOrWhiteSpace(Format) ? "Unspecified format" : Format;
            return string.IsNullOrWhiteSpace(Edition) ? format : $"{format} - {Edition}";
        }
    }

    public string PurchasePriceDisplay => PurchasePrice.HasValue
        ? PurchasePrice.Value.ToString("C")
        : "Not recorded";

    public string PurchaseDateDisplay => DateTimeOffset.TryParse(PurchaseDate, out DateTimeOffset date)
        ? date.ToLocalTime().ToString("d")
        : "Not recorded";

    public string StoreDisplay => string.IsNullOrWhiteSpace(Store) ? "Not recorded" : Store;
    public string LocationDisplay => string.IsNullOrWhiteSpace(Location) ? "Not recorded" : Location;
    public string PreferredDisplay => IsFavorite ? "Preferred" : string.Empty;
}
