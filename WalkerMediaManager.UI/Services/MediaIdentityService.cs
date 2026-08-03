using WalkerMediaManager.UI.Models;

namespace WalkerMediaManager.UI.Services;

public static class MediaIdentityService
{
    public static string NormalizeTitle(string? title) =>
        MediaDuplicateService.NormalizeTitle(title);

    public static string CreateKey(string? title, int year) =>
        MediaDuplicateService.CreateKey(title, year);

    public static bool IsSameTitle(
        string? firstTitle,
        int firstYear,
        string? secondTitle,
        int secondYear)
    {
        if (NormalizeTitle(firstTitle) != NormalizeTitle(secondTitle))
        {
            return false;
        }

        return firstYear == 0 ||
               secondYear == 0 ||
               firstYear == secondYear;
    }

    public static void PrepareWishlistItem(WishlistItem item)
    {
        item.Title = item.Title.Trim();
        item.MediaType = string.IsNullOrWhiteSpace(item.MediaType)
            ? "Movie"
            : item.MediaType.Trim();
        item.NormalizedTitle = NormalizeTitle(item.Title);
        item.PreferredFormat = item.PreferredFormat.Trim();
        item.PreferredStore = item.PreferredStore.Trim();
        item.Notes = item.Notes.Trim();
        item.Priority = System.Math.Clamp(item.Priority, 1, 5);
        item.Year = System.Math.Max(0, item.Year);

        if (item.TargetPrice < 0)
        {
            item.TargetPrice = null;
        }
    }
}
