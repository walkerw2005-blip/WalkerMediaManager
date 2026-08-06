using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using WalkerMediaManager.UI.Models;
using WalkerMediaManager.UI.Services;
using Windows.Security.Credentials;

namespace WalkerMediaManager.UI.Views;

public sealed partial class SettingsPage : Page
{
    private const string ServerUrlSettingKey = "PlexServerUrl";
    private const string MovieLibraryKeySettingKey = "PlexMovieLibraryKey";
    private const string TVLibraryKeySettingKey = "PlexTVLibraryKey";
    private const string SlideshowLibraryKeySettingKey = "PlexSlideshowLibraryKey";
    private const string CredentialResource = "WalkerMediaManager.Plex";
    private const string CredentialUserName = "PlexToken";

    private readonly PlexService _plexService = new();
    private readonly PlexMovieSyncService _plexMovieSyncService = new();
    private readonly PlexTVSyncService _plexTVSyncService = new();
    private readonly ArtworkMaintenanceService _artworkMaintenanceService = new();
    private bool _artworkMaintenanceRunning;
    private CancellationTokenSource? _artworkMaintenanceCts;

    public ObservableCollection<PlexLibrarySection> Libraries { get; } = [];
    public ObservableCollection<PlexLibrarySection> MovieLibraries { get; } = [];
    public ObservableCollection<PlexLibrarySection> TVLibraries { get; } = [];

    public SettingsPage()
    {
        InitializeComponent();
        Loaded += SettingsPage_Loaded;
        Unloaded += (_, _) => _artworkMaintenanceCts?.Cancel();
    }

    private void SettingsPage_Loaded(
        object sender,
        RoutedEventArgs e)
    {
        ServerUrlBox.Text = SettingsService.GetString(ServerUrlSettingKey);
        TokenBox.Password = LoadToken();
    }

    private async void TestConnectionButton_Click(
        object sender,
        RoutedEventArgs e)
    {
        await TestConnectionAsync();
    }

    private async void SaveSettingsButton_Click(
        object sender,
        RoutedEventArgs e)
    {
        string serverUrl = ServerUrlBox.Text.Trim();
        string token = TokenBox.Password.Trim();

        if (string.IsNullOrWhiteSpace(serverUrl) ||
            string.IsNullOrWhiteSpace(token))
        {
            ShowConnectionMessage(
                "Enter both the Plex server address and token.",
                InfoBarSeverity.Warning);
            return;
        }

        try
        {
            SettingsService.SetString(ServerUrlSettingKey, serverUrl);
            SaveToken(token);
            SaveSelectedLibraries();

            ShowConnectionMessage(
                "Plex settings were saved securely on this computer.",
                InfoBarSeverity.Success);

            await TestConnectionAsync();
        }
        catch (Exception exception)
        {
            DiagnosticsService.LogException(
                "SettingsPage failed to save Plex settings.",
                exception);

            ShowConnectionMessage(
                $"Plex settings could not be saved: {exception.Message}",
                InfoBarSeverity.Error);
        }
    }

    private async Task TestConnectionAsync()
    {
        string serverUrl = ServerUrlBox.Text.Trim();
        string token = TokenBox.Password.Trim();

        if (string.IsNullOrWhiteSpace(serverUrl) ||
            string.IsNullOrWhiteSpace(token))
        {
            ShowConnectionMessage(
                "Enter both the Plex server address and token.",
                InfoBarSeverity.Warning);
            return;
        }

        SetConnectionBusy(true);

        try
        {
            string connectionMessage =
                await _plexService.TestConnectionAsync(
                    serverUrl,
                    token);

            Libraries.Clear();
            MovieLibraries.Clear();
            TVLibraries.Clear();

            foreach (
                PlexLibrarySection library
                in await _plexService.GetLibrarySectionsAsync(
                    serverUrl,
                    token))
            {
                Libraries.Add(library);

                if (string.Equals(
                        library.Type,
                        "movie",
                        StringComparison.OrdinalIgnoreCase))
                {
                    MovieLibraries.Add(library);
                }
                else if (string.Equals(
                             library.Type,
                             "show",
                             StringComparison.OrdinalIgnoreCase))
                {
                    TVLibraries.Add(library);
                }
            }

            RestoreSelectedLibraries();
            SyncMoviesButton.IsEnabled = MovieLibraries.Count > 0;
            SyncSlideshowsButton.IsEnabled = MovieLibraries.Count > 0;
            SyncTVShowsButton.IsEnabled = TVLibraries.Count > 0;

            ShowConnectionMessage(
                $"{connectionMessage} Found {Libraries.Count} libraries.",
                InfoBarSeverity.Success);
        }
        catch (Exception exception)
        {
            DiagnosticsService.LogException("SettingsPage Plex connection test failed.", exception);

            Libraries.Clear();
            MovieLibraries.Clear();
            TVLibraries.Clear();
            SyncMoviesButton.IsEnabled = false;
            SyncSlideshowsButton.IsEnabled = false;
            SyncTVShowsButton.IsEnabled = false;

            ShowConnectionMessage(
                $"Plex connection failed: {exception.Message}",
                InfoBarSeverity.Error);
        }
        finally
        {
            SetConnectionBusy(false);
        }
    }

