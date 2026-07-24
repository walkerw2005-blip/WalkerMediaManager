using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using WalkerMediaManager.UI.Models;
using WalkerMediaManager.UI.Services;
using Windows.Security.Credentials;
using Windows.Storage;

namespace WalkerMediaManager.UI.Views;

public sealed partial class SettingsPage : Page
{
    private const string ServerUrlSettingKey = "PlexServerUrl";
    private const string MovieLibraryKeySettingKey = "PlexMovieLibraryKey";
    private const string TVLibraryKeySettingKey = "PlexTVLibraryKey";
    private const string CredentialResource = "WalkerMediaManager.Plex";
    private const string CredentialUserName = "PlexToken";

    private readonly PlexService _plexService = new();
    private readonly PlexMovieSyncService _plexMovieSyncService = new();
    private readonly PlexTVSyncService _plexTVSyncService = new();

    public ObservableCollection<PlexLibrarySection> Libraries { get; } = [];
    public ObservableCollection<PlexLibrarySection> MovieLibraries { get; } = [];
    public ObservableCollection<PlexLibrarySection> TVLibraries { get; } = [];

    public SettingsPage()
    {
        InitializeComponent();
        Loaded += SettingsPage_Loaded;
    }

    private void SettingsPage_Loaded(
        object sender,
        RoutedEventArgs e)
    {
        ApplicationDataContainer settings =
            ApplicationData.Current.LocalSettings;

        ServerUrlBox.Text =
            settings.Values[ServerUrlSettingKey]?.ToString()
            ?? string.Empty;

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

        ApplicationData.Current.LocalSettings.Values[
            ServerUrlSettingKey] = serverUrl;

        SaveToken(token);
        SaveSelectedLibraries();

        ShowConnectionMessage(
            "Plex settings were saved securely on this computer.",
            InfoBarSeverity.Success);

        await TestConnectionAsync();
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
            SyncTVShowsButton.IsEnabled = TVLibraries.Count > 0;

            ShowConnectionMessage(
                $"{connectionMessage} Found {Libraries.Count} libraries.",
                InfoBarSeverity.Success);
        }
        catch (Exception exception)
        {
            Libraries.Clear();
            MovieLibraries.Clear();
            TVLibraries.Clear();
            SyncMoviesButton.IsEnabled = false;
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
            Progress<string> progress = new(
                message => MovieSyncProgressText.Text = message);

            PlexSyncResult result =
                await _plexMovieSyncService.SyncMoviesAsync(
                    serverUrl,
                    token,
                    library.Key,
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
            ShowMovieSyncMessage(
                $"Plex movie sync failed: {exception.Message}",
                InfoBarSeverity.Error);
        }
        finally
        {
            SetMovieSyncBusy(false);
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
            ShowTVSyncMessage(
                $"Plex TV show sync failed: {exception.Message}",
                InfoBarSeverity.Error);
        }
        finally
        {
            SetTVSyncBusy(false);
        }
    }

    private void RestoreSelectedLibraries()
    {
        ApplicationDataContainer settings =
            ApplicationData.Current.LocalSettings;

        string movieKey =
            settings.Values[MovieLibraryKeySettingKey]?.ToString()
            ?? string.Empty;

        MovieLibraryComboBox.SelectedItem = MovieLibraries
            .FirstOrDefault(item => item.Key == movieKey)
            ?? MovieLibraries.FirstOrDefault();

        string tvKey =
            settings.Values[TVLibraryKeySettingKey]?.ToString()
            ?? string.Empty;

        TVLibraryComboBox.SelectedItem = TVLibraries
            .FirstOrDefault(item => item.Key == tvKey)
            ?? TVLibraries.FirstOrDefault();
    }

    private void SaveSelectedLibraries()
    {
        if (MovieLibraryComboBox.SelectedItem is PlexLibrarySection movieLibrary)
        {
            ApplicationData.Current.LocalSettings.Values[
                MovieLibraryKeySettingKey] = movieLibrary.Key;
        }

        if (TVLibraryComboBox.SelectedItem is PlexLibrarySection tvLibrary)
        {
            ApplicationData.Current.LocalSettings.Values[
                TVLibraryKeySettingKey] = tvLibrary.Key;
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
            return credential.Password;
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
