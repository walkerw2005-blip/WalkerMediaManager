using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Navigation;
using WalkerMediaManager.UI.Models;
using WalkerMediaManager.UI.Repositories;

namespace WalkerMediaManager.UI.Views;

public sealed partial class MovieDetailsPage : Page
{
    private readonly MovieRepository _movieRepository = new();
    private readonly OwnedCopyRepository _ownedCopyRepository = new();
    private Movie? _movie;
    private List<OwnedCopy> _ownedCopies = [];
    private OwnedCopy? _editingCopy;

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
            _movie = await _movieRepository.GetByIdAsync(movieId);
            if (_movie is null)
            {
                ShowError("This movie is no longer in the collection.");
                return;
            }

            Populate(_movie);
            await LoadOwnedCopiesAsync();
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
        ArtworkText.Text = BuildArtworkText(movie);
        PosterImage.ArtworkPath = movie.PosterPath;
        PosterImage.CacheKey = movie.PlexRatingKey;
        PlexRatingKeyText.Text = $"Plex rating key: {ValueOrFallback(movie.PlexRatingKey)}";
        PlexGuidText.Text = $"Plex GUID: {ValueOrFallback(movie.PlexGuid)}";
        IMDbText.Text = $"IMDb ID: {ValueOrFallback(movie.IMDbId)}";
        TMDbText.Text = $"TMDb ID: {(movie.TMDbId?.ToString() ?? "Not recorded")}";
    }

    private async Task LoadOwnedCopiesAsync()
    {
        if (_movie is null) return;

        _ownedCopies = await _ownedCopyRepository.GetForMovieAsync(_movie.Id);
        OwnedCopiesList.ItemsSource = _ownedCopies;
        NoCopiesText.Visibility = _ownedCopies.Count == 0 ? Visibility.Visible : Visibility.Collapsed;

        int count = _ownedCopies.Count;
        OwnershipText.Text = count switch
        {
            0 => "No owned copies recorded",
            1 => "1 owned copy",
            _ => $"{count} owned copies"
        };

        CopiesSummaryText.Text = count == 0
            ? "Track formats, editions, purchases, and shelf locations."
            : $"{count} {(count == 1 ? "copy" : "copies")} recorded";

        FormatText.Text = count == 0
            ? "Add a copy to track format and edition"
            : string.Join(", ", _ownedCopies
                .Select(copy => copy.Format)
                .Where(value => !string.IsNullOrWhiteSpace(value))
                .Distinct(StringComparer.OrdinalIgnoreCase));
    }

    private async void AddCopyButton_Click(object sender, RoutedEventArgs e)
    {
        if (_movie is null) return;

        _editingCopy = null;
        ClearCopyDialog();
        CopyDialogHeading.Text = "Add owned copy";
        CopyDialog.Title = _movie.Title;
        CopyDialog.XamlRoot = XamlRoot;
        await CopyDialog.ShowAsync();
    }

    private async void EditCopyButton_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not Button button ||
            !int.TryParse(button.Tag?.ToString(), out int copyId))
        {
            return;
        }

        _editingCopy = _ownedCopies.FirstOrDefault(copy => copy.Id == copyId);
        if (_editingCopy is null) return;

        FillCopyDialog(_editingCopy);
        CopyDialogHeading.Text = "Edit owned copy";
        CopyDialog.Title = _movie?.Title ?? "Movie";
        CopyDialog.XamlRoot = XamlRoot;
        await CopyDialog.ShowAsync();
    }

    private async void DeleteCopyButton_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not Button button ||
            !int.TryParse(button.Tag?.ToString(), out int copyId))
        {
            return;
        }

        OwnedCopy? copy = _ownedCopies.FirstOrDefault(item => item.Id == copyId);
        if (copy is null) return;

        ContentDialog confirmation = new()
        {
            Title = "Delete owned copy?",
            Content = $"Remove {copy.DisplayName} from this movie?",
            PrimaryButtonText = "Delete",
            CloseButtonText = "Cancel",
            DefaultButton = ContentDialogButton.Close,
            XamlRoot = XamlRoot
        };

        if (await confirmation.ShowAsync() != ContentDialogResult.Primary) return;

        try
        {
            await _ownedCopyRepository.DeleteAsync(copyId);
            await LoadOwnedCopiesAsync();
            ShowStatus("Owned copy deleted.", InfoBarSeverity.Success);
        }
        catch (Exception exception)
        {
            ShowStatus($"The owned copy could not be deleted: {exception.Message}", InfoBarSeverity.Error);
        }
    }

    private async void CopyDialog_PrimaryButtonClick(ContentDialog sender, ContentDialogButtonClickEventArgs args)
    {
        if (_movie is null) return;

        string format = GetComboText(FormatCombo);
        if (string.IsNullOrWhiteSpace(format))
        {
            args.Cancel = true;
            CopyDialogInfoBar.Message = "Format is required.";
            CopyDialogInfoBar.IsOpen = true;
            return;
        }

        ContentDialogButtonClickDeferral deferral = args.GetDeferral();
        try
        {
            OwnedCopy copy = _editingCopy ?? new OwnedCopy { MovieId = _movie.Id };
            copy.MovieId = _movie.Id;
            copy.Format = format;
            copy.Edition = EditionTextBox.Text.Trim();
            copy.Packaging = GetComboText(PackagingCombo);
            copy.Condition = GetComboText(ConditionCombo);
            copy.Store = StoreTextBox.Text.Trim();
            copy.PurchasePrice = double.IsNaN(PriceNumberBox.Value)
                ? null
                : Convert.ToDecimal(PriceNumberBox.Value);
            copy.PurchaseDate = PurchaseDatePicker.Date?.ToString("O") ?? string.Empty;
            copy.Location = LocationTextBox.Text.Trim();
            copy.Notes = NotesTextBox.Text.Trim();
            copy.IsDigital = DigitalCheckBox.IsChecked == true;
            copy.IsFavorite = FavoriteCheckBox.IsChecked == true;

            if (copy.Id == 0)
            {
                copy.Id = await _ownedCopyRepository.AddAsync(copy);
            }
            else
            {
                await _ownedCopyRepository.UpdateAsync(copy);
            }

            await LoadOwnedCopiesAsync();
            ShowStatus("Owned copy saved.", InfoBarSeverity.Success);
        }
        catch (Exception exception)
        {
            args.Cancel = true;
            CopyDialogInfoBar.Message = $"The owned copy could not be saved: {exception.Message}";
            CopyDialogInfoBar.IsOpen = true;
        }
        finally
        {
            deferral.Complete();
        }
    }

    private void ClearCopyDialog()
    {
        FormatCombo.SelectedIndex = -1;
        FormatCombo.Text = string.Empty;
        EditionTextBox.Text = string.Empty;
        PackagingCombo.SelectedIndex = -1;
        PackagingCombo.Text = string.Empty;
        ConditionCombo.SelectedIndex = -1;
        ConditionCombo.Text = string.Empty;
        StoreTextBox.Text = string.Empty;
        PriceNumberBox.Value = double.NaN;
        PurchaseDatePicker.Date = null;
        LocationTextBox.Text = string.Empty;
        NotesTextBox.Text = string.Empty;
        DigitalCheckBox.IsChecked = false;
        FavoriteCheckBox.IsChecked = false;
        CopyDialogInfoBar.IsOpen = false;
    }

    private void FillCopyDialog(OwnedCopy copy)
    {
        ClearCopyDialog();
        FormatCombo.Text = copy.Format;
        EditionTextBox.Text = copy.Edition;
        PackagingCombo.Text = copy.Packaging;
        ConditionCombo.Text = copy.Condition;
        StoreTextBox.Text = copy.Store;
        PriceNumberBox.Value = copy.PurchasePrice.HasValue
            ? Convert.ToDouble(copy.PurchasePrice.Value)
            : double.NaN;
        PurchaseDatePicker.Date = DateTimeOffset.TryParse(copy.PurchaseDate, out DateTimeOffset date)
            ? date
            : null;
        LocationTextBox.Text = copy.Location;
        NotesTextBox.Text = copy.Notes;
        DigitalCheckBox.IsChecked = copy.IsDigital;
        FavoriteCheckBox.IsChecked = copy.IsFavorite;
    }

    private static string GetComboText(ComboBox comboBox)
    {
        if (!string.IsNullOrWhiteSpace(comboBox.Text)) return comboBox.Text.Trim();
        return comboBox.SelectedItem?.ToString()?.Trim() ?? string.Empty;
    }

    private void BackButton_Click(object sender, RoutedEventArgs e)
    {
        if (Frame.CanGoBack) Frame.GoBack();
    }

    private void ShowError(string message)
    {
        ShowStatus(message, InfoBarSeverity.Error, "Unable to open movie");
        LoadingRing.IsActive = false;
        LoadingRing.Visibility = Visibility.Collapsed;
    }

    private void ShowStatus(string message, InfoBarSeverity severity, string title = "Movie collection")
    {
        StatusInfoBar.Title = title;
        StatusInfoBar.Message = message;
        StatusInfoBar.Severity = severity;
        StatusInfoBar.IsOpen = true;
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
