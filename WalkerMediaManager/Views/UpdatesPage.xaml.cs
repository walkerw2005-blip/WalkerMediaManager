using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace WalkerMediaManager.Views;

public sealed partial class UpdatesPage : Page
{
    private string? _downloadUrl;
    private string? _releasePageUrl;

    public UpdatesPage()
    {
        InitializeComponent();
        CurrentVersionText.Text = App.UpdateService.CurrentVersion;
    }

    private async void CheckButton_Click(object sender, RoutedEventArgs e)
    {
        SetBusy(true);
        var result = await App.UpdateService.CheckAsync();
        SetBusy(false);

        StatusBar.IsOpen = true;
        StatusBar.Title = result.UpdateAvailable ? $"Version {result.LatestVersion} is available" : "Update status";
        StatusBar.Message = result.Message;
        StatusBar.Severity = !result.Success
            ? InfoBarSeverity.Warning
            : result.UpdateAvailable ? InfoBarSeverity.Success : InfoBarSeverity.Informational;

        _downloadUrl = result.DownloadUrl;
        _releasePageUrl = result.ReleasePageUrl;
        DownloadButton.IsEnabled = result.UpdateAvailable &&
                                   (!string.IsNullOrWhiteSpace(_downloadUrl) || !string.IsNullOrWhiteSpace(_releasePageUrl));
    }

    private async void DownloadButton_Click(object sender, RoutedEventArgs e)
    {
        if (!string.IsNullOrWhiteSpace(_downloadUrl))
        {
            try
            {
                SetBusy(true);
                StatusBar.IsOpen = true;
                StatusBar.Title = "Downloading update";
                StatusBar.Message = "The installer will open when the download is complete.";
                StatusBar.Severity = InfoBarSeverity.Informational;

                var installer = await App.UpdateService.DownloadInstallerAsync(_downloadUrl);
                Services.UpdateService.LaunchInstaller(installer);
            }
            catch (Exception ex)
            {
                StatusBar.Title = "Update download failed";
                StatusBar.Message = ex.Message;
                StatusBar.Severity = InfoBarSeverity.Error;
            }
            finally
            {
                SetBusy(false);
            }
            return;
        }

        if (!string.IsNullOrWhiteSpace(_releasePageUrl))
            Services.UpdateService.OpenUrl(_releasePageUrl);
    }

    private void SetBusy(bool busy)
    {
        BusyRing.IsActive = busy;
        CheckButton.IsEnabled = !busy;
        DownloadButton.IsEnabled = !busy &&
                                   (!string.IsNullOrWhiteSpace(_downloadUrl) || !string.IsNullOrWhiteSpace(_releasePageUrl));
    }
}
