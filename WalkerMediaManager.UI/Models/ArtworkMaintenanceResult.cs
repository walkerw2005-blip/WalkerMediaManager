namespace WalkerMediaManager.UI.Models;

public sealed class ArtworkMaintenanceResult
{
    public int TotalCount { get; set; }
    public int RefreshedCount { get; set; }
    public int AlreadyCachedCount { get; set; }
    public int FailedCount { get; set; }
    public int MissingSourceCount { get; set; }

    public string Summary =>
        $"Processed {TotalCount}; refreshed {RefreshedCount}; already cached {AlreadyCachedCount}; " +
        $"failed {FailedCount}; no poster source {MissingSourceCount}.";
}