    private async void SyncMoviesButton_Click(
        object sender,
        RoutedEventArgs e)
    {
        if (MovieLibraryComboBox.SelectedItem is not PlexLibrarySection library)
        {
            ShowMovieSyncMessage(
                "Select the Plex movie library to sync.",
                InfoBarSeverity.Warning);
            return;
        }

        string serverUrl = ServerUrlBox.Text.Trim();
        string token = TokenBox.Password.Trim();

        if (string.IsNullOrWhiteSpace(serverUrl) ||
            string.IsNullOrWhiteSpace(token))
        {
            ShowMovieSyncMessage(
                "Enter the Plex server address and token first.",
                InfoBarSeverity.Warning);
            return;
        }

        SaveSelectedLibraries();
        SetMovieSyncBusy(true);

        try
        {
            MovieSyncProgressText.Text = "Creating database backup...";
            await DatabaseBackupService.CreateBackupAsync();

            Progress<string> progress = new(
                message => MovieSyncProgressText.Text = message);

            PlexSyncResult result =
                await _plexMovieSyncService.SyncMoviesAsync(
                    serverUrl,
                    token,
                    library.Key,
                    library.Title,
                    progress);

            MovieSyncProgressText.Text = "Movie sync complete.";

            ShowMovieSyncMessage(
                result.Summary,
                result.FailedCount > 0
                    ? InfoBarSeverity.Warning
                    : InfoBarSeverity.Success);
        }
        catch (Exception exception)
        {
            DiagnosticsService.LogException("SettingsPage movie sync failed.", exception);
            ShowMovieSyncMessage(
                $"Plex movie sync failed: {exception.Message}",
                InfoBarSeverity.Error);
        }
        finally
        {
            SetMovieSyncBusy(false);
        }
    }

    private async void SyncSlideshowsButton_Click(
        object sender,
        RoutedEventArgs e)
    {
        if (SlideshowLibraryComboBox.SelectedItem is not PlexLibrarySection library)
        {
            ShowSlideshowSyncMessage(
                "Select the Plex Slide Shows library to sync.",
                InfoBarSeverity.Warning);
            return;
        }

        string serverUrl = ServerUrlBox.Text.Trim();
        string token = TokenBox.Password.Trim();

        if (string.IsNullOrWhiteSpace(serverUrl) ||
            string.IsNullOrWhiteSpace(token))
        {
            ShowSlideshowSyncMessage(
                "Enter the Plex server address and token first.",
                InfoBarSeverity.Warning);
            return;
        }

        SaveSelectedLibraries();
        SetSlideshowSyncBusy(true);

        try
        {
            SlideshowSyncProgressText.Text = "Creating database backup...";
            await DatabaseBackupService.CreateBackupAsync();

            Progress<string> progress = new(
                message => SlideshowSyncProgressText.Text = message);

            PlexSyncResult result =
                await _plexMovieSyncService.SyncMoviesAsync(
                    serverUrl,
                    token,
                    library.Key,
                    library.Title,
                    progress);

            SlideshowSyncProgressText.Text = "Slide show sync complete.";

            ShowSlideshowSyncMessage(
                result.Summary,
                result.FailedCount > 0
                    ? InfoBarSeverity.Warning
                    : InfoBarSeverity.Success);
        }
        catch (Exception exception)
        {
            DiagnosticsService.LogException("SettingsPage slide show sync failed.", exception);
            ShowSlideshowSyncMessage(
                $"Plex slide show sync failed: {exception.Message}",
                InfoBarSeverity.Error);
        }
        finally
        {
            SetSlideshowSyncBusy(false);
        }
    }

