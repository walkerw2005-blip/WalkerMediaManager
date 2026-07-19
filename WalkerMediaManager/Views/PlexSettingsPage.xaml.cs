using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using WalkerMediaManager.Models;

namespace WalkerMediaManager.Views;

public sealed partial class PlexSettingsPage : Page
{
    public PlexSettingsPage()
    {
        InitializeComponent();
        Loaded += PlexSettingsPage_Loaded;
    }

    private async void PlexSettingsPage_Loaded(object sender, RoutedEventArgs e)
    {
        var settings = await App.SettingsService.LoadAsync();
        ServerUrlBox.Text = settings.PlexServerUrl;
        TokenBox.Password = App.CredentialService.LoadPlexToken();
    }

    private async void TestButton_Click(object sender, RoutedEventArgs e)
    {
        SetBusy(true);
        try
        {
            var result = await App.PlexService.TestConnectionAsync(ServerUrlBox.Text, TokenBox.Password);
            StatusBar.IsOpen = true;
            StatusBar.Title = result.Success ? "Connected" : "Connection failed";
            StatusBar.Message = result.Message;
            StatusBar.Severity = result.Success ? InfoBarSeverity.Success : InfoBarSeverity.Error;

            if (!result.Success) return;

            var libraries = await App.PlexService.GetLibrariesAsync(ServerUrlBox.Text, TokenBox.Password);
            LibrariesList.ItemsSource = libraries;
            await App.DatabaseService.SaveLibrariesAsync(libraries);

            var settings = new AppSettings
            {
                PlexServerUrl = ServerUrlBox.Text.Trim(),
                LastSuccessfulConnection = DateTimeOffset.Now
            };
            App.CredentialService.SavePlexToken(TokenBox.Password);
            await App.SettingsService.SaveAsync(settings);
        }
        catch (Exception ex)
        {
            StatusBar.IsOpen = true;
            StatusBar.Title = "Unable to load libraries";
            StatusBar.Message = ex.Message;
            StatusBar.Severity = InfoBarSeverity.Error;
        }
        finally
        {
            SetBusy(false);
        }
    }

    private async void SaveButton_Click(object sender, RoutedEventArgs e)
    {
        var existing = await App.SettingsService.LoadAsync();
        existing.PlexServerUrl = ServerUrlBox.Text.Trim();
        App.CredentialService.SavePlexToken(TokenBox.Password);
        await App.SettingsService.SaveAsync(existing);

        StatusBar.IsOpen = true;
        StatusBar.Title = "Saved";
        StatusBar.Message = "Your Plex settings were saved locally.";
        StatusBar.Severity = InfoBarSeverity.Success;
    }

    private void SetBusy(bool busy)
    {
        BusyRing.IsActive = busy;
        TestButton.IsEnabled = !busy;
    }
}
