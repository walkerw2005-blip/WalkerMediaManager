using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using WalkerMediaManager.UI.Models;
using WalkerMediaManager.UI.Repositories;

namespace WalkerMediaManager.UI.Services;

public sealed class RecommendationService
{
    private readonly CollectionSeriesService _collectionSeriesService;
    private readonly WishlistRepository _wishlistRepository;
    private readonly WatchOrderService _watchOrderService;

    public RecommendationService()
        : this(
            new CollectionSeriesService(),
            new WishlistRepository(),
            new WatchOrderService())
    {
    }

    public RecommendationService(
        CollectionSeriesService collectionSeriesService,
        WishlistRepository wishlistRepository,
        WatchOrderService watchOrderService)
    {
        _collectionSeriesService = collectionSeriesService ??
            throw new ArgumentNullException(nameof(collectionSeriesService));
        _wishlistRepository = wishlistRepository ??
            throw new ArgumentNullException(nameof(wishlistRepository));
        _watchOrderService = watchOrderService ??
            throw new ArgumentNullException(nameof(watchOrderService));
    }

    public async Task<List<RecommendationItem>> GetRecommendationsAsync(
        int maximumResults = 50)
    {
        List<CollectionSeriesProgress> collections =
            await _collectionSeriesService.GetProgressAsync();
        List<WishlistItem> wishlist = await _wishlistRepository.GetAllAsync();

        Dictionary<string, WishlistItem> wishlistByIdentity = wishlist
            .Where(item =>
                !item.IsPurchased &&
                string.Equals(item.MediaType, "Movie", StringComparison.OrdinalIgnoreCase))
            .GroupBy(item => Identity(item.Title, item.Year), StringComparer.Ordinal)
            .ToDictionary(
                group => group.Key,
                group => group
                    .OrderByDescending(item => item.Priority)
                    .ThenBy(item => item.DateAdded)
                    .First(),
                StringComparer.Ordinal);

        List<RecommendationItem> recommendations = [];

        foreach (CollectionSeriesProgress collection in collections)
        {
            if (collection.IsComplete || collection.OwnedCount <= 0)
            {
                continue;
            }

            CollectionSeriesTitleStatus? nextMissing = FindNextLogicalMissing(collection);
            if (nextMissing is null)
            {
                continue;
            }

            WishlistItem? matchingWishlist = FindWishlistMatch(
                wishlistByIdentity,
                nextMissing.Title,
                nextMissing.Year);

            RecommendationType type = GetCollectionRecommendationType(
                collection,
                matchingWishlist is not null);
            int score = GetCollectionScore(collection, type, matchingWishlist);

            recommendations.Add(new RecommendationItem
            {
                Title = nextMissing.Title,
                Year = nextMissing.Year,
                CollectionName = collection.Name,
                Type = type,
                Priority = ToPriority(score),
                Score = score,
                Reason = BuildCollectionReason(collection, nextMissing, type),
                PosterPath = nextMissing.PosterPath,
                MovieId = nextMissing.MovieId,
                WishlistItemId = matchingWishlist?.Id,
                IsOnWishlist = matchingWishlist is not null,
                CollectionOwnedCount = collection.OwnedCount,
                CollectionTotalCount = collection.TotalCount
            });

            AddWatchOrderGapRecommendation(
                recommendations,
                collection,
                wishlistByIdentity);
        }

        AddStandaloneWishlistRecommendations(
            recommendations,
            wishlist,
            collections);

        return recommendations
            .GroupBy(
                recommendation => Identity(
                    recommendation.Title,
                    recommendation.Year),
                StringComparer.Ordinal)
            .Select(group => group
                .OrderByDescending(item => item.Score)
                .ThenBy(item => item.CollectionName, StringComparer.OrdinalIgnoreCase)
                .First())
            .OrderByDescending(item => item.Score)
            .ThenBy(item => item.Title, StringComparer.OrdinalIgnoreCase)
            .Take(Math.Clamp(maximumResults, 1, 500))
            .ToList();
    }

    public async Task<RecommendationItem?> GetTopRecommendationAsync()
    {
        List<RecommendationItem> recommendations =
            await GetRecommendationsAsync(1);
        return recommendations.FirstOrDefault();
    }

