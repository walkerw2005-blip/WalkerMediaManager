using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using WalkerMediaManager.UI.Controls;
using WalkerMediaManager.UI.Models;
using WalkerMediaManager.UI.Repositories;
using WalkerMediaManager.UI.Services;

namespace WalkerMediaManager.UI.Views;

public sealed partial class SlideshowsPage : Page
{
    private const string ViewModeKey = "SlideshowsViewMode";
    private const string SortModeKey = "SlideshowsSortMode";

    private readonly MovieRepository _movieRepository = new();
    private readonly MovieSpreadsheetImportService _spreadsheetImportService = new();
    private readonly List<Movie> _allMovies = [];
    private Movie? _movieBeingEdited;
    private string _duplicateOverrideKey = string.Empty;
    private bool _isLoading;

    public ObservableCollection<Movie> DisplayMovies { get; } = [];

    public SlideshowsPage()
    {
        InitializeComponent();
        Loaded += SlideshowsPage_Loaded;
    }

    private async void SlideshowsPage_Loaded(object sender, RoutedEventArgs e)
    {
        if (_isLoading) return;
        _isLoading = true;

        RestorePreferences();

        try
        {
            await RefreshMoviesAsync();
        }
        catch (Exception exception)
        {
            DiagnosticsService.LogException("SlideshowsPage failed to load the slide show list.", exception);
            MovieCountText.Text = "Slide show load failed";
            VisibleCountText.Text = "0 shown";
            ShowStatus(
                $"The slide show list could not be loaded: {exception.Message}. A diagnostic log was written to {DiagnosticsService.LogFilePath}",
                InfoBarSeverity.Error);
        }
        finally
        {
            _isLoading = false;
        }
    }

    private async Task RefreshMoviesAsync()
    {
        _allMovies.Clear();
        _allMovies.AddRange(await _movieRepository.GetSlideshowsAsync());
        ApplyFilterAndSort();
        MovieCountText.Text = _allMovies.Count == 1 ? "1 slide show" : $"{_allMovies.Count} slide shows";
    }

    private void ApplyFilterAndSort()
    {
        string query = SearchBox.Text.Trim();
        IEnumerable<Movie> movies = _allMovies;

        if (!string.IsNullOrWhiteSpace(query))
        {
            movies = movies.Where(movie => MediaSearchService.Matches(
                query,
                movie.Title,
                movie.SortTitle,
                movie.ReleaseYear,
                movie.Rating,
                movie.Genre,
                movie.Director,
                movie.Studio,
                movie.Summary,
                movie.Format,
                movie.PlexLibraryTitle,
                movie.IMDbId,
                movie.TMDbId));
        }

        string filter = (FilterComboBox?.SelectedItem as ComboBoxItem)?.Tag?.ToString() ?? "All";
        movies = filter switch
        {
            "Owned" => movies.Where(movie => movie.Owned),
            "NotOwned" => movies.Where(movie => !movie.Owned),
            "MissingArtwork" => movies.Where(movie => string.IsNullOrWhiteSpace(movie.PosterPath)),
            "PlexLinked" => movies.Where(movie => !string.IsNullOrWhiteSpace(movie.PlexRatingKey) || !string.IsNullOrWhiteSpace(movie.PlexGuid)),
            "NotPlexLinked" => movies.Where(movie => string.IsNullOrWhiteSpace(movie.PlexRatingKey) && string.IsNullOrWhiteSpace(movie.PlexGuid)),
            _ => movies
        };

        string sort = (SortComboBox.SelectedItem as ComboBoxItem)?.Tag?.ToString() ?? "TitleAscending";
        movies = sort switch
        {
            "TitleDescending" => movies.OrderByDescending(movie => SortTitle(movie), StringComparer.OrdinalIgnoreCase),
            "YearDescending" => movies.OrderByDescending(movie => movie.ReleaseYear).ThenBy(movie => movie.Title),
            "YearAscending" => movies.OrderBy(movie => movie.ReleaseYear == 0 ? int.MaxValue : movie.ReleaseYear).ThenBy(movie => movie.Title),
            "RuntimeDescending" => movies.OrderByDescending(movie => movie.Runtime).ThenBy(movie => movie.Title),
            "RecentlySynced" => movies.OrderByDescending(movie => ParseDate(movie.LastSynced)).ThenBy(movie => movie.Title),
            _ => movies.OrderBy(movie => SortTitle(movie), StringComparer.OrdinalIgnoreCase)
        };

        DisplayMovies.Clear();
        foreach (Movie movie in movies)
        {
            DisplayMovies.Add(movie);
        }

        VisibleCountText.Text = DisplayMovies.Count == _allMovies.Count
            ? $"{DisplayMovies.Count} shown"
            : $"{DisplayMovies.Count} of {_allMovies.Count} shown";
    }

    private static string SortTitle(Movie movie) =>
        string.IsNullOrWhiteSpace(movie.SortTitle) ? movie.Title : movie.SortTitle;

