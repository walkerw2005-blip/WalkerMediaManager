using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using System;
using WalkerMediaManager.UI.Views;
using Windows.UI.ApplicationSettings;

namespace WalkerMediaManager.UI;

public sealed partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
        Title = "Walker Media Manager";

        ContentFrame.Navigate(typeof(DashboardPage));
    }

    private void MainNavigationView_SelectionChanged(
        NavigationView sender,
        NavigationViewSelectionChangedEventArgs args)
    {
        if (args.IsSettingsSelected)
        {
            NavigateTo(typeof(SettingsPage));
            return;
        }

        if (args.SelectedItemContainer is not NavigationViewItem selectedItem)
        {
            return;
        }

        string? tag = selectedItem.Tag?.ToString();

        Type? destinationPage = tag switch
        {
            "dashboard" => typeof(DashboardPage),
            "smartbuy" => typeof(SmartBuyPage),
            "movies" => typeof(MoviesPage),
            "tvshows" => typeof(TvShowsPage),
            "collections" => typeof(CollectionsPage),
            "wishlist" => typeof(WishlistPage),
            "reports" => typeof(ReportsPage),
            _ => null
        };

        if (destinationPage is not null)
        {
            NavigateTo(destinationPage);
        }
    }

    private void NavigateTo(Type pageType)
    {
        if (ContentFrame.CurrentSourcePageType != pageType)
        {
            ContentFrame.Navigate(pageType);
        }
    }
}