    private void AddWatchOrderGapRecommendation(
        ICollection<RecommendationItem> recommendations,
        CollectionSeriesProgress collection,
        IReadOnlyDictionary<string, WishlistItem> wishlistByIdentity)
    {
        WatchOrderDefinition? order = _watchOrderService
            .GetOrders(collection.Name)
            .FirstOrDefault();

        if (order is null)
        {
            return;
        }

        HashSet<string> ownedIdentities = collection.Titles
            .Where(title => title.IsOwned)
            .Select(title => Identity(title.Title, title.Year))
            .ToHashSet(StringComparer.Ordinal);

        bool hasSeenOwnedTitle = false;

        foreach (WatchOrderEntry entry in order.Entries)
        {
            bool isOwned = EntryMatchesOwned(entry, ownedIdentities, collection.Titles);
            if (isOwned)
            {
                hasSeenOwnedTitle = true;
                continue;
            }

            if (!hasSeenOwnedTitle)
            {
                continue;
            }

            CollectionSeriesTitleStatus? missingStatus = collection.Titles
                .FirstOrDefault(status => EntryMatchesStatus(entry, status));

            if (missingStatus is null || missingStatus.IsOwned)
            {
                continue;
            }

            WishlistItem? matchingWishlist = FindWishlistMatch(
                wishlistByIdentity,
                missingStatus.Title,
                missingStatus.Year);
            int score = 85 + (matchingWishlist is null ? 0 : 5);

            recommendations.Add(new RecommendationItem
            {
                Title = missingStatus.Title,
                Year = missingStatus.Year,
                CollectionName = collection.Name,
                Type = RecommendationType.ContinueWatchOrder,
                Priority = ToPriority(score),
                Score = score,
                Reason = $"This is the next missing movie in the {order.Name.ToLowerInvariant()} for {collection.Name}.",
                PosterPath = missingStatus.PosterPath,
                MovieId = missingStatus.MovieId,
                WishlistItemId = matchingWishlist?.Id,
                IsOnWishlist = matchingWishlist is not null,
                CollectionOwnedCount = collection.OwnedCount,
                CollectionTotalCount = collection.TotalCount
            });
            return;
        }
    }

    private static void AddStandaloneWishlistRecommendations(
        ICollection<RecommendationItem> recommendations,
        IEnumerable<WishlistItem> wishlist,
        IReadOnlyCollection<CollectionSeriesProgress> collections)
    {
        HashSet<string> collectionTitleIdentities = collections
            .SelectMany(collection => collection.Titles)
            .Select(title => Identity(title.Title, title.Year))
            .ToHashSet(StringComparer.Ordinal);

        foreach (WishlistItem item in wishlist.Where(item =>
                     !item.IsPurchased &&
                     string.Equals(item.MediaType, "Movie", StringComparison.OrdinalIgnoreCase)))
        {
            string identity = Identity(item.Title, item.Year);
            if (collectionTitleIdentities.Contains(identity))
            {
                continue;
            }

            int score = 65 + Math.Clamp(item.Priority, 1, 5);
            recommendations.Add(new RecommendationItem
            {
                Title = item.Title,
                Year = item.Year,
                Type = RecommendationType.Wishlist,
                Priority = ToPriority(score),
                Score = score,
                Reason = "This movie is already on your wishlist.",
                WishlistItemId = item.Id,
                IsOnWishlist = true
            });
        }
    }

    private static CollectionSeriesTitleStatus? FindNextLogicalMissing(
        CollectionSeriesProgress collection)
    {
        List<CollectionSeriesTitleStatus> titles = collection.Titles.ToList();
        int lastOwnedIndex = titles.FindLastIndex(title => title.IsOwned);

        if (lastOwnedIndex >= 0)
        {
            CollectionSeriesTitleStatus? nextAfterOwned = titles
                .Skip(lastOwnedIndex + 1)
                .FirstOrDefault(title => !title.IsOwned);
            if (nextAfterOwned is not null)
            {
                return nextAfterOwned;
            }
        }

        return titles.FirstOrDefault(title => !title.IsOwned);
    }

