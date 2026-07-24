namespace WalkerMediaManager.UI.Models;

public sealed class PlexSyncResult
{
    public int AddedCount { get; set; }
    public int UpdatedCount { get; set; }
    public int MatchedCount { get; set; }
    public int SkippedCount { get; set; }
    public int FailedCount { get; set; }

    public string Summary =>
        $"Added: {AddedCount} | Updated: {UpdatedCount} | " +
        $"Matched: {MatchedCount} | Skipped: {SkippedCount} | Failed: {FailedCount}";
}
