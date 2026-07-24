using System;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Navigation;
using WalkerMediaManager.UI.Models;
using WalkerMediaManager.UI.Repositories;

namespace WalkerMediaManager.UI.Views;

public sealed partial class MovieDetailsPage : Page
{
    private readonly MovieRepository _movieRepository = new();

    public MovieDetailsPage()
    {
        InitializeComponent();
    }

    protected override async void OnNavigatedTo(NavigationEventArgs e)
    {
        base.OnNavigatedTo(e);

        if (e.Parameter is not int movieId)
        {
            ShowError("The selected movie could not be identified.");
            return;
        }

        try
        {
            Movie? movie = await _movieRepository.GetByIdAsync(movieId);
            if (movie is null)
            {
                ShowError("This movie is no longer in the collection.");
                return;
            }

            Populate(movie);
        }
        catch (Exception exception)
        {
            ShowError($"Movie details could not be loaded: {exception.Message}");
        }
        finally
        {
            LoadingRing.IsActive = false;
            LoadingRing.Visibility = Visibility.Collapsed;
        }
    }

    private void Populate(Movie movie)
    {
        PageTitleText.Text = movie.Title;
        TitleText.Text = movie.Title;
        YearText.Text = movie.YearDisplay;
        RatingText.Text = movie.RatingDisplay;
        RuntimeText.Text = string.IsNullOrWhiteSpace(movie.RuntimeDisplay) ? "Runtime unknown" : movie.RuntimeDisplay;
        SummaryText.Text = ValueOrFallback(movie.Summary, "No summary is available.");
        GenreText.Text = ValueOrFallback(movie.Genre);
        DirectorText.Text = ValueOrFallback(movie.Director);
        StudioText.Text = ValueOrFallback(movie.Studio);
        SortTitleText.Text = ValueOrFallback(movie.SortTitle);
        LastSyncedText.Text = FormatDate(movie.LastSynced);
        OwnershipText.Text = movie.Owned ? "Owned" : "Not owned";
        FormatText.Text = ValueOrFallback(movie.Format, "Format not recorded");
        ArtworkText.Text = BuildArtworkText(movie);
        PlexRatingKeyText.Text = $"Plex rating key: {ValueOrFallback(movie.PlexRatingKey)}";
        PlexGuidText.Text = $"Plex GUID: {ValueOrFallback(movie.PlexGuid)}";
        IMDbText.Text = $"IMDb ID: {ValueOrFallback(movie.IMDbId)}";
        TMDbText.Text = $"TMDb ID: {(movie.TMDbId?.ToString() ?? "Not recorded")}";
    }

    private void BackButton_Click(object sender, RoutedEventArgs e)
    {
        if (Frame.CanGoBack) Frame.GoBack();
    }

    private void ShowError(string message)
    {
        StatusInfoBar.Title = "Unable to open movie";
        StatusInfoBar.Message = message;
        StatusInfoBar.Severity = InfoBarSeverity.Error;
        StatusInfoBar.IsOpen = true;
        LoadingRing.IsActive = false;
        LoadingRing.Visibility = Visibility.Collapsed;
    }

    private static string ValueOrFallback(string value, string fallback = "Not recorded") =>
        string.IsNullOrWhiteSpace(value) ? fallback : value;

    private static string FormatDate(string value) =>
        DateTimeOffset.TryParse(value, out DateTimeOffset date)
            ? date.ToLocalTime().ToString("g")
            : "Never";

    private static string BuildArtworkText(Movie movie)
    {
        string poster = ValueOrFallback(movie.PosterPath);
        string background = ValueOrFallback(movie.BackgroundPath);
        return $"Poster: {poster}\nBackground: {background}";
    }
}
