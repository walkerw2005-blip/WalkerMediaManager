using System;
using System.Collections.ObjectModel;
using System.Threading.Tasks;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using WalkerMediaManager.UI.Models;
using WalkerMediaManager.UI.Repositories;
using Windows.System;

namespace WalkerMediaManager.UI.Views;

public sealed partial class SmartBuyPage : Page
{
    private readonly SmartBuyRepository _smartBuyRepository =
        new();

    public ObservableCollection<SmartBuyResult> Results { get; } =
        [];

    public SmartBuyPage()
    {
        InitializeComponent();

        Loaded += SmartBuyPage_Loaded;
    }

    private void SmartBuyPage_Loaded(
        object sender,
        RoutedEventArgs e)
    {
        SearchBox.Focus(FocusState.Programmatic);
    }

    private async void SearchButton_Click(
        object sender,
        RoutedEventArgs e)
    {
        await RunSearchAsync();
    }

    private async void SearchBox_KeyDown(
        object sender,
        KeyRoutedEventArgs e)
    {
        if (e.Key != VirtualKey.Enter)
        {
            return;
        }

        e.Handled = true;

        await RunSearchAsync();
    }

    private void ClearButton_Click(
        object sender,
        RoutedEventArgs e)
    {
        SearchBox.Text = string.Empty;

        Results.Clear();

        ResultsListView.Visibility =
            Visibility.Collapsed;

        EmptyStatePanel.Visibility =
            Visibility.Visible;

        ResultCountText.Text = string.Empty;

        SearchInfoBar.IsOpen = false;

        SearchBox.Focus(FocusState.Programmatic);
    }

    private async Task RunSearchAsync()
    {
        string searchText = SearchBox.Text.Trim();

        if (string.IsNullOrWhiteSpace(searchText))
        {
            ShowMessage(
                "Enter a movie or TV-show title.",
                InfoBarSeverity.Warning);

            return;
        }

        SetSearchingState(true);

        try
        {
            Results.Clear();

            foreach (
                SmartBuyResult result
                in await _smartBuyRepository.SearchAsync(searchText))
            {
                Results.Add(result);
            }

            if (Results.Count == 0)
            {
                ResultsListView.Visibility =
                    Visibility.Collapsed;

                EmptyStatePanel.Visibility =
                    Visibility.Visible;

                ResultCountText.Text =
                    "0 results";

                ShowMessage(
                    $"No owned title matched “{searchText}”. " +
                    "This may be a safe purchase, but confirm the title and year first.",
                    InfoBarSeverity.Informational);

                return;
            }

            EmptyStatePanel.Visibility =
                Visibility.Collapsed;

            ResultsListView.Visibility =
                Visibility.Visible;

            ResultCountText.Text =
                Results.Count == 1
                    ? "1 result"
                    : $"{Results.Count} results";

            ShowMessage(
                Results.Count == 1
                    ? "You already own a matching title."
                    : "You already own matching titles.",
                InfoBarSeverity.Warning);
        }
        catch (Exception exception)
        {
            ResultsListView.Visibility =
                Visibility.Collapsed;

            EmptyStatePanel.Visibility =
                Visibility.Visible;

            ResultCountText.Text =
                string.Empty;

            ShowMessage(
                $"Smart Buy could not search the collection: " +
                exception.Message,
                InfoBarSeverity.Error);
        }
        finally
        {
            SetSearchingState(false);
        }
    }

    private void SetSearchingState(
        bool isSearching)
    {
        SearchProgressRing.IsActive =
            isSearching;

        SearchProgressRing.Visibility =
            isSearching
                ? Visibility.Visible
                : Visibility.Collapsed;

        SearchBox.IsEnabled =
            !isSearching;
    }

    private void ShowMessage(
        string message,
        InfoBarSeverity severity)
    {
        SearchInfoBar.Message = message;
        SearchInfoBar.Severity = severity;
        SearchInfoBar.IsOpen = true;
    }
}