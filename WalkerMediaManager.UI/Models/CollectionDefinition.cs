using System.Collections.Generic;

namespace WalkerMediaManager.UI.Models;

public sealed class CollectionDefinition
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string Category { get; set; } = string.Empty;
    public string Studio { get; set; } = string.Empty;
    public string Type { get; set; } = "Franchise";
    public List<string> Aliases { get; set; } = [];
    public List<CollectionMovieDefinition> Movies { get; set; } = [];
}

public sealed class CollectionMovieDefinition
{
    public string Title { get; set; } = string.Empty;
    public int Year { get; set; }
    public string ImdbId { get; set; } = string.Empty;
    public int? TmdbId { get; set; }
    public List<string> Aliases { get; set; } = [];
}
