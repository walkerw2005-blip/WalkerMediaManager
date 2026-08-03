using System;
using System.Collections.Generic;
using System.Linq;
using WalkerMediaManager.UI.Models;

namespace WalkerMediaManager.UI.Services;

public sealed class UpgradeAdvisorService
{
    private static readonly Dictionary<string, int> FormatRanks =
        new(StringComparer.OrdinalIgnoreCase)
        {
            ["VHS"] = 10,
            ["LASERDISC"] = 20,
            ["DVD"] = 30,
            ["DIGITAL"] = 35,
            ["BLU RAY"] = 40,
            ["3D BLU RAY"] = 45,
            ["4K UHD"] = 50,
            ["4K UHD BLU RAY"] = 50
        };

    public void ApplyRecommendation(SmartBuyResult result, string plannedFormat, string plannedEdition)
    {
        ArgumentNullException.ThrowIfNull(result);

        string format = NormalizeFormat(plannedFormat);
        string edition = NormalizeEdition(plannedEdition);
        List<string> ownedFormats = SplitValues(result.OwnedFormats);
        List<string> ownedEditions = SplitValues(result.OwnedEditions);
        List<string> ownedPackaging = SplitValues(result.OwnedPackaging);

        if (result.OwnedCopyCount <= 0)
        {
            result.Recommendation = result.IsWishlist
                ? "On wishlist - ownership unconfirmed"
                : "Ownership details missing";
            result.RecommendationDetail = result.IsWishlist
                ? "This title is on your wishlist and also appears in the movie library, but no owned copy has been recorded. Verify the title before buying."
                : "The title is in your movie library, but no physical or digital copy has been recorded. Verify the title before buying another copy.";
            result.RecommendationGlyph = "\uE7BA";
            result.RecommendationColor = "#9D5D00";
            return;
        }

        if (string.IsNullOrWhiteSpace(format) && string.IsNullOrWhiteSpace(edition))
        {
            result.Recommendation = "Already owned";
            result.RecommendationDetail =
                $"You already own {result.OwnedCopyCount} " +
                (result.OwnedCopyCount == 1 ? "copy" : "copies") +
                ". Select the format and optional edition you are considering for a specific recommendation.";
            result.RecommendationGlyph = "\uE73E";
            result.RecommendationColor = "#C42B1C";
            return;
        }

        int plannedRank = GetFormatRank(format);
        int bestOwnedRank = ownedFormats.Count == 0 ? 0 : ownedFormats.Max(GetFormatRank);
        string bestOwnedFormat = ownedFormats
            .OrderByDescending(GetFormatRank)
            .FirstOrDefault() ?? "an unrecorded format";

        bool ownsSameFormat = !string.IsNullOrWhiteSpace(format) &&
            ownedFormats.Any(value => GetFormatRank(value) == plannedRank && plannedRank > 0);
        bool ownsSameEdition = !string.IsNullOrWhiteSpace(edition) &&
            ownedEditions.Concat(ownedPackaging).Any(value => EditionMatches(value, edition));
        bool plannedIsSpecialEdition = IsSpecialEdition(edition);
        bool ownsAnySpecialEdition = ownedEditions.Concat(ownedPackaging).Any(IsSpecialEdition);

        if (plannedRank > bestOwnedRank && plannedRank > 0)
        {
            result.Recommendation = "Upgrade available";
            result.RecommendationDetail =
                $"You own {bestOwnedFormat}; the planned {DisplayPlanned(format, edition)} is a format upgrade. Confirm the transfer quality and included features before buying.";
            result.RecommendationGlyph = "\uE74A";
            result.RecommendationColor = "#107C10";
            return;
        }

        if (plannedRank > 0 && bestOwnedRank > plannedRank)
        {
            result.Recommendation = "Already own better edition";
            result.RecommendationDetail =
                $"Your best recorded copy is {bestOwnedFormat}, which ranks above the planned {DisplayPlanned(format, edition)}. Skip this purchase unless the new copy has a special feature you specifically want.";
            result.RecommendationGlyph = "\uE73E";
            result.RecommendationColor = "#C42B1C";
            return;
        }

        if (ownsSameFormat && (!plannedIsSpecialEdition || ownsSameEdition))
        {
            result.Recommendation = "Duplicate purchase";
            result.RecommendationDetail = string.IsNullOrWhiteSpace(edition)
                ? $"You already own this title on {format}. Buy only if this is a replacement copy or an edition with features not recorded in the app."
                : $"You already appear to own this title as {DisplayPlanned(format, edition)}. Review the existing copy before buying another one.";
            result.RecommendationGlyph = "\uEA39";
            result.RecommendationColor = "#C42B1C";
            return;
        }

        if ((ownsSameFormat && plannedIsSpecialEdition && !ownsSameEdition) ||
            (plannedRank == bestOwnedRank && !string.IsNullOrWhiteSpace(edition) && !ownsSameEdition) ||
            (plannedRank == 0 && plannedIsSpecialEdition && !ownsSameEdition))
        {
            result.Recommendation = "Different edition";
            result.RecommendationDetail = ownsAnySpecialEdition
                ? $"You own the title in the same general quality tier, but the planned {DisplayPlanned(format, edition)} may be a different collectible edition. Compare packaging, discs, and bonus features."
                : $"You own the title, but not this recorded edition. The planned {DisplayPlanned(format, edition)} may be worthwhile for its packaging or bonus features rather than picture quality.";
            result.RecommendationGlyph = "\uE8D5";
            result.RecommendationColor = "#0067C0";
            return;
        }

        if (plannedRank == bestOwnedRank && plannedRank > 0)
        {
            result.Recommendation = "Already own best format";
            result.RecommendationDetail =
                $"You already own this title at the {bestOwnedFormat} quality tier. The planned copy is not a format upgrade; compare the edition details before buying.";
            result.RecommendationGlyph = "\uE73E";
            result.RecommendationColor = "#9D5D00";
            return;
        }

        result.Recommendation = "Possible duplicate";
        result.RecommendationDetail =
            $"You already own this title on {result.FormatSummary}. The planned {DisplayPlanned(format, edition)} is not a clear format upgrade.";
        result.RecommendationGlyph = "\uE7BA";
        result.RecommendationColor = "#9D5D00";
    }

