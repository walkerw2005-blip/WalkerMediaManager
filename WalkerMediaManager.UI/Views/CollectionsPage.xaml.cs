using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using WalkerMediaManager.UI.Models;
using WalkerMediaManager.UI.Repositories;
using WalkerMediaManager.UI.Services;

namespace WalkerMediaManager.UI.Views;

public sealed partial class CollectionsPage : Page
{
    private readonly CollectionRepository _collectionRepository = new();
    private readonly SmartCollectionService _smartCollectionService = new();
    private readonly ShoppingPlannerService _shoppingPlannerService = new();
    private readonly WishlistRepository _wishlistRepository = new();
    private readonly List<CollectionSeriesProgress> _allSeries = [];

    private MediaCollection? _collectionBeingEdited;

    public ObservableCollection<MediaCollection> Collections { get; } = [];
    public ObservableCollection<CollectionSeriesProgress> FilteredSeries { get; } = [];
    public ObservableCollection<ShoppingRecommendation> ShoppingRecommendations { get; } = [];
    public ObservableCollection<string> Categories { get; } = ["All Categories"];
    public ObservableCollection<string> Studios { get; } = ["All Studios"];

    public CollectionsPage()
    {
        InitializeComponent();
        Loaded += CollectionsPage_Loaded;
    }

    private async void CollectionsPage_Loaded(object sender, RoutedEventArgs e)
    {
        await RefreshAllAsync();
    }

    private async void RefreshButton_Click(object sender, RoutedEventArgs e)
    {
        await RefreshAllAsync();
    }

    private async Task RefreshAllAsync()
    {
        await Task.WhenAll(
            RefreshAutomaticSeriesAsync(),
            RefreshCollectionsAsync());
    }

    private async Task RefreshAutomaticSeriesAsync()
    {
        try
        {
            _allSeries.Clear();
            _allSeries.AddRange(await _smartCollectionService.GetProgressAsync());
            UpdateSmartSummary();
            RefreshFilterOptions();
            ApplySeriesFilter();

            int completeCount = _allSeries.Count(series => series.IsComplete);
            AutomaticStatusInfoBar.Message =
                $"Analyzed {_allSeries.Count} collections. {completeCount} complete and {_allSeries.Sum(series => series.MissingCount)} titles missing.";
            AutomaticStatusInfoBar.Severity = InfoBarSeverity.Informational;
            AutomaticStatusInfoBar.IsOpen = true;
        }
        catch (Exception exception)
        {
            AutomaticStatusInfoBar.Message =
                $"Collection progress could not be calculated: {exception.Message}";
            AutomaticStatusInfoBar.Severity = InfoBarSeverity.Error;
            AutomaticStatusInfoBar.IsOpen = true;
        }
    }


    private void UpdateSmartSummary()
    {
        int complete = _allSeries.Count(series => series.IsComplete);
        int almostComplete = _allSeries.Count(series => !series.IsComplete && series.CompletionPercent >= 75);
        int missingTitles = _allSeries.Sum(series => series.MissingCount);
        int wishlistTitles = _allSeries.Sum(series => series.WishlistCount);
        decimal estimatedCost = _allSeries.Sum(series => series.EstimatedCompletionCost);

        SmartCompleteText.Text = complete.ToString();
        SmartAlmostText.Text = almostComplete.ToString();
        SmartMissingText.Text = missingTitles.ToString();
        SmartWishlistText.Text = wishlistTitles.ToString();
        SmartCostText.Text = estimatedCost.ToString("C");
    }


