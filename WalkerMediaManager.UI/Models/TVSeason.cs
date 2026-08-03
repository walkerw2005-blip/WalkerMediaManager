namespace WalkerMediaManager.UI.Models;

public sealed class TVSeason
{
    public int Id { get; set; }
    public int TVShowId { get; set; }
    public int SeasonNumber { get; set; }
    public string Name { get; set; } = string.Empty;
    public int EpisodeCount { get; set; }
    public bool IsOwned { get; set; }
    public string Format { get; set; } = "DVD";
    public bool HasDigitalCopy { get; set; } = true;
    public decimal? PurchasePrice { get; set; }
    public string PurchaseDate { get; set; } = string.Empty;
    public string StorageLocation { get; set; } = string.Empty;
    public string Notes { get; set; } = string.Empty;

    public string DisplayName => string.IsNullOrWhiteSpace(Name)
        ? $"Season {SeasonNumber}"
        : Name;

    public string OwnershipStatus => IsOwned ? "Owned" : "Missing";
    public string DigitalStatus => HasDigitalCopy ? "Digital copy" : "No digital copy";
}