    private static RecommendationType GetCollectionRecommendationType(
        CollectionSeriesProgress collection,
        bool isOnWishlist)
    {
        if (collection.MissingCount == 1)
        {
            return collection.TotalCount == 3
                ? RecommendationType.CompleteTrilogy
                : RecommendationType.CompleteCollection;
        }

        return isOnWishlist
            ? RecommendationType.WishlistCollection
            : RecommendationType.ContinueFranchise;
    }

    private static int GetCollectionScore(
        CollectionSeriesProgress collection,
        RecommendationType type,
        WishlistItem? wishlistItem)
    {
        int score = type switch
        {
            RecommendationType.CompleteCollection => 100,
            RecommendationType.CompleteTrilogy => 95,
            RecommendationType.WishlistCollection => 82,
            _ => 80
        };

        if (wishlistItem is not null)
        {
            score += 5 + Math.Clamp(wishlistItem.Priority, 1, 5);
        }

        if (collection.CompletionPercent >= 75 && collection.MissingCount > 1)
        {
            score += 3;
        }

        return Math.Min(score, 110);
    }

    private static string BuildCollectionReason(
        CollectionSeriesProgress collection,
        CollectionSeriesTitleStatus missingTitle,
        RecommendationType type) => type switch
    {
        RecommendationType.CompleteCollection =>
            $"Buying {missingTitle.Title} completes the {collection.Name} collection.",
        RecommendationType.CompleteTrilogy =>
            $"Buying {missingTitle.Title} completes the {collection.Name} trilogy.",
        RecommendationType.WishlistCollection =>
            $"This wishlist movie advances {collection.Name}, where you own {collection.OwnedCount} of {collection.TotalCount} movies.",
        _ =>
            $"This is the next logical addition to {collection.Name}; you own {collection.OwnedCount} of {collection.TotalCount} movies."
    };

    private static RecommendationPriority ToPriority(int score) => score switch
    {
        >= 100 => RecommendationPriority.Critical,
        >= 90 => RecommendationPriority.VeryHigh,
        >= 80 => RecommendationPriority.High,
        >= 70 => RecommendationPriority.Good,
        _ => RecommendationPriority.Normal
    };

    private static WishlistItem? FindWishlistMatch(
        IReadOnlyDictionary<string, WishlistItem> wishlistByIdentity,
        string title,
        int year)
    {
        if (wishlistByIdentity.TryGetValue(Identity(title, year), out WishlistItem? exact))
        {
            return exact;
        }

        string normalizedTitle = MediaIdentityService.NormalizeTitle(title);
        return wishlistByIdentity.Values.FirstOrDefault(item =>
            string.Equals(
                MediaIdentityService.NormalizeTitle(item.Title),
                normalizedTitle,
                StringComparison.Ordinal) &&
            (year <= 0 || item.Year <= 0 || item.Year == year));
    }

    private static bool EntryMatchesOwned(
        WatchOrderEntry entry,
        IReadOnlySet<string> ownedIdentities,
        IEnumerable<CollectionSeriesTitleStatus> statuses)
    {
        if (ownedIdentities.Contains(Identity(entry.Title, entry.Year)))
        {
            return true;
        }

        return statuses.Any(status => status.IsOwned && EntryMatchesStatus(entry, status));
    }

    private static bool EntryMatchesStatus(
        WatchOrderEntry entry,
        CollectionSeriesTitleStatus status)
    {
        if (YearsConflict(entry.Year, status.Year))
        {
            return false;
        }

        HashSet<string> acceptedTitles = new(StringComparer.Ordinal)
        {
            MediaIdentityService.NormalizeTitle(entry.Title)
        };

        foreach (string alias in entry.Aliases)
        {
            acceptedTitles.Add(MediaIdentityService.NormalizeTitle(alias));
        }

        return acceptedTitles.Contains(MediaIdentityService.NormalizeTitle(status.Title));
    }

    private static bool YearsConflict(int firstYear, int secondYear) =>
        firstYear > 0 && secondYear > 0 && firstYear != secondYear;

    private static string Identity(string title, int year) =>
        $"{MediaIdentityService.NormalizeTitle(title)}|{Math.Max(0, year)}";
}
