using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.UI;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Navigation;
using WalkerMediaManager.UI.Models;
using WalkerMediaManager.UI.Repositories;
using WalkerMediaManager.UI.Services;

namespace WalkerMediaManager.UI.Views;

public sealed partial class CollectionDetailsPage : Page
{
    private readonly WatchOrderService _watchOrderService = new();
    private readonly WishlistRepository _wishlistRepository = new();
    private readonly OwnedCopyRepository _ownedCopyRepository = new();
    private CollectionSeriesProgress? _series;
    private List<WishlistItem> _wishlistItems = [];

    public CollectionDetailsPage()
    {
        InitializeComponent();
    }

    protected override async void OnNavigatedTo(NavigationEventArgs e)
    {
        base.OnNavigatedTo(e);

        if (e.Parameter is not CollectionSeriesProgress series)
        {
            PageTitleText.Text = "Collection unavailable";
            DescriptionText.Text = "The selected collection could not be loaded.";
            WatchOrderPivotItem.Visibility = Visibility.Collapsed;
            return;
        }

        _series = series;
        PopulateOverview(series);
        await EnrichOwnedCopyDetailsAsync(series);
        PopulateOverview(series);
        await LoadWatchOrdersAsync(series);
    }

    private void PopulateOverview(CollectionSeriesProgress series)
    {
        List<CollectionSeriesTitleStatus> owned = series.Titles
            .Where(title => title.IsOwned)
            .ToList();

        List<CollectionSeriesTitleStatus> missing = series.Titles
            .Where(title => !title.IsOwned)
            .ToList();

        PageTitleText.Text = series.Name;
        DescriptionText.Text = series.Description;
        CollectionMetadataText.Text = series.MetadataDisplay;
        ProgressText.Text = series.ProgressDisplay;
        CompletionText.Text = series.CompletionDisplay;
        CompletionProgressBar.Value = series.CompletionPercent;
        CollectionStatusText.Text = series.CompleteNowDisplay;
        OwnedCountText.Text = series.OwnedCount.ToString();
        MissingCountText.Text = series.MissingCount.ToString();
        RuntimeText.Text = series.RuntimeDisplay;
        WishlistCountText.Text = series.WishlistSummaryDisplay;
        EstimatedCostText.Text = series.EstimatedCompletionDisplay;
        ReleaseYearsText.Text = series.ReleaseSpanDisplay;
        TotalMoviesText.Text = series.TotalCount == 1 ? "1 movie" : $"{series.TotalCount} movies";
        PreferredFormatText.Text = series.PreferredFormatSummary;
        TotalInvestmentText.Text = series.TotalInvestmentDisplay;
        AveragePriceText.Text = series.AveragePurchasePriceDisplay;
        DigitalCoverageText.Text = series.DigitalCoverageDisplay;
        FormatBreakdownText.Text = series.FormatBreakdownDisplay;
        StorageLocationsText.Text = series.StorageLocationsDisplay;

        OwnedHeadingText.Text = $"Owned Movies ({owned.Count})";
        MissingHeadingText.Text = $"Missing Movies ({missing.Count})";

        OwnedMoviesGrid.ItemsSource = owned;
        MissingMoviesList.ItemsSource = missing;

        NoOwnedMoviesText.Visibility = owned.Count == 0
            ? Visibility.Visible
            : Visibility.Collapsed;

        NoMissingMoviesText.Visibility = missing.Count == 0
            ? Visibility.Visible
            : Visibility.Collapsed;
        AddAllMissingButton.Visibility = missing.Count == 0
            ? Visibility.Collapsed
            : Visibility.Visible;
        AddAllMissingButton.IsEnabled = missing.Any(title => title.CanAddToWishlist);
    }

