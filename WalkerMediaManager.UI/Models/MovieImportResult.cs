namespace WalkerMediaManager.UI.Models;

public sealed class MovieImportResult
{
    public int ImportedCount { get; set; }

    public int DuplicateCount { get; set; }

    public int SkippedCount { get; set; }

    public int FailedCount { get; set; }

    public string Summary =>
        $"Imported: {ImportedCount} | " +
        $"Duplicates skipped: {DuplicateCount} | " +
        $"Blank rows skipped: {SkippedCount} | " +
        $"Failed: {FailedCount}";
}