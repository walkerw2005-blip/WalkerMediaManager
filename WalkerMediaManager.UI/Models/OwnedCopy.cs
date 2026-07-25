using System;

namespace WalkerMediaManager.UI.Models;

public sealed class OwnedCopy
{
    public int Id { get; set; }
    public int MovieId { get; set; }
    public string Format { get; set; } = string.Empty;
    public string Edition { get; set; } = string.Empty;
    public string Packaging { get; set; } = string.Empty;
    public string Condition { get; set; } = string.Empty;
    public string Store { get; set; } = string.Empty;
    public decimal? PurchasePrice { get; set; }
    public string PurchaseDate { get; set; } = string.Empty;
    public string Location { get; set; } = string.Empty;
    public string Notes { get; set; } = string.Empty;
    public bool IsDigital { get; set; }
    public bool IsFavorite { get; set; }

    public string DisplayName
    {
        get
        {
            string format = string.IsNullOrWhiteSpace(Format) ? "Format not recorded" : Format;
            return string.IsNullOrWhiteSpace(Edition) ? format : $"{format} - {Edition}";
        }
    }

    public string PackagingDisplay =>
        string.IsNullOrWhiteSpace(Packaging) ? "Packaging not recorded" : Packaging;

    public string ConditionDisplay =>
        string.IsNullOrWhiteSpace(Condition) ? "Condition not recorded" : Condition;

    public string PurchaseDisplay
    {
        get
        {
            string store = string.IsNullOrWhiteSpace(Store) ? "Store not recorded" : Store;
            string price = PurchasePrice.HasValue ? PurchasePrice.Value.ToString("C") : "Price not recorded";
            return $"{store} - {price}";
        }
    }

    public string PurchaseDateDisplay =>
        DateTimeOffset.TryParse(PurchaseDate, out DateTimeOffset date)
            ? date.ToLocalTime().ToString("d")
            : "Date not recorded";

    public string LocationDisplay =>
        string.IsNullOrWhiteSpace(Location) ? "Location not recorded" : Location;

    public string FavoriteDisplay => IsFavorite ? "Preferred copy" : string.Empty;
}
