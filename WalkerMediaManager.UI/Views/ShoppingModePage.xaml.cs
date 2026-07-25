using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using WalkerMediaManager.UI.Models;
using WalkerMediaManager.UI.Repositories;

namespace WalkerMediaManager.UI.Views;

public sealed partial class ShoppingModePage : Page
{
    private readonly SmartBuyRepository _smartBuyRepository = new();
    private readonly ShoppingRepository _shoppingRepository = new();
    private SmartBuyResult? _currentResult;
    private string _currentBarcode = string.Empty;

    public ShoppingModePage()
    {
        InitializeComponent();
        Loaded += ShoppingModePage_Loaded;
    }

    private async void ShoppingModePage_Loaded(object sender, RoutedEventArgs e)
    {
        await RefreshHistoryAsync();
        LookupBox.Focus(FocusState.Programmatic);
    }

    private async void CheckButton_Click(object sender, RoutedEventArgs e) => await RunLookupAsync();

    private async void LookupBox_KeyDown(object sender, KeyRoutedEventArgs e)
    {
        if (e.Key == Windows.System.VirtualKey.Enter)
        {
            e.Handled = true;
            await RunLookupAsync();
        }
    }

    private async System.Threading.Tasks.Task RunLookupAsync()
    {
        string input = LookupBox.Text.Trim();
        if (string.IsNullOrWhiteSpace(input))
        {
            ShowStatus("Scan a barcode or enter a title.", InfoBarSeverity.Warning);
            return;
        }

        try
        {
            _currentBarcode = LooksLikeBarcode(input) ? NormalizeBarcode(input) : string.Empty;
            string titleSearch = input;

            if (!string.IsNullOrWhiteSpace(_currentBarcode))
            {
                BarcodeRecord? barcode = await _shoppingRepository.FindBarcodeAsync(_currentBarcode);
                if (barcode is null)
                {
                    _currentResult = null;
                    ResultPanel.Visibility = Visibility.Collapsed;
                    ShowStatus("This barcode has not been assigned yet. Search by title, open the result, then choose Assign barcode.", InfoBarSeverity.Warning);
                    return;
                }
                titleSearch = barcode.Title;
            }

            string format = FormatBox.SelectedItem?.ToString() ?? string.Empty;
            decimal? price = ReadPrice();
            List<SmartBuyResult> results = await _smartBuyRepository.SearchAsync(titleSearch, format, price);
            _currentResult = results.FirstOrDefault();

            if (_currentResult is null)
            {
                ResultPanel.Visibility = Visibility.Collapsed;
                ShowStatus("No matching movie or TV show was found.", InfoBarSeverity.Warning);
                return;
            }

            DisplayResult(_currentResult);
            await RecordHistoryAsync("Checked");
            ShowStatus("Collection check complete.", InfoBarSeverity.Success);
        }
        catch (Exception exception)
        {
            ShowStatus("Shopping Mode could not complete the lookup: " + exception.Message, InfoBarSeverity.Error);
        }
    }

    private void DisplayResult(SmartBuyResult result)
    {
        TitleText.Text = result.Title;
        MetadataText.Text = $"{result.MediaType} • {result.YearDisplay}";
        RecommendationText.Text = result.Recommendation;
        RecommendationDetailText.Text = result.RecommendationDetail;
        OwnedFormatsText.Text = "Owned formats: " + result.FormatSummary;
        LocationsText.Text = "Locations: " + result.LocationSummary;
        PosterImage.ArtworkPath = result.PosterPath;
        PosterImage.CacheKey = result.CacheKey;
        ResultPanel.Visibility = Visibility.Visible;
    }

    private async void DecisionButton_Click(object sender, RoutedEventArgs e)
    {
        if (_currentResult is null || sender is not Button button) return;
        string decision = button.Tag?.ToString() ?? button.Content?.ToString() ?? "Recorded";
        await RecordHistoryAsync(decision);
        ShowStatus($"Decision recorded: {decision}.", InfoBarSeverity.Success);
        await RefreshHistoryAsync();
    }

    private async void AssignBarcodeButton_Click(object sender, RoutedEventArgs e)
    {
        if (_currentResult is null || _currentResult.MediaType != "Movie")
        {
            ShowStatus("Select a movie result before assigning a barcode.", InfoBarSeverity.Warning);
            return;
        }

        string barcode = _currentBarcode;
        if (string.IsNullOrWhiteSpace(barcode))
        {
            ContentDialog dialog = new()
            {
                Title = "Assign barcode",
                PrimaryButtonText = "Save",
                CloseButtonText = "Cancel",
                XamlRoot = XamlRoot
            };
            TextBox box = new() { Header = "UPC / EAN", PlaceholderText = "Scan or type the barcode" };
            dialog.Content = box;
            if (await dialog.ShowAsync() != ContentDialogResult.Primary) return;
            barcode = NormalizeBarcode(box.Text);
        }

        if (string.IsNullOrWhiteSpace(barcode))
        {
            ShowStatus("Enter a valid barcode.", InfoBarSeverity.Warning);
            return;
        }

        await _shoppingRepository.SaveBarcodeAsync(
            barcode,
            _currentResult.Id,
            FormatBox.SelectedItem?.ToString() ?? string.Empty,
            string.Empty,
            "Assigned from Shopping Mode");
        _currentBarcode = barcode;
        ShowStatus($"Barcode {barcode} is now assigned to {_currentResult.Title}.", InfoBarSeverity.Success);
    }

    private async System.Threading.Tasks.Task RecordHistoryAsync(string decision)
    {
        if (_currentResult is null) return;
        await _shoppingRepository.AddHistoryAsync(new ShoppingHistoryItem
        {
            SearchText = LookupBox.Text,
            Barcode = _currentBarcode,
            MovieId = _currentResult.MediaType == "Movie" ? _currentResult.Id : null,
            Title = _currentResult.Title,
            Store = StoreBox.Text,
            PlannedFormat = FormatBox.SelectedItem?.ToString() ?? string.Empty,
            Price = ReadPrice(),
            Decision = decision
        });
    }

    private async void RefreshHistoryButton_Click(object sender, RoutedEventArgs e) => await RefreshHistoryAsync();

    private async System.Threading.Tasks.Task RefreshHistoryAsync() =>
        HistoryList.ItemsSource = await _shoppingRepository.GetRecentHistoryAsync();

    private decimal? ReadPrice()
    {
        double value = PriceBox.Value;
        return double.IsNaN(value) || double.IsInfinity(value) || value < 0
            ? null
            : Convert.ToDecimal(value, CultureInfo.InvariantCulture);
    }

    private void ShowStatus(string message, InfoBarSeverity severity)
    {
        StatusBar.Message = message;
        StatusBar.Severity = severity;
        StatusBar.IsOpen = true;
    }

    private static bool LooksLikeBarcode(string value)
    {
        string normalized = NormalizeBarcode(value);
        return normalized.Length is >= 8 and <= 14 && normalized.All(char.IsDigit);
    }

    private static string NormalizeBarcode(string value) =>
        new(value.Where(char.IsLetterOrDigit).ToArray());
}
