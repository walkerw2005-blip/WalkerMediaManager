namespace WalkerMediaManager.UI.Models;

public class TVShow
{
    public int Id { get; set; }

    public string Title { get; set; } = string.Empty;

    public int Year { get; set; }

    public int Seasons { get; set; }

    public int Episodes { get; set; }

    public bool Owned { get; set; }

    public string PlexRatingKey { get; set; } = string.Empty;

    public string PlexGuid { get; set; } = string.Empty;

    public int? TMDbId { get; set; }

    public string IMDbId { get; set; } = string.Empty;

    public string Studio { get; set; } = string.Empty;

    public string Summary { get; set; } = string.Empty;

    public string PosterPath { get; set; } = string.Empty;

    public string BackgroundPath { get; set; } = string.Empty;

    public string LastSynced { get; set; } = string.Empty;
}
