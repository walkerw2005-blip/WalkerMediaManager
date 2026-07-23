using System;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Navigation;
using WalkerMediaManager.UI.Repositories;

namespace WalkerMediaManager.UI.Views;

public sealed partial class DashboardPage : Page
{
    private readonly MovieRepository _movieRepository = new();
    private readonly TVShowRepository _tvShowRepository = new();
    private readonly WishlistRepository _wishlistRepository = new();
    private readonly CollectionHealthRepository
        _collectionHealthRepository = new();

    public DashboardPage()
    {
        InitializeComponent();
    }

    protected override async void OnNavigatedTo(
        NavigationEventArgs e)
    {
        base.OnNavigatedTo(e);

        try
        {
            DashboardErrorInfoBar.IsOpen = false;
            DashboardProgressRing.IsActive = true;
            DashboardProgressRing.Visibility = Visibility.Visible;

            int movieCount =
                await _movieRepository.CountAsync();

            int tvShowCount =
                await _tvShowRepository.CountAsync();

            int wishlistCount =
                await _wishlistRepository.CountAsync();

            int duplicateCount =
                await _collectionHealthRepository
                    .CountPossibleDuplicateMoviesAsync();

            int incompleteCollectionCount =
                await _collectionHealthRepository
                    .CountIncompleteCollectionsAsync();

            int missingMetadataCount =
                await _collectionHealthRepository
                    .CountMoviesWithMissingMetadataAsync();

            MovieCountText.Text =
                movieCount.ToString();

            TvSeriesCountText.Text =
                tvShowCount.ToString();

            OwnedTitleCountText.Text =
                (movieCount + tvShowCount).ToString();

            WishlistCountText.Text =
                wishlistCount.ToString();

            DuplicateCountText.Text =
                duplicateCount.ToString();

            IncompleteCollectionCountText.Text =
                incompleteCollectionCount.ToString();

            MissingMetadataCountText.Text =
                missingMetadataCount.ToString();

            DatabaseStatusText.Text = "Connected";
        }
        catch (Exception exception)
        {
            DatabaseStatusText.Text = "Error";

            DashboardErrorInfoBar.Message =
                $"Dashboard data could not be loaded: {exception.Message}";

            DashboardErrorInfoBar.Severity =
                InfoBarSeverity.Error;

            DashboardErrorInfoBar.IsOpen = true;
        }
        finally
        {
            DashboardProgressRing.IsActive = false;
            DashboardProgressRing.Visibility =
                Visibility.Collapsed;
        }
    }

    private void OpenSmartBuy_Click(
        object sender,
        RoutedEventArgs e)
    {
        Frame.Navigate(typeof(SmartBuyPage));
    }
}