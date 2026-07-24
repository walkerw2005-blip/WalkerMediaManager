using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using WalkerMediaManager.UI.Models;
using WalkerMediaManager.UI.Repositories;

namespace WalkerMediaManager.UI.Views;

public sealed partial class TvShowsPage : Page
{
    private readonly TVShowRepository _tvShowRepository = new();
    private readonly List<TVShow> _allShows = [];
    private TVShow? _showBeingEdited;

    public ObservableCollection<TVShow> DisplayShows { get; } = [];

    public TvShowsPage()
    {
        InitializeComponent();
        Loaded += TvShowsPage_Loaded;
    }

    private async void TvShowsPage_Loaded(object sender, RoutedEventArgs e)
    {
        await RefreshShowsAsync();
    }

    private async Task RefreshShowsAsync()
    {
        _allShows.Clear();
        _allShows.AddRange(await _tvShowRepository.GetAllAsync());
        ApplySearchAndSort();

        ShowCountText.Text = _allShows.Count == 1
            ? "1 TV show"
            : $"{_allShows.Count} TV shows";
    }

    private void ApplySearchAndSort()
    {
        string query = SearchBox?.Text?.Trim() ?? string.Empty;
        IEnumerable<TVShow> filtered = _allShows;

        if (!string.IsNullOrWhiteSpace(query))
        {
            filtered = filtered.Where(show =>
                Contains(show.Title, query) ||
                Contains(show.YearDisplay, query) ||
                Contains(show.Studio, query) ||
                Contains(show.Summary, query));
        }

        string sort = (SortComboBox?.SelectedItem as ComboBoxItem)?.Tag?.ToString()
            ?? "TitleAscending";

        filtered = sort switch
        {
            "TitleDescending" => filtered.OrderByDescending(show => show.Title),
            "YearDescending" => filtered.OrderByDescending(show => show.Year).ThenBy(show => show.Title),
            "YearAscending" => filtered.OrderBy(show => show.Year == 0 ? int.MaxValue : show.Year).ThenBy(show => show.Title),
            "SeasonsDescending" => filtered.OrderByDescending(show => show.Seasons).ThenBy(show => show.Title),
            "EpisodesDescending" => filtered.OrderByDescending(show => show.Episodes).ThenBy(show => show.Title),
            "RecentlySynced" => filtered.OrderByDescending(show => ParseDate(show.LastSynced)).ThenBy(show => show.Title),
            _ => filtered.OrderBy(show => show.Title)
        };

        DisplayShows.Clear();
        foreach (TVShow show in filtered)
        {
            DisplayShows.Add(show);
        }

        if (VisibleCountText is not null)
        {
            VisibleCountText.Text = $"{DisplayShows.Count} shown";
        }
    }

    private static bool Contains(string? value, string query) =>
        !string.IsNullOrWhiteSpace(value) &&
        value.Contains(query, StringComparison.OrdinalIgnoreCase);

    private static DateTimeOffset ParseDate(string value) =>
        DateTimeOffset.TryParse(value, out DateTimeOffset parsed)
            ? parsed
            : DateTimeOffset.MinValue;

    private void SearchBox_TextChanged(AutoSuggestBox sender, AutoSuggestBoxTextChangedEventArgs args)
    {
        if (args.Reason == AutoSuggestionBoxTextChangeReason.UserInput)
        {
            ApplySearchAndSort();
        }
    }