    private async void SyncTVShowsButton_Click(
        object sender,
        RoutedEventArgs e)
    {
        if (TVLibraryComboBox.SelectedItem is not PlexLibrarySection library)
        {
            ShowTVSyncMessage(
                "Select the Plex TV library to sync.",
                InfoBarSeverity.Warning);
            return;
        }

        string serverUrl = ServerUrlBox.Text.Trim();
        string token = TokenBox.Password.Trim();

        if (string.IsNullOrWhiteSpace(serverUrl) ||
            string.IsNullOrWhiteSpace(token))
        {
            ShowTVSyncMessage(
                "Enter the Plex server address and token first.",
                InfoBarSeverity.Warning);
            return;
        }

        SaveSelectedLibraries();
        SetTVSyncBusy(true);

        try
        {
            TVSyncProgressText.Text = "Creating database backup...";
            await DatabaseBackupService.CreateBackupAsync();

            Progress<string> progress = new(
                message => TVSyncProgressText.Text = message);

            PlexSyncResult result =
                await _plexTVSyncService.SyncTVShowsAsync(
                    serverUrl,
                    token,
                    library.Key,
                    progress);

            TVSyncProgressText.Text = "TV show sync complete.";

            ShowTVSyncMessage(
                result.Summary,
                result.FailedCount > 0
                    ? InfoBarSeverity.Warning
                    : InfoBarSeverity.Success);
        }
        catch (Exception exception)
        {
            DiagnosticsService.LogException("SettingsPage TV show sync failed.", exception);
            ShowTVSyncMessage(
                $"Plex TV show sync failed: {exception.Message}",
                InfoBarSeverity.Error);
        }
        finally
        {
            SetTVSyncBusy(false);
        }
    }

    private async void RefreshMissingPostersButton_Click(object sender, RoutedEventArgs e)
    {
        await RunArtworkRefreshAsync(refreshAll: false);
    }

    private async void RefreshAllPostersButton_Click(object sender, RoutedEventArgs e)
    {
        if (!await ConfirmArtworkActionAsync(
                "Refresh every cached poster?",
                "Every movie and TV poster with a source path will be downloaded again. This may take several minutes.",
                "Refresh All"))
        {
            return;
        }

        await RunArtworkRefreshAsync(refreshAll: true);
    }

    private async Task RunArtworkRefreshAsync(bool refreshAll)
    {
        if (_artworkMaintenanceRunning)
        {
            return;
        }

        _artworkMaintenanceCts = new CancellationTokenSource();
        SetArtworkMaintenanceBusy(true, indeterminate: false, allowCancellation: true);
        ArtworkMaintenanceProgressText.Text = refreshAll
            ? "Preparing to refresh all posters..."
            : "Checking for uncached posters...";

        Progress<ArtworkMaintenanceProgress> progress = new(update =>
        {
            ArtworkMaintenanceProgressBar.Maximum = Math.Max(1, update.Total);
            ArtworkMaintenanceProgressBar.Value = Math.Min(update.Current, update.Total);
            ArtworkMaintenanceProgressText.Text = update.Message;
        });

        try
        {
            ArtworkMaintenanceResult result = await _artworkMaintenanceService.RefreshPostersAsync(
                refreshAll,
                progress,
                _artworkMaintenanceCts.Token);
            ArtworkMaintenanceProgressText.Text = "Artwork refresh complete.";
            ShowArtworkMaintenanceMessage(
                result.Summary,
                result.FailedCount > 0 || result.MissingSourceCount > 0
                    ? InfoBarSeverity.Warning
                    : InfoBarSeverity.Success);
        }
        catch (OperationCanceledException)
        {
            ArtworkMaintenanceProgressText.Text = "Artwork refresh canceled.";
            ShowArtworkMaintenanceMessage(
                "Artwork refresh was canceled. Posters completed before cancellation remain safely cached.",
                InfoBarSeverity.Informational);
        }
        catch (Exception exception)
        {
            DiagnosticsService.LogException("SettingsPage artwork refresh failed.", exception);
            ShowArtworkMaintenanceMessage(
                $"Artwork refresh failed: {exception.Message}",
                InfoBarSeverity.Error);
        }
        finally
        {
            _artworkMaintenanceCts.Dispose();
            _artworkMaintenanceCts = null;
            SetArtworkMaintenanceBusy(false, indeterminate: false, allowCancellation: false);
        }
    }

