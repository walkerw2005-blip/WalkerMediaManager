using System;

namespace WalkerMediaManager.UI.Models;

public class TVShow
{
    public int Id { get; set; }
    public string Title { get; set; } = string.Empty;
    public int Year { get; set; }
    public int Seasons { get; set; }
    public int TotalSeasons { get; set; }
    public int Episodes { get; set; }
    public bool Owned { get; set; } = true;
    public string PlexRatingKey { get; set; } = string.Empty;
    public string PlexGuid { get; set; } = string.Empty;
    public int? TMDbId { get; set; }
    public int? TVMazeId { get; set; }
    public string IMDbId { get; set; } = string.Empty;
    public string Studio { get; set; } = string.Empty;
    public string Summary { get; set; } = string.Empty;
    public string PosterPath { get; set; } = string.Empty;
    public string BackgroundPath { get; set; } = string.Empty;
    public string LastSynced { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public string FirstAirDate { get; set; } = string.Empty;
    public string LastAirDate { get; set; } = string.Empty;
    public string Network { get; set; } = string.Empty;
    public string MetadataLastSynced { get; set; } = string.Empty;
    public int TrackedSeasons { get; set; }
    public int OwnedSeasons { get; set; }
    public int DigitalSeasons { get; set; }

    public string YearDisplay => Year > 0 ? Year.ToString() : "Year unknown";
    public string SeasonsDisplay => Seasons == 1 ? "1 season in Plex" : $"{Seasons} seasons in Plex";
    public string EpisodesDisplay => Episodes == 1 ? "1 episode" : $"{Episodes} episodes";
    public bool HasKnownTotalSeasons => TotalSeasons > 0;
    public int ExpectedSeasons => HasKnownTotalSeasons ? TotalSeasons : TrackedSeasons;
    public int MissingSeasons => HasKnownTotalSeasons ? Math.Max(0, TotalSeasons - OwnedSeasons) : 0;
    public double CompletionPercentage => HasKnownTotalSeasons && TotalSeasons > 0
        ? Math.Min(100.0, (double)OwnedSeasons / TotalSeasons * 100.0)
        : 0;
    public string SeasonProgressDisplay => HasKnownTotalSeasons
        ? $"{OwnedSeasons} of {TotalSeasons} seasons owned"
        : OwnedSeasons == 1 ? "1 season owned" : $"{OwnedSeasons} seasons owned";
    public string CompletionDisplay => HasKnownTotalSeasons
        ? $"{CompletionPercentage:0}% complete"
        : "Total season count not set";
    public string MissingSeasonsDisplay => !HasKnownTotalSeasons
        ? "Total not set"
        : MissingSeasons == 0
            ? "Complete"
            : MissingSeasons == 1 ? "1 season missing" : $"{MissingSeasons} seasons missing";
    public string DigitalCoverageDisplay => OwnedSeasons == 0 ? "No owned seasons" : $"{DigitalSeasons} of {OwnedSeasons} digital";
    public string StudioDisplay => string.IsNullOrWhiteSpace(Studio) ? "Studio unknown" : Studio;
    public string NetworkDisplay => string.IsNullOrWhiteSpace(Network) ? StudioDisplay : Network;
    public string StatusDisplay => string.IsNullOrWhiteSpace(Status) ? "Status unknown" : Status;
    public string AirDateDisplay => string.IsNullOrWhiteSpace(FirstAirDate)
        ? "Air dates unknown"
        : string.IsNullOrWhiteSpace(LastAirDate)
            ? $"Premiered {FirstAirDate}"
            : $"{FirstAirDate} to {LastAirDate}";
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
