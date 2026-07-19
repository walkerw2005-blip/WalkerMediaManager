using System.Diagnostics;
using System.Net.Http.Headers;
using System.Reflection;
using System.Text.Json;
using WalkerMediaManager.Models;

namespace WalkerMediaManager.Services;

public sealed class UpdateService
{
    private readonly HttpClient _httpClient = new() { Timeout = TimeSpan.FromSeconds(15) };

    public string CurrentVersion
    {
        get
        {
            var informational = Assembly.GetExecutingAssembly()
                .GetCustomAttribute<AssemblyInformationalVersionAttribute>()?
                .InformationalVersion?
                .Split('+')[0];
            return string.IsNullOrWhiteSpace(informational) ? "0.1.0" : informational;
        }
    }

    public async Task<UpdateCheckResult> CheckAsync()
    {
        try
        {
            var channelPath = Path.Combine(AppContext.BaseDirectory, "update-channel.json");
            if (!File.Exists(channelPath))
                return new(false, false, CurrentVersion, CurrentVersion,
                    "Update information is not configured in this build.");

            var channel = JsonSerializer.Deserialize<UpdateChannel>(await File.ReadAllTextAsync(channelPath));
            if (channel is null || string.IsNullOrWhiteSpace(channel.Repository) || channel.Repository.Contains('/' ) is false)
                return new(false, false, CurrentVersion, CurrentVersion,
                    "The update channel is incomplete.");

            using var request = new HttpRequestMessage(
                HttpMethod.Get,
                $"https://api.github.com/repos/{channel.Repository}/releases/latest");
            request.Headers.UserAgent.Add(new ProductInfoHeaderValue("WalkerMediaManager", CurrentVersion));
            request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/vnd.github+json"));

            using var response = await _httpClient.SendAsync(request);
            if (!response.IsSuccessStatusCode)
                return new(false, false, CurrentVersion, CurrentVersion,
                    $"GitHub returned {(int)response.StatusCode} {response.ReasonPhrase}.");

            using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
            var root = document.RootElement;
            var tag = root.GetProperty("tag_name").GetString() ?? CurrentVersion;
            var latestText = tag.TrimStart('v', 'V');
            var pageUrl = root.TryGetProperty("html_url", out var page) ? page.GetString() : null;
            string? downloadUrl = null;

            if (root.TryGetProperty("assets", out var assets))
            {
                foreach (var asset in assets.EnumerateArray())
                {
                    var name = asset.GetProperty("name").GetString();
                    if (string.Equals(name, "WalkerMediaManagerSetup.exe", StringComparison.OrdinalIgnoreCase))
                    {
                        downloadUrl = asset.GetProperty("browser_download_url").GetString();
                        break;
                    }
                }
            }

            var updateAvailable = Version.TryParse(CurrentVersion, out var current) &&
                                  Version.TryParse(latestText, out var latest) && latest > current;

            return new(true, updateAvailable, CurrentVersion, latestText,
                updateAvailable ? "A newer version is available." : "You are running the latest version.",
                downloadUrl, pageUrl);
        }
        catch (Exception ex)
        {
            LogService.Write($"Update check failed: {ex}");
            return new(false, false, CurrentVersion, CurrentVersion, ex.Message);
        }
    }

    public async Task<string> DownloadInstallerAsync(string downloadUrl)
    {
        var target = Path.Combine(Path.GetTempPath(), "WalkerMediaManagerSetup.exe");
        using var response = await _httpClient.GetAsync(downloadUrl, HttpCompletionOption.ResponseHeadersRead);
        response.EnsureSuccessStatusCode();
        await using var source = await response.Content.ReadAsStreamAsync();
        await using var destination = File.Create(target);
        await source.CopyToAsync(destination);
        return target;
    }

    public static void LaunchInstaller(string installerPath)
    {
        Process.Start(new ProcessStartInfo(installerPath) { UseShellExecute = true });
    }

    public static void OpenUrl(string url)
    {
        Process.Start(new ProcessStartInfo(url) { UseShellExecute = true });
    }
}
