using System.Collections.Generic;

namespace WalkerMediaManager.UI.Models;

public sealed class CollectionSeriesDefinition
{
    public string Name { get; init; } = string.Empty;
    public string Description { get; init; } = string.Empty;
    public IReadOnlyList<CollectionSeriesTitle> Titles { get; init; } = [];
}

public sealed class CollectionSeriesTitle
{
    public string Title { get; init; } = string.Empty;
    public int Year { get; init; }
    public IReadOnlyList<string> Aliases { get; init; } = [];
}