    private async void VerifyArtworkCacheButton_Click(object sender, RoutedEventArgs e)
    {
        if (_artworkMaintenanceRunning)
        {
            return;
        }

        _artworkMaintenanceCts = new CancellationTokenSource();
        SetArtworkMaintenanceBusy(true, indeterminate: true, allowCancellation: true);
        ArtworkMaintenanceProgressText.Text = "Verifying cached artwork files...";

        try
        {
            ArtworkCacheVerificationResult result = await ArtworkService.Current.VerifyCacheAsync(
                _artworkMaintenanceCts.Token);
            ArtworkMaintenanceProgressText.Text = "Artwork cache verification complete.";
            ShowArtworkMaintenanceMessage(
                result.Summary,
                result.RemovedFiles > 0 ? InfoBarSeverity.Warning : InfoBarSeverity.Success);
        }
        catch (OperationCanceledException)
        {
            ArtworkMaintenanceProgressText.Text = "Artwork cache verification canceled.";
            ShowArtworkMaintenanceMessage(
                "Artwork cache verification was canceled. Files already checked remain unchanged.",
                InfoBarSeverity.Informational);
        }
        catch (Exception exception)
        {
            DiagnosticsService.LogException("SettingsPage artwork cache verification failed.", exception);
            ShowArtworkMaintenanceMessage(
                $"Artwork cache verification failed: {exception.Message}",
                InfoBarSeverity.Error);
        }
        finally
        {
            _artworkMaintenanceCts.Dispose();
            _artworkMaintenanceCts = null;
            SetArtworkMaintenanceBusy(false, indeterminate: false, allowCancellation: false);
        }
    }

    private async void ClearArtworkCacheButton_Click(object sender, RoutedEventArgs e)
    {
        if (!await ConfirmArtworkActionAsync(
                "Clear the artwork cache?",
                "Downloaded poster files and missing-artwork markers will be removed. Library records and source paths will remain unchanged, and posters will download again as needed.",
                "Clear Cache"))
        {
            return;
        }

        SetArtworkMaintenanceBusy(true, indeterminate: true, allowCancellation: false);
        ArtworkMaintenanceProgressText.Text = "Clearing the artwork cache...";

        try
        {
            await ArtworkService.Current.ClearCacheAsync();
            ArtworkMaintenanceProgressText.Text = "Artwork cache cleared.";
            ShowArtworkMaintenanceMessage(
                "The disposable artwork cache was cleared. Your library and poster source paths were not changed.",
                InfoBarSeverity.Success);
        }
        catch (Exception exception)
        {
            DiagnosticsService.LogException("SettingsPage could not clear the artwork cache.", exception);
            ShowArtworkMaintenanceMessage(
                $"The artwork cache could not be cleared: {exception.Message}",
                InfoBarSeverity.Error);
        }
        finally
        {
            SetArtworkMaintenanceBusy(false, indeterminate: false, allowCancellation: false);
        }
    }

    private void CancelArtworkMaintenanceButton_Click(object sender, RoutedEventArgs e)
    {
        CancelArtworkMaintenanceButton.IsEnabled = false;
        ArtworkMaintenanceProgressText.Text = "Canceling after the current file...";
        _artworkMaintenanceCts?.Cancel();
    }

    private async Task<bool> ConfirmArtworkActionAsync(
        string title,
        string message,
        string primaryButtonText)
    {
        ContentDialog dialog = new()
        {
            Title = title,
            Content = message,
            PrimaryButtonText = primaryButtonText,
            CloseButtonText = "Cancel",
            DefaultButton = ContentDialogButton.Close,
            XamlRoot = XamlRoot
        };

        return await dialog.ShowAsync() == ContentDialogResult.Primary;
    }

