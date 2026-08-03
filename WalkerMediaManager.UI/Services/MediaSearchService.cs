using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;

namespace WalkerMediaManager.UI.Services;

public static class MediaSearchService
{
    public static bool Matches(string? query, params object?[] values)
    {
        string normalizedQuery = Normalize(query);
        if (string.IsNullOrWhiteSpace(normalizedQuery))
        {
            return true;
        }

        string searchableText = string.Join(
            ' ',
            values
                .Where(value => value is not null)
                .Select(value => Normalize(Convert.ToString(value, CultureInfo.InvariantCulture))));

        if (string.IsNullOrWhiteSpace(searchableText))
        {
            return false;
        }

        string[] terms = normalizedQuery.Split(
            ' ',
            StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

        return terms.All(term =>
            searchableText.Contains(term, StringComparison.Ordinal));
    }

    public static string Normalize(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return string.Empty;
        }

        string decomposed = value.Trim().Normalize(NormalizationForm.FormD);
        StringBuilder builder = new(decomposed.Length);
        bool previousWasSpace = false;

        foreach (char character in decomposed)
        {
            UnicodeCategory category = CharUnicodeInfo.GetUnicodeCategory(character);
            if (category == UnicodeCategory.NonSpacingMark)
            {
                continue;
            }

            if (char.IsLetterOrDigit(character))
            {
                builder.Append(char.ToUpperInvariant(character));
                previousWasSpace = false;
            }
            else if (!previousWasSpace && builder.Length > 0)
            {
                builder.Append(' ');
                previousWasSpace = true;
            }
        }

        return builder.ToString().Trim();
    }
}
