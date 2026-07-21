using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using System;
using System.Collections.ObjectModel;
using System.Threading.Tasks;
using WalkerMediaManager.UI.Models;
using WalkerMediaManager.UI.Repositories;

namespace WalkerMediaManager.UI.Views;

public sealed partial class MoviesPage : Page
{
    private readonly MovieRepository _movieRepository = new();

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

    private async void AddMovie_Click(
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

        int.TryParse(YearBox.Text.Trim(), out int releaseYear);

        Movie movie = new()
        {
            Title = title,
            ReleaseYear = releaseYear,
            Rating = RatingBox.Text.Trim(),
            Genre = GenreBox.Text.Trim(),
            Director = DirectorBox.Text.Trim(),
            Runtime = 0,
            PlexGuid = string.Empty,
            IMDbId = string.Empty
        };

        try
        {
            movie.Id = await _movieRepository.AddAsync(movie);

            ClearForm();

            await RefreshMoviesAsync();

            ShowStatus(
                $"{movie.Title} was added successfully.",
                InfoBarSeverity.Success);
        }
        catch (Exception exception)
        {
            ShowStatus(
                $"The movie could not be saved: {exception.Message}",
                InfoBarSeverity.Error);
        }
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

    private void ClearForm()
    {
        TitleBox.Text = string.Empty;
        YearBox.Text = string.Empty;
        RatingBox.Text = string.Empty;
        GenreBox.Text = string.Empty;
        DirectorBox.Text = string.Empty;

        TitleBox.Focus(FocusState.Programmatic);
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