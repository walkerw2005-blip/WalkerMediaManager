using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using WalkerMediaManager.UI.Models;

namespace WalkerMediaManager.UI.Services;

public static class MediaDuplicateService
{
    public static string NormalizeTitle(string? title)
    {
        if (string.IsNullOrWhiteSpace(title))
        {
            return string.Empty;
        }

        string decomposed = title
            .Trim()
            .Normalize(NormalizationForm.FormD);

        StringBuilder normalized = new(decomposed.Length);

        foreach (char character in decomposed)
        {
            UnicodeCategory category =
                CharUnicodeInfo.GetUnicodeCategory(character);

            if (category == UnicodeCategory.NonSpacingMark)
            {
                continue;
            }

            if (char.IsLetterOrDigit(character))
            {
                normalized.Append(char.ToUpperInvariant(character));
            }
        }

        return normalized
            .ToString()
            .Normalize(NormalizationForm.FormC);
    }

    public static string CreateKey(string? title, int releaseYear) =>
        $"{NormalizeTitle(title)}|{releaseYear}";

    public static Movie? FindPossibleDuplicate(
        IEnumerable<Movie> existingItems,
        string title,
        int releaseYear,
        int? excludedMovieId = null)
    {
        string normalizedTitle = NormalizeTitle(title);

        if (string.IsNullOrEmpty(normalizedTitle))
        {
            return null;
        }

        foreach (Movie existingItem in existingItems)
        {
            if (excludedMovieId.HasValue &&
                existingItem.Id == excludedMovieId.Value)
            {
                continue;
            }

            if (!string.Equals(
                    NormalizeTitle(existingItem.Title),
                    normalizedTitle,
                    StringComparison.Ordinal))
            {
                continue;
            }

            if (releaseYear == 0 ||
                existingItem.ReleaseYear == 0 ||
                existingItem.ReleaseYear == releaseYear)
            {
                return existingItem;
            }
        }

        return null;
    }
}
