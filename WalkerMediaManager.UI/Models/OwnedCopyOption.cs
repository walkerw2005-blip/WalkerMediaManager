namespace WalkerMediaManager.UI.Models;

public sealed class OwnedCopyOption
{
    public int OwnedCopyId { get; set; }
    public int MovieId { get; set; }
    public string Title { get; set; } = string.Empty;
    public int ReleaseYear { get; set; }
    public string Format { get; set; } = string.Empty;
    public string Edition { get; set; } = string.Empty;

    public string DisplayName
    {
        get
        {
            string year = ReleaseYear > 0 ? $" ({ReleaseYear})" : string.Empty;
            string edition = string.IsNullOrWhiteSpace(Edition) ? string.Empty : $" - {Edition}";
            return $"{Title}{year} - {Format}{edition}";
        }
    }
}
