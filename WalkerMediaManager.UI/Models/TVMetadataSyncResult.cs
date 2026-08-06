using System.Collections.Generic;

namespace WalkerMediaManager.UI.Models;

public sealed class TVMetadataSyncResult
{
    public int UpdatedCount { get; set; }
    public int NotFoundCount { get; set; }
    public int FailedCount { get; set; }
    public int PosterAvailableCount { get; set; }
    public int PosterMissingCount { get; set; }
    public string DiagnosticsReportPath { get; set; } = string.Empty;
    public List<TVMetadataDiagnostic> Diagnostics { get; } = [];

    public string Summary =>
        $"Updated {UpdatedCount}; not found {NotFoundCount}; failed {FailedCount}; " +
        $"posters available {PosterAvailableCount}; posters missing {PosterMissingCount}." +
        (string.IsNullOrWhiteSpace(DiagnosticsReportPath)
            ? string.Empty
            : $" Diagnostics: {DiagnosticsReportPath}");
}
