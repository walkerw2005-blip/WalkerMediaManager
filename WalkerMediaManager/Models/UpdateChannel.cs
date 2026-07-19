using System.Text.Json.Serialization;

namespace WalkerMediaManager.Models;

public sealed class UpdateChannel
{
    [JsonPropertyName("repository")]
    public string Repository { get; set; } = string.Empty;

    [JsonPropertyName("channel")]
    public string Channel { get; set; } = "stable";
}
