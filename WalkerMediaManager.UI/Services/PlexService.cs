using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using System.Xml.Linq;
using WalkerMediaManager.UI.Models;

namespace WalkerMediaManager.UI.Services;

public sealed class PlexService
{
    private const int PageSize = 200;

    private readonly HttpClient _httpClient = new()
    {
        Timeout = TimeSpan.FromSeconds(60)
    };

    public async Task<string> TestConnectionAsync(
        string serverUrl,
        string token,
        CancellationToken cancellationToken = default)
    {
        using HttpRequestMessage request = CreateRequest(
            serverUrl,
            "/identity",
            token);

        using HttpResponseMessage response =
            await _httpClient.SendAsync(request, cancellationToken);

        response.EnsureSuccessStatusCode();

        string xml = await response.Content.ReadAsStringAsync(
            cancellationToken);

        XDocument document = XDocument.Parse(xml);
        XElement? container = document.Root;

        string machineIdentifier =
            container?.Attribute("machineIdentifier")?.Value
            ?? string.Empty;

        string version =
            container?.Attribute("version")?.Value
            ?? string.Empty;

        if (string.IsNullOrWhiteSpace(machineIdentifier))
        {
            throw new InvalidOperationException(
                "The Plex server responded, but its identity could not be read.");
        }

        return string.IsNullOrWhiteSpace(version)
            ? $"Connected to Plex server {machineIdentifier}."
            : $"Connected to Plex server {machineIdentifier} (version {version}).";
    }

    public async Task<IReadOnlyList<PlexLibrarySection>>
        GetLibrarySectionsAsync(
            string serverUrl,
            string token,
            CancellationToken cancellationToken = default)
    {
        using HttpRequestMessage request = CreateRequest(
            serverUrl,
            "/library/sections",
            token);

        using HttpResponseMessage response =
            await _httpClient.SendAsync(request, cancellationToken);

        response.EnsureSuccessStatusCode();

        string xml = await response.Content.ReadAsStringAsync(
            cancellationToken);

        XDocument document = XDocument.Parse(xml);

        return document
            .Descendants("Directory")
            .Select(
                element => new PlexLibrarySection
                {
                    Key = element.Attribute("key")?.Value
                        ?? string.Empty,
                    Title = element.Attribute("title")?.Value
                        ?? string.Empty,
                    Type = element.Attribute("type")?.Value
                        ?? string.Empty
                })
            .Where(section => !string.IsNullOrWhiteSpace(section.Key))
            .OrderBy(section => section.Title)
            .ToList();
    }

    public async Task<IReadOnlyList<PlexMovie>> GetMoviesAsync(
        string serverUrl,
        string token,
        string librarySectionKey,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(librarySectionKey))
        {
            throw new ArgumentException(
                "Select a Plex movie library.",
                nameof(librarySectionKey));
        }

        List<PlexMovie> movies = new();
        int start = 0;

        while (true)
        {
            cancellationToken.ThrowIfCancellationRequested();

            XDocument document = await GetLibraryPageAsync(
                serverUrl,
                token,
                librarySectionKey,
                start,
                cancellationToken);

            List<PlexMovie> page = document
                .Descendants("Video")
                .Where(element =>
                    string.Equals(
                        element.Attribute("type")?.Value,
                        "movie",
                        StringComparison.OrdinalIgnoreCase))
                .Select(ParseMovie)
                .Where(movie => !string.IsNullOrWhiteSpace(movie.Title))
                .ToList();

            movies.AddRange(page);

            if (ShouldStopPaging(document.Root, page.Count, ref start))
            {
                break;
            }
        }

