namespace WalkerMediaManager.UI.Models;

public sealed record ArtworkMaintenanceProgress(
    int Current,
    int Total,
    string MediaType,
    string Title,
    string Stage)
{
    public string Message => $"{Current} of {Total}: {MediaType} — {Title} — {Stage}";
}
