using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using WalkerMediaManager.UI.Models;
using WalkerMediaManager.UI.Repositories;

namespace WalkerMediaManager.UI.Services;

public sealed class TVMetadataService
{
    private const int MinimumMatchScore = 70;
    private const int AmbiguousMatchMargin = 4;
    private static readonly TimeSpan RequestRetryDelay = TimeSpan.FromSeconds(1);
    private static readonly TimeSpan ShowRequestDelay = TimeSpan.FromMilliseconds(1100);
    private static readonly HttpClient HttpClient = CreateClient();
    private readonly TVShowRepository _repository = new();

    public async Task<TVMetadataSyncResult> RefreshAllAsync(
        IProgress<TVMetadataProgress>? progress = null,
        CancellationToken cancellationToken = default)
    {
        Stopwatch stopwatch = Stopwatch.StartNew();
        List<TVShow> shows = await _repository.GetAllAsync();
        TVMetadataSyncResult result = new();

        DiagnosticsService.Log($"TV metadata refresh started for {shows.Count} shows.");

        for (int index = 0; index < shows.Count; index++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            TVShow show = shows[index];
            progress?.Report(new TVMetadataProgress(
                index + 1,
                shows.Count,
                show.Title,
                "Matching provider metadata"));

            try
            {
                TVMatchResult matchResult = await FindBestMatchAsync(show, cancellationToken);
                if (matchResult.Show is null)
                {
                    result.NotFoundCount++;
                    AddDiagnostic(result, show, matchResult, "NotFound", matchResult.Reason);
                    progress?.Report(new TVMetadataProgress(
                        index + 1,
                        shows.Count,
                        show.Title,
                        "No safe match"));
                    continue;
                }

                TVMazeShow match = matchResult.Show;
                progress?.Report(new TVMetadataProgress(
                    index + 1,
                    shows.Count,
                    show.Title,
                    "Loading season data"));
                IReadOnlyList<TVMazeSeason> seasons = await GetSeasonsAsync(match.Id, cancellationToken);
                int totalSeasons = seasons
                    .Where(season => season.Number > 0)
                    .Select(season => season.Number)
                    .DefaultIfEmpty(0)
                    .Max();

                int totalEpisodes = seasons
                    .Where(season => season.Number > 0)
                    .Sum(season => Math.Max(0, season.EpisodeOrder));

                show.TVMazeId = match.Id;
                show.TotalSeasons = totalSeasons > 0 ? totalSeasons : show.TotalSeasons;
                show.Episodes = totalEpisodes > 0 ? totalEpisodes : show.Episodes;
                show.Status = match.Status;
                show.FirstAirDate = match.Premiered;
                show.LastAirDate = match.Ended;
                show.Network = FirstNonEmpty(match.NetworkName, match.WebChannelName);
                show.Studio = FirstNonEmpty(show.Studio, show.Network);
                show.Summary = FirstNonEmpty(CleanHtml(match.Summary), show.Summary);
                show.PosterPath = FirstNonEmpty(match.OriginalImage, match.MediumImage, show.PosterPath);
                show.IMDbId = FirstNonEmpty(match.IMDbId, show.IMDbId, ExtractExternalId(show.PlexGuid, "imdb"));
                show.MetadataLastSynced = DateTimeOffset.UtcNow.ToString("O");

                progress?.Report(new TVMetadataProgress(
                    index + 1,
                    shows.Count,
                    show.Title,
                    "Saving metadata"));
                await _repository.UpdateMetadataAsync(show);
                result.UpdatedCount++;

                string reason = string.IsNullOrWhiteSpace(show.PosterPath)
                    ? "Matched provider record, but neither the provider nor the existing record contains a poster."
                    : string.Empty;
                AddDiagnostic(result, show, matchResult, "Updated", reason);
                progress?.Report(new TVMetadataProgress(
                    index + 1,
                    shows.Count,
                    show.Title,
                    "Complete"));
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception exception)
            {
                result.FailedCount++;
                TVMatchResult failedMatch = TVMatchResult.Failed(exception.Message);
                AddDiagnostic(result, show, failedMatch, "Failed", exception.Message);
                progress?.Report(new TVMetadataProgress(
                    index + 1,
                    shows.Count,
                    show.Title,
                    "Failed"));
                DiagnosticsService.LogException($"TV metadata refresh failed for '{show.Title}'.", exception);
            }
            finally
            {
                if (index < shows.Count - 1)
                {
                    await Task.Delay(ShowRequestDelay, cancellationToken);
                }
            }
        }

        stopwatch.Stop();
        result.Elapsed = stopwatch.Elapsed;
        result.DiagnosticsReportPath = WriteDiagnosticsReport(result.Diagnostics);
        DiagnosticsService.Log($"TV metadata refresh finished. {result.Summary}");
        return result;
    }

