namespace WalkerMediaManager.UI.Models;

public sealed class TVMetadataSyncResult
{
    public int UpdatedCount { get; set; }
    public int NotFoundCount { get; set; }
    public int FailedCount { get; set; }

    public string Summary => $"Updated {UpdatedCount}; not found {NotFoundCount}; failed {FailedCount}.";
}
