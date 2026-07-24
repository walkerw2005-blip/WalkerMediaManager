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
    private const string CredentialResource = "WalkerMediaManager.Plex";
    private const string CredentialUserName = "PlexToken";

    private readonly PlexService _plexService = new();
    private readonly PlexMovieSyncService _plexMovieSyncService = new();

    public ObservableCollection<PlexLibrarySection> Libraries { get; } = [];
    public ObservableCollection<PlexLibrarySection> MovieLibraries { get; } = [];

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
        SaveSelectedMovieLibrary();

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
            }

            RestoreSelectedMovieLibrary();
            SyncMoviesButton.IsEnabled = MovieLibraries.Count > 0;

            ShowConnectionMessage(
                $"{connectionMessage} Found {Libraries.Count} libraries.",
                InfoBarSeverity.Success);
        }
        catch (Exception exception)
        {
            Libraries.Clear();
            MovieLibraries.Clear();
            SyncMoviesButton.IsEnabled = false;

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
            ShowSyncMessage(
                "Select the Plex movie library to sync.",
                InfoBarSeverity.Warning);
            return;
        }

        string serverUrl = ServerUrlBox.Text.Trim();
        string token = TokenBox.Password.Trim();

        if (string.IsNullOrWhiteSpace(serverUrl) ||
            string.IsNullOrWhiteSpace(token))
        {
            ShowSyncMessage(
                "Enter the Plex server address and token first.",
                InfoBarSeverity.Warning);
            return;
        }

        SaveSelectedMovieLibrary();
        SetSyncBusy(true);

        try
        {
            Progress<string> progress = new(
                message => SyncProgressText.Text = message);

            PlexSyncResult result =
                await _plexMovieSyncService.SyncMoviesAsync(
                    serverUrl,
                    token,
                    library.Key,
                    progress);

            SyncProgressText.Text = "Movie sync complete.";

            ShowSyncMessage(
                result.Summary,
                result.FailedCount > 0
                    ? InfoBarSeverity.Warning
                    : InfoBarSeverity.Success);
        }
        catch (Exception exception)
        {
            ShowSyncMessage(
                $"Plex movie sync failed: {exception.Message}",
                InfoBarSeverity.Error);
        }
        finally
        {
            SetSyncBusy(false);
        }
    }

    private void RestoreSelectedMovieLibrary()
    {
        string selectedKey =
            ApplicationData.Current.LocalSettings.Values[
                MovieLibraryKeySettingKey]?.ToString()
            ?? string.Empty;

        PlexLibrarySection? selected = MovieLibraries
            .FirstOrDefault(item => item.Key == selectedKey)
            ?? MovieLibraries.FirstOrDefault();

        MovieLibraryComboBox.SelectedItem = selected;
    }

    private void SaveSelectedMovieLibrary()
    {
        if (MovieLibraryComboBox.SelectedItem is PlexLibrarySection library)
        {
            ApplicationData.Current.LocalSettings.Values[
                MovieLibraryKeySettingKey] = library.Key;
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

    private void SetSyncBusy(bool isBusy)
    {
        SyncMoviesButton.IsEnabled = !isBusy;
        MovieLibraryComboBox.IsEnabled = !isBusy;
        TestConnectionButton.IsEnabled = !isBusy;
        SaveSettingsButton.IsEnabled = !isBusy;

        SyncProgressRing.IsActive = isBusy;
        SyncProgressRing.Visibility =
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

    private void ShowSyncMessage(
        string message,
        InfoBarSeverity severity)
    {
        SyncInfoBar.Message = message;
        SyncInfoBar.Severity = severity;
        SyncInfoBar.IsOpen = true;
    }
}