    private static void AddDiagnostic(
        TVMetadataSyncResult result,
        TVShow show,
        TVMatchResult matchResult,
        string outcome,
        string reason)
    {
        bool hasPoster = !string.IsNullOrWhiteSpace(show.PosterPath);
        if (hasPoster)
        {
            result.PosterAvailableCount++;
        }
        else
        {
            result.PosterMissingCount++;
        }

        TVMetadataDiagnostic diagnostic = new()
        {
            Title = show.Title,
            Year = show.Year,
            Outcome = outcome,
            MatchMethod = matchResult.Method,
            ConfidenceScore = matchResult.Score,
            CandidateCount = matchResult.CandidateCount,
            ProviderId = matchResult.Show?.Id,
            ProviderTitle = matchResult.Show?.Name ?? string.Empty,
            PosterUrl = matchResult.Show is null
                ? show.PosterPath
                : FirstNonEmpty(matchResult.Show.OriginalImage, matchResult.Show.MediumImage, show.PosterPath),
            PosterStatus = hasPoster ? "Available" : "Missing",
            Reason = reason
        };

        result.Diagnostics.Add(diagnostic);
        DiagnosticsService.Log(
            $"TV metadata: Title='{diagnostic.Title}', Outcome={diagnostic.Outcome}, " +
            $"Method={ValueOrNone(diagnostic.MatchMethod)}, Confidence={diagnostic.ConfidenceScore}, " +
            $"ProviderTitle='{ValueOrNone(diagnostic.ProviderTitle)}', Poster={diagnostic.PosterStatus}, " +
            $"Reason='{ValueOrNone(diagnostic.Reason)}'.");
    }

