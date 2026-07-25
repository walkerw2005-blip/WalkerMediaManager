namespace WalkerMediaManager.UI.Models;

public sealed class OwnershipReportSummary
{
    public int OwnedCopyCount { get; set; }
    public int MovieCountWithCopies { get; set; }
    public decimal RecordedCollectionValue { get; set; }
    public decimal AverageRecordedPrice { get; set; }
    public int PhysicalCopyCount { get; set; }
    public int DigitalCopyCount { get; set; }
    public int MissingPriceCount { get; set; }
    public int MissingPurchaseDateCount { get; set; }
    public int MissingLocationCount { get; set; }

    public string OwnedCopyCountDisplay => OwnedCopyCount.ToString("N0");
    public string MovieCountDisplay => MovieCountWithCopies.ToString("N0");
    public string CollectionValueDisplay => RecordedCollectionValue.ToString("C");
    public string AveragePriceDisplay => AverageRecordedPrice.ToString("C");
    public string PhysicalCountDisplay => PhysicalCopyCount.ToString("N0");
    public string DigitalCountDisplay => DigitalCopyCount.ToString("N0");
    public string MissingPriceDisplay => MissingPriceCount.ToString("N0");
    public string MissingDateDisplay => MissingPurchaseDateCount.ToString("N0");
    public string MissingLocationDisplay => MissingLocationCount.ToString("N0");
}
