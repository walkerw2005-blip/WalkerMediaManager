namespace WalkerMediaManager.UI.Models;

public sealed class PurchaseHistorySummary
{
    public int PurchaseCount { get; set; }
    public decimal RecordedSpending { get; set; }
    public decimal AveragePrice { get; set; }
    public int StoreCount { get; set; }
    public int MissingDateCount { get; set; }

    public string PurchaseCountDisplay => PurchaseCount.ToString("N0");
    public string RecordedSpendingDisplay => RecordedSpending.ToString("C");
    public string AveragePriceDisplay => AveragePrice.ToString("C");
    public string StoreCountDisplay => StoreCount.ToString("N0");
    public string MissingDateCountDisplay => MissingDateCount.ToString("N0");
}