    private static async Task<TVMatchResult> FindBestMatchAsync(
        TVShow show,
        CancellationToken cancellationToken)
    {
        if (show.TVMazeId.HasValue)
        {
            TVMazeShow? byId = await GetShowByIdAsync(show.TVMazeId.Value, cancellationToken);
            if (byId is not null)
            {
                return TVMatchResult.Matched(byId, "TVMazeId", 100, 1);
            }
        }

        string imdbId = FirstNonEmpty(show.IMDbId, ExtractExternalId(show.PlexGuid, "imdb"));
        if (!string.IsNullOrWhiteSpace(imdbId))
        {
            TVMazeShow? byImdb = await LookupByImdbAsync(imdbId, cancellationToken);
            if (byImdb is not null)
            {
                return TVMatchResult.Matched(byImdb, "IMDbId", 100, 1);
            }
        }

        HashSet<string> queryTitles = new(StringComparer.OrdinalIgnoreCase)
        {
            show.Title.Trim(),
            RemoveTrailingYear(show.Title),
            ReplaceAmpersand(show.Title),
            RemoveLeadingArticle(show.Title)
        };

        Dictionary<int, SearchCandidate> candidates = [];
        foreach (string queryTitle in queryTitles.Where(value => !string.IsNullOrWhiteSpace(value)))
        {
            foreach (SearchCandidate candidate in await SearchShowsAsync(queryTitle, cancellationToken))
            {
                if (!candidates.TryGetValue(candidate.Show.Id, out SearchCandidate? existing) ||
                    candidate.ProviderScore > existing.ProviderScore)
                {
                    candidates[candidate.Show.Id] = candidate;
                }
            }
        }

        List<ScoredCandidate> scored = candidates.Values
            .Select(candidate => new ScoredCandidate(
                candidate.Show,
                CalculateScore(show.Title, show.Year, candidate.Show.Name, candidate.Show.PremiereYear),
                candidate.ProviderScore,
                "TitleAndYear"))
            .OrderByDescending(candidate => candidate.Score)
            .ThenByDescending(candidate => candidate.ProviderScore)
            .ToList();

        ScoredCandidate? best = scored.FirstOrDefault();
        if (best is null || best.Score < MinimumMatchScore)
        {
            best = await FindAliasMatchAsync(show, scored.Take(3), cancellationToken);
        }

        if (best is null || best.Score < MinimumMatchScore)
        {
            return TVMatchResult.NotFound(
                candidates.Count,
                candidates.Count == 0
                    ? "The provider returned no candidates for the title variants."
                    : $"No candidate met the minimum confidence score of {MinimumMatchScore}.");
        }

        ScoredCandidate? runnerUp = scored
            .Where(candidate => candidate.Show.Id != best.Show.Id)
            .OrderByDescending(candidate => candidate.Score)
            .FirstOrDefault();
        if (runnerUp is not null &&
            best.Score - runnerUp.Score < AmbiguousMatchMargin &&
            best.Score < 95)
        {
            return TVMatchResult.NotFound(
                candidates.Count,
                $"The two best candidates were too close to choose safely ({best.Score} vs. {runnerUp.Score}).");
        }

        string method = best.Method == "AliasTitle"
            ? best.Method
            : IsExactNormalizedMatch(show.Title, best.Show.Name)
                ? show.Year > 0 && best.Show.PremiereYear > 0 ? "TitleAndYear" : "NormalizedTitle"
                : "NormalizedTitle";

        return TVMatchResult.Matched(best.Show, method, best.Score, candidates.Count);
    }

    private static async Task<ScoredCandidate?> FindAliasMatchAsync(
        TVShow show,
        IEnumerable<ScoredCandidate> candidates,
        CancellationToken cancellationToken)
    {
        ScoredCandidate? best = null;
        foreach (ScoredCandidate candidate in candidates)
        {
            foreach (string alias in await GetAliasesAsync(candidate.Show.Id, cancellationToken))
            {
                int score = CalculateScore(show.Title, show.Year, alias, candidate.Show.PremiereYear);
                if (best is null || score > best.Score)
                {
                    best = new ScoredCandidate(
                        candidate.Show,
                        score,
                        candidate.ProviderScore,
                        "AliasTitle");
                }
            }
        }

        return best;
    }

    private static int CalculateScore(string targetTitle, int targetYear, string candidateTitle, int candidateYear)
    {
        string target = Normalize(targetTitle);
        string candidate = Normalize(candidateTitle);
        string canonicalTarget = Normalize(RemoveLeadingArticle(targetTitle));
        string canonicalCandidate = Normalize(RemoveLeadingArticle(candidateTitle));

        int score;
        if (target == candidate)
        {
            score = 90;
        }
        else if (canonicalTarget == canonicalCandidate)
        {
            score = 84;
        }
        else
        {
            double similarity = TokenSimilarity(targetTitle, candidateTitle);
            score = similarity >= 0.80
                ? 65 + (int)Math.Round(similarity * 20)
                : target.Contains(candidate, StringComparison.Ordinal) ||
                  candidate.Contains(target, StringComparison.Ordinal)
                    ? 70
                    : (int)Math.Round(similarity * 65);
        }

        if (targetYear > 0 && candidateYear > 0)
        {
            int difference = Math.Abs(targetYear - candidateYear);
            score += difference switch
            {
                0 => 10,
                1 => 3,
                <= 3 => -5,
                _ => -15
            };
        }

        return Math.Clamp(score, 0, 100);
    }

