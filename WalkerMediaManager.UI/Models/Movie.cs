namespace WalkerMediaManager.UI.Models;

public sealed class Movie
{
    public int Id { get; set; }
    public string Title { get; set; } = string.Empty;
    public int ReleaseYear { get; set; }
    public string Rating { get; set; } = string.Empty;
    public int Runtime { get; set; }
    public string Genre { get; set; } = string.Empty;
    public string Director { get; set; } = string.Empty;
    public string PlexRatingKey { get; set; } = string.Empty;
    public string PlexGuid { get; set; } = string.Empty;
    public int? TMDbId { get; set; }
    public string IMDbId { get; set; } = string.Empty;
    public string SortTitle { get; set; } = string.Empty;
    public string Studio { get; set; } = string.Empty;
    public string Summary { get; set; } = string.Empty;
    public string PosterPath { get; set; } = string.Empty;
    public string BackgroundPath { get; set; } = string.Empty;
    public string LastSynced { get; set; } = string.Empty;
    public string Format { get; set; } = string.Empty;
    public bool Owned { get; set; } = true;

    public string YearDisplay => ReleaseYear > 0 ? ReleaseYear.ToString() : "Year unknown";
    public string RuntimeDisplay => Runtime > 0 ? $"{Runtime} min" : string.Empty;
    public string RatingDisplay => string.IsNullOrWhiteSpace(Rating) ? "Not rated" : Rating;
}
