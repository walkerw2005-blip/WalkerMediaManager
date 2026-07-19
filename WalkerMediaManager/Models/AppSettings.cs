namespace WalkerMediaManager.Models;

public sealed class AppSettings
{
    public string PlexServerUrl { get; set; } = string.Empty;
    public DateTimeOffset? LastSuccessfulConnection { get; set; }
}
