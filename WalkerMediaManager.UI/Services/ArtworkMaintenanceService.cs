using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using WalkerMediaManager.UI.Models;
using WalkerMediaManager.UI.Repositories;

namespace WalkerMediaManager.UI.Services;

public sealed class ArtworkMaintenanceService
{
    private readonly MovieRepository _movieRepository = new();
    private readonly TVShowRepository _tvShowRepository = new();

    public async Task<ArtworkMaintenanceResult> RefreshPostersAsync(
        bool refreshAll,
        IProgress<ArtworkMaintenanceProgress>? progress = null,
        CancellationToken cancellationToken = default)
    {
        Stopwatch stopwatch = Stopwatch.StartNew();
        List<ArtworkItem> items = [];
        items.AddRange((await _movieRepository.GetAllAsync()).Select(movie => new ArtworkItem(
            "Movie",
            movie.Title,
            movie.PosterPath,
            movie.PlexRatingKey)));
        items.AddRange((await _tvShowRepository.GetAllAsync()).Select(show => new ArtworkItem(
            "TV show",
            show.Title,
            show.PosterPath,
            show.PlexRatingKey)));

        ArtworkMaintenanceResult result = new() { TotalCount = items.Count };
        DiagnosticsService.Log(
            $"Artwork maintenance started. Mode={(refreshAll ? "All" : "Missing")}; Items={items.Count}.");

        for (int index = 0; index < items.Count; index++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            ArtworkItem item = items[index];
            int current = index + 1;

            if (string.IsNullOrWhiteSpace(item.ArtworkPath))
            {
                result.MissingSourceCount++;
                progress?.Report(new ArtworkMaintenanceProgress(
                    current,
                    items.Count,
                    item.MediaType,
                    item.Title,
                    "No poster source"));
                continue;
            }

            if (!refreshAll && ArtworkService.Current.IsArtworkCached(item.ArtworkPath, item.CacheKey))
            {
                result.AlreadyCachedCount++;
                progress?.Report(new ArtworkMaintenanceProgress(
                    current,
                    items.Count,
                    item.MediaType,
                    item.Title,
                    "Already cached"));
                continue;
            }

            progress?.Report(new ArtworkMaintenanceProgress(
                current,
                items.Count,
                item.MediaType,
                item.Title,
                "Downloading poster"));

            try
            {
                bool forceRefresh = refreshAll ||
                                    !ArtworkService.Current.IsArtworkCached(item.ArtworkPath, item.CacheKey);
                Windows.Storage.StorageFile? file = await ArtworkService.Current.RefreshArtworkFileAsync(
                    item.ArtworkPath,
                    item.CacheKey,
                    forceRefresh,
                    cancellationToken);

                if (file is null)
                {
                    result.FailedCount++;
                }
                else
                {
                    result.RefreshedCount++;
                }
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception exception)
            {
                result.FailedCount++;
                DiagnosticsService.LogException(
                    $"Artwork maintenance failed for {item.MediaType} '{item.Title}'.",
                    exception);
            }
        }

        stopwatch.Stop();
        result.Elapsed = stopwatch.Elapsed;
        DiagnosticsService.Log($"Artwork maintenance finished. {result.Summary}");
        return result;
    }

    private sealed record ArtworkItem(
        string MediaType,
        string Title,
        string ArtworkPath,
        string CacheKey);
}
