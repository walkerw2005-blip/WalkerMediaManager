namespace WalkerMediaManager.UI.Models;

public sealed class CollectionIntelligenceSummary
{
    public int UniqueOwnedMovies { get; set; }
    public int TotalCopies { get; set; }
    public int DuplicateTitleCount { get; set; }
    public int UpgradeOpportunityCount { get; set; }
    public int ActiveLoanCount { get; set; }
    public int IncompleteGoalCount { get; set; }
    public decimal EstimatedReplacementValue { get; set; }

    public string UniqueOwnedMoviesDisplay => UniqueOwnedMovies.ToString("N0");
    public string TotalCopiesDisplay => TotalCopies.ToString("N0");
    public string DuplicateTitleCountDisplay => DuplicateTitleCount.ToString("N0");
    public string UpgradeOpportunityCountDisplay => UpgradeOpportunityCount.ToString("N0");
    public string ActiveLoanCountDisplay => ActiveLoanCount.ToString("N0");
    public string IncompleteGoalCountDisplay => IncompleteGoalCount.ToString("N0");
    public string EstimatedReplacementValueDisplay => EstimatedReplacementValue.ToString("C");
}
