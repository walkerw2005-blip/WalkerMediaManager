using System;
using System.Collections.ObjectModel;
using System.Globalization;
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

    public ObservableCollection<SmartBuyResult> Results { get; } = [];

    public SmartBuyPage()
    {
        InitializeComponent();
        Loaded += SmartBuyPage_Loaded;
    }

    private void SmartBuyPage_Loaded(object sender, RoutedEventArgs e)
    {
        SearchBox.Focus(FocusState.Programmatic);
    }

    private async void SearchButton_Click(object sender, RoutedEventArgs e)
    {
        await RunSearchAsync();
    }

    private async void SearchBox_KeyDown(object sender, KeyRoutedEventArgs e)
    {
        if (e.Key != VirtualKey.Enter)
            return;

        e.Handled = true;
        await RunSearchAsync();
    }

    private async void PlannedFormatComboBox_SelectionChanged(
        object sender,
        SelectionChangedEventArgs e)
    {
        if (!string.IsNullOrWhiteSpace(SearchBox.Text) && Results.Count > 0)
            await RunSearchAsync();
    }

    private async void AddToWishlistButton_Click(object sender, RoutedEventArgs e)
    {
        string title = SearchBox.Text.Trim();
        if (string.IsNullOrWhiteSpace(title))
            return;

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
                Priority = 2,
                Notes = BuildWishlistNotes()
            };

            await _wishlistRepository.AddAsync(item);
            AddToWishlistButton.Visibility = Visibility.Collapsed;

            ShowMessage(
                $"{title} was added to your wishlist.",
                InfoBarSeverity.Success);
        }
        catch (Exception exception)
        {
            ShowMessage(
                "The title could not be added to your wishlist: " +
                exception.Message,
                InfoBarSeverity.Error);
        }
    }

    private void ClearButton_Click(object sender, RoutedEventArgs e)
    {
        SearchBox.Text = string.Empty;
        PlannedFormatComboBox.SelectedIndex = -1;
        PlannedPriceNumberBox.Value = double.NaN;
        Results.Clear();

        ResultsListView.Visibility = Visibility.Collapsed;
        EmptyStatePanel.Visibility = Visibility.Visible;
        AddToWishlistButton.Visibility = Visibility.Collapsed;
        ResultCountText.Text = string.Empty;
        SearchStatusText.Text = string.Empty;
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

        string plannedFormat =
            PlannedFormatComboBox.SelectedItem?.ToString() ?? string.Empty;
        decimal? plannedPrice = ReadPlannedPrice();

        SetSearchingState(true);
        AddToWishlistButton.Visibility = Visibility.Collapsed;

        try
        {
            Results.Clear();

            foreach (SmartBuyResult result in
                     await _smartBuyRepository.SearchAsync(
                         searchText,
                         plannedFormat,
                         plannedPrice))
            {
                Results.Add(result);
            }

            if (Results.Count == 0)
            {
                ResultsListView.Visibility = Visibility.Collapsed;
                EmptyStatePanel.Visibility = Visibility.Visible;
                AddToWishlistButton.Visibility = Visibility.Visible;
                ResultCountText.Text = "0 results";
                SearchStatusText.Text = "No matching title is in your local collection.";

                ShowMessage(
                    $"No collection title matched “{searchText}”.",
                    InfoBarSeverity.Informational);
                return;
            }

            EmptyStatePanel.Visibility = Visibility.Collapsed;
            ResultsListView.Visibility = Visibility.Visible;
            ResultCountText.Text = Results.Count == 1
                ? "1 result"
                : $"{Results.Count} results";

            int duplicateCount = 0;
            int upgradeCount = 0;
            int missingDetailsCount = 0;

            foreach (SmartBuyResult result in Results)
            {
                if (result.Recommendation is "Duplicate format" or "Already owned")
                    duplicateCount++;
                else if (result.Recommendation == "Upgrade available")
                    upgradeCount++;
                else if (result.Recommendation == "Ownership details missing")
                    missingDetailsCount++;
            }

            SearchStatusText.Text = BuildSearchStatus(
                duplicateCount,
                upgradeCount,
                missingDetailsCount);

            if (duplicateCount > 0)
            {
                ShowMessage(
                    "A matching owned title or format was found. Review the recommendation before buying.",
                    InfoBarSeverity.Warning);
            }
            else if (upgradeCount > 0)
            {
                ShowMessage(
                    "A possible format upgrade was found.",
                    InfoBarSeverity.Success);
            }
            else
            {
                ShowMessage(
                    "Matching collection records were found.",
                    InfoBarSeverity.Informational);
            }
        }
        catch (Exception exception)
        {
            ResultsListView.Visibility = Visibility.Collapsed;
            EmptyStatePanel.Visibility = Visibility.Visible;
            ResultCountText.Text = string.Empty;
            SearchStatusText.Text = string.Empty;

            ShowMessage(
                "Smart Buy could not search the collection: " +
                exception.Message,
                InfoBarSeverity.Error);
        }
        finally
        {
            SetSearchingState(false);
        }
    }

    private decimal? ReadPlannedPrice()
    {
        double value = PlannedPriceNumberBox.Value;
        if (double.IsNaN(value) || double.IsInfinity(value) || value < 0)
            return null;

        return Convert.ToDecimal(value, CultureInfo.InvariantCulture);
    }

    private string BuildWishlistNotes()
    {
        string format = PlannedFormatComboBox.SelectedItem?.ToString() ?? string.Empty;
        decimal? price = ReadPlannedPrice();

        if (string.IsNullOrWhiteSpace(format) && !price.HasValue)
            return "Added from Smart Buy 2.0.";

        if (!string.IsNullOrWhiteSpace(format) && price.HasValue)
            return $"Smart Buy target: {format} at {price.Value:C}.";

        if (!string.IsNullOrWhiteSpace(format))
            return $"Smart Buy target format: {format}.";

        return $"Smart Buy target price: {price!.Value:C}.";
    }

    private static string BuildSearchStatus(
        int duplicateCount,
        int upgradeCount,
        int missingDetailsCount)
    {
        if (duplicateCount > 0)
            return $"{duplicateCount} duplicate warning" +
                   (duplicateCount == 1 ? string.Empty : "s");

        if (upgradeCount > 0)
            return $"{upgradeCount} upgrade candidate" +
                   (upgradeCount == 1 ? string.Empty : "s");

        if (missingDetailsCount > 0)
            return $"{missingDetailsCount} record" +
                   (missingDetailsCount == 1 ? string.Empty : "s") +
                   " need ownership details";

        return "Collection match found";
    }

    private void SetSearchingState(bool isSearching)
    {
        SearchProgressRing.IsActive = isSearching;
        SearchProgressRing.Visibility =
            isSearching ? Visibility.Visible : Visibility.Collapsed;

        SearchBox.IsEnabled = !isSearching;
        PlannedFormatComboBox.IsEnabled = !isSearching;
        PlannedPriceNumberBox.IsEnabled = !isSearching;
    }

    private void ShowMessage(string message, InfoBarSeverity severity)
    {
        SearchInfoBar.Message = message;
        SearchInfoBar.Severity = severity;
        SearchInfoBar.IsOpen = true;
    }
}
