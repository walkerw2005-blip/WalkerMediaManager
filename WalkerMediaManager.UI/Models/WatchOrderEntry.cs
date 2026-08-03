using System.Collections.Generic;

namespace WalkerMediaManager.UI.Models;

public sealed class WatchOrderEntry
{
    public int Position { get; set; }

    public string Title { get; set; } = string.Empty;

    public int Year { get; set; }

    public IReadOnlyList<string> Aliases { get; set; } = [];

    public string YearDisplay => Year > 0 ? Year.ToString() : string.Empty;
}
