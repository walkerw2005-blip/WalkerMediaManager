using System;

namespace WalkerMediaManager.UI.Models;

public sealed class ShoppingHistoryItem
{
    public int Id { get; set; }
    public string SearchText { get; set; } = string.Empty;
    public string Barcode { get; set; } = string.Empty;
    public int? MovieId { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Store { get; set; } = string.Empty;
    public string PlannedFormat { get; set; } = string.Empty;
    public decimal? Price { get; set; }
    public string Decision { get; set; } = string.Empty;
    public string Notes { get; set; } = string.Empty;
    public DateTime SearchedAt { get; set; }
    public string PriceDisplay => Price.HasValue ? Price.Value.ToString("C") : string.Empty;
    public string DateDisplay => SearchedAt.ToLocalTime().ToString("g");
}
