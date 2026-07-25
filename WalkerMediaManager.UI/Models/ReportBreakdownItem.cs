namespace WalkerMediaManager.UI.Models;

public sealed class ReportBreakdownItem
{
    public string Name { get; set; } = string.Empty;
    public int Count { get; set; }
    public decimal Value { get; set; }

    public string CountDisplay => Count.ToString("N0");
    public string ValueDisplay => Value.ToString("C");
}