    private static DateTimeOffset ParseDate(string value) =>
        DateTimeOffset.TryParse(value, out DateTimeOffset date) ? date : DateTimeOffset.MinValue;

    private void SearchBox_TextChanged(AutoSuggestBox sender, AutoSuggestBoxTextChangedEventArgs args)
    {
        if (args.Reason == AutoSuggestionBoxTextChangeReason.UserInput)
        {
            ApplyFilterAndSort();
        }
    }

    private void FilterComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (IsLoaded)
        {
            ApplyFilterAndSort();
        }
    }

    private void SortComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (!IsLoaded) return;

        SavePreference(SortModeKey, SortComboBox.SelectedIndex);
        ApplyFilterAndSort();
    }

    private void GridViewToggle_Click(object sender, RoutedEventArgs e)
    {
        bool showGrid = GridViewToggle.IsChecked == true;
        GridViewToggle.Content = showGrid ? "Grid" : "List";
        MovieGridView.Visibility = showGrid ? Visibility.Visible : Visibility.Collapsed;
        MovieListView.Visibility = showGrid ? Visibility.Collapsed : Visibility.Visible;

        SavePreference(ViewModeKey, showGrid ? "Grid" : "List");
    }

    private void RestorePreferences()
    {
        int sortIndex = SettingsService.GetInt32(SortModeKey, 0);
        if (sortIndex < 0 || sortIndex >= SortComboBox.Items.Count)
        {
            sortIndex = 0;
        }

        bool showGrid = !string.Equals(
            SettingsService.GetString(ViewModeKey, "Grid"),
            "List",
            StringComparison.OrdinalIgnoreCase);

        SortComboBox.SelectedIndex = sortIndex;
        GridViewToggle.IsChecked = showGrid;
        GridViewToggle.Content = showGrid ? "Grid" : "List";
        MovieGridView.Visibility = showGrid ? Visibility.Visible : Visibility.Collapsed;
        MovieListView.Visibility = showGrid ? Visibility.Collapsed : Visibility.Visible;
    }

    private static void SavePreference(string key, object value)
    {
        try
        {
            if (value is int intValue)
            {
                SettingsService.SetInt32(key, intValue);
            }
            else
            {
                SettingsService.SetString(key, value?.ToString() ?? string.Empty);
            }
        }
        catch (Exception exception)
        {
            DiagnosticsService.LogException(
                $"SlideshowsPage could not save the preference '{key}'.",
                exception);
        }
    }

    private async void ShowEditorButton_Click(object sender, RoutedEventArgs e)
    {
        ResetEditor();
        await MovieEditorDialog.ShowAsync();
    }

    private void MediaCard_OpenRequested(object? sender, Movie movie) => OpenMovie(movie);
    private async void MediaCard_EditRequested(object? sender, Movie movie) => await ShowEditDialogAsync(movie);
    private async void MediaCard_DeleteRequested(object? sender, Movie movie) => await ConfirmDeleteAsync(movie);

    private void OpenMovie_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button { Tag: Movie movie }) OpenMovie(movie);
    }

    private void OpenMovie(Movie movie)
    {
        Frame.Navigate(typeof(MovieDetailsPage), movie.Id);
    }

    private async void EditMovie_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button { Tag: Movie movie }) await ShowEditDialogAsync(movie);
    }

    private async void DeleteMovie_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button { Tag: Movie movie }) await ConfirmDeleteAsync(movie);
    }

    private async Task ShowEditDialogAsync(Movie movie)
    {
        _movieBeingEdited = movie;
        MovieEditorDialog.Title = "Edit Movie";
        TitleBox.Text = movie.Title;
        YearBox.Text = movie.ReleaseYear > 0 ? movie.ReleaseYear.ToString() : string.Empty;
        RatingBox.Text = movie.Rating;
        GenreBox.Text = movie.Genre;
        DirectorBox.Text = movie.Director;
        await MovieEditorDialog.ShowAsync();
    }

    private async void MovieEditorDialog_PrimaryButtonClick(ContentDialog sender, ContentDialogButtonClickEventArgs args)
    {
        ContentDialogButtonClickDeferral deferral = args.GetDeferral();

        try
        {
            string title = TitleBox.Text.Trim();
            if (string.IsNullOrWhiteSpace(title))
            {
                args.Cancel = true;
                ShowStatus("A movie title is required.", InfoBarSeverity.Warning);
                return;
            }

            if (!string.IsNullOrWhiteSpace(YearBox.Text) &&
                !int.TryParse(YearBox.Text.Trim(), out _))
            {
                args.Cancel = true;
                ShowStatus("Release year must be a number.", InfoBarSeverity.Warning);
                return;
            }

            int.TryParse(YearBox.Text.Trim(), out int year);

            Movie? possibleDuplicate =
                MediaDuplicateService.FindPossibleDuplicate(
                    _allMovies,
                    title,
                    year,
                    _movieBeingEdited?.Id);

            string duplicateKey =
                MediaDuplicateService.CreateKey(title, year);

            if (possibleDuplicate is not null &&
                !string.Equals(
                    _duplicateOverrideKey,
                    duplicateKey,
                    StringComparison.Ordinal))
            {
                _duplicateOverrideKey = duplicateKey;
                args.Cancel = true;

                string existingYear =
                    possibleDuplicate.ReleaseYear > 0
                        ? $" ({possibleDuplicate.ReleaseYear})"
                        : string.Empty;

                ShowStatus(
                    $"Possible duplicate: {possibleDuplicate.Title}{existingYear} already exists. " +
                    "Select Save again to add it anyway.",
                    InfoBarSeverity.Warning);
                return;
            }

            _duplicateOverrideKey = string.Empty;

            Movie movie = _movieBeingEdited ?? new Movie { Owned = true, PlexLibraryTitle = "Slide Shows" };
            movie.Title = title;
            movie.ReleaseYear = year;
            movie.Rating = RatingBox.Text.Trim();
            movie.Genre = GenreBox.Text.Trim();
            movie.Director = DirectorBox.Text.Trim();

            if (_movieBeingEdited is null)
            {
                movie.Id = await _movieRepository.AddAsync(movie);
                ShowStatus($"{movie.Title} was added.", InfoBarSeverity.Success);
            }
            else
            {
                await _movieRepository.UpdateAsync(movie);
                ShowStatus($"{movie.Title} was updated.", InfoBarSeverity.Success);
            }

            await RefreshMoviesAsync();
            ResetEditor();
        }
        catch (Exception exception)
        {
            args.Cancel = true;
            DiagnosticsService.LogException("SlideshowsPage failed to save a slide show.", exception);
            ShowStatus($"The movie could not be saved: {exception.Message}", InfoBarSeverity.Error);
        }
        finally
        {
            deferral.Complete();
        }
    }

    private async Task ConfirmDeleteAsync(Movie movie)
    {
        ContentDialog dialog = new()
        {
            Title = "Delete movie?",
            Content = $"Remove {movie.Title} from Walker Media Manager?",
            PrimaryButtonText = "Delete",
            CloseButtonText = "Cancel",
            DefaultButton = ContentDialogButton.Close,
            XamlRoot = XamlRoot
        };

        if (await dialog.ShowAsync() != ContentDialogResult.Primary) return;

        try
        {
            await _movieRepository.DeleteAsync(movie.Id);
            await RefreshMoviesAsync();
            ShowStatus($"{movie.Title} was deleted.", InfoBarSeverity.Success);
        }
        catch (Exception exception)
        {
            DiagnosticsService.LogException("SlideshowsPage failed to delete a slide show.", exception);
            ShowStatus($"The movie could not be deleted: {exception.Message}", InfoBarSeverity.Error);
        }
    }

    private async void ImportSpreadsheetButton_Click(object sender, RoutedEventArgs e)
    {
        string filePath = SpreadsheetPathBox.Text.Trim();
        if (string.IsNullOrWhiteSpace(filePath))
        {
            ShowImportStatus("Paste the full path to the Excel spreadsheet.", InfoBarSeverity.Warning);
            return;
        }

        SetImportingState(true);

        try
        {
            MovieImportResult result = await _spreadsheetImportService.ImportAsync(filePath);
            await RefreshMoviesAsync();
            ShowImportStatus(
                result.Summary,
                result.FailedCount > 0 ? InfoBarSeverity.Warning : InfoBarSeverity.Success);
        }
        catch (Exception exception)
        {
            DiagnosticsService.LogException("SlideshowsPage failed to import a spreadsheet.", exception);
            ShowImportStatus($"The spreadsheet could not be imported: {exception.Message}", InfoBarSeverity.Error);
        }
        finally
        {
            SetImportingState(false);
        }
    }

    private void ResetEditor()
    {
        _movieBeingEdited = null;
        _duplicateOverrideKey = string.Empty;
        MovieEditorDialog.Title = "Add Movie";
        TitleBox.Text = string.Empty;
        YearBox.Text = string.Empty;
        RatingBox.Text = string.Empty;
        GenreBox.Text = string.Empty;
        DirectorBox.Text = string.Empty;
    }

    private void SetImportingState(bool importing)
    {
        ImportSpreadsheetButton.IsEnabled = !importing;
        SpreadsheetPathBox.IsEnabled = !importing;
        ImportProgressRing.IsActive = importing;
        ImportProgressRing.Visibility = importing ? Visibility.Visible : Visibility.Collapsed;
    }

    private void ShowImportStatus(string message, InfoBarSeverity severity)
    {
        ImportInfoBar.Message = message;
        ImportInfoBar.Severity = severity;
        ImportInfoBar.IsOpen = true;
    }

    private void ShowStatus(string message, InfoBarSeverity severity)
    {
        StatusInfoBar.Message = message;
        StatusInfoBar.Severity = severity;
        StatusInfoBar.IsOpen = true;
    }
}
