using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace WalkerMediaManager.Views;

public sealed partial class DashboardPage : Page
{
    public DashboardPage()
    {
        InitializeComponent();
        Loaded += DashboardPage_Loaded;
    }

    private async void DashboardPage_Loaded(object sender, RoutedEventArgs e)
    {
        LibraryCountText.Text = (await App.DatabaseService.GetLibraryCountAsync()).ToString();
        var settings = await App.SettingsService.LoadAsync();
        ConnectionText.Text = settings.LastSuccessfulConnection.HasValue ? "Connected" : "Not configured";

        var update = await App.UpdateService.CheckAsync();
        if (update.Success && update.UpdateAvailable)
        {
            UpdateInfoBar.Message = $"Version {update.LatestVersion} is ready. Open Updates to download it.";
            UpdateInfoBar.IsOpen = true;
        }
    }
}