    private async Task EnrichOwnedCopyDetailsAsync(CollectionSeriesProgress series)
    {
        foreach (CollectionSeriesTitleStatus title in series.Titles.Where(title => title.IsOwned && title.MovieId.HasValue))
        {
            try
            {
                List<OwnedCopy> copies = await _ownedCopyRepository.GetForMovieAsync(title.MovieId!.Value);
                title.OwnedFormats = copies
                    .Where(copy => !copy.IsDigital && !string.IsNullOrWhiteSpace(copy.Format))
                    .Select(copy => copy.Format.Trim())
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .ToList();
                title.PurchasePriceTotal = copies.Where(copy => copy.PurchasePrice.HasValue).Sum(copy => copy.PurchasePrice ?? 0);
                title.HasDigitalCopy = copies.Any(copy => copy.IsDigital);
                title.PurchaseDate = copies
                    .Where(copy => !string.IsNullOrWhiteSpace(copy.PurchaseDate))
                    .OrderBy(copy => copy.PurchaseDate, StringComparer.OrdinalIgnoreCase)
                    .Select(copy => copy.PurchaseDate)
                    .FirstOrDefault() ?? string.Empty;
                title.StorageLocation = string.Join(", ", copies
                    .Where(copy => !string.IsNullOrWhiteSpace(copy.Location))
                    .Select(copy => copy.Location.Trim())
                    .Distinct(StringComparer.OrdinalIgnoreCase));
            }
            catch
            {
                // Keep collection details usable even if copy metadata cannot be read.
            }
        }
    }

    private async Task LoadWatchOrdersAsync(CollectionSeriesProgress series)
    {
        IReadOnlyList<WatchOrderDefinition> orders = _watchOrderService.GetOrders(series.Name);

        WatchOrderComboBox.ItemsSource = orders;
        NoWatchOrdersText.Visibility = orders.Count == 0
            ? Visibility.Visible
            : Visibility.Collapsed;
        WatchOrderSummaryCard.Visibility = orders.Count == 0
            ? Visibility.Collapsed
            : Visibility.Visible;
        WatchOrderProgressPanel.Visibility = orders.Count == 0
            ? Visibility.Collapsed
            : Visibility.Visible;
        WatchOrderList.Visibility = orders.Count == 0
            ? Visibility.Collapsed
            : Visibility.Visible;

        if (orders.Count == 0)
        {
            return;
        }

        try
        {
            _wishlistItems = await _wishlistRepository.GetAllAsync();
        }
        catch
        {
            _wishlistItems = [];
        }

        WatchOrderComboBox.SelectedIndex = 0;
    }

    private void PopulateWatchOrder(WatchOrderDefinition order)
    {
        if (_series is null)
        {
            return;
        }

        List<WatchOrderRowViewModel> rows = order.Entries
            .Select(entry => CreateWatchOrderRow(entry, _series))
            .ToList();

        int ownedCount = rows.Count(row => row.IsOwned);
        int wishlistCount = rows.Count(row => row.IsWishlist);
        int missingCount = rows.Count - ownedCount - wishlistCount;
        double percent = rows.Count == 0 ? 0 : (double)ownedCount / rows.Count * 100;

        WatchOrderDescriptionText.Text = order.Description;
        WatchOwnedCountText.Text = ownedCount.ToString();
        WatchWishlistCountText.Text = wishlistCount.ToString();
        WatchMissingCountText.Text = missingCount.ToString();
        WatchOrderProgressText.Text = $"{ownedCount} of {rows.Count} owned - {percent:0}% complete";
        WatchOrderProgressBar.Value = percent;
        WatchOrderList.ItemsSource = rows;
    }

