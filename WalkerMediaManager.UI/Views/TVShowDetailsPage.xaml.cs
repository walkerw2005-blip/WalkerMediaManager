using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Navigation;
using WalkerMediaManager.UI.Models;
using WalkerMediaManager.UI.Repositories;

namespace WalkerMediaManager.UI.Views;

public sealed partial class TVShowDetailsPage : Page
{
    private readonly TVShowRepository _repository = new();
    private readonly TVSeasonRepository _seasonRepository = new();
    private TVShow? _show;
    private List<TVSeason> _seasons = [];

    public TVShowDetailsPage()
    {
        InitializeComponent();
    }

    protected override async void OnNavigatedTo(NavigationEventArgs e)
    {
        base.OnNavigatedTo(e);
        if (e.Parameter is not int showId)
        {
            ShowError("The TV show could not be identified.");
            return;
        }

        try
        {
            await LoadShowAsync(showId);
        }
        catch (Exception exception)
        {
            ShowError($"The TV show could not be loaded: {exception.Message}");
        }
    }

    private async System.Threading.Tasks.Task LoadShowAsync(int showId)
    {
        _show = await _repository.GetByIdAsync(showId);
        if (_show is null)
        {
            ShowError("The TV show could not be found.");
            return;
        }

        int seasonsToTrack = Math.Max(_show.Seasons, _show.TotalSeasons);
        await _seasonRepository.EnsureRowsAsync(_show.Id, seasonsToTrack, _show.Seasons);
        _show = await _repository.GetByIdAsync(showId) ?? _show;
        _seasons = await _seasonRepository.GetForShowAsync(_show.Id);
        DisplayShow(_show);
        SeasonsList.ItemsSource = _seasons;
    }

    private void DisplayShow(TVShow show)
    {
        TitleText.Text = show.Title;
        YearText.Text = show.YearDisplay;
        StudioText.Text = show.StudioDisplay;
        SummaryText.Text = show.SummaryDisplay;
        SeasonProgressText.Text = show.SeasonProgressDisplay;
        CompletionText.Text = show.MissingSeasonsDisplay;
        DigitalCoverageText.Text = show.DigitalCoverageDisplay;
        EpisodesText.Text = show.EpisodesDisplay;
        SeasonProgressBar.Value = show.CompletionPercentage;
        TotalSeasonsNumberBox.Value = show.TotalSeasons;
        PlexStatusText.Text = show.PlexStatus;
        PlexRatingKeyText.Text = $"Plex rating key: {ValueOrNotAvailable(show.PlexRatingKey)}";
        PlexGuidText.Text = $"Plex GUID: {ValueOrNotAvailable(show.PlexGuid)}";
        LastSyncedText.Text = $"Last synced: {show.LastSyncedDisplay}";
        IMDbText.Text = $"IMDb ID: {ValueOrNotAvailable(show.IMDbId)}";
        TMDbText.Text = $"TMDb ID: {(show.TMDbId?.ToString() ?? "Not available")}";
        TVMazeText.Text = $"TVMaze ID: {(show.TVMazeId?.ToString() ?? "Not available")}";
        SeriesStatusText.Text = $"Series status: {show.StatusDisplay}";
        AirDatesText.Text = $"Air dates: {show.AirDateDisplay}";
        NetworkText.Text = $"Network / streamer: {show.NetworkDisplay}";
        MetadataSyncedText.Text = $"Metadata refreshed: {FormatDate(show.MetadataLastSynced)}";
        PosterImage.ArtworkPath = show.PosterPath;
        PosterImage.CacheKey = !string.IsNullOrWhiteSpace(show.PlexRatingKey)
            ? show.PlexRatingKey
            : show.TVMazeId?.ToString() ?? show.Title;
    }

    private async void ApplyTotalSeasons_Click(object sender, RoutedEventArgs e)
    {
        if (_show is null)
        {
            return;
        }

        try
        {
            int totalSeasons = double.IsNaN(TotalSeasonsNumberBox.Value)
                ? 0
                : Math.Max(0, (int)TotalSeasonsNumberBox.Value);
            int ownedSeasons = _seasons.Count(season => season.IsOwned);

            if (totalSeasons > 0 && totalSeasons < ownedSeasons)
            {
                ShowError($"The total season count cannot be lower than the {ownedSeasons} seasons currently marked as owned.");
                return;
            }

            await _repository.SetTotalSeasonsAsync(_show.Id, totalSeasons, ownedSeasons);
            await LoadShowAsync(_show.Id);

            StatusInfoBar.Title = totalSeasons > 0 ? "Total season count saved" : "Total season count cleared";
            StatusInfoBar.Message = totalSeasons > 0
                ? $"{_show.Title} is now tracked as a {totalSeasons}-season series. Missing seasons were added to the list."
                : "The total season count is unknown. The app will show owned seasons without marking the series complete.";
            StatusInfoBar.Severity = InfoBarSeverity.Success;
            StatusInfoBar.IsOpen = true;
        }
        catch (Exception exception)
        {
            ShowError($"The total season count could not be saved: {exception.Message}");
        }
    }

    private async void SaveAllSeasons_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            foreach (TVSeason season in _seasons)
            {
                if (season.IsOwned && string.IsNullOrWhiteSpace(season.Format))
                {
                    season.Format = "DVD";
                }

                if (season.IsOwned)
                {
                    season.HasDigitalCopy = true;
                }
                else
                {
                    season.HasDigitalCopy = false;
                }

                await _seasonRepository.UpdateAsync(season);
            }

            if (_show is not null)
            {
                await LoadShowAsync(_show.Id);
            }

            StatusInfoBar.Title = "Season ownership saved";
            StatusInfoBar.Message = "The TV-series season details were updated.";
            StatusInfoBar.Severity = InfoBarSeverity.Success;
            StatusInfoBar.IsOpen = true;
        }
        catch (Exception exception)
        {
            ShowError($"The season details could not be saved: {exception.Message}");
        }
    }

    private static string ValueOrNotAvailable(string value) => string.IsNullOrWhiteSpace(value) ? "Not available" : value;

    private static string FormatDate(string value) =>
        DateTimeOffset.TryParse(value, out DateTimeOffset parsed)
            ? parsed.ToLocalTime().ToString("g")
            : "Not yet refreshed";

    private void BackButton_Click(object sender, RoutedEventArgs e)
    {
        if (Frame.CanGoBack)
        {
            Frame.GoBack();
        }
        else
        {
            Frame.Navigate(typeof(TvShowsPage));
        }
    }

    private void ShowError(string message)
    {
        StatusInfoBar.Title = "TV Series";
        StatusInfoBar.Message = message;
        StatusInfoBar.Severity = InfoBarSeverity.Error;
        StatusInfoBar.IsOpen = true;
    }
}
