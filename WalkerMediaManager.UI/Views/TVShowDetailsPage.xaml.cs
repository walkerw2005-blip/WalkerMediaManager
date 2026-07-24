using System;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Navigation;
using WalkerMediaManager.UI.Models;
using WalkerMediaManager.UI.Repositories;

namespace WalkerMediaManager.UI.Views;

public sealed partial class TVShowDetailsPage : Page
{
    private readonly TVShowRepository _repository = new();

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
            TVShow? show = await _repository.GetByIdAsync(showId);
            if (show is null)
            {
                ShowError("The TV show could not be found.");
                return;
            }

            DisplayShow(show);
        }
        catch (Exception exception)
        {
            ShowError($"The TV show could not be loaded: {exception.Message}");
        }
    }

    private void DisplayShow(TVShow show)
    {
        TitleText.Text = show.Title;
        YearText.Text = show.YearDisplay;
        SeasonsText.Text = show.SeasonsDisplay;
        EpisodesText.Text = show.EpisodesDisplay;
        StudioText.Text = show.StudioDisplay;
        SummaryText.Text = show.SummaryDisplay;
        PlexStatusText.Text = show.PlexStatus;
        PlexRatingKeyText.Text = $"Plex rating key: {ValueOrNotAvailable(show.PlexRatingKey)}";
        PlexGuidText.Text = $"Plex GUID: {ValueOrNotAvailable(show.PlexGuid)}";
        LastSyncedText.Text = $"Last synced: {show.LastSyncedDisplay}";
        IMDbText.Text = $"IMDb ID: {ValueOrNotAvailable(show.IMDbId)}";
        TMDbText.Text = $"TMDb ID: {(show.TMDbId?.ToString() ?? "Not available")}";
        PosterPathText.Text = $"Poster path: {ValueOrNotAvailable(show.PosterPath)}";
        BackgroundPathText.Text = $"Background path: {ValueOrNotAvailable(show.BackgroundPath)}";
    }

    private static string ValueOrNotAvailable(string value) =>
        string.IsNullOrWhiteSpace(value) ? "Not available" : value;

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
        StatusInfoBar.Message = message;
        StatusInfoBar.Severity = InfoBarSeverity.Error;
        StatusInfoBar.IsOpen = true;
    }
}
