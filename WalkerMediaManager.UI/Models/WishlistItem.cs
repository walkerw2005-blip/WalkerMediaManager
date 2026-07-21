using System;

namespace WalkerMediaManager.UI.Models;

public class WishlistItem
{
    public int Id { get; set; }

    public string Title { get; set; } = "";

    public int? TMDbId { get; set; }

    public string Notes { get; set; } = "";

    public int Priority { get; set; }

    public DateTime DateAdded { get; set; } = DateTime.Now;
}