using System;

namespace WalkerMediaManager.UI.Models;

public sealed class LoanRecord
{
    public int Id { get; set; }
    public int OwnedCopyId { get; set; }
    public int MovieId { get; set; }
    public string Title { get; set; } = string.Empty;
    public int ReleaseYear { get; set; }
    public string Format { get; set; } = string.Empty;
    public string Edition { get; set; } = string.Empty;
    public string Borrower { get; set; } = string.Empty;
    public string LoanedDate { get; set; } = string.Empty;
    public string DueDate { get; set; } = string.Empty;
    public string ReturnedDate { get; set; } = string.Empty;
    public string Notes { get; set; } = string.Empty;

    public bool IsReturned => !string.IsNullOrWhiteSpace(ReturnedDate);
    public string TitleDisplay => ReleaseYear > 0 ? $"{Title} ({ReleaseYear})" : Title;
    public string CopyDisplay => string.IsNullOrWhiteSpace(Edition) ? Format : $"{Format} - {Edition}";
    public string LoanedDateDisplay => FormatDate(LoanedDate);
    public string DueDateDisplay => string.IsNullOrWhiteSpace(DueDate) ? "No due date" : FormatDate(DueDate);
    public string StatusDisplay => IsReturned ? $"Returned {FormatDate(ReturnedDate)}" : "On loan";

    private static string FormatDate(string value) =>
        DateTime.TryParse(value, out DateTime date) ? date.ToString("MMM d, yyyy") : value;
}
