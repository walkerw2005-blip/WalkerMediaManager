namespace WalkerMediaManager.UI.Models;

public sealed class SmartBuyResult
{
    public int Id { get; set; }

    public string MediaType { get; set; } = string.Empty;

    public string Title { get; set; } = string.Empty;

    public int Year { get; set; }

    public string Details { get; set; } = string.Empty;

    public bool IsOwned { get; set; }

    public string OwnershipStatus =>
        IsOwned
            ? "Already Owned"
            : "Not Owned";

    public string YearDisplay =>
        Year > 0
            ? Year.ToString()
            : "Year unknown";
}