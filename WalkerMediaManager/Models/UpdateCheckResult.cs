namespace WalkerMediaManager.Models;

public sealed record UpdateCheckResult(
    bool Success,
    bool UpdateAvailable,
    string CurrentVersion,
    string LatestVersion,
    string Message,
    string? DownloadUrl = null,
    string? ReleasePageUrl = null);
