using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using WalkerMediaManager.Views;

namespace WalkerMediaManager;

public sealed partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
        NavView.SelectedItem = NavView.MenuItems[0];
        ContentFrame.Navigate(typeof(DashboardPage));
    }

    private void NavView_SelectionChanged(NavigationView sender, NavigationViewSelectionChangedEventArgs args)
    {
        var tag = args.SelectedItemContainer?.Tag?.ToString();
        ContentFrame.Navigate(tag switch
        {
            "plex" => typeof(PlexSettingsPage),
            "updates" => typeof(UpdatesPage),
            _ => typeof(DashboardPage)
        });
    }
}
