namespace WalkerMediaManager.UI.Models;

public sealed class TVMetadataDiagnostic
{
    public string Title { get; init; } = string.Empty;
    public int Year { get; init; }
    public string Outcome { get; init; } = string.Empty;
    public string MatchMethod { get; init; } = string.Empty;
    public int ConfidenceScore { get; init; }
    public int CandidateCount { get; init; }
    public int? ProviderId { get; init; }
    public string ProviderTitle { get; init; } = string.Empty;
    public string PosterUrl { get; init; } = string.Empty;
    public string PosterStatus { get; init; } = string.Empty;
    public string Reason { get; init; } = string.Empty;
}