    private void RestoreSelectedLibraries()
    {
        string movieKey = SettingsService.GetString(MovieLibraryKeySettingKey);

        MovieLibraryComboBox.SelectedItem = MovieLibraries
            .FirstOrDefault(item => item.Key == movieKey)
            ?? MovieLibraries.FirstOrDefault();

        string slideshowKey =
            SettingsService.GetString(SlideshowLibraryKeySettingKey);

        SlideshowLibraryComboBox.SelectedItem = MovieLibraries
            .FirstOrDefault(item => item.Key == slideshowKey)
            ?? MovieLibraries.FirstOrDefault(
                item => item.Title.Contains(
                    "slide",
                    StringComparison.OrdinalIgnoreCase));

        string tvKey = SettingsService.GetString(TVLibraryKeySettingKey);

        TVLibraryComboBox.SelectedItem = TVLibraries
            .FirstOrDefault(item => item.Key == tvKey)
            ?? TVLibraries.FirstOrDefault();
    }

    private void SaveSelectedLibraries()
    {
        if (MovieLibraryComboBox.SelectedItem is PlexLibrarySection movieLibrary)
        {
            SettingsService.SetString(
                MovieLibraryKeySettingKey,
                movieLibrary.Key);
        }

        if (SlideshowLibraryComboBox.SelectedItem is PlexLibrarySection slideshowLibrary)
        {
            SettingsService.SetString(
                SlideshowLibraryKeySettingKey,
                slideshowLibrary.Key);
        }

        if (TVLibraryComboBox.SelectedItem is PlexLibrarySection tvLibrary)
        {
            SettingsService.SetString(
                TVLibraryKeySettingKey,
                tvLibrary.Key);
        }
    }

    private static string LoadToken()
    {
        try
        {
            PasswordVault vault = new();

            PasswordCredential credential = vault.Retrieve(
                CredentialResource,
                CredentialUserName);

            credential.RetrievePassword();
            return credential.Password ?? string.Empty;
        }
        catch
        {
            return string.Empty;
        }
    }

    private static void SaveToken(string token)
    {
        PasswordVault vault = new();

        try
        {
            PasswordCredential existing = vault.Retrieve(
                CredentialResource,
                CredentialUserName);

            vault.Remove(existing);
        }
        catch
        {
        }

        vault.Add(
            new PasswordCredential(
                CredentialResource,
                CredentialUserName,
                token));
    }

    private void SetConnectionBusy(bool isBusy)
    {
        TestConnectionButton.IsEnabled = !isBusy;
        SaveSettingsButton.IsEnabled = !isBusy;
        ServerUrlBox.IsEnabled = !isBusy;
        TokenBox.IsEnabled = !isBusy;

        ConnectionProgressRing.IsActive = isBusy;
        ConnectionProgressRing.Visibility =
            isBusy ? Visibility.Visible : Visibility.Collapsed;
        SetArtworkMaintenanceButtonsEnabled(!isBusy && !_artworkMaintenanceRunning);
    }

    private void SetMovieSyncBusy(bool isBusy)
    {
        SyncMoviesButton.IsEnabled = !isBusy && MovieLibraries.Count > 0;
        MovieLibraryComboBox.IsEnabled = !isBusy;
        SyncTVShowsButton.IsEnabled = !isBusy && TVLibraries.Count > 0;
        TVLibraryComboBox.IsEnabled = !isBusy;
        TestConnectionButton.IsEnabled = !isBusy;
        SaveSettingsButton.IsEnabled = !isBusy;

        MovieSyncProgressRing.IsActive = isBusy;
        MovieSyncProgressRing.Visibility =
            isBusy ? Visibility.Visible : Visibility.Collapsed;
        SetArtworkMaintenanceButtonsEnabled(!isBusy && !_artworkMaintenanceRunning);
    }

    private void SetSlideshowSyncBusy(bool isBusy)
    {
        SyncSlideshowsButton.IsEnabled = !isBusy;
        SlideshowSyncProgressRing.IsActive = isBusy;
        SlideshowSyncProgressRing.Visibility =
            isBusy ? Visibility.Visible : Visibility.Collapsed;
        SetArtworkMaintenanceButtonsEnabled(!isBusy && !_artworkMaintenanceRunning);
    }