    private WatchOrderRowViewModel CreateWatchOrderRow(
        WatchOrderEntry entry,
        CollectionSeriesProgress series)
    {
        CollectionSeriesTitleStatus? ownedTitle = series.Titles.FirstOrDefault(title =>
            title.IsOwned && EntryMatches(entry, title.Title, title.Year));

        if (ownedTitle is not null)
        {
            return new WatchOrderRowViewModel
            {
                Position = entry.Position,
                Title = entry.Title,
                Year = entry.Year,
                StatusText = "Owned",
                DetailText = string.IsNullOrWhiteSpace(ownedTitle.OwnedFormat)
                    ? "In your library"
                    : ownedTitle.OwnedFormat,
                PosterPath = ownedTitle.PosterPath,
                PlexRatingKey = ownedTitle.PlexRatingKey,
                MovieId = ownedTitle.MovieId,
                IsOwned = true,
                StatusBrush = new SolidColorBrush(Colors.DarkGreen)
            };
        }

        WishlistItem? wishlistItem = _wishlistItems.FirstOrDefault(item =>
            EntryMatches(entry, item.Title, item.Year));

        if (wishlistItem is not null)
        {
            return new WatchOrderRowViewModel
            {
                Position = entry.Position,
                Title = entry.Title,
                Year = entry.Year,
                StatusText = "Wishlist",
                DetailText = "Saved to your wishlist",
                IsWishlist = true,
                StatusBrush = new SolidColorBrush(Colors.DarkGoldenrod)
            };
        }

        return new WatchOrderRowViewModel
        {
            Position = entry.Position,
            Title = entry.Title,
            Year = entry.Year,
            StatusText = "Missing",
            DetailText = "Not currently owned",
            StatusBrush = new SolidColorBrush(Colors.DimGray)
        };
    }

    private static bool EntryMatches(WatchOrderEntry entry, string candidateTitle, int candidateYear)
    {
        if (entry.Year > 0 && candidateYear > 0 && entry.Year != candidateYear)
        {
            return false;
        }

        string candidate = MediaIdentityService.NormalizeTitle(candidateTitle);
        if (string.IsNullOrWhiteSpace(candidate))
        {
            return false;
        }

        IEnumerable<string> acceptedTitles = new[] { entry.Title }.Concat(entry.Aliases);
        return acceptedTitles.Any(title =>
            string.Equals(
                MediaIdentityService.NormalizeTitle(title),
                candidate,
                StringComparison.OrdinalIgnoreCase));
    }

