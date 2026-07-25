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

        string serverUrl =
            ApplicationData.Current.LocalSettings.Values[ServerUrlSettingKey]?.ToString()
            ?? string.Empty;
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

        StorageFolder folder = await ApplicationData.Current.LocalCacheFolder.CreateFolderAsync(
            CacheFolderName,
            CreationCollisionOption.OpenIfExists);

        string key = string.IsNullOrWhiteSpace(cacheKey)
            ? artworkPath
            : cacheKey + "|" + artworkPath;
        string fileName = CreateFileName(key);

        try
        {
            StorageFile cachedFile = await folder.GetFileAsync(fileName);
            Debug.WriteLine($"Artwork cache hit: {cachedFile.Path}");
            return cachedFile;
        }
        catch (FileNotFoundException)
        {
            // Expected on the first request for an image.
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

        StorageFile file = await folder.CreateFileAsync(
            fileName,
            CreationCollisionOption.ReplaceExisting);

        try
        {
            await using Stream target = await file.OpenStreamForWriteAsync();
            target.SetLength(0);
            await response.Content.CopyToAsync(target, cancellationToken);
            await target.FlushAsync(cancellationToken);

            if (target.Length == 0)
            {
                throw new InvalidDataException(
                    $"Plex returned an empty artwork file for '{artworkPath}'.");
            }
        }
        catch
        {
            try
            {
                await file.DeleteAsync(StorageDeleteOption.PermanentDelete);
            }
            catch
            {
                // Preserve the original exception.
            }

            throw;
        }

        Debug.WriteLine($"Artwork cached: {file.Path}");
        return file;
    }

    public async Task ClearCacheAsync()
    {
        StorageFolder folder = await ApplicationData.Current.LocalCacheFolder.CreateFolderAsync(
            CacheFolderName,
            CreationCollisionOption.OpenIfExists);

        foreach (StorageFile file in await folder.GetFilesAsync())
        {
            await file.DeleteAsync(StorageDeleteOption.PermanentDelete);
        }

        Debug.WriteLine("Artwork cache cleared.");
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
