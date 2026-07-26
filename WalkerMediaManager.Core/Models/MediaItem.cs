using System;

namespace WalkerMediaManager.Core.Models;

public sealed class MediaItem
{
    public int Id { get; set; }

    public string Title { get; set; } = string.Empty;

    public string SortTitle { get; set; } = string.Empty;

    public string LibraryName { get; set; } = string.Empty;

    public string MediaType { get; set; } = string.Empty;

    public string PlexRatingKey { get; set; } = string.Empty;

    public string PosterPath { get; set; } = string.Empty;

    public DateTime? DateAdded { get; set; }

    public DateTime? LastPlayed { get; set; }
}