    private void SortComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (IsLoaded)
        {
            ApplySearchAndSort();
        }
    }

    private void GridViewToggle_Click(object sender, RoutedEventArgs e)
    {
        bool showGrid = GridViewToggle.IsChecked == true;
        ShowGridView.Visibility = showGrid ? Visibility.Visible : Visibility.Collapsed;
        ShowListView.Visibility = showGrid ? Visibility.Collapsed : Visibility.Visible;
        GridViewToggle.Content = showGrid ? "Grid" : "List";
    }

    private async void ShowEditorButton_Click(object sender, RoutedEventArgs e)
    {
        BeginAdd();
        await ShowEditorDialog.ShowAsync();
    }

    private void BeginAdd()
    {
        _showBeingEdited = null;
        ShowEditorDialog.Title = "Add TV Show";
        ClearEditor();
    }

    private async void EditShow_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button button && button.Tag is TVShow show)
        {
            await BeginEditAsync(show);
        }
    }

    private async void TVShowCard_EditRequested(object sender, TVShow show)
    {
        await BeginEditAsync(show);
    }

    private async Task BeginEditAsync(TVShow show)
    {
        _showBeingEdited = show;
        ShowEditorDialog.Title = "Edit TV Show";
        TitleBox.Text = show.Title;
        YearBox.Text = show.Year > 0 ? show.Year.ToString() : string.Empty;
        SeasonsBox.Text = show.Seasons.ToString();
        EpisodesBox.Text = show.Episodes.ToString();
        StudioBox.Text = show.Studio;
        SummaryBox.Text = show.Summary;
        await ShowEditorDialog.ShowAsync();
    }

    private async void ShowEditorDialog_PrimaryButtonClick(
        ContentDialog sender,
        ContentDialogButtonClickEventArgs args)
    {
        ContentDialogButtonClickDeferral deferral = args.GetDeferral();

        try
        {
            string title = TitleBox.Text.Trim();
            if (string.IsNullOrWhiteSpace(title))
            {
                args.Cancel = true;
                ShowStatus("A TV-series title is required.", InfoBarSeverity.Warning);
                return;
            }

            if (!TryReadNumber(YearBox.Text, "Release year", out int year) ||
                !TryReadNumber(SeasonsBox.Text, "Seasons owned", out int seasons) ||
                !TryReadNumber(EpisodesBox.Text, "Episodes owned", out int episodes))
            {
                args.Cancel = true;
                return;
            }

            if (_showBeingEdited is null)
            {
                TVShow show = new()
                {
                    Title = title,
                    Year = year,
                    Seasons = seasons,
                    Episodes = episodes,
                    Studio = StudioBox.Text.Trim(),
                    Summary = SummaryBox.Text.Trim(),
                    Owned = true
                };

                show.Id = await _tvShowRepository.AddAsync(show);
                ShowStatus($"{show.Title} was added.", InfoBarSeverity.Success);
            }
            else
            {
                _showBeingEdited.Title = title;
                _showBeingEdited.Year = year;
                _showBeingEdited.Seasons = seasons;
                _showBeingEdited.Episodes = episodes;
                _showBeingEdited.Studio = StudioBox.Text.Trim();
                _showBeingEdited.Summary = SummaryBox.Text.Trim();
                await _tvShowRepository.UpdateAsync(_showBeingEdited);
                ShowStatus($"{_showBeingEdited.Title} was updated.", InfoBarSeverity.Success);
            }

            await RefreshShowsAsync();
        }
        catch (Exception exception)
        {
            args.Cancel = true;
            ShowStatus($"The TV show could not be saved: {exception.Message}", InfoBarSeverity.Error);
        }
        finally
        {
            deferral.Complete();
        }
    }

    private bool TryReadNumber(string text, string fieldName, out int value)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            value = 0;
            return true;
        }

        if (!int.TryParse(text.Trim(), out value) || value < 0)
        {
            ShowStatus($"{fieldName} must be a non-negative whole number.", InfoBarSeverity.Warning);
            return false;
        }

        return true;
    }

    private void ClearEditor()
    {
        TitleBox.Text = string.Empty;
        YearBox.Text = string.Empty;
        SeasonsBox.Text = string.Empty;
        EpisodesBox.Text = string.Empty;
        StudioBox.Text = string.Empty;
        SummaryBox.Text = string.Empty;
    }

    private void OpenShow_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button button && button.Tag is TVShow show)
        {
            OpenShow(show);
        }
    }

    private void TVShowCard_OpenRequested(object sender, TVShow show) => OpenShow(show);

    private void OpenShow(TVShow show)
    {
        Frame.Navigate(typeof(TVShowDetailsPage), show.Id);
    }

    private async void DeleteShow_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button button && button.Tag is TVShow show)
        {
            await DeleteShowAsync(show);
        }
    }

    private async void TVShowCard_DeleteRequested(object sender, TVShow show)
    {
        await DeleteShowAsync(show);
    }

    private async Task DeleteShowAsync(TVShow show)
    {
        ContentDialog dialog = new()
        {
            Title = "Delete TV show?",
            Content = $"Remove {show.Title} from Walker Media Manager?",
            PrimaryButtonText = "Delete",
            CloseButtonText = "Cancel",
            DefaultButton = ContentDialogButton.Close,
            XamlRoot = XamlRoot
        };

        if (await dialog.ShowAsync() != ContentDialogResult.Primary)
        {
            return;
        }

        try
        {
            await _tvShowRepository.DeleteAsync(show.Id);
            await RefreshShowsAsync();
            ShowStatus($"{show.Title} was deleted.", InfoBarSeverity.Success);
        }
        catch (Exception exception)
        {
            ShowStatus($"The TV show could not be deleted: {exception.Message}", InfoBarSeverity.Error);
        }
    }

    private void ShowStatus(string message, InfoBarSeverity severity)
    {
        StatusInfoBar.Message = message;
        StatusInfoBar.Severity = severity;
        StatusInfoBar.IsOpen = true;
    }
}
