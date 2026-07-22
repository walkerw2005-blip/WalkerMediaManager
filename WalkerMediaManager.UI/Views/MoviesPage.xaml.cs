using System;
using System.Collections.ObjectModel;
using System.Threading.Tasks;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using WalkerMediaManager.UI.Models;
using WalkerMediaManager.UI.Repositories;

namespace WalkerMediaManager.UI.Views;

public sealed partial class MoviesPage : Page
{
    private readonly MovieRepository _movieRepository = new();

    private Movie? _movieBeingEdited;

    public ObservableCollection<Movie> Movies { get; } = [];

    public MoviesPage()
    {
        InitializeComponent();

        Loaded += MoviesPage_Loaded;
    }

    private async void MoviesPage_Loaded(
        object sender,
        RoutedEventArgs e)
    {
        await RefreshMoviesAsync();
    }

    private async void SaveMovie_Click(
        object sender,
        RoutedEventArgs e)
    {
        string title = TitleBox.Text.Trim();

        if (string.IsNullOrWhiteSpace(title))
        {
            ShowStatus(
                "A movie title is required.",
                InfoBarSeverity.Warning);

            return;
        }

        if (!string.IsNullOrWhiteSpace(YearBox.Text) &&
            !int.TryParse(YearBox.Text.Trim(), out _))
        {
            ShowStatus(
                "Release year must be a number.",
                InfoBarSeverity.Warning);

            return;
        }

        int.TryParse(YearBox.Text.Trim(), out int releaseYear);

        try
        {
            if (_movieBeingEdited is null)
            {
                Movie movie = CreateMovieFromForm();

                movie.Id = await _movieRepository.AddAsync(movie);

                ShowStatus(
                    $"{movie.Title} was added successfully.",
                    InfoBarSeverity.Success);
            }
            else
            {
                UpdateMovieFromForm(_movieBeingEdited);

                await _movieRepository.UpdateAsync(_movieBeingEdited);

                ShowStatus(
                    $"{_movieBeingEdited.Title} was updated successfully.",
                    InfoBarSeverity.Success);
            }

            ResetForm();

            await RefreshMoviesAsync();
        }
        catch (Exception exception)
        {
            ShowStatus(
                $"The movie could not be saved: {exception.Message}",
                InfoBarSeverity.Error);
        }
    }

    private void EditMovie_Click(
        object sender,
        RoutedEventArgs e)
    {
        if (sender is not Button button ||
            button.Tag is not Movie movie)
        {
            return;
        }

        _movieBeingEdited = movie;

        FormTitleText.Text = "Edit Movie";
        SaveMovieButton.Content = "Save Changes";
        CancelEditButton.Visibility = Visibility.Visible;

        TitleBox.Text = movie.Title;
        YearBox.Text =
            movie.ReleaseYear == 0
                ? string.Empty
                : movie.ReleaseYear.ToString();

        RatingBox.Text = movie.Rating;
        GenreBox.Text = movie.Genre;
        DirectorBox.Text = movie.Director;

        TitleBox.Focus(FocusState.Programmatic);
    }

    private async void DeleteMovie_Click(
        object sender,
        RoutedEventArgs e)
    {
        if (sender is not Button button ||
            button.Tag is not Movie movie)
        {
            return;
        }

        ContentDialog confirmationDialog = new()
        {
            Title = "Delete movie?",
            Content = $"Remove {movie.Title} from Walker Media Manager?",
            PrimaryButtonText = "Delete",
            CloseButtonText = "Cancel",
            DefaultButton = ContentDialogButton.Close,
            XamlRoot = XamlRoot
        };

        ContentDialogResult result =
            await confirmationDialog.ShowAsync();

        if (result != ContentDialogResult.Primary)
        {
            return;
        }

        try
        {
            await _movieRepository.DeleteAsync(movie.Id);

            if (_movieBeingEdited?.Id == movie.Id)
            {
                ResetForm();
            }

            await RefreshMoviesAsync();

            ShowStatus(
                $"{movie.Title} was deleted.",
                InfoBarSeverity.Success);
        }
        catch (Exception exception)
        {
            ShowStatus(
                $"The movie could not be deleted: {exception.Message}",
                InfoBarSeverity.Error);
        }
    }

    private void CancelEdit_Click(
        object sender,
        RoutedEventArgs e)
    {
        ResetForm();
    }

    private Movie CreateMovieFromForm()
    {
        int.TryParse(YearBox.Text.Trim(), out int releaseYear);

        return new Movie
        {
            Title = TitleBox.Text.Trim(),
            ReleaseYear = releaseYear,
            Rating = RatingBox.Text.Trim(),
            Genre = GenreBox.Text.Trim(),
            Director = DirectorBox.Text.Trim(),
            Runtime = 0,
            PlexGuid = string.Empty,
            IMDbId = string.Empty
        };
    }

    private void UpdateMovieFromForm(Movie movie)
    {
        int.TryParse(YearBox.Text.Trim(), out int releaseYear);

        movie.Title = TitleBox.Text.Trim();
        movie.ReleaseYear = releaseYear;
        movie.Rating = RatingBox.Text.Trim();
        movie.Genre = GenreBox.Text.Trim();
        movie.Director = DirectorBox.Text.Trim();
    }

    private async Task RefreshMoviesAsync()
    {
        Movies.Clear();

        foreach (Movie movie in await _movieRepository.GetAllAsync())
        {
            Movies.Add(movie);
        }

        MovieCountText.Text =
            Movies.Count == 1
                ? "1 movie"
                : $"{Movies.Count} movies";
    }

    private void ResetForm()
    {
        _movieBeingEdited = null;

        FormTitleText.Text = "Add Movie";
        SaveMovieButton.Content = "Add Movie";
        CancelEditButton.Visibility = Visibility.Collapsed;

        TitleBox.Text = string.Empty;
        YearBox.Text = string.Empty;
        RatingBox.Text = string.Empty;
        GenreBox.Text = string.Empty;
        DirectorBox.Text = string.Empty;
    }

    private void ShowStatus(
        string message,
        InfoBarSeverity severity)
    {
        StatusInfoBar.Message = message;
        StatusInfoBar.Severity = severity;
        StatusInfoBar.IsOpen = true;
    }
}