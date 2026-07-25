using System;
using System.Collections.ObjectModel;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using WalkerMediaManager.UI.Models;
using WalkerMediaManager.UI.Repositories;

namespace WalkerMediaManager.UI.Views;

public sealed partial class ReportsPage : Page
{
    private readonly OwnershipReportRepository _repository = new();
    private bool _hasLoaded;

    public ObservableCollection<ReportBreakdownItem> FormatBreakdown { get; } = [];
    public ObservableCollection<ReportBreakdownItem> StoreBreakdown { get; } = [];
    public ObservableCollection<OwnedCopyReportRow> RecentPurchases { get; } = [];
    public ObservableCollection<OwnedCopyReportRow> MissingInformation { get; } = [];

    public ReportsPage()
    {
        InitializeComponent();
        FormatBreakdownList.ItemsSource = FormatBreakdown;
        StoreBreakdownList.ItemsSource = StoreBreakdown;
        RecentPurchasesList.ItemsSource = RecentPurchases;
        MissingInformationList.ItemsSource = MissingInformation;
    }

    private async void Page_Loaded(object sender, RoutedEventArgs e)
    {
        if (_hasLoaded)
            return;

        _hasLoaded = true;
        await LoadReportsAsync();
    }

    private async void RefreshButton_Click(object sender, RoutedEventArgs e)
    {
        await LoadReportsAsync();
    }

    private async System.Threading.Tasks.Task LoadReportsAsync()
    {
        LoadingRing.IsActive = true;
        StatusInfoBar.IsOpen = false;

        try
        {
            OwnershipReportSummary summary = await _repository.GetSummaryAsync();
            var formats = await _repository.GetFormatBreakdownAsync();
            var stores = await _repository.GetStoreBreakdownAsync();
            var recent = await _repository.GetRecentPurchasesAsync();
            var missing = await _repository.GetMissingInformationAsync();

            OwnedCopiesText.Text = summary.OwnedCopyCountDisplay;
            OwnedMoviesText.Text = $"{summary.MovieCountDisplay} movies represented";
            CollectionValueText.Text = summary.CollectionValueDisplay;
            AveragePriceText.Text = $"{summary.AveragePriceDisplay} average recorded price";
            PhysicalDigitalText.Text = $"{summary.PhysicalCountDisplay} / {summary.DigitalCountDisplay}";
            MissingPriceText.Text = summary.MissingPriceDisplay;
            MissingDateText.Text = summary.MissingDateDisplay;
            MissingLocationText.Text = summary.MissingLocationDisplay;
            NeedsAttentionText.Text = missing.Count.ToString("N0");

            ReplaceItems(FormatBreakdown, formats);
            ReplaceItems(StoreBreakdown, stores);
            ReplaceItems(RecentPurchases, recent);
            ReplaceItems(MissingInformation, missing);
        }
        catch (Exception ex)
        {
            StatusInfoBar.Severity = InfoBarSeverity.Error;
            StatusInfoBar.Title = "Unable to load reports";
            StatusInfoBar.Message = ex.Message;
            StatusInfoBar.IsOpen = true;
        }
        finally
        {
            LoadingRing.IsActive = false;
        }
    }

    private static void ReplaceItems<T>(ObservableCollection<T> target, System.Collections.Generic.IEnumerable<T> source)
    {
        target.Clear();
        foreach (T item in source)
            target.Add(item);
    }
}