    private void RefreshFilterOptions()
    {
        string selectedCategory = CategoryFilterBox?.SelectedItem?.ToString() ?? "All Categories";
        string selectedStudio = StudioFilterBox?.SelectedItem?.ToString() ?? "All Studios";

        Categories.Clear();
        Categories.Add("All Categories");
        foreach (string category in _allSeries
                     .Select(series => series.Category?.Trim() ?? string.Empty)
                     .Where(category => !string.IsNullOrWhiteSpace(category))
                     .Distinct(StringComparer.OrdinalIgnoreCase)
                     .OrderBy(category => category, StringComparer.OrdinalIgnoreCase))
        {
            Categories.Add(category);
        }

        Studios.Clear();
        Studios.Add("All Studios");
        foreach (string studio in _allSeries
                     .Select(series => series.Studio?.Trim() ?? string.Empty)
                     .Where(studio => !string.IsNullOrWhiteSpace(studio))
                     .Distinct(StringComparer.OrdinalIgnoreCase)
                     .OrderBy(studio => studio, StringComparer.OrdinalIgnoreCase))
        {
            Studios.Add(studio);
        }

        CategoryFilterBox.SelectedItem = Categories.FirstOrDefault(item =>
            string.Equals(item, selectedCategory, StringComparison.OrdinalIgnoreCase)) ?? "All Categories";
        StudioFilterBox.SelectedItem = Studios.FirstOrDefault(item =>
            string.Equals(item, selectedStudio, StringComparison.OrdinalIgnoreCase)) ?? "All Studios";
    }

    private void SeriesSearchBox_TextChanged(object sender, TextChangedEventArgs e)
    {
        ApplySeriesFilter();
    }

    private void ApplySeriesFilter()
    {
        string searchText = SeriesSearchBox?.Text?.Trim() ?? string.Empty;

        IEnumerable<CollectionSeriesProgress> results = _allSeries;

        string statusFilter =
            (SeriesStatusFilter?.SelectedItem as ComboBoxItem)?.Tag?.ToString() ?? "All";

        results = statusFilter switch
        {
            "Complete" => results.Where(series => series.IsComplete),
            "Almost" => results.Where(series => !series.IsComplete && series.CompletionPercent >= 75),
            "InProgress" => results.Where(series => series.CompletionPercent > 0 && series.CompletionPercent < 75),
            "NotStarted" => results.Where(series => series.OwnedCount == 0),
            "Wishlist" => results.Where(series => series.WishlistCount > 0),
            "Unplanned" => results.Where(series => series.UnplannedMissingCount > 0),
            _ => results
        };

        string categoryFilter = CategoryFilterBox?.SelectedItem?.ToString() ?? "All Categories";
        if (!string.Equals(categoryFilter, "All Categories", StringComparison.OrdinalIgnoreCase))
        {
            results = results.Where(series =>
                string.Equals(series.Category, categoryFilter, StringComparison.OrdinalIgnoreCase));
        }

        string studioFilter = StudioFilterBox?.SelectedItem?.ToString() ?? "All Studios";
        if (!string.Equals(studioFilter, "All Studios", StringComparison.OrdinalIgnoreCase))
        {
            results = results.Where(series =>
                string.Equals(series.Studio, studioFilter, StringComparison.OrdinalIgnoreCase));
        }

        if (!string.IsNullOrWhiteSpace(searchText))
        {
            results = results.Where(series =>
                series.Name.Contains(searchText, StringComparison.OrdinalIgnoreCase) ||
                series.Description.Contains(searchText, StringComparison.OrdinalIgnoreCase) ||
                series.Category.Contains(searchText, StringComparison.OrdinalIgnoreCase) ||
                series.Studio.Contains(searchText, StringComparison.OrdinalIgnoreCase) ||
                series.CollectionType.Contains(searchText, StringComparison.OrdinalIgnoreCase) ||
                series.Titles.Any(title =>
                    title.Title.Contains(searchText, StringComparison.OrdinalIgnoreCase)));
        }

        string sortMode =
            (SeriesSortBox?.SelectedItem as ComboBoxItem)?.Tag?.ToString() ?? "Progress";

        results = sortMode switch
        {
            "Name" => results.OrderBy(series => series.Name, StringComparer.OrdinalIgnoreCase),
            "Missing" => results.OrderBy(series => series.MissingCount)
                .ThenBy(series => series.Name, StringComparer.OrdinalIgnoreCase),
            "Size" => results.OrderByDescending(series => series.TotalCount)
                .ThenBy(series => series.Name, StringComparer.OrdinalIgnoreCase),
            "CostLow" => results.OrderBy(series => series.EstimatedCompletionCost)
                .ThenByDescending(series => series.CompletionPercent),
            "CostHigh" => results.OrderByDescending(series => series.EstimatedCompletionCost)
                .ThenBy(series => series.Name, StringComparer.OrdinalIgnoreCase),
            "Smart" => results.OrderByDescending(series => series.SmartPriorityScore)
                .ThenByDescending(series => series.CompletionPercent),
            _ => results.OrderByDescending(series => series.CompletionPercent)
                .ThenBy(series => series.Name, StringComparer.OrdinalIgnoreCase)
        };

        FilteredSeries.Clear();
        foreach (CollectionSeriesProgress series in results)
        {
            FilteredSeries.Add(series);
        }

        SeriesCountText.Text = FilteredSeries.Count == 1
            ? "1 collection"
            : $"{FilteredSeries.Count} collections";
    }


