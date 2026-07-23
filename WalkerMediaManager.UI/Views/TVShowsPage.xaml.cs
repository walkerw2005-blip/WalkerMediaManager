using System;
using System.Collections.ObjectModel;
using System.Threading.Tasks;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using WalkerMediaManager.UI.Models;
using WalkerMediaManager.UI.Repositories;

namespace WalkerMediaManager.UI.Views;

public sealed partial class TvShowsPage : Page
{
    private readonly TVShowRepository _tvShowRepository = new();

    private TVShow? _showBeingEdited;

    public ObservableCollection<TVShow> Shows { get; } = [];

    public TvShowsPage()
    {
        InitializeComponent();

        Loaded += TvShowsPage_Loaded;
    }

    private async void TvShowsPage_Loaded(
        object sender,
        RoutedEventArgs e)
    {
        await RefreshShowsAsync();
    }

    private async void SaveShow_Click(
        object sender,
        RoutedEventArgs e)
    {
        string title = TitleBox.Text.Trim();

        if (string.IsNullOrWhiteSpace(title))
        {
            ShowStatus(
                "A TV-series title is required.",
                InfoBarSeverity.Warning);

            return;
        }

        if (!TryReadNonNegativeNumber(
                SeasonsBox.Text,
                "Seasons owned",
                out int seasons))
        {
            return;
        }

        if (!TryReadNonNegativeNumber(
                EpisodesBox.Text,
                "Episodes owned",
                out int episodes))
        {
            return;
        }

        try
        {
            if (_showBeingEdited is null)
            {
                TVShow show = new()
                {
                    Title = title,
                    Seasons = seasons,
                    Episodes = episodes,
                    Owned = true,
                    PlexGuid = string.Empty,
                    TMDbId = null
                };

                show.Id = await _tvShowRepository.AddAsync(show);

                ShowStatus(
                    $"{show.Title} was added successfully.",
                    InfoBarSeverity.Success);
            }
            else
            {
                _showBeingEdited.Title = title;
                _showBeingEdited.Seasons = seasons;
                _showBeingEdited.Episodes = episodes;

                await _tvShowRepository.UpdateAsync(_showBeingEdited);

                ShowStatus(
                    $"{_showBeingEdited.Title} was updated successfully.",
                    InfoBarSeverity.Success);
            }

            ResetForm();
            await RefreshShowsAsync();
        }
        catch (Exception exception)
        {
            ShowStatus(
                $"The TV show could not be saved: {exception.Message}",
                InfoBarSeverity.Error);
        }
    }

    private void EditShow_Click(
        object sender,
        RoutedEventArgs e)
    {
        if (sender is not Button button ||
            button.Tag is not TVShow show)
        {
            return;
        }

        _showBeingEdited = show;

        FormTitleText.Text = "Edit TV Show";
        SaveShowButton.Content = "Save Changes";
        CancelEditButton.Visibility = Visibility.Visible;

        TitleBox.Text = show.Title;
        SeasonsBox.Text = show.Seasons.ToString();
        EpisodesBox.Text = show.Episodes.ToString();

        TitleBox.Focus(FocusState.Programmatic);
    }

    private async void DeleteShow_Click(
        object sender,
        RoutedEventArgs e)
    {
        if (sender is not Button button ||
            button.Tag is not TVShow show)
        {
            return;
        }

        ContentDialog dialog = new()
        {
            Title = "Delete TV show?",
            Content =
                $"Remove {show.Title} from Walker Media Manager?",
            PrimaryButtonText = "Delete",
            CloseButtonText = "Cancel",
            DefaultButton = ContentDialogButton.Close,
            XamlRoot = XamlRoot
        };

        ContentDialogResult result = await dialog.ShowAsync();

        if (result != ContentDialogResult.Primary)
        {
            return;
        }

        try
        {
            await _tvShowRepository.DeleteAsync(show.Id);

            if (_showBeingEdited?.Id == show.Id)
            {
                ResetForm();
            }

            await RefreshShowsAsync();

            ShowStatus(
                $"{show.Title} was deleted.",
                InfoBarSeverity.Success);
        }
        catch (Exception exception)
        {
            ShowStatus(
                $"The TV show could not be deleted: {exception.Message}",
                InfoBarSeverity.Error);
        }
    }

    private void CancelEdit_Click(
        object sender,
        RoutedEventArgs e)
    {
        ResetForm();
    }

    private bool TryReadNonNegativeNumber(
        string text,
        string fieldName,
        out int value)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            value = 0;
            return true;
        }

        if (!int.TryParse(text.Trim(), out value) || value < 0)
        {
            ShowStatus(
                $"{fieldName} must be a non-negative whole number.",
                InfoBarSeverity.Warning);

            return false;
        }

        return true;
    }

    private async Task RefreshShowsAsync()
    {
        Shows.Clear();

        foreach (TVShow show in await _tvShowRepository.GetAllAsync())
        {
            Shows.Add(show);
        }

        ShowCountText.Text =
            Shows.Count == 1
                ? "1 TV show"
                : $"{Shows.Count} TV shows";
    }

    private void ResetForm()
    {
        _showBeingEdited = null;

        FormTitleText.Text = "Add TV Show";
        SaveShowButton.Content = "Add TV Show";
        CancelEditButton.Visibility = Visibility.Collapsed;

        TitleBox.Text = string.Empty;
        SeasonsBox.Text = string.Empty;
        EpisodesBox.Text = string.Empty;
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