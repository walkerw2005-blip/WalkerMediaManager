namespace WalkerMediaManager.UI.Models;

public class TVShow
{
    public int Id { get; set; }

    public string Title { get; set; } = "";

    public int Seasons { get; set; }

    public int Episodes { get; set; }

    public bool Owned { get; set; }

    public string PlexGuid { get; set; } = "";

    public int? TMDbId { get; set; }
}