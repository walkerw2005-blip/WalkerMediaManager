namespace WalkerMediaManager.UI.Models;

public sealed class ShoppingRecommendation
{
    public CollectionSeriesProgress Collection { get; set; } = new();
    public CollectionSeriesTitleStatus Title { get; set; } = new();
    public decimal EstimatedPrice => Title.EstimatedPurchasePrice;
    public int ImpactScore { get; set; }
    public bool CompletesCollection => Collection.MissingCount == 1;
    public string ImpactDisplay => CompletesCollection
        ? $"Completes {Collection.Name}"
        : $"Moves {Collection.Name} to {(Collection.OwnedCount + 1) * 100 / Collection.TotalCount}%";
    public string PriceDisplay => EstimatedPrice.ToString("C");
}
