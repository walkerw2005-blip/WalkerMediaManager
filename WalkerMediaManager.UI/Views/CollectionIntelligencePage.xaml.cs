using System;
using System.Collections.ObjectModel;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using WalkerMediaManager.UI.Models;
using WalkerMediaManager.UI.Repositories;

namespace WalkerMediaManager.UI.Views;

public sealed partial class CollectionIntelligencePage : Page
{
    private readonly CollectionIntelligenceRepository _repository = new();
    private bool _hasLoaded;

    public ObservableCollection<LoanRecord> Loans { get; } = [];
    public ObservableCollection<DuplicateOwnershipItem> Duplicates { get; } = [];
    public ObservableCollection<UpgradeOpportunityItem> Upgrades { get; } = [];
    public ObservableCollection<MediaCollection> Goals { get; } = [];

    public CollectionIntelligencePage()
    {
        InitializeComponent();
        LoansList.ItemsSource = Loans;
        DuplicatesList.ItemsSource = Duplicates;
        UpgradesList.ItemsSource = Upgrades;
        GoalsList.ItemsSource = Goals;
    }

    private async void Page_Loaded(object sender, RoutedEventArgs e)
    {
        if (_hasLoaded) return;
        _hasLoaded = true;
        await LoadAsync();
    }

    private async void RefreshButton_Click(object sender, RoutedEventArgs e) => await LoadAsync();

    private async System.Threading.Tasks.Task LoadAsync()
    {
        LoadingRing.IsActive = true;
        StatusInfoBar.IsOpen = false;
        try
        {
            CollectionIntelligenceSummary summary = await _repository.GetSummaryAsync();
            OwnedTitlesText.Text = summary.UniqueOwnedMoviesDisplay;
            TotalCopiesText.Text = $"{summary.TotalCopiesDisplay} total copies";
            ReplacementValueText.Text = summary.EstimatedReplacementValueDisplay;
            DuplicateCountText.Text = summary.DuplicateTitleCountDisplay;
            UpgradeCountText.Text = summary.UpgradeOpportunityCountDisplay;
            ActiveLoansText.Text = summary.ActiveLoanCountDisplay;
            IncompleteGoalsText.Text = summary.IncompleteGoalCountDisplay;

            ReplaceItems(Loans, await _repository.GetLoansAsync());
            ReplaceItems(Duplicates, await _repository.GetDuplicatesAsync());
            ReplaceItems(Upgrades, await _repository.GetUpgradeOpportunitiesAsync());
            ReplaceItems(Goals, await _repository.GetIncompleteGoalsAsync());
            NoLoansText.Visibility = Loans.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
        }
        catch (Exception ex)
        {
            StatusInfoBar.Severity = InfoBarSeverity.Error;
            StatusInfoBar.Title = "Unable to load collection intelligence";
            StatusInfoBar.Message = ex.Message;
            StatusInfoBar.IsOpen = true;
        }
        finally
        {
            LoadingRing.IsActive = false;
        }
    }

    private async void AddLoanButton_Click(object sender, RoutedEventArgs e)
    {
        var copies = await _repository.GetAvailableCopiesAsync();
        if (copies.Count == 0)
        {
            ShowMessage("No available copies", "Every owned copy is already on loan, or no owned copies have been added yet.");
            return;
        }

        ComboBox copyBox = new() { ItemsSource = copies, DisplayMemberPath = nameof(OwnedCopyOption.DisplayName), SelectedIndex = 0, MinWidth = 430 };
        TextBox borrowerBox = new() { Header = "Borrower", PlaceholderText = "Name" };
        CalendarDatePicker loanedPicker = new() { Header = "Loaned date", Date = DateTimeOffset.Now };
        CalendarDatePicker duePicker = new() { Header = "Due date (optional)" };
        TextBox notesBox = new() { Header = "Notes", AcceptsReturn = true, MinHeight = 80 };
        StackPanel panel = new() { Spacing = 12 };
        panel.Children.Add(copyBox);
        panel.Children.Add(borrowerBox);
        panel.Children.Add(loanedPicker);
        panel.Children.Add(duePicker);
        panel.Children.Add(notesBox);

        ContentDialog dialog = new()
        {
            XamlRoot = XamlRoot,
            Title = "Add loan",
            Content = panel,
            PrimaryButtonText = "Save loan",
            CloseButtonText = "Cancel",
            DefaultButton = ContentDialogButton.Primary
        };

        if (await dialog.ShowAsync() != ContentDialogResult.Primary) return;
        if (copyBox.SelectedItem is not OwnedCopyOption copy || string.IsNullOrWhiteSpace(borrowerBox.Text))
        {
            ShowMessage("Loan not saved", "Choose a copy and enter the borrower's name.");
            return;
        }

        DateTime loanedDate = loanedPicker.Date?.DateTime ?? DateTime.Today;
        DateTime? dueDate = duePicker.Date?.DateTime;
        await _repository.AddLoanAsync(copy.OwnedCopyId, borrowerBox.Text, loanedDate, dueDate, notesBox.Text);
        await LoadAsync();
    }

    private async void MarkReturnedButton_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not Button { Tag: LoanRecord loan }) return;
        ContentDialog dialog = new()
        {
            XamlRoot = XamlRoot,
            Title = "Mark this copy returned?",
            Content = $"{loan.TitleDisplay}\nBorrowed by {loan.Borrower}",
            PrimaryButtonText = "Mark returned",
            CloseButtonText = "Cancel",
            DefaultButton = ContentDialogButton.Primary
        };
        if (await dialog.ShowAsync() == ContentDialogResult.Primary)
        {
            await _repository.MarkReturnedAsync(loan.Id, DateTime.Today);
            await LoadAsync();
        }
    }

    private async void ShowMessage(string title, string message)
    {
        ContentDialog dialog = new() { XamlRoot = XamlRoot, Title = title, Content = message, CloseButtonText = "OK" };
        await dialog.ShowAsync();
    }

    private static void ReplaceItems<T>(ObservableCollection<T> target, System.Collections.Generic.IEnumerable<T> source)
    {
        target.Clear();
        foreach (T item in source) target.Add(item);
    }
}
