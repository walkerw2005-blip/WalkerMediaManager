using System;

namespace WalkerMediaManager.UI.Models;

public sealed class BarcodeRecord
{
    public int Id { get; set; }
    public string Code { get; set; } = string.Empty;
    public int MovieId { get; set; }
    public string Title { get; set; } = string.Empty;
    public int Year { get; set; }
    public string Format { get; set; } = string.Empty;
    public string Edition { get; set; } = string.Empty;
    public string Notes { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
}
