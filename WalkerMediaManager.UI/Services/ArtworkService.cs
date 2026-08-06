using System;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using WalkerMediaManager.UI.Models;
using Windows.Graphics.Imaging;
using Windows.Security.Credentials;
using Windows.Storage;

namespace WalkerMediaManager.UI.Services;

/// <summary>
/// Provides the shared artwork loading and caching pipeline for every media type.
/// </summary>
public sealed class ArtworkService
{
    private const string ServerUrlSettingKey = "PlexServerUrl";
    private const string CredentialResource = "WalkerMediaManager.Plex";
    private const string CredentialUserName = "PlexToken";
    private const string CacheFolderName = "ArtworkCache";
    private const string MissingMarkerExtension = ".missing";

    private static readonly TimeSpan MissingArtworkRetryDelay = TimeSpan.FromHours(24);
    private static readonly TimeSpan CacheRetention = TimeSpan.FromDays(120);

    private readonly HttpClient _httpClient = new()
    {
        Timeout = TimeSpan.FromSeconds(30)
    };

    private readonly ConcurrentDictionary<string, StorageFile> _memoryCache =
        new(StringComparer.OrdinalIgnoreCase);

    private readonly ConcurrentDictionary<string, Lazy<Task<StorageFile?>>> _inFlightRequests =
        new(StringComparer.OrdinalIgnoreCase);

    private int _cleanupStarted;

    public static ArtworkService Current { get; } = new();

    private ArtworkService()
    {
    }

    public async Task<StorageFile?> GetArtworkFileAsync(
        string artworkPath,
        string cacheKey,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(artworkPath))
        {
            return null;
        }

        artworkPath = artworkPath.Trim();

        // Plex artwork paths commonly begin with "/". On Windows, Path.IsPathRooted
        // also returns true for those server-relative paths, so only treat a rooted
        // path as local when a real file exists at that location.
        if (Path.IsPathRooted(artworkPath) && File.Exists(artworkPath))
        {
            return await GetLocalFileAsync(artworkPath);
        }

        string folderPath = GetCacheFolderPath();
        StartCleanupOnce(folderPath);

        string key = BuildCacheKey(artworkPath, cacheKey);
        string filePath = Path.Combine(folderPath, CreateFileName(key));

        if (_memoryCache.TryGetValue(filePath, out StorageFile? memoryFile))
        {
            try
            {
                if (File.Exists(memoryFile.Path) && new FileInfo(memoryFile.Path).Length > 0)
                {
                    TouchFile(memoryFile.Path);
                    return memoryFile;
                }
            }
            catch
            {
                // Remove stale memory entries and continue with the disk/download path.
            }

            _memoryCache.TryRemove(filePath, out _);
        }

        StorageFile? diskFile = await TryGetCachedFileAsync(filePath);
        if (diskFile is not null)
        {
            _memoryCache[filePath] = diskFile;
            return diskFile;
        }

        string missingMarkerPath = filePath + MissingMarkerExtension;
        if (IsNegativeCacheActive(missingMarkerPath))
        {
            Debug.WriteLine($"Artwork negative-cache hit: {artworkPath}");
            return null;
        }

        Lazy<Task<StorageFile?>> lazyRequest = _inFlightRequests.GetOrAdd(
            filePath,
            _ => new Lazy<Task<StorageFile?>>(
                () => DownloadAndCacheAsync(
                    artworkPath,
                    filePath,
                    missingMarkerPath,
                    CancellationToken.None),
                LazyThreadSafetyMode.ExecutionAndPublication));

