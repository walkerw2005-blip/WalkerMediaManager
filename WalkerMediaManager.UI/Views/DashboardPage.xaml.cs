using System;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Navigation;
using WalkerMediaManager.UI.Models;
using WalkerMediaManager.UI.Services;

namespace WalkerMediaManager.UI.Views;

public sealed partial class DashboardPage : Page
{
    private readonly DashboardStatisticsService _statisticsService = new();

    public DashboardPage() => InitializeComponent();

    protected override async void OnNavigatedTo(NavigationEventArgs e)
    {
        base.OnNavigatedTo(e);
        try
        {
            DashboardErrorInfoBar.IsOpen = false;
            DashboardProgressRing.IsActive = true;
            DashboardProgressRing.Visibility = Visibility.Visible;
            DashboardStatistics statistics = await _statisticsService.GetAsync();

            MovieCountText.Text = statistics.MovieCount.ToString("N0");
            TvSeriesCountText.Text = statistics.TvSeriesCount.ToString("N0");
            OwnedTitleCountText.Text = statistics.OwnedTitleCount.ToString("N0");
            WishlistCountText.Text = statistics.WishlistCount.ToString("N0");
            RuntimeText.Text = statistics.RuntimeDisplay;
            CollectionCountText.Text = statistics.CollectionCount.ToString("N0");
            CollectionSummaryText.Text = statistics.CollectionSummaryDisplay;
            AverageCompletionText.Text = statistics.AverageCompletionDisplay;
            AverageCompletionBar.Value = statistics.AverageCollectionCompletion;

            TopGenresList.ItemsSource = statistics.TopGenres;
            DecadesList.ItemsSource = statistics.Decades;
            FormatsList.ItemsSource = statistics.Formats;
            InsightsList.ItemsSource = statistics.Insights;
            AchievementsList.ItemsSource = statistics.Achievements;
            ActivityList.ItemsSource = statistics.Activity;

            GenresEmptyText.Visibility = statistics.TopGenres.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
            DecadesEmptyText.Visibility = statistics.Decades.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
            FormatsEmptyText.Visibility = statistics.Formats.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
            InsightsEmptyText.Visibility = statistics.Insights.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
            ActivityEmptyText.Visibility = statistics.Activity.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
        }
        catch (Exception exception)
        {
            DashboardErrorInfoBar.Title = "Dashboard could not be loaded";
            DashboardErrorInfoBar.Message = exception.Message;
            DashboardErrorInfoBar.Severity = InfoBarSeverity.Error;
            DashboardErrorInfoBar.IsOpen = true;
        }
        finally
        {
            DashboardProgressRing.IsActive = false;
            DashboardProgressRing.Visibility = Visibility.Collapsed;
        }
    }

    private void InsightAction_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not Button button || button.Tag is not string action) return;
        switch (action)
        {
            case "collections": Frame.Navigate(typeof(CollectionsPage)); break;
            case "recommendations": Frame.Navigate(typeof(RecommendationsPage)); break;
            case "wishlist": Frame.Navigate(typeof(WishlistPage)); break;
            case "movies": Frame.Navigate(typeof(MoviesPage)); break;
        }
    }

    private void OpenSmartBuy_Click(object sender, RoutedEventArgs e) => Frame.Navigate(typeof(SmartBuyPage));
    private void OpenRecommendations_Click(object sender, RoutedEventArgs e) => Frame.Navigate(typeof(RecommendationsPage));
    private void OpenCollections_Click(object sender, RoutedEventArgs e) => Frame.Navigate(typeof(CollectionsPage));
    private void OpenWishlist_Click(object sender, RoutedEventArgs e) => Frame.Navigate(typeof(WishlistPage));
    private void OpenMovies_Click(object sender, RoutedEventArgs e) => Frame.Navigate(typeof(MoviesPage));
}