    private void SeriesFilter_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (IsLoaded)
        {
            ApplySeriesFilter();
        }
    }

    private void ViewSeriesButton_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not Button button ||
            button.Tag is not CollectionSeriesProgress series)
        {
            return;
        }

        Frame.Navigate(typeof(CollectionDetailsPage), series);
    }


    private void BuildShoppingPlanButton_Click(object sender, RoutedEventArgs e)
    {
        if (!decimal.TryParse(BudgetBox.Text.Trim(), out decimal budget) || budget <= 0)
        {
            AutomaticStatusInfoBar.Message = "Enter a budget greater than $0.00.";
            AutomaticStatusInfoBar.Severity = InfoBarSeverity.Warning;
            AutomaticStatusInfoBar.IsOpen = true;
            return;
        }

        ShoppingPlan plan = _shoppingPlannerService.BuildPlan(_allSeries, budget);
        ShoppingRecommendations.Clear();
        foreach (ShoppingRecommendation recommendation in plan.Recommendations)
        {
            ShoppingRecommendations.Add(recommendation);
        }

        ShoppingPlanSummaryText.Text = plan.SummaryDisplay;
        ShoppingPlanPanel.Visibility = Visibility.Visible;
    }

    private async void AddMissingToWishlistButton_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not Button button || button.Tag is not CollectionSeriesProgress collection)
        {
            return;
        }

        int added = 0;
        foreach (CollectionSeriesTitleStatus title in collection.Titles.Where(item => !item.IsOwned && !item.IsOnWishlist))
        {
            try
            {
                WishlistItem item = new()
                {
                    MediaType = "Movie",
                    Title = title.Title,
                    Year = title.Year,
                    PreferredFormat = string.IsNullOrWhiteSpace(title.PreferredFormat) ? "Blu-ray" : title.PreferredFormat,
                    TargetPrice = title.EstimatedPurchasePrice,
                    Priority = collection.MissingCount == 1 ? 5 : collection.CompletionPercent >= 75 ? 4 : 3,
                    Notes = $"Added from {collection.Name} collection planner."
                };

                await _wishlistRepository.AddAsync(item);
                added++;
            }
            catch (InvalidOperationException)
            {
                // Already on the wishlist; continue with the remaining titles.
            }
        }

        await RefreshAutomaticSeriesAsync();
        AutomaticStatusInfoBar.Message = added == 1
            ? $"Added 1 missing title from {collection.Name} to the wishlist."
            : $"Added {added} missing titles from {collection.Name} to the wishlist.";
        AutomaticStatusInfoBar.Severity = InfoBarSeverity.Success;
        AutomaticStatusInfoBar.IsOpen = true;
    }

    private async void SaveButton_Click(object sender, RoutedEventArgs e)
    {
        string name = NameBox.Text.Trim();

        if (string.IsNullOrWhiteSpace(name))
        {
            ShowStatus("A collection name is required.", InfoBarSeverity.Warning);
            return;
        }

        if (!TryReadCount(OwnedCountBox.Text, "Titles owned", out int ownedCount) ||
            !TryReadCount(TargetCountBox.Text, "Total titles", out int targetCount))
        {
            return;
        }

        if (ownedCount > targetCount)
        {
            ShowStatus(
                "Titles owned cannot be greater than total titles.",
                InfoBarSeverity.Warning);
            return;
        }

        try
        {
            if (_collectionBeingEdited is null)
            {
                if (await _collectionRepository.ExistsAsync(name))
                {
                    ShowStatus($"{name} already exists.", InfoBarSeverity.Warning);
                    return;
                }

                MediaCollection collection = new()
                {
                    Name = name,
                    Description = DescriptionBox.Text.Trim(),
                    OwnedCount = ownedCount,
                    TargetCount = targetCount
                };

                collection.Id = await _collectionRepository.AddAsync(collection);
                ShowStatus($"{collection.Name} was added.", InfoBarSeverity.Success);
            }
            else
            {
                _collectionBeingEdited.Name = name;
                _collectionBeingEdited.Description = DescriptionBox.Text.Trim();
                _collectionBeingEdited.OwnedCount = ownedCount;
                _collectionBeingEdited.TargetCount = targetCount;

                await _collectionRepository.UpdateAsync(_collectionBeingEdited);
                ShowStatus(
                    $"{_collectionBeingEdited.Name} was updated.",
                    InfoBarSeverity.Success);
            }

            ResetForm();
            await RefreshCollectionsAsync();
        }
        catch (Exception exception)
        {
            ShowStatus(
                $"The collection could not be saved: {exception.Message}",
                InfoBarSeverity.Error);
        }
    }

    private void EditButton_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not Button button ||
            button.Tag is not MediaCollection collection)
        {
            return;
        }

        _collectionBeingEdited = collection;
        FormTitleText.Text = "Edit Collection";
        SaveButton.Content = "Save Changes";
        CancelButton.Visibility = Visibility.Visible;
        NameBox.Text = collection.Name;
        DescriptionBox.Text = collection.Description;
        OwnedCountBox.Text = collection.OwnedCount.ToString();
        TargetCountBox.Text = collection.TargetCount.ToString();
        NameBox.Focus(FocusState.Programmatic);
    }

    private async void DeleteButton_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not Button button ||
            button.Tag is not MediaCollection collection)
        {
            return;
        }

        ContentDialog dialog = new()
        {
            Title = "Delete collection?",
            Content = $"Delete the {collection.Name} collection?",
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
            await _collectionRepository.DeleteAsync(collection.Id);

            if (_collectionBeingEdited?.Id == collection.Id)
            {
                ResetForm();
            }

            await RefreshCollectionsAsync();
            ShowStatus($"{collection.Name} was deleted.", InfoBarSeverity.Success);
        }
        catch (Exception exception)
        {
            ShowStatus(
                $"The collection could not be deleted: {exception.Message}",
                InfoBarSeverity.Error);
        }
    }

    private void CancelButton_Click(object sender, RoutedEventArgs e)
    {
        ResetForm();
    }

    private bool TryReadCount(string text, string fieldName, out int value)
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

    private async Task RefreshCollectionsAsync()
    {
        Collections.Clear();

        foreach (MediaCollection collection in await _collectionRepository.GetAllAsync())
        {
            Collections.Add(collection);
        }

        CollectionCountText.Text = Collections.Count == 1
            ? "1 collection"
            : $"{Collections.Count} collections";
    }

    private void ResetForm()
    {
        _collectionBeingEdited = null;
        FormTitleText.Text = "Add Collection";
        SaveButton.Content = "Add Collection";
        CancelButton.Visibility = Visibility.Collapsed;
        NameBox.Text = string.Empty;
        DescriptionBox.Text = string.Empty;
        OwnedCountBox.Text = string.Empty;
        TargetCountBox.Text = string.Empty;
    }

    private void ShowStatus(string message, InfoBarSeverity severity)
    {
        StatusInfoBar.Message = message;
        StatusInfoBar.Severity = severity;
        StatusInfoBar.IsOpen = true;
    }
}