        return movies
            .GroupBy(
                movie => string.IsNullOrWhiteSpace(movie.PlexGuid)
                    ? $"{movie.Title}|{movie.ReleaseYear}|{movie.PlexKey}"
                    : movie.PlexGuid,
                StringComparer.OrdinalIgnoreCase)
            .Select(group => group.First())
            .OrderBy(movie => movie.Title)
            .ThenBy(movie => movie.ReleaseYear)
            .ToList();
    }

    public async Task<IReadOnlyList<PlexTVShow>> GetTVShowsAsync(
        string serverUrl,
        string token,
        string librarySectionKey,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(librarySectionKey))
        {
            throw new ArgumentException(
                "Select a Plex TV library.",
                nameof(librarySectionKey));
        }

        List<PlexTVShow> shows = new();
        int start = 0;

        while (true)
        {
            cancellationToken.ThrowIfCancellationRequested();

            XDocument document = await GetLibraryPageAsync(
                serverUrl,
                token,
                librarySectionKey,
                start,
                cancellationToken);

            List<PlexTVShow> page = document
                .Descendants("Directory")
                .Where(element =>
                    string.Equals(
                        element.Attribute("type")?.Value,
                        "show",
                        StringComparison.OrdinalIgnoreCase))
                .Select(ParseTVShow)
                .Where(show => !string.IsNullOrWhiteSpace(show.Title))
                .ToList();

            shows.AddRange(page);

            if (ShouldStopPaging(document.Root, page.Count, ref start))
            {
                break;
            }
        }

        return shows
            .GroupBy(
                show => string.IsNullOrWhiteSpace(show.PlexGuid)
                    ? $"{show.Title}|{show.Year}|{show.PlexRatingKey}"
                    : show.PlexGuid,
                StringComparer.OrdinalIgnoreCase)
            .Select(group => group.First())
            .OrderBy(show => show.Title)
            .ThenBy(show => show.Year)
            .ToList();
    }

    private async Task<XDocument> GetLibraryPageAsync(
        string serverUrl,
        string token,
        string librarySectionKey,
        int start,
        CancellationToken cancellationToken)
    {
        string relativePath =
            $"/library/sections/{Uri.EscapeDataString(librarySectionKey)}/all" +
            $"?X-Plex-Container-Start={start}" +
            $"&X-Plex-Container-Size={PageSize}";

        using HttpRequestMessage request = CreateRequest(
            serverUrl,
            relativePath,
            token);

        using HttpResponseMessage response =
            await _httpClient.SendAsync(
                request,
                HttpCompletionOption.ResponseHeadersRead,
                cancellationToken);

        response.EnsureSuccessStatusCode();

        string xml = await response.Content.ReadAsStringAsync(
            cancellationToken);

        return XDocument.Parse(xml);
    }

    private static bool ShouldStopPaging(
        XElement? root,
        int returnedCount,
        ref int start)
    {
        if (returnedCount == 0)
        {
            return true;
        }

        start += returnedCount;

        int totalSize = ParseIntAttribute(root, "totalSize");

        if (totalSize > 0 && start >= totalSize)
        {
            return true;
        }

        return returnedCount < PageSize;
    }

    private static PlexMovie ParseMovie(XElement element)
    {
        int.TryParse(element.Attribute("year")?.Value, out int year);
        long.TryParse(element.Attribute("duration")?.Value, out long durationMs);

        return new PlexMovie
        {
            PlexKey = element.Attribute("ratingKey")?.Value
                ?? element.Attribute("key")?.Value
                ?? string.Empty,
            PlexGuid = element.Attribute("guid")?.Value
                ?? string.Empty,
            Title = element.Attribute("title")?.Value
                ?? string.Empty,
            ReleaseYear = year,
            Rating = element.Attribute("contentRating")?.Value
                ?? string.Empty,
            RuntimeMinutes = durationMs > 0
                ? (int)Math.Round(durationMs / 60000d)
                : 0,
            Summary = element.Attribute("summary")?.Value
                ?? string.Empty,
            Studio = element.Attribute("studio")?.Value
                ?? string.Empty,
            ThumbPath = element.Attribute("thumb")?.Value
                ?? string.Empty,
            Genres = element
                .Elements("Genre")
                .Select(item => item.Attribute("tag")?.Value ?? string.Empty)
                .Where(value => !string.IsNullOrWhiteSpace(value))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList(),
            Directors = element
                .Elements("Director")
                .Select(item => item.Attribute("tag")?.Value ?? string.Empty)
                .Where(value => !string.IsNullOrWhiteSpace(value))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList()
        };
    }

    private static PlexTVShow ParseTVShow(XElement element)
    {
        int.TryParse(element.Attribute("year")?.Value, out int year);
        int seasons = ParseIntAttribute(element, "childCount");
        int episodes = ParseIntAttribute(element, "leafCount");

        return new PlexTVShow
        {
            PlexRatingKey = element.Attribute("ratingKey")?.Value
                ?? element.Attribute("key")?.Value
                ?? string.Empty,
            PlexGuid = element.Attribute("guid")?.Value
                ?? string.Empty,
            Title = element.Attribute("title")?.Value
                ?? string.Empty,
            Year = year,
            Seasons = seasons,
            Episodes = episodes,
            Studio = element.Attribute("studio")?.Value
                ?? string.Empty,
            Summary = element.Attribute("summary")?.Value
                ?? string.Empty,
            PosterPath = element.Attribute("thumb")?.Value
                ?? string.Empty,
            BackgroundPath = element.Attribute("art")?.Value
                ?? string.Empty
        };
    }

    private static int ParseIntAttribute(
        XElement? element,
        string attributeName)
    {
        if (element is null)
        {
            return 0;
        }

        return int.TryParse(
            element.Attribute(attributeName)?.Value,
            out int value)
            ? value
            : 0;
    }

    private static HttpRequestMessage CreateRequest(
        string serverUrl,
        string relativePath,
        string token)
    {
        string normalizedServerUrl = NormalizeServerUrl(serverUrl);

        HttpRequestMessage request = new(
            HttpMethod.Get,
            $"{normalizedServerUrl}{relativePath}");

        request.Headers.Add("X-Plex-Token", token.Trim());
        request.Headers.Add("X-Plex-Product", "Walker Media Manager");
        request.Headers.Add("X-Plex-Version", "0.4.0.2");
        request.Headers.Add("X-Plex-Client-Identifier", "WalkerMediaManager-Windows");
        request.Headers.Add("Accept", "application/xml");

        return request;
    }

    private static string NormalizeServerUrl(string serverUrl)
    {
        string value = serverUrl.Trim().TrimEnd('/');

        if (string.IsNullOrWhiteSpace(value))
        {
            throw new ArgumentException(
                "Enter the Plex server address.");
        }

        if (!value.StartsWith("http://", StringComparison.OrdinalIgnoreCase) &&
            !value.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
        {
            value = $"http://{value}";
        }

        if (!Uri.TryCreate(value, UriKind.Absolute, out Uri? uri) ||
            (uri.Scheme != Uri.UriSchemeHttp &&
             uri.Scheme != Uri.UriSchemeHttps))
        {
            throw new ArgumentException(
                "Enter a valid Plex address, such as http://192.168.1.5:32400.");
        }

        return uri.ToString().TrimEnd('/');
    }
}
