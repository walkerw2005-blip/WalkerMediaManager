namespace WalkerMediaManager.UI.Models;

public class Movie
{
    public int Id { get; set; }

    public string Title { get; set; } = "";

    public int ReleaseYear { get; set; }

    public string Rating { get; set; } = "";

    public int Runtime { get; set; }

    public string Genre { get; set; } = "";

    public string Director { get; set; } = "";

    public string Format { get; set; } = "";

    public bool Owned { get; set; }

    public string PlexGuid { get; set; } = "";

    public int? TMDbId { get; set; }

    public string IMDbId { get; set; } = "";
}