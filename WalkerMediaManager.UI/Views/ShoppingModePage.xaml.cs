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
    private readonly WishlistRepository _wishlistRepository = new();
    private SmartBuyResult? _currentResult;
    private WishlistItem? _currentWishlistItem;
    private string _currentBarcode = string.Empty;
    private string _notFoundTitle = string.Empty;

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

        ResetResults();

        try
        {
            _currentBarcode = LooksLikeBarcode(input) ? NormalizeBarcode(input) : string.Empty;
            string titleSearch = input;

            if (!string.IsNullOrWhiteSpace(_currentBarcode))
            {
                BarcodeRecord? barcode = await _shoppingRepository.FindBarcodeAsync(_currentBarcode);
                if (barcode is null)
                {
                    ShowNotFound(input);
                    ShowStatus("This barcode has not been assigned. Search by title first, then assign the barcode to the matching movie.", InfoBarSeverity.Warning);
                    return;
                }
                titleSearch = barcode.Title;
            }

            string format = FormatBox.SelectedItem?.ToString() ?? string.Empty;
            string edition = EditionBox.SelectedItem?.ToString() ?? string.Empty;
            decimal? price = ReadPrice();
            List<SmartBuyResult> results = await _smartBuyRepository.SearchAsync(titleSearch, format, edition, price);

            if (results.Count == 0)
            {
                ShowNotFound(titleSearch);
                await RecordNotFoundHistoryAsync(titleSearch);
                ShowStatus("No matching owned title or wishlist item was found.", InfoBarSeverity.Success);
                return;
            }

            MatchesList.ItemsSource = results;
            MatchesHeading.Text = results.Count == 1 ? "1 match found" : $"{results.Count} matches found — select the correct title";
            MatchesPanel.Visibility = results.Count > 1 ? Visibility.Visible : Visibility.Collapsed;

            _currentResult = results[0];
            if (results.Count > 1) MatchesList.SelectedIndex = 0;
            await DisplayResultAsync(_currentResult);
            await RecordHistoryAsync("Checked");

            InfoBarSeverity severity = _currentResult.IsOwned
                ? InfoBarSeverity.Warning
                : _currentResult.IsWishlist
                    ? InfoBarSeverity.Informational
                    : InfoBarSeverity.Success;
            ShowStatus(_currentResult.Recommendation, severity);
        }
        catch (Exception exception)
        {
            ShowStatus("Purchase Checker could not complete the lookup: " + exception.Message, InfoBarSeverity.Error);
        }
    }

    private async void MatchesList_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (MatchesList.SelectedItem is not SmartBuyResult selected) return;
        _currentResult = selected;
        await DisplayResultAsync(selected);
    }

    private async System.Threading.Tasks.Task DisplayResultAsync(SmartBuyResult result)
    {
        NotFoundPanel.Visibility = Visibility.Collapsed;
        OwnershipStateText.Text = result.OwnershipState;
        TitleText.Text = result.Title;
        MetadataText.Text = $"{result.MediaType} • {result.YearDisplay}";
        RecommendationText.Text = result.Recommendation;
        RecommendationDetailText.Text = result.RecommendationDetail;
        OwnedFormatsText.Text = result.IsOwned ? "Owned formats: " + result.FormatSummary : result.OwnershipSummary;
        OwnedEditionsText.Text = result.IsOwned ? "Owned editions: " + result.EditionSummary : string.Empty;
        LocationsText.Text = result.IsOwned ? "Locations: " + result.LocationSummary : string.Empty;
        PosterImage.ArtworkPath = result.PosterPath;
        PosterImage.CacheKey = result.CacheKey;
        OpenRecordButton.Visibility = result.CanOpenRecord ? Visibility.Visible : Visibility.Collapsed;
        AssignBarcodeButton.Visibility = result.MediaType == "Movie" ? Visibility.Visible : Visibility.Collapsed;

        _currentWishlistItem = result.MediaType == "Wishlist"
            ? await _wishlistRepository.GetByIdAsync(result.Id)
            : await _wishlistRepository.FindMatchAsync(result.Title, result.Year, NormalizeMediaType(result.MediaType));
        bool onWishlist = _currentWishlistItem is not null || result.IsWishlist;
        WishlistActionButton.Content = onWishlist ? "Open Wishlist Item" : "Add to Wishlist";
        WishlistActionButton.Visibility = result.IsOwned && !onWishlist
            ? Visibility.Collapsed
            : Visibility.Visible;

        ResultPanel.Visibility = Visibility.Visible;
    }

    private void ShowNotFound(string title)
    {
        _currentResult = null;
        _currentWishlistItem = null;
        _notFoundTitle = title.Trim();
        MatchesList.ItemsSource = null;
        MatchesPanel.Visibility = Visibility.Collapsed;
        ResultPanel.Visibility = Visibility.Collapsed;
        NotFoundTitleText.Text = _notFoundTitle;
        NotFoundPanel.Visibility = Visibility.Visible;
    }

    private void ResetResults()
    {
        _currentResult = null;
        _currentWishlistItem = null;
        _notFoundTitle = string.Empty;
        AddNotFoundToWishlistButton.Content = "Add to Wishlist";
        AddNotFoundToWishlistButton.Click -= OpenNotFoundWishlistButton_Click;
        AddNotFoundToWishlistButton.Click -= AddToWishlistButton_Click;
        AddNotFoundToWishlistButton.Click += AddToWishlistButton_Click;
        MatchesList.ItemsSource = null;
        MatchesPanel.Visibility = Visibility.Collapsed;
        ResultPanel.Visibility = Visibility.Collapsed;
        NotFoundPanel.Visibility = Visibility.Collapsed;
        StatusBar.IsOpen = false;
    }

    private void OpenRecordButton_Click(object sender, RoutedEventArgs e)
    {
        if (_currentResult is null) return;

        if (_currentResult.MediaType == "Movie")
            Frame.Navigate(typeof(MovieDetailsPage), _currentResult.Id);
        else if (_currentResult.MediaType == "TV Show")
            Frame.Navigate(typeof(TVShowDetailsPage), _currentResult.Id);
    }

    private async void WishlistActionButton_Click(object sender, RoutedEventArgs e)
    {
        if (_currentWishlistItem is not null)
        {
            Frame.Navigate(typeof(WishlistPage), _currentWishlistItem.Id);
            return;
        }

        if (_currentResult is null) return;
        await AddToWishlistAsync(_currentResult.Title, _currentResult.Year, NormalizeMediaType(_currentResult.MediaType));
    }

    private async void AddToWishlistButton_Click(object sender, RoutedEventArgs e)
    {
        if (string.IsNullOrWhiteSpace(_notFoundTitle)) return;
        await AddToWishlistAsync(_notFoundTitle, 0, "Movie");
    }

    private async System.Threading.Tasks.Task AddToWishlistAsync(string title, int year, string mediaType)
    {
        WishlistItem? existing = await _wishlistRepository.FindMatchAsync(title, year, mediaType);
        if (existing is not null)
        {
            _currentWishlistItem = existing;
            ShowStatus($"{existing.Title} is already on your wishlist.", InfoBarSeverity.Informational);
            Frame.Navigate(typeof(WishlistPage), existing.Id);
            return;
        }

        ComboBox mediaTypeBox = new() { Header = "Media type", SelectedIndex = mediaType == "TV Show" ? 1 : 0 };
        mediaTypeBox.Items.Add(new ComboBoxItem { Content = "Movie" });
        mediaTypeBox.Items.Add(new ComboBoxItem { Content = "TV Show" });

        NumberBox yearBox = new() { Header = "Release year", Minimum = 0, Maximum = 9999, Value = year > 0 ? year : double.NaN };
        TextBox formatBox = new() { Header = "Preferred format", Text = FormatBox.SelectedItem?.ToString() ?? string.Empty };
        NumberBox targetPriceBox = new() { Header = "Target price", Minimum = 0, Value = PriceBox.Value };
        TextBox storeBox = new() { Header = "Preferred store", Text = StoreBox.Text.Trim() };
        ComboBox priorityBox = new() { Header = "Priority", SelectedIndex = 1 };
        priorityBox.Items.Add(new ComboBoxItem { Content = "Low", Tag = "1" });
        priorityBox.Items.Add(new ComboBoxItem { Content = "Normal", Tag = "2" });
        priorityBox.Items.Add(new ComboBoxItem { Content = "High", Tag = "3" });
        TextBox notesBox = new() { Header = "Notes", AcceptsReturn = true, TextWrapping = TextWrapping.Wrap, MinHeight = 72 };

        StackPanel fields = new() { Spacing = 10, MinWidth = 420 };
        fields.Children.Add(new TextBlock { Text = title, FontSize = 20, FontWeight = Microsoft.UI.Text.FontWeights.SemiBold, TextWrapping = TextWrapping.Wrap });
        fields.Children.Add(mediaTypeBox);
        fields.Children.Add(yearBox);
        fields.Children.Add(formatBox);
        fields.Children.Add(targetPriceBox);
        fields.Children.Add(storeBox);
        fields.Children.Add(priorityBox);
        fields.Children.Add(notesBox);

        ContentDialog dialog = new()
        {
            Title = "Add to Wishlist",
            Content = new ScrollViewer { Content = fields, MaxHeight = 520 },
            PrimaryButtonText = "Add",
            CloseButtonText = "Cancel",
            DefaultButton = ContentDialogButton.Primary,
            XamlRoot = XamlRoot
        };

        if (await dialog.ShowAsync() != ContentDialogResult.Primary) return;

        int selectedPriority = priorityBox.SelectedItem is ComboBoxItem priorityItem &&
                               int.TryParse(priorityItem.Tag?.ToString(), out int parsedPriority)
            ? parsedPriority
            : 2;

        WishlistItem item = new()
        {
            Title = title.Trim(),
            MediaType = mediaTypeBox.SelectedIndex == 1 ? "TV Show" : "Movie",
            Year = double.IsNaN(yearBox.Value) ? 0 : Convert.ToInt32(yearBox.Value),
            PreferredFormat = formatBox.Text.Trim(),
            TargetPrice = ReadNullableDecimal(targetPriceBox.Value),
            PreferredStore = storeBox.Text.Trim(),
            Priority = selectedPriority,
            Notes = notesBox.Text.Trim()
        };

        try
        {
            item.Id = await _wishlistRepository.AddAsync(item);
            _currentWishlistItem = item;
            await RecordWishlistHistoryAsync(item.Title);
            ShowStatus($"{item.Title} was added to your wishlist.", InfoBarSeverity.Success);

            if (_currentResult is not null)
            {
                _currentResult.IsWishlist = true;
                await DisplayResultAsync(_currentResult);
            }
            else
            {
                AddNotFoundToWishlistButton.Content = "Open Wishlist Item";
                AddNotFoundToWishlistButton.Click -= AddToWishlistButton_Click;
                AddNotFoundToWishlistButton.Click += OpenNotFoundWishlistButton_Click;
            }
        }
        catch (InvalidOperationException)
        {
            WishlistItem? match = await _wishlistRepository.FindMatchAsync(item.Title, item.Year, item.MediaType);
            if (match is not null)
            {
                _currentWishlistItem = match;
                ShowStatus($"{match.Title} is already on your wishlist.", InfoBarSeverity.Informational);
                return;
            }
            throw;
        }
        catch (Exception exception)
        {
            ShowStatus("The title could not be added to your wishlist: " + exception.Message, InfoBarSeverity.Error);
        }
    }

    private void OpenNotFoundWishlistButton_Click(object sender, RoutedEventArgs e)
    {
        if (_currentWishlistItem is not null)
            Frame.Navigate(typeof(WishlistPage), _currentWishlistItem.Id);
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
            "Assigned from Purchase Checker");
        _currentBarcode = barcode;
        ShowStatus($"Barcode {barcode} is now assigned to {_currentResult.Title}.", InfoBarSeverity.Success);
    }

    private async System.Threading.Tasks.Task RecordNotFoundHistoryAsync(string title)
    {
        await _shoppingRepository.AddHistoryAsync(new ShoppingHistoryItem
        {
            SearchText = LookupBox.Text,
            Barcode = _currentBarcode,
            MovieId = null,
            Title = title,
            Store = StoreBox.Text,
            PlannedFormat = FormatBox.SelectedItem?.ToString() ?? string.Empty,
            Price = ReadPrice(),
            Decision = "Not owned"
        });
        await RefreshHistoryAsync();
    }

    private async System.Threading.Tasks.Task RecordWishlistHistoryAsync(string title)
    {
        await _shoppingRepository.AddHistoryAsync(new ShoppingHistoryItem
        {
            SearchText = LookupBox.Text,
            Barcode = _currentBarcode,
            MovieId = _currentResult?.MediaType == "Movie" ? _currentResult.Id : null,
            Title = title,
            Store = StoreBox.Text,
            PlannedFormat = FormatBox.SelectedItem?.ToString() ?? string.Empty,
            Price = ReadPrice(),
            Decision = "Added to wishlist"
        });
        await RefreshHistoryAsync();
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

    private decimal? ReadPrice() => ReadNullableDecimal(PriceBox.Value);

    private static decimal? ReadNullableDecimal(double value) =>
        double.IsNaN(value) || double.IsInfinity(value) || value < 0
            ? null
            : Convert.ToDecimal(value, CultureInfo.InvariantCulture);

    private void ShowStatus(string message, InfoBarSeverity severity)
    {
        StatusBar.Message = message;
        StatusBar.Severity = severity;
        StatusBar.IsOpen = true;
    }

    private static string NormalizeMediaType(string mediaType) =>
        mediaType.Equals("TV Show", StringComparison.OrdinalIgnoreCase) ? "TV Show" : "Movie";

    private static bool LooksLikeBarcode(string value)
    {
        string normalized = NormalizeBarcode(value);
        return normalized.Length is >= 8 and <= 14 && normalized.All(char.IsDigit);
    }

    private static string NormalizeBarcode(string value) =>
        new(value.Where(char.IsLetterOrDigit).ToArray());
}
