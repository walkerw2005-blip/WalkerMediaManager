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
    private readonly SmartBuyRepository _smartBuyRepository = new();
    private readonly WishlistRepository _wishlistRepository = new();

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

    private async void AddToWishlistButton_Click(
        object sender,
        RoutedEventArgs e)
    {
        string title = SearchBox.Text.Trim();

        if (string.IsNullOrWhiteSpace(title))
        {
            return;
        }

        try
        {
            if (await _wishlistRepository.ExistsAsync(title))
            {
                ShowMessage(
                    $"{title} is already on your wishlist.",
                    InfoBarSeverity.Warning);

                return;
            }

            WishlistItem item = new()
            {
                Title = title,
                Priority = 2
            };

            await _wishlistRepository.AddAsync(item);

            AddToWishlistButton.Visibility =
                Visibility.Collapsed;

            ShowMessage(
                $"{title} was added to your wishlist.",
                InfoBarSeverity.Success);
        }
        catch (Exception exception)
        {
            ShowMessage(
                $"The title could not be added to your wishlist: " +
                exception.Message,
                InfoBarSeverity.Error);
        }
    }

    private void ClearButton_Click(
        object sender,
        RoutedEventArgs e)
    {
        SearchBox.Text = string.Empty;
        Results.Clear();

        ResultsListView.Visibility = Visibility.Collapsed;
        EmptyStatePanel.Visibility = Visibility.Visible;
        AddToWishlistButton.Visibility = Visibility.Collapsed;

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
        AddToWishlistButton.Visibility = Visibility.Collapsed;

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
                ResultsListView.Visibility = Visibility.Collapsed;
                EmptyStatePanel.Visibility = Visibility.Visible;
                AddToWishlistButton.Visibility = Visibility.Visible;

                ResultCountText.Text = "0 results";

                ShowMessage(
                    $"No owned title matched “{searchText}”.",
                    InfoBarSeverity.Informational);

                return;
            }

            EmptyStatePanel.Visibility = Visibility.Collapsed;
            ResultsListView.Visibility = Visibility.Visible;

            ResultCountText.Text =
                Results.Count == 1
                    ? "1 result"
                    : $"{Results.Count} results";

            ShowMessage(
                "You already own a matching title.",
                InfoBarSeverity.Warning);
        }
        catch (Exception exception)
        {
            ResultsListView.Visibility = Visibility.Collapsed;
            EmptyStatePanel.Visibility = Visibility.Visible;
            ResultCountText.Text = string.Empty;

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
        SearchProgressRing.IsActive = isSearching;

        SearchProgressRing.Visibility =
            isSearching
                ? Visibility.Visible
                : Visibility.Collapsed;

        SearchBox.IsEnabled = !isSearching;
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