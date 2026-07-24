using System;

namespace WalkerMediaManager.UI.Models;

public class TVShow
{
    public int Id { get; set; }
    public string Title { get; set; } = string.Empty;
    public int Year { get; set; }
    public int Seasons { get; set; }
    public int Episodes { get; set; }
    public bool Owned { get; set; } = true;
    public string PlexRatingKey { get; set; } = string.Empty;
    public string PlexGuid { get; set; } = string.Empty;
    public int? TMDbId { get; set; }
    public string IMDbId { get; set; } = string.Empty;
    public string Studio { get; set; } = string.Empty;
    public string Summary { get; set; } = string.Empty;
    public string PosterPath { get; set; } = string.Empty;
    public string BackgroundPath { get; set; } = string.Empty;
    public string LastSynced { get; set; } = string.Empty;

    public string YearDisplay => Year > 0 ? Year.ToString() : "Year unknown";
    public string SeasonsDisplay => Seasons == 1 ? "1 season" : $"{Seasons} seasons";
    public string EpisodesDisplay => Episodes == 1 ? "1 episode" : $"{Episodes} episodes";
    public string StudioDisplay => string.IsNullOrWhiteSpace(Studio) ? "Studio unknown" : Studio;
    public string SummaryDisplay => string.IsNullOrWhiteSpace(Summary) ? "No summary is available." : Summary;
    public string PlexStatus => string.IsNullOrWhiteSpace(PlexGuid) && string.IsNullOrWhiteSpace(PlexRatingKey)
        ? "Not linked to Plex"
        : "Linked to Plex";

    public string LastSyncedDisplay
    {
        get
        {
            if (string.IsNullOrWhiteSpace(LastSynced))
            {
                return "Never synced";
            }

            return DateTimeOffset.TryParse(LastSynced, out DateTimeOffset value)
                ? value.ToLocalTime().ToString("g")
                : LastSynced;
        }
    }
}