    private static double TokenSimilarity(string left, string right)
    {
        HashSet<string> leftTokens = Tokenize(left);
        HashSet<string> rightTokens = Tokenize(right);
        if (leftTokens.Count == 0 || rightTokens.Count == 0)
        {
            return 0;
        }

        int intersection = leftTokens.Intersect(rightTokens).Count();
        int union = leftTokens.Union(rightTokens).Count();
        return union == 0 ? 0 : (double)intersection / union;
    }

    private static HashSet<string> Tokenize(string value)
    {
        string normalized = Regex.Replace(
            value.ToLowerInvariant().Replace("&", " and ", StringComparison.Ordinal),
            "[^a-z0-9]+",
            " ",
            RegexOptions.CultureInvariant);

        return normalized
            .Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Where(token => token is not "the" and not "a" and not "an")
            .ToHashSet(StringComparer.Ordinal);
    }

    private static async Task<IReadOnlyList<SearchCandidate>> SearchShowsAsync(
        string title,
        CancellationToken cancellationToken)
    {
        using HttpResponseMessage response = await SendWithRetryAsync(
            $"search/shows?q={Uri.EscapeDataString(title)}",
            cancellationToken);
        response.EnsureSuccessStatusCode();

        await using Stream stream = await response.Content.ReadAsStreamAsync(cancellationToken);
        using JsonDocument document = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken);
        List<SearchCandidate> candidates = [];
        foreach (JsonElement item in document.RootElement.EnumerateArray())
        {
            if (item.TryGetProperty("show", out JsonElement showElement))
            {
                candidates.Add(new SearchCandidate(ParseShow(showElement), GetDouble(item, "score")));
            }
        }

