using System.Collections.Generic;

namespace WalkerMediaManager.UI.Models;

public sealed class PlexMovie
{
    public string PlexKey { get; set; } = string.Empty;
    public string PlexGuid { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public int ReleaseYear { get; set; }
    public string Rating { get; set; } = string.Empty;
    public int RuntimeMinutes { get; set; }
    public string Summary { get; set; } = string.Empty;
    public string Studio { get; set; } = string.Empty;
    public string ThumbPath { get; set; } = string.Empty;
    public List<string> Genres { get; set; } = [];
    public List<string> Directors { get; set; } = [];

    public string GenreDisplay => string.Join(", ", Genres);
    public string DirectorDisplay => string.Join(", ", Directors);
}
