using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using WalkerMediaManager.UI.Models;
using WalkerMediaManager.UI.Repositories;

namespace WalkerMediaManager.UI.Views;

public sealed partial class PurchaseHistoryPage : Page
{
    private readonly PurchaseHistoryRepository _repository = new();
    private bool _hasLoaded;
    private bool _loadingFilters;

    public PurchaseHistoryPage()
    {
        InitializeComponent();
        MediaTypeCombo.SelectedIndex = 0;
        SortCombo.SelectedIndex = 0;
    }

    private async void Page_Loaded(object sender, RoutedEventArgs e)
    {
        if (_hasLoaded)
            return;

        _hasLoaded = true;
        await LoadPageAsync();
    }

    private async Task LoadPageAsync()
    {
        LoadingRing.IsActive = true;
        StatusInfoBar.IsOpen = false;

        try
        {
            PurchaseHistorySummary summary = await _repository.GetSummaryAsync();
            ApplySummary(summary);
            await LoadFiltersAsync();
            await LoadResultsAsync();
        }
        catch (Exception exception)
        {
            ShowError(exception.Message);
        }
        finally
        {
            LoadingRing.IsActive = false;
        }
    }

    private async Task LoadFiltersAsync()
    {
        string selectedStore = GetSelectedText(StoreCombo, "All stores");
        string selectedFormat = GetSelectedText(FormatCombo, "All formats");

        _loadingFilters = true;
        try
        {
            List<string> stores = await _repository.GetStoresAsync();
            List<string> formats = await _repository.GetFormatsAsync();

            StoreCombo.Items.Clear();
            StoreCombo.Items.Add("All stores");
            foreach (string store in stores)
                StoreCombo.Items.Add(store);

            FormatCombo.Items.Clear();
            FormatCombo.Items.Add("All formats");
            foreach (string format in formats)
                FormatCombo.Items.Add(format);

            SelectItem(StoreCombo, selectedStore);
            SelectItem(FormatCombo, selectedFormat);
        }
        finally
        {
            _loadingFilters = false;
        }
    }

    private async Task LoadResultsAsync()
    {
        LoadingRing.IsActive = true;

        try
        {
            string store = GetSelectedText(StoreCombo, "All stores");
            string format = GetSelectedText(FormatCombo, "All formats");
            string mediaType = GetSelectedText(MediaTypeCombo, "All types");
            string sort = GetSelectedText(SortCombo, "Date newest");

            if (store == "All stores") store = string.Empty;
            if (format == "All formats") format = string.Empty;
            if (mediaType == "All types") mediaType = string.Empty;

            List<PurchaseHistoryRow> rows = await _repository.SearchAsync(
                SearchTextBox.Text,
                store,
                format,
                mediaType,
                sort);

            PurchaseList.ItemsSource = rows;
            EmptyText.Visibility = rows.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
            ResultSummaryText.Text = rows.Count == 1
                ? "1 purchase record"
                : $"{rows.Count:N0} purchase records";
        }
        catch (Exception exception)
        {
            ShowError(exception.Message);
        }
        finally
        {
            LoadingRing.IsActive = false;
        }
    }

    private void ApplySummary(PurchaseHistorySummary summary)
    {
        PurchaseCountText.Text = summary.PurchaseCountDisplay;
        SpendingText.Text = summary.RecordedSpendingDisplay;
        AveragePriceText.Text = summary.AveragePriceDisplay;
        StoreCountText.Text = summary.StoreCountDisplay;
        MissingDateText.Text = summary.MissingDateCountDisplay;
    }

    private async void RefreshButton_Click(object sender, RoutedEventArgs e)
    {
        await LoadPageAsync();
    }

    private async void Filter_Changed(object sender, RoutedEventArgs e)
    {
        if (!_hasLoaded || _loadingFilters)
            return;

        await LoadResultsAsync();
    }

    private void OpenMovieButton_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button button && int.TryParse(button.Tag?.ToString(), out int movieId))
            Frame.Navigate(typeof(MovieDetailsPage), movieId);
    }

    private static string GetSelectedText(ComboBox comboBox, string fallback) =>
        comboBox.SelectedItem?.ToString() ?? fallback;

    private static void SelectItem(ComboBox comboBox, string value)
    {
        for (int index = 0; index < comboBox.Items.Count; index++)
        {
            if (string.Equals(comboBox.Items[index]?.ToString(), value, StringComparison.OrdinalIgnoreCase))
            {
                comboBox.SelectedIndex = index;
                return;
            }
        }

        comboBox.SelectedIndex = 0;
    }

    private void ShowError(string message)
    {
        StatusInfoBar.Title = "Unable to load purchase history";
        StatusInfoBar.Message = message;
        StatusInfoBar.Severity = InfoBarSeverity.Error;
        StatusInfoBar.IsOpen = true;
    }
}
