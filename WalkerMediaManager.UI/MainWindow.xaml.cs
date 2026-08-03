using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Navigation;
using System;
using WalkerMediaManager.UI.Services;
using WalkerMediaManager.UI.Views;

namespace WalkerMediaManager.UI;

public sealed partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
        Title = "Walker Media Manager";
        ContentFrame.NavigationFailed += ContentFrame_NavigationFailed;

        NavigateTo(typeof(DashboardPage), "initial startup");
    }

    private void MainNavigationView_SelectionChanged(
        NavigationView sender,
        NavigationViewSelectionChangedEventArgs args)
    {
        if (args.IsSettingsSelected)
        {
            NavigateTo(typeof(SettingsPage), "Settings");
            return;
        }

        if (args.SelectedItemContainer is not NavigationViewItem selectedItem)
        {
            DiagnosticsService.Log("Navigation selection did not contain a NavigationViewItem.");
            return;
        }

        string? tag = selectedItem.Tag?.ToString();

        Type? destinationPage = tag switch
        {
            "dashboard" => typeof(DashboardPage),
            "recommendations" => typeof(RecommendationsPage),
            "smartbuy" => typeof(SmartBuyPage),
            "shopping" => typeof(ShoppingModePage),
            "movies" => typeof(MoviesPage),
            "slideshows" => typeof(SlideshowsPage),
            "tvshows" => typeof(TvShowsPage),
            "collections" => typeof(CollectionsPage),
            "wishlist" => typeof(WishlistPage),
            "locations" => typeof(StorageLocationsPage),
            "purchases" => typeof(PurchaseHistoryPage),
            "reports" => typeof(ReportsPage),
            "intelligence" => typeof(CollectionIntelligencePage),
            _ => null
        };

        if (destinationPage is null)
        {
            DiagnosticsService.Log($"No destination page is registered for navigation tag '{tag ?? "<null>"}'.");
            return;
        }

        NavigateTo(destinationPage, selectedItem.Content?.ToString() ?? tag ?? destinationPage.Name);
    }

    private void NavigateTo(Type pageType, string destinationName)
    {
        if (ContentFrame.CurrentSourcePageType == pageType)
        {
            return;
        }

        try
        {
            DiagnosticsService.Log($"Navigating to {destinationName} ({pageType.Name}).");

            bool navigationStarted = ContentFrame.Navigate(pageType);
            if (!navigationStarted)
            {
                throw new InvalidOperationException(
                    $"The frame declined navigation to {destinationName} ({pageType.FullName}).");
            }

            DiagnosticsService.Log($"Navigation completed: {destinationName}.");
        }
        catch (Exception exception)
        {
            DiagnosticsService.LogException(
                $"Navigation to {destinationName} ({pageType.FullName}) failed.",
                exception);

            ContentFrame.Content = CreateNavigationErrorView(destinationName, exception);
        }
    }

    private void ContentFrame_NavigationFailed(object sender, NavigationFailedEventArgs e)
    {
        DiagnosticsService.LogException(
            $"Frame navigation failed for {e.SourcePageType?.FullName ?? "an unknown page"}.",
            e.Exception);

        e.Handled = true;
        ContentFrame.Content = CreateNavigationErrorView(
            e.SourcePageType?.Name ?? "the requested page",
            e.Exception);
    }

    private static UIElement CreateNavigationErrorView(
        string destinationName,
        Exception exception)
    {
        return new ScrollViewer
        {
            Content = new StackPanel
            {
                Padding = new Thickness(32),
                Spacing = 12,
                Children =
                {
                    new TextBlock
                    {
                        Text = $"Could not open {destinationName}",
                        FontSize = 26,
                        FontWeight = Microsoft.UI.Text.FontWeights.SemiBold
                    },
                    new TextBlock
                    {
                        Text = exception.Message,
                        TextWrapping = TextWrapping.Wrap
                    },
                    new TextBlock
                    {
                        Text = $"Diagnostic log: {DiagnosticsService.LogFilePath}",
                        TextWrapping = TextWrapping.Wrap,
                        Opacity = 0.7
                    }
                }
            }
        };
    }
}
