namespace WalkerMediaManager.UI.Models;

public sealed class Achievement
{
    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string Glyph { get; set; } = "\uE7C1";
    public double Progress { get; set; }
    public bool IsUnlocked { get; set; }
    public string StatusDisplay => IsUnlocked ? "Unlocked" : $"{Progress:0}%";
}
