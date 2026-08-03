using System;
using System.Collections.Generic;
using System.Globalization;
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
    private static readonly HttpClient HttpClient = CreateClient();
    private readonly TVShowRepository _repository = new();

    public async Task<TVMetadataSyncResult> RefreshAllAsync(
        IProgress<string>? progress = null,
        CancellationToken cancellationToken = default)
    {
        List<TVShow> shows = await _repository.GetAllAsync();
        TVMetadataSyncResult result = new();

        for (int index = 0; index < shows.Count; index++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            TVShow show = shows[index];
            progress?.Report($"Refreshing {index + 1} of {shows.Count}: {show.Title}");

            try
            {
                TVMazeShow? match = await FindBestMatchAsync(show, cancellationToken);
                if (match is null)
                {
                    result.NotFoundCount++;
                    continue;
                }

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
                show.BackgroundPath = FirstNonEmpty(match.BackgroundImage, show.BackgroundPath);
                show.IMDbId = FirstNonEmpty(match.IMDbId, show.IMDbId);
                show.MetadataLastSynced = DateTimeOffset.UtcNow.ToString("O");

                await _repository.UpdateMetadataAsync(show);
                result.UpdatedCount++;
                await Task.Delay(TimeSpan.FromMilliseconds(1100), cancellationToken);
            }
            catch (Exception exception)
            {
                result.FailedCount++;
                DiagnosticsService.LogException($"TV metadata refresh failed for '{show.Title}'.", exception);
            }
        }

        return result;
    }

    private static async Task<TVMazeShow?> FindBestMatchAsync(
        TVShow show,
        CancellationToken cancellationToken)
    {
        if (show.TVMazeId.HasValue)
        {
            TVMazeShow? byId = await GetShowByIdAsync(show.TVMazeId.Value, cancellationToken);
            if (byId is not null)
            {
                return byId;
            }
        }

        if (!string.IsNullOrWhiteSpace(show.IMDbId))
        {
            TVMazeShow? byImdb = await LookupByImdbAsync(show.IMDbId, cancellationToken);
            if (byImdb is not null)
            {
                return byImdb;
            }
        }

        string query = Uri.EscapeDataString(show.Title);
        using HttpResponseMessage response = await HttpClient.GetAsync(
            $"search/shows?q={query}",
            cancellationToken);
        response.EnsureSuccessStatusCode();

        await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
        using JsonDocument document = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken);

        List<TVMazeShow> candidates = [];
        foreach (JsonElement item in document.RootElement.EnumerateArray())
        {
            if (item.TryGetProperty("show", out JsonElement showElement))
            {
                candidates.Add(ParseShow(showElement));
            }
        }

        string normalizedTarget = Normalize(show.Title);
        return candidates
            .Select(candidate => new
            {
                Show = candidate,
                Score = CalculateScore(normalizedTarget, show.Year, candidate)
            })
            .Where(item => item.Score >= 60)
            .OrderByDescending(item => item.Score)
            .Select(item => item.Show)
            .FirstOrDefault();
    }

    private static int CalculateScore(string normalizedTarget, int year, TVMazeShow candidate)
    {
        string normalizedCandidate = Normalize(candidate.Name);
        int score = normalizedCandidate == normalizedTarget ? 90 : 0;

        if (score == 0 && (normalizedCandidate.Contains(normalizedTarget, StringComparison.Ordinal) ||
                           normalizedTarget.Contains(normalizedCandidate, StringComparison.Ordinal)))
        {
            score = 70;
        }

        if (year > 0 && candidate.PremiereYear > 0)
        {
            int difference = Math.Abs(year - candidate.PremiereYear);
            score += difference == 0 ? 10 : difference == 1 ? 4 : -15;
        }

        return score;
    }

    private static async Task<TVMazeShow?> LookupByImdbAsync(string imdbId, CancellationToken cancellationToken)
    {
        using HttpResponseMessage response = await HttpClient.GetAsync(
            $"lookup/shows?imdb={Uri.EscapeDataString(imdbId)}",
            cancellationToken);

        if (response.StatusCode == HttpStatusCode.NotFound)
        {
            return null;
        }

        response.EnsureSuccessStatusCode();
        await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
        using JsonDocument document = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken);
        return ParseShow(document.RootElement);
    }

    private static async Task<TVMazeShow?> GetShowByIdAsync(int id, CancellationToken cancellationToken)
    {
        using HttpResponseMessage response = await HttpClient.GetAsync($"shows/{id}", cancellationToken);
        if (response.StatusCode == HttpStatusCode.NotFound)
        {
            return null;
        }

        response.EnsureSuccessStatusCode();
        await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
        using JsonDocument document = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken);
        return ParseShow(document.RootElement);
    }

    private static async Task<IReadOnlyList<TVMazeSeason>> GetSeasonsAsync(
        int showId,
        CancellationToken cancellationToken)
    {
        using HttpResponseMessage response = await HttpClient.GetAsync(
            $"shows/{showId}/seasons",
            cancellationToken);
        response.EnsureSuccessStatusCode();

        await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
        using JsonDocument document = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken);

        List<TVMazeSeason> seasons = [];
        foreach (JsonElement element in document.RootElement.EnumerateArray())
        {
            seasons.Add(new TVMazeSeason
            {
                Number = GetInt(element, "number"),
                EpisodeOrder = GetInt(element, "episodeOrder")
            });
        }

        return seasons;
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
            PremiereYear = DateTime.TryParse(premiered, CultureInfo.InvariantCulture, DateTimeStyles.None, out DateTime date)
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

    private static string Normalize(string value) => Regex.Replace(
        value.ToLowerInvariant(),
        "[^a-z0-9]",
        string.Empty,
        RegexOptions.CultureInvariant);

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
        value.TryGetInt32(out int result)
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
        public string BackgroundImage { get; init; } = string.Empty;
        public string IMDbId { get; init; } = string.Empty;
        public string NetworkName { get; init; } = string.Empty;
        public string WebChannelName { get; init; } = string.Empty;
    }

    private sealed class TVMazeSeason
    {
        public int Number { get; init; }
        public int EpisodeOrder { get; init; }
    }
}