    private void WatchOrderComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (WatchOrderComboBox.SelectedItem is WatchOrderDefinition order)
        {
            PopulateWatchOrder(order);
        }
    }


    private async void AddMissingToWishlistButton_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not Button { Tag: CollectionSeriesTitleStatus title })
        {
            return;
        }

        await AddTitleToWishlistAsync(title, showSuccessMessage: true);
    }

    private async void AddAllMissingButton_Click(object sender, RoutedEventArgs e)
    {
        if (_series is null)
        {
            return;
        }

        List<CollectionSeriesTitleStatus> titles = _series.Titles
            .Where(title => title.CanAddToWishlist)
            .ToList();

        if (titles.Count == 0)
        {
            ShowWishlistStatus("Nothing to add", "Every missing title is already on your wishlist.", InfoBarSeverity.Informational);
            return;
        }

        int added = 0;
        foreach (CollectionSeriesTitleStatus title in titles)
        {
            if (await AddTitleToWishlistAsync(title, showSuccessMessage: false))
            {
                added++;
            }
        }

        ShowWishlistStatus(
            "Wishlist updated",
            added == 1 ? "1 missing movie was added to your wishlist." : $"{added} missing movies were added to your wishlist.",
            InfoBarSeverity.Success);
    }

    private async Task<bool> AddTitleToWishlistAsync(
        CollectionSeriesTitleStatus title,
        bool showSuccessMessage)
    {
        if (title.IsOwned || title.IsOnWishlist)
        {
            return false;
        }

        try
        {
            WishlistItem item = new()
            {
                MediaType = "Movie",
                Title = title.Title,
                Year = title.Year,
                PreferredFormat = "DVD",
                Priority = _series?.MissingCount == 1 ? 5 : 3,
                Notes = string.IsNullOrWhiteSpace(_series?.Name)
                    ? "Added from Collection Details"
                    : $"Added from the {_series.Name} collection"
            };

            int id = await _wishlistRepository.AddAsync(item);
            title.IsOnWishlist = true;
            title.WishlistItemId = id;
            title.PreferredFormat = item.PreferredFormat;
            title.WishlistTargetPrice = item.TargetPrice;

            RefreshMissingMovies();

            if (showSuccessMessage)
            {
                ShowWishlistStatus(
                    "Added to wishlist",
                    $"{title.DisplayTitle} was added as DVD.",
                    InfoBarSeverity.Success);
            }

            return true;
        }
        catch (InvalidOperationException)
        {
            WishlistItem? existing = await _wishlistRepository.FindMatchAsync(title.Title, title.Year, "Movie");
            if (existing is not null)
            {
                title.IsOnWishlist = true;
                title.WishlistItemId = existing.Id;
                title.PreferredFormat = existing.PreferredFormat;
                title.WishlistTargetPrice = existing.TargetPrice;
                RefreshMissingMovies();
                return false;
            }

            ShowWishlistStatus(
                "Could not add movie",
                $"{title.DisplayTitle} could not be added to the wishlist.",
                InfoBarSeverity.Error);
            return false;
        }
        catch (Exception exception)
        {
            ShowWishlistStatus("Could not add movie", exception.Message, InfoBarSeverity.Error);
            return false;
        }
    }

    private void RefreshMissingMovies()
    {
        if (_series is null)
        {
            return;
        }

        List<CollectionSeriesTitleStatus> missing = _series.Titles
            .Where(title => !title.IsOwned)
            .ToList();

        MissingMoviesList.ItemsSource = null;
        MissingMoviesList.ItemsSource = missing;
        AddAllMissingButton.IsEnabled = missing.Any(title => title.CanAddToWishlist);
    }

    private void ShowWishlistStatus(string title, string message, InfoBarSeverity severity)
    {
        WishlistStatusBar.Title = title;
        WishlistStatusBar.Message = message;
        WishlistStatusBar.Severity = severity;
        WishlistStatusBar.IsOpen = true;
    }

    private void OpenWishlistButton_Click(object sender, RoutedEventArgs e)
    {
        Frame.Navigate(typeof(WishlistPage));
    }

    private void OpenShoppingModeButton_Click(object sender, RoutedEventArgs e)
    {
        Frame.Navigate(typeof(ShoppingModePage));
    }

    private void BackButton_Click(object sender, RoutedEventArgs e)
    {
        if (Frame.CanGoBack)
        {
            Frame.GoBack();
        }
        else
        {
            Frame.Navigate(typeof(CollectionsPage));
        }
    }

    private void OwnedMoviesGrid_ItemClick(object sender, ItemClickEventArgs e)
    {
        if (e.ClickedItem is CollectionSeriesTitleStatus { MovieId: int movieId })
        {
            Frame.Navigate(typeof(MovieDetailsPage), movieId);
        }
    }

    private void WatchOrderList_ItemClick(object sender, ItemClickEventArgs e)
    {
        if (e.ClickedItem is WatchOrderRowViewModel { MovieId: int movieId })
        {
            Frame.Navigate(typeof(MovieDetailsPage), movieId);
        }
    }

    private sealed class WatchOrderRowViewModel
    {
        public int Position { get; set; }
        public string Title { get; set; } = string.Empty;
        public int Year { get; set; }
        public string StatusText { get; set; } = string.Empty;
        public string DetailText { get; set; } = string.Empty;
        public string PosterPath { get; set; } = string.Empty;
        public string PlexRatingKey { get; set; } = string.Empty;
        public int? MovieId { get; set; }
        public bool IsOwned { get; set; }
        public bool IsWishlist { get; set; }
        public Brush StatusBrush { get; set; } = new SolidColorBrush(Colors.DimGray);
        public string YearDisplay => Year > 0 ? Year.ToString() : "Year unknown";
    }
}