        try
        {
            StorageFile? result = await lazyRequest.Value.WaitAsync(cancellationToken);
            if (result is not null)
            {
                _memoryCache[filePath] = result;
            }

            return result;
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            return null;
        }
        catch (Exception exception)
        {
            DiagnosticsService.LogException(
                $"Could not load artwork '{artworkPath}'.",
                exception);
            return null;
        }
        finally
        {
            if (lazyRequest.IsValueCreated && lazyRequest.Value.IsCompleted)
            {
                _inFlightRequests.TryRemove(filePath, out _);
            }
        }
    }

    public Task ClearCacheAsync()
    {
        _memoryCache.Clear();
        _inFlightRequests.Clear();

        string folderPath = GetCacheFolderPath();
        int failedDeletes = 0;
        if (Directory.Exists(folderPath))
        {
            foreach (string filePath in Directory.EnumerateFiles(folderPath))
            {
                if (!TryDeleteFile(filePath))
                {
                    failedDeletes++;
                }
            }
        }

        if (failedDeletes > 0)
        {
            throw new IOException($"{failedDeletes} artwork cache files are currently locked and could not be removed.");
        }

        DiagnosticsService.Log("Artwork cache cleared.");
        return Task.CompletedTask;
    }

    public bool IsArtworkCached(string artworkPath, string cacheKey)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(artworkPath))
            {
                return false;
            }

            artworkPath = artworkPath.Trim();
            if (Path.IsPathRooted(artworkPath) && File.Exists(artworkPath))
            {
                return new FileInfo(artworkPath).Length > 0;
            }

            string filePath = GetCachedFilePath(artworkPath, cacheKey);
            return File.Exists(filePath) && new FileInfo(filePath).Length > 0;
        }
        catch
        {
            return false;
        }
    }

    public async Task<StorageFile?> RefreshArtworkFileAsync(
        string artworkPath,
        string cacheKey,
        bool forceRefresh,
        CancellationToken cancellationToken = default)
    {
        if (forceRefresh && !string.IsNullOrWhiteSpace(artworkPath))
        {
            InvalidateCachedArtwork(artworkPath.Trim(), cacheKey);
        }

        return await GetArtworkFileAsync(artworkPath, cacheKey, cancellationToken);
    }

    public async Task<ArtworkCacheVerificationResult> VerifyCacheAsync(
        CancellationToken cancellationToken = default)
    {
        Stopwatch stopwatch = Stopwatch.StartNew();
        _memoryCache.Clear();
        string folderPath = GetCacheFolderPath();
        int validFiles = 0;
        int removedFiles = 0;
        int missingMarkers = 0;

        foreach (string filePath in Directory.EnumerateFiles(folderPath))
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (filePath.EndsWith(MissingMarkerExtension, StringComparison.OrdinalIgnoreCase))
            {
                if (IsNegativeCacheActive(filePath))
                {
                    missingMarkers++;
                }
                else if (!File.Exists(filePath))
                {
                    removedFiles++;
                }

                continue;
            }

            if (filePath.EndsWith(".tmp", StringComparison.OrdinalIgnoreCase))
            {
                if (TryDeleteFile(filePath))
                {
                    removedFiles++;
                }

                continue;
            }

            try
            {
                StorageFile file = await StorageFile.GetFileFromPathAsync(filePath);
                using Windows.Storage.Streams.IRandomAccessStreamWithContentType stream = await file.OpenReadAsync();
                BitmapDecoder decoder = await BitmapDecoder.CreateAsync(stream);
                if (decoder.PixelWidth == 0 || decoder.PixelHeight == 0)
                {
                    throw new InvalidDataException("The cached image has no dimensions.");
                }

                validFiles++;
            }
            catch
            {
                if (TryDeleteFile(filePath))
                {
                    removedFiles++;
                }
            }
        }

        stopwatch.Stop();
        DiagnosticsService.Log(
            $"Artwork cache verification finished. Valid={validFiles}; Removed={removedFiles}; " +
            $"MissingMarkers={missingMarkers}; Elapsed={stopwatch.Elapsed:hh\\:mm\\:ss}.");
        return new ArtworkCacheVerificationResult(
            validFiles,
            removedFiles,
            missingMarkers,
            stopwatch.Elapsed);
    }

    private void InvalidateCachedArtwork(string artworkPath, string cacheKey)
    {
        if (Path.IsPathRooted(artworkPath) && File.Exists(artworkPath))
        {
            return;
        }

        string filePath = GetCachedFilePath(artworkPath, cacheKey);
        _memoryCache.TryRemove(filePath, out _);
        TryDeleteFile(filePath);
        TryDeleteFile(filePath + MissingMarkerExtension);
    }

    private static string GetCachedFilePath(string artworkPath, string cacheKey)
    {
        string key = BuildCacheKey(artworkPath, cacheKey);
        return Path.Combine(GetCacheFolderPath(), CreateFileName(key));
    }

    private async Task<StorageFile?> DownloadAndCacheAsync(
        string artworkPath,
        string filePath,
        string missingMarkerPath,
        CancellationToken cancellationToken)
    {
        bool isExternalWebArtwork = Uri.TryCreate(artworkPath, UriKind.Absolute, out Uri? absoluteArtworkUri) &&
                                    (absoluteArtworkUri.Scheme.Equals(Uri.UriSchemeHttp, StringComparison.OrdinalIgnoreCase) ||
                                     absoluteArtworkUri.Scheme.Equals(Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase));

        string serverUrl = string.Empty;
        string token = string.Empty;
        Uri uri;

        if (isExternalWebArtwork)
        {
            // TV metadata providers return complete HTTPS image URLs. These must be downloaded
            // directly and must not depend on Plex settings or receive a Plex token.
            uri = absoluteArtworkUri!;
        }
        else
        {
            serverUrl = SettingsService.GetString(ServerUrlSettingKey);
            token = LoadToken();

            if (string.IsNullOrWhiteSpace(serverUrl) || string.IsNullOrWhiteSpace(token))
            {
                return null;
            }

            uri = BuildPlexArtworkUri(serverUrl, artworkPath, token);
        }

        Debug.WriteLine($"Artwork request: {SanitizeUriForLogging(uri)}");

        using HttpRequestMessage request = new(HttpMethod.Get, uri);
        request.Headers.TryAddWithoutValidation("Accept", "image/*");
        request.Headers.TryAddWithoutValidation("User-Agent", "WalkerMediaManager/1.0");

        if (!isExternalWebArtwork)
        {
            request.Headers.TryAddWithoutValidation("X-Plex-Token", token);
            request.Headers.TryAddWithoutValidation("X-Plex-Product", "Walker Media Manager");
            request.Headers.TryAddWithoutValidation("X-Plex-Client-Identifier", "walker-media-manager");
        }

        try
        {
            using HttpResponseMessage response = await _httpClient.SendAsync(
                request,
                HttpCompletionOption.ResponseHeadersRead,
                cancellationToken);

            if (response.StatusCode is HttpStatusCode.NotFound or HttpStatusCode.Gone)
            {
                WriteNegativeCacheMarker(missingMarkerPath);
                return null;
            }

            if (!response.IsSuccessStatusCode)
            {
                Debug.WriteLine(
                    $"Artwork request failed: {(int)response.StatusCode} {response.ReasonPhrase}; " +
                    SanitizeUriForLogging(uri));
                return null;
            }

            string? mediaType = response.Content.Headers.ContentType?.MediaType;
            if (!string.IsNullOrWhiteSpace(mediaType) &&
                !mediaType.StartsWith("image/", StringComparison.OrdinalIgnoreCase) &&
                !mediaType.Equals("application/octet-stream", StringComparison.OrdinalIgnoreCase))
            {
                WriteNegativeCacheMarker(missingMarkerPath);
                return null;
            }

            string temporaryPath = filePath + ".tmp";
            TryDeleteFile(temporaryPath);

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
                    WriteNegativeCacheMarker(missingMarkerPath);
                    return null;
                }

                File.Move(temporaryPath, filePath, true);
                TryDeleteFile(missingMarkerPath);

                StorageFile cachedFile = await StorageFile.GetFileFromPathAsync(filePath);
                Debug.WriteLine($"Artwork cached: {filePath}");
                return cachedFile;
            }
            finally
            {
                TryDeleteFile(temporaryPath);
            }
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (HttpRequestException exception)
        {
            Debug.WriteLine($"Artwork network error: {exception.Message}");
            return null;
        }
        catch (Exception exception)
        {
            DiagnosticsService.LogException(
                $"Could not download artwork '{artworkPath}'.",
                exception);
            return null;
        }
    }

    private static async Task<StorageFile?> GetLocalFileAsync(string artworkPath)
    {
        try
        {
            if (!File.Exists(artworkPath) || new FileInfo(artworkPath).Length == 0)
            {
                return null;
            }

            return await StorageFile.GetFileFromPathAsync(artworkPath);
        }
        catch
        {
            return null;
        }
    }

    private static async Task<StorageFile?> TryGetCachedFileAsync(string filePath)
    {
        try
        {
            if (!File.Exists(filePath) || new FileInfo(filePath).Length == 0)
            {
                return null;
            }

            TouchFile(filePath);
            return await StorageFile.GetFileFromPathAsync(filePath);
        }
        catch
        {
            TryDeleteFile(filePath);
            return null;
        }
    }

    private static string BuildCacheKey(string artworkPath, string cacheKey)
    {
        return string.IsNullOrWhiteSpace(cacheKey)
            ? artworkPath
            : cacheKey.Trim() + "|" + artworkPath;
    }

    private static bool IsNegativeCacheActive(string markerPath)
    {
        try
        {
            if (!File.Exists(markerPath))
            {
                return false;
            }

            if (DateTime.UtcNow - File.GetLastWriteTimeUtc(markerPath) < MissingArtworkRetryDelay)
            {
                return true;
            }

            File.Delete(markerPath);
        }
        catch
        {
            // A marker that cannot be read should not permanently block artwork loading.
        }

        return false;
    }

    private static void WriteNegativeCacheMarker(string markerPath)
    {
        try
        {
            File.WriteAllText(markerPath, DateTime.UtcNow.ToString("O"));
        }
        catch
        {
            // Negative caching is an optimization only.
        }
    }

    private void StartCleanupOnce(string folderPath)
    {
        if (Interlocked.Exchange(ref _cleanupStarted, 1) != 0)
        {
            return;
        }

        _ = Task.Run(() => CleanupOldCacheFiles(folderPath));
    }

    private static void CleanupOldCacheFiles(string folderPath)
    {
        try
        {
            if (!Directory.Exists(folderPath))
            {
                return;
            }

            DateTime cutoff = DateTime.UtcNow - CacheRetention;
            foreach (string filePath in Directory.EnumerateFiles(folderPath))
            {
                try
                {
                    if (File.GetLastWriteTimeUtc(filePath) < cutoff)
                    {
                        File.Delete(filePath);
                    }
                }
                catch
                {
                    // One locked or damaged cache file should not stop cleanup.
                }
            }
        }
        catch (Exception exception)
        {
            DiagnosticsService.LogException("Artwork cache cleanup failed.", exception);
        }
    }

    private static string GetCacheFolderPath()
    {
        string folderPath = Path.Combine(SettingsService.AppDataFolder, CacheFolderName);
        Directory.CreateDirectory(folderPath);
        return folderPath;
    }

    private static void TouchFile(string filePath)
    {
        try
        {
            File.SetLastWriteTimeUtc(filePath, DateTime.UtcNow);
        }
        catch
        {
            // Cache access time is only used for cleanup decisions.
        }
    }

    private static bool TryDeleteFile(string filePath)
    {
        try
        {
            if (File.Exists(filePath))
            {
                File.Delete(filePath);
            }

            return !File.Exists(filePath);
        }
        catch
        {
            // Cache files are disposable and may occasionally be locked by the image decoder.
            return false;
        }
    }

    private static Uri BuildPlexArtworkUri(string serverUrl, string artworkPath, string token)
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
