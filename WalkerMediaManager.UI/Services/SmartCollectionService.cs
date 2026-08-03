using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using WalkerMediaManager.UI.Models;
using WalkerMediaManager.UI.Repositories;

namespace WalkerMediaManager.UI.Services;

/// <summary>
/// Combines automatic series detection with wishlist data and conservative
/// purchase estimates for the Smart Collection Manager.
/// </summary>
public sealed class SmartCollectionService
{
    private const decimal DefaultDvdPrice = 12.99m;
    private const decimal DefaultBluRayPrice = 17.99m;
    private const decimal DefaultFourKPrice = 24.99m;

    private readonly CollectionSeriesService _seriesService = new();
    private readonly WishlistRepository _wishlistRepository = new();
    private readonly CollectionHealthService _healthService = new();

    public async Task<List<CollectionSeriesProgress>> GetProgressAsync()
    {
        List<CollectionSeriesProgress> series = await _seriesService.GetProgressAsync();
        List<WishlistItem> wishlist;

        try
        {
            wishlist = await _wishlistRepository.GetAllAsync();
        }
        catch
        {
            wishlist = [];
        }

        foreach (CollectionSeriesProgress collection in series)
        {
            foreach (CollectionSeriesTitleStatus title in collection.Titles.Where(item => !item.IsOwned))
            {
                WishlistItem? match = FindWishlistMatch(wishlist, title);
                title.IsOnWishlist = match is not null;
                title.WishlistItemId = match?.Id;
                title.PreferredFormat = match?.PreferredFormat ?? string.Empty;
                title.WishlistTargetPrice = match?.TargetPrice;
                title.EstimatedPurchasePrice = match?.TargetPrice ?? EstimatePrice(match?.PreferredFormat);
            }

            collection.HealthScore = _healthService.Calculate(collection);
        }

        return series
            .OrderByDescending(item => item.SmartPriorityScore)
            .ThenByDescending(item => item.CompletionPercent)
            .ThenBy(item => item.Name, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private static WishlistItem? FindWishlistMatch(
        IEnumerable<WishlistItem> wishlist,
        CollectionSeriesTitleStatus title)
    {
        string normalizedTitle = MediaIdentityService.NormalizeTitle(title.Title);

        return wishlist.FirstOrDefault(item =>
            string.Equals(
                MediaIdentityService.NormalizeTitle(item.Title),
                normalizedTitle,
                StringComparison.OrdinalIgnoreCase) &&
            (title.Year <= 0 || item.Year <= 0 || title.Year == item.Year));
    }

    private static decimal EstimatePrice(string? preferredFormat)
    {
        string format = preferredFormat?.Trim() ?? string.Empty;

        if (format.Contains("4K", StringComparison.OrdinalIgnoreCase) ||
            format.Contains("UHD", StringComparison.OrdinalIgnoreCase))
        {
            return DefaultFourKPrice;
        }

        if (format.Contains("Blu", StringComparison.OrdinalIgnoreCase))
        {
            return DefaultBluRayPrice;
        }

        return DefaultDvdPrice;
    }
}
