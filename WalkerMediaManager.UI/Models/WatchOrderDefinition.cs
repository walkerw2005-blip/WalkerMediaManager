using System.Collections.Generic;

namespace WalkerMediaManager.UI.Models;

public sealed class WatchOrderDefinition
{
    public string CollectionName { get; set; } = string.Empty;

    public string Name { get; set; } = string.Empty;

    public string Description { get; set; } = string.Empty;

    public IReadOnlyList<WatchOrderEntry> Entries { get; set; } = [];

    public int MovieCount => Entries.Count;
}
