namespace WalkerMediaManager.UI.Models;

public sealed record TVMetadataProgress(
    int Current,
    int Total,
    string Title,
    string Stage)
{
    public string Message => $"{Current} of {Total}: {Title} — {Stage}";
}
