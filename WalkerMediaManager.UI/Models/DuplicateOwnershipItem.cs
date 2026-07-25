namespace WalkerMediaManager.UI.Models;

public sealed class DuplicateOwnershipItem
{
    public int MovieId { get; set; }
    public string Title { get; set; } = string.Empty;
    public int ReleaseYear { get; set; }
    public int CopyCount { get; set; }
    public string Formats { get; set; } = string.Empty;
    public decimal RecordedValue { get; set; }

    public string TitleDisplay => ReleaseYear > 0 ? $"{Title} ({ReleaseYear})" : Title;
    public string CopyCountDisplay => CopyCount == 1 ? "1 copy" : $"{CopyCount} copies";
    public string RecordedValueDisplay => RecordedValue.ToString("C");
}
