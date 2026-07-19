using System.Net.Http.Headers;
using System.Xml.Linq;
using WalkerMediaManager.Models;

namespace WalkerMediaManager.Services;

public sealed class PlexService
{
    private readonly HttpClient _httpClient = new() { Timeout = TimeSpan.FromSeconds(15) };

    public async Task<PlexConnectionResult> TestConnectionAsync(string serverUrl, string token)
    {
        try
        {
            var baseUrl = NormalizeServerUrl(serverUrl);
            using var request = CreateRequest(HttpMethod.Get, $"{baseUrl}/identity", token);
            using var response = await _httpClient.SendAsync(request);

            if (!response.IsSuccessStatusCode)
                return new(false, $"Plex returned {(int)response.StatusCode} {response.ReasonPhrase}.");

            var xml = XDocument.Parse(await response.Content.ReadAsStringAsync());
            var container = xml.Root;
            var name = container?.Attribute("machineIdentifier")?.Value;
            return new(true, "Connection successful.", name);
        }
        catch (TaskCanceledException)
        {
            return new(false, "Connection timed out. Confirm the server address and that Plex is running.");
        }
        catch (Exception ex)
        {
            LogService.Write($"Plex test failed: {ex}");
            return new(false, ex.Message);
        }
    }

    public async Task<IReadOnlyList<PlexLibrary>> GetLibrariesAsync(string serverUrl, string token)
    {
        var baseUrl = NormalizeServerUrl(serverUrl);
        using var request = CreateRequest(HttpMethod.Get, $"{baseUrl}/library/sections", token);
        using var response = await _httpClient.SendAsync(request);
        response.EnsureSuccessStatusCode();

        var xml = XDocument.Parse(await response.Content.ReadAsStringAsync());
        return xml.Descendants("Directory")
            .Select(x => new PlexLibrary(
                x.Attribute("key")?.Value ?? string.Empty,
                x.Attribute("title")?.Value ?? "Unnamed Library",
                x.Attribute("type")?.Value ?? "unknown"))
            .Where(x => !string.IsNullOrWhiteSpace(x.Key))
            .OrderBy(x => x.Title)
            .ToList();
    }

    private static HttpRequestMessage CreateRequest(HttpMethod method, string url, string token)
    {
        var request = new HttpRequestMessage(method, url);
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/xml"));
        request.Headers.Add("X-Plex-Token", token.Trim());
        request.Headers.Add("X-Plex-Product", "Walker Media Manager");
        request.Headers.Add("X-Plex-Version", "0.1.0");
        request.Headers.Add("X-Plex-Client-Identifier", "walker-media-manager-desktop");
        return request;
    }

    private static string NormalizeServerUrl(string serverUrl)
    {
        var value = serverUrl.Trim().TrimEnd('/');
        if (!value.StartsWith("http://", StringComparison.OrdinalIgnoreCase) &&
            !value.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
            value = "http://" + value;
        return value;
    }
}
