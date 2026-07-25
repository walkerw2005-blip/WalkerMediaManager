using System;

namespace WalkerMediaManager.UI.Models;

public sealed class PurchaseHistoryRow
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

    public string MovieDisplay => MovieYear.HasValue ? $"{MovieTitle} ({MovieYear.Value})" : MovieTitle;

    public string FormatDisplay
    {
        get
        {
            string format = string.IsNullOrWhiteSpace(Format) ? "Format not recorded" : Format;
            return string.IsNullOrWhiteSpace(Edition) ? format : $"{format} - {Edition}";
        }
    }

    public string PackagingDisplay =>
        string.IsNullOrWhiteSpace(Packaging) ? "Packaging not recorded" : Packaging;

    public string StoreDisplay =>
        string.IsNullOrWhiteSpace(Store) ? "Store not recorded" : Store;

    public string PriceDisplay =>
        PurchasePrice.HasValue ? PurchasePrice.Value.ToString("C") : "Price not recorded";

    public string DateDisplay =>
        DateTimeOffset.TryParse(PurchaseDate, out DateTimeOffset date)
            ? date.ToLocalTime().ToString("d")
            : "Date not recorded";

    public string LocationDisplay =>
        string.IsNullOrWhiteSpace(Location) ? "Location not recorded" : Location;

    public string MediaTypeDisplay => IsDigital ? "Digital" : "Physical";
}