        return candidates;
    }

    private static async Task<IReadOnlyList<string>> GetAliasesAsync(
        int showId,
        CancellationToken cancellationToken)
    {
        using HttpResponseMessage response = await SendWithRetryAsync($"shows/{showId}/akas", cancellationToken);
        if (response.StatusCode == HttpStatusCode.NotFound)
        {
            return [];
        }

        response.EnsureSuccessStatusCode();
        await using Stream stream = await response.Content.ReadAsStreamAsync(cancellationToken);
        using JsonDocument document = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken);
        return document.RootElement
            .EnumerateArray()
            .Select(element => GetString(element, "name"))
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private static async Task<TVMazeShow?> LookupByImdbAsync(string imdbId, CancellationToken cancellationToken)
    {
        using HttpResponseMessage response = await SendWithRetryAsync(
            $"lookup/shows?imdb={Uri.EscapeDataString(imdbId)}",
            cancellationToken);
        if (response.StatusCode == HttpStatusCode.NotFound)
        {
            return null;
        }

        response.EnsureSuccessStatusCode();
        await using Stream stream = await response.Content.ReadAsStreamAsync(cancellationToken);
        using JsonDocument document = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken);
        return ParseShow(document.RootElement);
    }

    private static async Task<TVMazeShow?> GetShowByIdAsync(int id, CancellationToken cancellationToken)
    {
        using HttpResponseMessage response = await SendWithRetryAsync($"shows/{id}", cancellationToken);
        if (response.StatusCode == HttpStatusCode.NotFound)
        {
            return null;
        }

        response.EnsureSuccessStatusCode();
        await using Stream stream = await response.Content.ReadAsStreamAsync(cancellationToken);
        using JsonDocument document = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken);
        return ParseShow(document.RootElement);
    }

    private static async Task<IReadOnlyList<TVMazeSeason>> GetSeasonsAsync(
        int showId,
        CancellationToken cancellationToken)
    {
        using HttpResponseMessage response = await SendWithRetryAsync($"shows/{showId}/seasons", cancellationToken);
        response.EnsureSuccessStatusCode();
        await using Stream stream = await response.Content.ReadAsStreamAsync(cancellationToken);
        using JsonDocument document = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken);

        return document.RootElement
            .EnumerateArray()
            .Select(element => new TVMazeSeason
            {
                Number = GetInt(element, "number"),
                EpisodeOrder = GetInt(element, "episodeOrder")
            })
            .ToList();
    }

    private static async Task<HttpResponseMessage> SendWithRetryAsync(
        string relativePath,
        CancellationToken cancellationToken)
    {
        for (int attempt = 0; ; attempt++)
        {
            HttpResponseMessage response = await HttpClient.GetAsync(relativePath, cancellationToken);
            bool transient = response.StatusCode == HttpStatusCode.TooManyRequests ||
                             (int)response.StatusCode >= 500;
            if (!transient || attempt >= 2)
            {
                return response;
            }

            TimeSpan delay = response.Headers.RetryAfter?.Delta
                             ?? TimeSpan.FromMilliseconds(RequestRetryDelay.TotalMilliseconds * (attempt + 1));
            response.Dispose();
            await Task.Delay(delay, cancellationToken);
        }
    }

    private static TVMazeShow ParseShow(JsonElement element)
    {
        JsonElement image = GetObject(element, "image");
        JsonElement externals = GetObject(element, "externals");
        JsonElement network = GetObject(element, "network");
        JsonElement webChannel = GetObject(element, "webChannel");
        string premiered = GetString(element, "premiered");

        return new TVMazeShow
        {
            Id = GetInt(element, "id"),
            Name = GetString(element, "name"),
            Status = GetString(element, "status"),
            Premiered = premiered,
            Ended = GetString(element, "ended"),
            PremiereYear = DateTime.TryParse(
                premiered,
                CultureInfo.InvariantCulture,
                DateTimeStyles.None,
                out DateTime date)
                    ? date.Year
                    : 0,
            Summary = GetString(element, "summary"),
            MediumImage = GetString(image, "medium"),
            OriginalImage = GetString(image, "original"),
            IMDbId = GetString(externals, "imdb"),
            NetworkName = GetString(network, "name"),
            WebChannelName = GetString(webChannel, "name")
        };
    }

    private static string WriteDiagnosticsReport(IReadOnlyCollection<TVMetadataDiagnostic> diagnostics)
    {
        Directory.CreateDirectory(ApplicationPaths.LogFolder);
        string reportPath = Path.Combine(ApplicationPaths.LogFolder, "tv-metadata-refresh-latest.json");
        string temporaryPath = reportPath + ".tmp";

        try
        {
            string json = JsonSerializer.Serialize(
                diagnostics,
                new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(temporaryPath, json);
            File.Move(temporaryPath, reportPath, true);
        }
        catch (Exception exception)
        {
            DiagnosticsService.LogException("Could not write the TV metadata diagnostic report.", exception);
        }
        finally
        {
            TryDelete(temporaryPath);
        }

        return reportPath;
    }

    private static void TryDelete(string path)
    {
        try
        {
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }
        catch
        {
        }
    }

    private static string Normalize(string value)
    {
        string normalized = RemoveTrailingYear(value)
            .ToLowerInvariant()
            .Replace("&", "and", StringComparison.Ordinal);
        return Regex.Replace(normalized, "[^a-z0-9]", string.Empty, RegexOptions.CultureInvariant);
    }

    private static string RemoveTrailingYear(string value) => Regex.Replace(
        value.Trim(),
        @"\s*[\(\[](?:19|20)\d{2}[\)\]]\s*$",
        string.Empty,
        RegexOptions.CultureInvariant);

    private static string ReplaceAmpersand(string value) =>
        value.Replace("&", "and", StringComparison.OrdinalIgnoreCase);

    private static string RemoveLeadingArticle(string value) => Regex.Replace(
        value.Trim(),
        @"^(?:the|an|a)\s+",
        string.Empty,
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

    private static bool IsExactNormalizedMatch(string left, string right) =>
        string.Equals(Normalize(left), Normalize(right), StringComparison.Ordinal);

    private static string ExtractExternalId(string guid, string provider)
    {
        string prefix = provider + "://";
        string value = guid?.Trim() ?? string.Empty;
        return value.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)
            ? value[prefix.Length..]
            : string.Empty;
    }

    private static string CleanHtml(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return string.Empty;
        }

        return WebUtility.HtmlDecode(Regex.Replace(value, "<.*?>", string.Empty)).Trim();
    }

    private static string FirstNonEmpty(params string[] values) =>
        values.FirstOrDefault(value => !string.IsNullOrWhiteSpace(value)) ?? string.Empty;

    private static string ValueOrNone(string value) =>
        string.IsNullOrWhiteSpace(value) ? "None" : value;

    private static JsonElement GetObject(JsonElement element, string propertyName) =>
        element.ValueKind == JsonValueKind.Object &&
        element.TryGetProperty(propertyName, out JsonElement value) &&
        value.ValueKind == JsonValueKind.Object
            ? value
            : default;

    private static string GetString(JsonElement element, string propertyName) =>
        element.ValueKind == JsonValueKind.Object &&
        element.TryGetProperty(propertyName, out JsonElement value) &&
        value.ValueKind == JsonValueKind.String
            ? value.GetString() ?? string.Empty
            : string.Empty;

    private static int GetInt(JsonElement element, string propertyName) =>
        element.ValueKind == JsonValueKind.Object &&
        element.TryGetProperty(propertyName, out JsonElement value) &&
        value.ValueKind == JsonValueKind.Number &&
        value.TryGetInt32(out int result)
            ? result
            : 0;

    private static double GetDouble(JsonElement element, string propertyName) =>
        element.ValueKind == JsonValueKind.Object &&
        element.TryGetProperty(propertyName, out JsonElement value) &&
        value.ValueKind == JsonValueKind.Number &&
        value.TryGetDouble(out double result)
            ? result
            : 0;

    private static HttpClient CreateClient()
    {
        HttpClient client = new()
        {
            BaseAddress = new Uri("https://api.tvmaze.com/"),
            Timeout = TimeSpan.FromSeconds(30)
        };
        client.DefaultRequestHeaders.UserAgent.ParseAdd("WalkerMediaManager/1.0");
        return client;
    }

    private sealed class TVMazeShow
    {
        public int Id { get; init; }
        public string Name { get; init; } = string.Empty;
        public int PremiereYear { get; init; }
        public string Status { get; init; } = string.Empty;
        public string Premiered { get; init; } = string.Empty;
        public string Ended { get; init; } = string.Empty;
        public string Summary { get; init; } = string.Empty;
        public string MediumImage { get; init; } = string.Empty;
        public string OriginalImage { get; init; } = string.Empty;
        public string IMDbId { get; init; } = string.Empty;
        public string NetworkName { get; init; } = string.Empty;
        public string WebChannelName { get; init; } = string.Empty;
    }

    private sealed record SearchCandidate(TVMazeShow Show, double ProviderScore);

    private sealed record ScoredCandidate(
        TVMazeShow Show,
        int Score,
        double ProviderScore,
        string Method);

    private sealed class TVMatchResult
    {
        public TVMazeShow? Show { get; private init; }
        public string Method { get; private init; } = string.Empty;
        public int Score { get; private init; }
        public int CandidateCount { get; private init; }
        public string Reason { get; private init; } = string.Empty;

        public static TVMatchResult Matched(TVMazeShow show, string method, int score, int candidateCount) =>
            new() { Show = show, Method = method, Score = score, CandidateCount = candidateCount };

        public static TVMatchResult NotFound(int candidateCount, string reason) =>
            new() { CandidateCount = candidateCount, Reason = reason };

        public static TVMatchResult Failed(string reason) => new() { Reason = reason };
    }

    private sealed class TVMazeSeason
    {
        public int Number { get; init; }
        public int EpisodeOrder { get; init; }
    }
}
