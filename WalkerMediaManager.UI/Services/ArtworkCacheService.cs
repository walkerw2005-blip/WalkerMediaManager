using System;
using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Windows.Security.Credentials;
using Windows.Storage;

namespace WalkerMediaManager.UI.Services;

public sealed class ArtworkCacheService
{
    private const string ServerUrlSettingKey = "PlexServerUrl";
    private const string CredentialResource = "WalkerMediaManager.Plex";
    private const string CredentialUserName = "PlexToken";
    private const string CacheFolderName = "ArtworkCache";

    private readonly HttpClient _httpClient = new() { Timeout = TimeSpan.FromSeconds(60) };

    public static ArtworkCacheService Current { get; } = new();

    private ArtworkCacheService()
    {
    }

    public async Task<StorageFile?> GetArtworkFileAsync(
        string artworkPath,
        string cacheKey,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(artworkPath))
        {
            Debug.WriteLine("Artwork skipped: artwork path is empty.");
            return null;
        }

        if (Path.IsPathRooted(artworkPath) && File.Exists(artworkPath))
        {
            Debug.WriteLine($"Artwork loaded from local path: {artworkPath}");
            return await StorageFile.GetFileFromPathAsync(artworkPath);
        }

        string serverUrl = SettingsService.GetString(ServerUrlSettingKey);
        string token = LoadToken();

        if (string.IsNullOrWhiteSpace(serverUrl))
        {
            Debug.WriteLine("Artwork download skipped: Plex server URL is missing.");
            return null;
        }

        if (string.IsNullOrWhiteSpace(token))
        {
            Debug.WriteLine("Artwork download skipped: Plex token is missing.");
            return null;
        }

        string folderPath = Path.Combine(SettingsService.AppDataFolder, CacheFolderName);
        Directory.CreateDirectory(folderPath);

        string key = string.IsNullOrWhiteSpace(cacheKey)
            ? artworkPath
            : cacheKey + "|" + artworkPath;
        string filePath = Path.Combine(folderPath, CreateFileName(key));

        if (File.Exists(filePath) && new FileInfo(filePath).Length > 0)
        {
            Debug.WriteLine($"Artwork cache hit: {filePath}");
            return await StorageFile.GetFileFromPathAsync(filePath);
        }

        Uri uri = BuildArtworkUri(serverUrl, artworkPath, token);
        Debug.WriteLine($"Artwork request: {SanitizeUriForLogging(uri)}");

        using HttpRequestMessage request = new(HttpMethod.Get, uri);
        request.Headers.TryAddWithoutValidation("X-Plex-Token", token);
        request.Headers.TryAddWithoutValidation("X-Plex-Product", "Walker Media Manager");
        request.Headers.TryAddWithoutValidation("X-Plex-Client-Identifier", "walker-media-manager");
        request.Headers.TryAddWithoutValidation("Accept", "image/*");

        using HttpResponseMessage response = await _httpClient.SendAsync(
            request,
            HttpCompletionOption.ResponseHeadersRead,
            cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            Debug.WriteLine(
                $"Artwork request failed: {(int)response.StatusCode} {response.ReasonPhrase}; " +
                SanitizeUriForLogging(uri));
            response.EnsureSuccessStatusCode();
        }

        string? mediaType = response.Content.Headers.ContentType?.MediaType;
        if (!string.IsNullOrWhiteSpace(mediaType) &&
            !mediaType.StartsWith("image/", StringComparison.OrdinalIgnoreCase) &&
            !mediaType.Equals("application/octet-stream", StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidDataException(
                $"Plex returned '{mediaType}' instead of image data for artwork path '{artworkPath}'.");
        }

        string temporaryPath = filePath + ".tmp";
        try
        {
            await using (FileStream target = new(
                             temporaryPath,
                             FileMode.Create,
                             FileAccess.Write,
                             FileShare.None,
                             81920,
                             useAsync: true))
            {
                await response.Content.CopyToAsync(target, cancellationToken);
                await target.FlushAsync(cancellationToken);
            }

            if (new FileInfo(temporaryPath).Length == 0)
            {
                throw new InvalidDataException(
                    $"Plex returned an empty artwork file for '{artworkPath}'.");
            }

            File.Move(temporaryPath, filePath, true);
        }
        catch
        {
            try
            {
                if (File.Exists(temporaryPath)) File.Delete(temporaryPath);
            }
            catch
            {
                // Preserve the original exception.
            }

            throw;
        }

        Debug.WriteLine($"Artwork cached: {filePath}");
        return await StorageFile.GetFileFromPathAsync(filePath);
    }

    public Task ClearCacheAsync()
    {
        string folderPath = Path.Combine(SettingsService.AppDataFolder, CacheFolderName);
        if (Directory.Exists(folderPath))
        {
            foreach (string filePath in Directory.EnumerateFiles(folderPath))
            {
                try
                {
                    File.Delete(filePath);
                }
                catch (Exception exception)
                {
                    DiagnosticsService.LogException($"Could not delete cached artwork '{filePath}'.", exception);
                }
            }
        }

        Debug.WriteLine("Artwork cache cleared.");
        return Task.CompletedTask;
    }

    private static Uri BuildArtworkUri(string serverUrl, string artworkPath, string token)
    {
        UriBuilder builder;

        if (Uri.TryCreate(artworkPath, UriKind.Absolute, out Uri? absolute))
        {
            builder = new UriBuilder(absolute);
        }
        else
        {
            string baseUrl = serverUrl.Trim().TrimEnd('/');
            string path = artworkPath.StartsWith('/') ? artworkPath : "/" + artworkPath;
            builder = new UriBuilder(baseUrl + path);
        }

        string existingQuery = builder.Query.TrimStart('?');
        string tokenQuery = "X-Plex-Token=" + Uri.EscapeDataString(token);
        builder.Query = string.IsNullOrWhiteSpace(existingQuery)
            ? tokenQuery
            : existingQuery + "&" + tokenQuery;

        return builder.Uri;
    }

    private static string SanitizeUriForLogging(Uri uri)
    {
        UriBuilder builder = new(uri);
        string[] queryParts = builder.Query.TrimStart('?').Split(
            '&',
            StringSplitOptions.RemoveEmptyEntries);

        for (int i = 0; i < queryParts.Length; i++)
        {
            if (queryParts[i].StartsWith("X-Plex-Token=", StringComparison.OrdinalIgnoreCase))
            {
                queryParts[i] = "X-Plex-Token=[REDACTED]";
            }
        }

        builder.Query = string.Join("&", queryParts);
        return builder.Uri.ToString();
    }

    private static string CreateFileName(string value)
    {
        byte[] hash = SHA256.HashData(Encoding.UTF8.GetBytes(value));
        return Convert.ToHexString(hash) + ".img";
    }

    private static string LoadToken()
    {
        try
        {
            PasswordCredential credential =
                new PasswordVault().Retrieve(CredentialResource, CredentialUserName);
            credential.RetrievePassword();
            return credential.Password ?? string.Empty;
        }
        catch
        {
            return string.Empty;
        }
    }
}
