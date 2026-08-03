using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;

namespace WalkerMediaManager.UI.Services;

public static partial class CollectionTitleNormalizer
{
    private static readonly IReadOnlyDictionary<string, string> RomanNumerals =
        new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["i"] = "1", ["ii"] = "2", ["iii"] = "3", ["iv"] = "4", ["v"] = "5",
            ["vi"] = "6", ["vii"] = "7", ["viii"] = "8", ["ix"] = "9", ["x"] = "10",
            ["xi"] = "11", ["xii"] = "12", ["xiii"] = "13", ["xiv"] = "14", ["xv"] = "15"
        };

    public static string Normalize(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return string.Empty;
        }

        string decomposed = value.Normalize(NormalizationForm.FormD);
        StringBuilder cleaned = new(decomposed.Length);

        foreach (char character in decomposed)
        {
            UnicodeCategory category = CharUnicodeInfo.GetUnicodeCategory(character);
            if (category == UnicodeCategory.NonSpacingMark)
            {
                continue;
            }

            cleaned.Append(char.IsLetterOrDigit(character) ? char.ToLowerInvariant(character) : ' ');
        }

        string[] tokens = WhitespaceRegex().Split(cleaned.ToString().Trim());
        for (int index = 0; index < tokens.Length; index++)
        {
            if (RomanNumerals.TryGetValue(tokens[index], out string? number))
            {
                tokens[index] = number;
            }
        }

        return string.Concat(tokens.Where(token => token.Length > 0));
    }

    public static IReadOnlySet<string> BuildAcceptedKeys(string title, IEnumerable<string>? aliases = null)
    {
        HashSet<string> keys = new(StringComparer.Ordinal);
        AddKeyVariants(keys, title);

        if (aliases is not null)
        {
            foreach (string alias in aliases)
            {
                AddKeyVariants(keys, alias);
            }
        }

        return keys;
    }

    private static void AddKeyVariants(HashSet<string> keys, string? value)
    {
        string normalized = Normalize(value);
        if (normalized.Length == 0)
        {
            return;
        }

        keys.Add(normalized);

        foreach (string article in new[] { "the", "a", "an" })
        {
            if (normalized.StartsWith(article, StringComparison.Ordinal) && normalized.Length > article.Length)
            {
                keys.Add(normalized[article.Length..]);
            }
        }
    }

    [GeneratedRegex(@"\s+", RegexOptions.CultureInvariant)]
    private static partial Regex WhitespaceRegex();
}
