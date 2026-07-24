namespace WalkerMediaManager.UI.Models;

public sealed class PlexLibrarySection
{
    public string Key { get; set; } = string.Empty;

    public string Title { get; set; } = string.Empty;

    public string Type { get; set; } = string.Empty;

    public string DisplayName =>
        string.IsNullOrWhiteSpace(Type)
            ? Title
            : $"{Title} ({Type})";
}