    private static string DisplayPlanned(string format, string edition)
    {
        if (string.IsNullOrWhiteSpace(format)) return edition;
        if (string.IsNullOrWhiteSpace(edition)) return format;
        return $"{format} {edition}";
    }

    private static string NormalizeFormat(string value)
    {
        string normalized = NormalizeWords(value);
        if (normalized.Contains("4K", StringComparison.Ordinal)) return "4K UHD";
        if (normalized.Contains("3D", StringComparison.Ordinal) && normalized.Contains("BLU RAY", StringComparison.Ordinal)) return "3D Blu-ray";
        if (normalized.Contains("BLU RAY", StringComparison.Ordinal)) return "Blu-ray";
        if (normalized.Contains("DVD", StringComparison.Ordinal)) return "DVD";
        if (normalized.Contains("LASER", StringComparison.Ordinal)) return "LaserDisc";
        if (normalized.Contains("VHS", StringComparison.Ordinal)) return "VHS";
        if (normalized.Contains("DIGITAL", StringComparison.Ordinal)) return "Digital";
        return value.Trim();
    }

    private static string NormalizeEdition(string value) => value.Trim();

    private static int GetFormatRank(string format)
    {
        string key = NormalizeWords(NormalizeFormat(format));
        return FormatRanks.TryGetValue(key, out int rank) ? rank : 0;
    }

    private static bool EditionMatches(string owned, string planned) =>
        NormalizeWords(owned).Contains(NormalizeWords(planned), StringComparison.Ordinal) ||
        NormalizeWords(planned).Contains(NormalizeWords(owned), StringComparison.Ordinal);

    private static bool IsSpecialEdition(string value)
    {
        string normalized = NormalizeWords(value);
        return normalized.Contains("STEELBOOK", StringComparison.Ordinal) ||
               normalized.Contains("COLLECTOR", StringComparison.Ordinal) ||
               normalized.Contains("LIMITED", StringComparison.Ordinal) ||
               normalized.Contains("DELUXE", StringComparison.Ordinal) ||
               normalized.Contains("DIRECTOR", StringComparison.Ordinal) ||
               normalized.Contains("EXTENDED", StringComparison.Ordinal) ||
               normalized.Contains("ANNIVERSARY", StringComparison.Ordinal) ||
               normalized.Contains("SPECIAL", StringComparison.Ordinal);
    }

    private static string NormalizeWords(string value)
    {
        if (string.IsNullOrWhiteSpace(value)) return string.Empty;
        char[] characters = value
            .ToUpperInvariant()
            .Select(character => char.IsLetterOrDigit(character) ? character : ' ')
            .ToArray();
        return string.Join(' ', new string(characters)
            .Split(' ', StringSplitOptions.RemoveEmptyEntries));
    }

    private static List<string> SplitValues(string value) =>
        string.IsNullOrWhiteSpace(value)
            ? []
            : value.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();
}