    private void ShowSlideshowSyncMessage(
        string message,
        InfoBarSeverity severity)
    {
        SlideshowSyncInfoBar.Message = message;
        SlideshowSyncInfoBar.Severity = severity;
        SlideshowSyncInfoBar.IsOpen = true;
    }

    private void SetTVSyncBusy(bool isBusy)
    {
        SyncTVShowsButton.IsEnabled = !isBusy && TVLibraries.Count > 0;
        TVLibraryComboBox.IsEnabled = !isBusy;
        SyncMoviesButton.IsEnabled = !isBusy && MovieLibraries.Count > 0;
        MovieLibraryComboBox.IsEnabled = !isBusy;
        TestConnectionButton.IsEnabled = !isBusy;
        SaveSettingsButton.IsEnabled = !isBusy;

        TVSyncProgressRing.IsActive = isBusy;
        TVSyncProgressRing.Visibility =
            isBusy ? Visibility.Visible : Visibility.Collapsed;
        SetArtworkMaintenanceButtonsEnabled(!isBusy && !_artworkMaintenanceRunning);
    }

    private void SetArtworkMaintenanceBusy(
        bool isBusy,
        bool indeterminate,
        bool allowCancellation)
    {
        _artworkMaintenanceRunning = isBusy;
        SetArtworkMaintenanceButtonsEnabled(!isBusy);
        TestConnectionButton.IsEnabled = !isBusy;
        SaveSettingsButton.IsEnabled = !isBusy;
        ServerUrlBox.IsEnabled = !isBusy;
        TokenBox.IsEnabled = !isBusy;
        SyncMoviesButton.IsEnabled = !isBusy && MovieLibraries.Count > 0;
        SyncSlideshowsButton.IsEnabled = !isBusy && MovieLibraries.Count > 0;
        SyncTVShowsButton.IsEnabled = !isBusy && TVLibraries.Count > 0;
        MovieLibraryComboBox.IsEnabled = !isBusy;
        SlideshowLibraryComboBox.IsEnabled = !isBusy;
        TVLibraryComboBox.IsEnabled = !isBusy;
        ArtworkMaintenanceProgressBar.IsIndeterminate = isBusy && indeterminate;
        ArtworkMaintenanceProgressBar.Visibility = isBusy
            ? Visibility.Visible
            : Visibility.Collapsed;

        if (isBusy && !indeterminate)
        {
            ArtworkMaintenanceProgressBar.Value = 0;
            ArtworkMaintenanceProgressBar.Maximum = 1;
        }

        CancelArtworkMaintenanceButton.IsEnabled = isBusy && allowCancellation;
        CancelArtworkMaintenanceButton.Visibility = isBusy && allowCancellation
            ? Visibility.Visible
            : Visibility.Collapsed;
    }

    private void SetArtworkMaintenanceButtonsEnabled(bool isEnabled)
    {
        RefreshMissingPostersButton.IsEnabled = isEnabled;
        RefreshAllPostersButton.IsEnabled = isEnabled;
        VerifyArtworkCacheButton.IsEnabled = isEnabled;
        ClearArtworkCacheButton.IsEnabled = isEnabled;
    }

    private void ShowArtworkMaintenanceMessage(string message, InfoBarSeverity severity)
    {
        ArtworkMaintenanceInfoBar.Message = message;
        ArtworkMaintenanceInfoBar.Severity = severity;
        ArtworkMaintenanceInfoBar.IsOpen = true;
    }

    private void ShowConnectionMessage(
        string message,
        InfoBarSeverity severity)
    {
        ConnectionInfoBar.Message = message;
        ConnectionInfoBar.Severity = severity;
        ConnectionInfoBar.IsOpen = true;
    }

    private void ShowMovieSyncMessage(
        string message,
        InfoBarSeverity severity)
    {
        MovieSyncInfoBar.Message = message;
        MovieSyncInfoBar.Severity = severity;
        MovieSyncInfoBar.IsOpen = true;
    }

    private void ShowTVSyncMessage(
        string message,
        InfoBarSeverity severity)
    {
        TVSyncInfoBar.Message = message;
        TVSyncInfoBar.Severity = severity;
        TVSyncInfoBar.IsOpen = true;
    }
}
