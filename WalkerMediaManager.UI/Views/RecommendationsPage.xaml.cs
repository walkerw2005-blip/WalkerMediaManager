using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using WalkerMediaManager.UI.Models;
using WalkerMediaManager.UI.Services;

namespace WalkerMediaManager.UI.Views;

public sealed partial class RecommendationsPage : Page
{
    private const string DismissedRecommendationsSetting = "DismissedRecommendationKeys";

    private readonly RecommendationService _recommendationService = new();
    private readonly CollectionSeriesService _collectionSeriesService = new();
    private readonly HashSet<string> _dismissedKeys = new(StringComparer.Ordinal);
    private List<RecommendationItem> _allRecommendations = [];

    public RecommendationsPage()
    {
        InitializeComponent();
        LoadDismissedKeys();
        Loaded += RecommendationsPage_Loaded;
    }

    private async void RecommendationsPage_Loaded(object sender, RoutedEventArgs e)
    {
        await LoadRecommendationsAsync();
    }

    private async Task LoadRecommendationsAsync()
    {
        LoadingRing.IsActive = true;
        RecommendationsList.Visibility = Visibility.Collapsed;
        EmptyState.Visibility = Visibility.Collapsed;

        try
        {
            _allRecommendations = await _recommendationService.GetRecommendationsAsync(100);
            ApplyFilter();
        }
        catch (Exception exception)
        {
            StatusBar.Title = "Recommendations could not be loaded";
            StatusBar.Message = exception.Message;
            StatusBar.Severity = InfoBarSeverity.Error;
            StatusBar.IsOpen = true;
            EmptyState.Visibility = Visibility.Visible;
        }
        finally
        {
            LoadingRing.IsActive = false;
        }
    }

    private void ApplyFilter()
    {
        string filter = (FilterComboBox.SelectedItem as ComboBoxItem)?.Tag?.ToString() ?? "all";
        IEnumerable<RecommendationItem> items = _allRecommendations
            .Where(item => !_dismissedKeys.Contains(item.RecommendationKey));

        items = filter switch
        {
            "complete" => items.Where(item => item.Type is RecommendationType.CompleteCollection or RecommendationType.CompleteTrilogy),
            "wishlist" => items.Where(item => item.IsOnWishlist || item.Type is RecommendationType.Wishlist or RecommendationType.WishlistCollection),
            "watch" => items.Where(item => item.Type == RecommendationType.ContinueWatchOrder),
            _ => items
        };

        List<RecommendationItem> filtered = items.ToList();
        RecommendationsList.ItemsSource = filtered;
        RecommendationsList.Visibility = filtered.Count > 0 ? Visibility.Visible : Visibility.Collapsed;
        EmptyState.Visibility = filtered.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
    }

    private async void RefreshButton_Click(object sender, RoutedEventArgs e)
    {
        await LoadRecommendationsAsync();
    }

    private void RestoreDismissedButton_Click(object sender, RoutedEventArgs e)
    {
        _dismissedKeys.Clear();
        SaveDismissedKeys();
        ApplyFilter();

        StatusBar.Title = "Dismissed recommendations restored";
        StatusBar.Message = "All recommendations are visible again.";
        StatusBar.Severity = InfoBarSeverity.Success;
        StatusBar.IsOpen = true;
    }

    private void FilterComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (IsLoaded)
        {
            ApplyFilter();
        }
    }

    private async void OpenButton_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button button && button.Tag is RecommendationItem item)
        {
            await OpenRecommendationAsync(item);
        }
    }

    private void DismissButton_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not Button button || button.Tag is not RecommendationItem item)
        {
            return;
        }

        _dismissedKeys.Add(item.RecommendationKey);
        SaveDismissedKeys();
        ApplyFilter();

        StatusBar.Title = "Recommendation dismissed";
        StatusBar.Message = $"{item.DisplayTitle} has been hidden. Use Restore dismissed to bring it back.";
        StatusBar.Severity = InfoBarSeverity.Informational;
        StatusBar.IsOpen = true;
    }

    private async Task OpenRecommendationAsync(RecommendationItem item)
    {
        if (item.WishlistItemId is int wishlistId)
        {
            Frame.Navigate(typeof(WishlistPage), wishlistId);
            return;
        }

        if (!string.IsNullOrWhiteSpace(item.CollectionName))
        {
            List<CollectionSeriesProgress> collections = await _collectionSeriesService.GetProgressAsync();
            CollectionSeriesProgress? collection = collections.FirstOrDefault(candidate =>
                string.Equals(candidate.Name, item.CollectionName, StringComparison.OrdinalIgnoreCase));

            if (collection is not null)
            {
                Frame.Navigate(typeof(CollectionDetailsPage), collection);
                return;
            }
        }

        StatusBar.Title = "No destination available";
        StatusBar.Message = "This recommendation is not yet linked to an owned movie or collection.";
        StatusBar.Severity = InfoBarSeverity.Informational;
        StatusBar.IsOpen = true;
    }

    private void LoadDismissedKeys()
    {
        string stored = SettingsService.GetString(DismissedRecommendationsSetting);

        foreach (string key in stored.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            _dismissedKeys.Add(key);
        }
    }

    private void SaveDismissedKeys()
    {
        SettingsService.SetString(
            DismissedRecommendationsSetting,
            string.Join('\n', _dismissedKeys.OrderBy(key => key, StringComparer.Ordinal)));
    }
}
