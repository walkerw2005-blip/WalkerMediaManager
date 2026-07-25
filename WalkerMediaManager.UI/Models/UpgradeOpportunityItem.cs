namespace WalkerMediaManager.UI.Models;

public sealed class UpgradeOpportunityItem
{
    public int MovieId { get; set; }
    public string Title { get; set; } = string.Empty;
    public int ReleaseYear { get; set; }
    public string CurrentFormats { get; set; } = string.Empty;
    public string Recommendation { get; set; } = string.Empty;

    public string TitleDisplay => ReleaseYear > 0 ? $"{Title} ({ReleaseYear})" : Title;
}
