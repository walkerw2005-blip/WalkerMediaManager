using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using WalkerMediaManager.UI.Models;
using WalkerMediaManager.UI.Repositories;

namespace WalkerMediaManager.UI.Views;

public sealed partial class StorageLocationsPage : Page
{
    private readonly StorageLocationRepository _repository = new();
    private List<StorageLocation> _locations = [];
    private StorageLocation? _editingLocation;

    public StorageLocationsPage()
    {
        InitializeComponent();
        Loaded += StorageLocationsPage_Loaded;
    }

    private async void StorageLocationsPage_Loaded(object sender, RoutedEventArgs e)
    {
        await LoadLocationsAsync();
    }

    private async Task LoadLocationsAsync()
    {
        LoadingRing.IsActive = true;
        try
        {
            _locations = await _repository.GetAllAsync(ShowInactiveCheckBox.IsChecked == true);
            ApplyFilter();
        }
        catch (Exception exception)
        {
            ShowStatus($"Storage locations could not be loaded: {exception.Message}", InfoBarSeverity.Error);
        }
        finally
        {
            LoadingRing.IsActive = false;
        }
    }

    private void ApplyFilter()
    {
        string search = SearchTextBox.Text.Trim();
        IEnumerable<StorageLocation> filtered = _locations;

        if (!string.IsNullOrWhiteSpace(search))
        {
            filtered = filtered.Where(location =>
                Contains(location.Name, search) ||
                Contains(location.Room, search) ||
                Contains(location.Area, search) ||
                Contains(location.Shelf, search) ||
                Contains(location.Bin, search) ||
                Contains(location.Notes, search));
        }

        List<StorageLocation> results = filtered.ToList();
        LocationsList.ItemsSource = results;
        EmptyText.Visibility = results.Count == 0 ? Visibility.Visible : Visibility.Collapsed;

        int activeCount = _locations.Count(location => location.IsActive);
        int copyCount = _locations.Sum(location => location.CopyCount);
        SummaryText.Text = $"{results.Count} {(results.Count == 1 ? "location" : "locations")} • " +
                           $"{activeCount} active • {copyCount} {(copyCount == 1 ? "copy" : "copies")} assigned";
    }

    private async void AddLocationButton_Click(object sender, RoutedEventArgs e)
    {
        _editingLocation = null;
        ClearDialog();
        LocationDialog.Title = "Add storage location";
        LocationDialog.XamlRoot = XamlRoot;
        await LocationDialog.ShowAsync();
    }

    private async void EditLocationButton_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not Button button ||
            !int.TryParse(button.Tag?.ToString(), out int locationId))
        {
            return;
        }

        _editingLocation = _locations.FirstOrDefault(location => location.Id == locationId);
        if (_editingLocation is null)
        {
            return;
        }

        FillDialog(_editingLocation);
        LocationDialog.Title = "Edit storage location";
        LocationDialog.XamlRoot = XamlRoot;
        await LocationDialog.ShowAsync();
    }

    private async void DeleteLocationButton_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not Button button ||
            !int.TryParse(button.Tag?.ToString(), out int locationId))
        {
            return;
        }

        StorageLocation? location = _locations.FirstOrDefault(item => item.Id == locationId);
        if (location is null)
        {
            return;
        }

        ContentDialog confirmation = new()
        {
            Title = "Delete storage location?",
            Content = location.CopyCount > 0
                ? $"{location.DisplayName} is used by {location.CopyCount} owned copies. The copy records will keep their location text, but this managed location will be removed."
                : $"Delete {location.DisplayName}?",
            PrimaryButtonText = "Delete",
            CloseButtonText = "Cancel",
            DefaultButton = ContentDialogButton.Close,
            XamlRoot = XamlRoot
        };

        if (await confirmation.ShowAsync() != ContentDialogResult.Primary)
        {
            return;
        }

        try
        {
            await _repository.DeleteAsync(locationId);
            await LoadLocationsAsync();
            ShowStatus("Storage location deleted.", InfoBarSeverity.Success);
        }
        catch (Exception exception)
        {
            ShowStatus($"The storage location could not be deleted: {exception.Message}", InfoBarSeverity.Error);
        }
    }

    private async void ImportExistingButton_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            int imported = await _repository.ImportExistingLocationsAsync();
            await LoadLocationsAsync();
            ShowStatus(imported == 0
                ? "No new locations were found in existing ownership records."
                : $"Imported {imported} {(imported == 1 ? "location" : "locations")} from existing ownership records.",
                InfoBarSeverity.Success);
        }
        catch (Exception exception)
        {
            ShowStatus($"Existing locations could not be imported: {exception.Message}", InfoBarSeverity.Error);
        }
    }

    private async void LocationDialog_PrimaryButtonClick(ContentDialog sender, ContentDialogButtonClickEventArgs args)
    {
        string name = NameTextBox.Text.Trim();
        if (string.IsNullOrWhiteSpace(name))
        {
            args.Cancel = true;
            DialogInfoBar.Title = "Location name required";
            DialogInfoBar.Message = "Enter a short, unique name such as Living Room Shelf 2.";
            DialogInfoBar.Severity = InfoBarSeverity.Warning;
            DialogInfoBar.IsOpen = true;
            return;
        }

        if (_locations.Any(location =>
                location.Id != (_editingLocation?.Id ?? 0) &&
                string.Equals(location.Name.Trim(), name, StringComparison.OrdinalIgnoreCase)))
        {
            args.Cancel = true;
            DialogInfoBar.Title = "Duplicate location";
            DialogInfoBar.Message = "A storage location with this name already exists.";
            DialogInfoBar.Severity = InfoBarSeverity.Warning;
            DialogInfoBar.IsOpen = true;
            return;
        }

        ContentDialogButtonClickDeferral deferral = args.GetDeferral();
        try
        {
            StorageLocation location = _editingLocation ?? new StorageLocation();
            location.Name = name;
            location.Room = RoomTextBox.Text.Trim();
            location.Area = AreaTextBox.Text.Trim();
            location.Shelf = ShelfTextBox.Text.Trim();
            location.Bin = BinTextBox.Text.Trim();
            location.Notes = NotesTextBox.Text.Trim();
            location.IsActive = ActiveCheckBox.IsChecked == true;

            if (location.Id == 0)
            {
                location.Id = await _repository.AddAsync(location);
            }
            else
            {
                await _repository.UpdateAsync(location);
            }

            await LoadLocationsAsync();
            ShowStatus("Storage location saved.", InfoBarSeverity.Success);
        }
        catch (Exception exception)
        {
            args.Cancel = true;
            DialogInfoBar.Title = "Unable to save location";
            DialogInfoBar.Message = exception.Message;
            DialogInfoBar.Severity = InfoBarSeverity.Error;
            DialogInfoBar.IsOpen = true;
        }
        finally
        {
            deferral.Complete();
        }
    }

    private async void RefreshButton_Click(object sender, RoutedEventArgs e)
    {
        await LoadLocationsAsync();
    }

    private async void ShowInactiveCheckBox_Click(object sender, RoutedEventArgs e)
    {
        await LoadLocationsAsync();
    }

    private void SearchTextBox_TextChanged(object sender, TextChangedEventArgs e)
    {
        ApplyFilter();
    }

    private void ClearDialog()
    {
        NameTextBox.Text = string.Empty;
        RoomTextBox.Text = string.Empty;
        AreaTextBox.Text = string.Empty;
        ShelfTextBox.Text = string.Empty;
        BinTextBox.Text = string.Empty;
        NotesTextBox.Text = string.Empty;
        ActiveCheckBox.IsChecked = true;
        DialogInfoBar.IsOpen = false;
    }

    private void FillDialog(StorageLocation location)
    {
        ClearDialog();
        NameTextBox.Text = location.Name;
        RoomTextBox.Text = location.Room;
        AreaTextBox.Text = location.Area;
        ShelfTextBox.Text = location.Shelf;
        BinTextBox.Text = location.Bin;
        NotesTextBox.Text = location.Notes;
        ActiveCheckBox.IsChecked = location.IsActive;
    }

    private void ShowStatus(string message, InfoBarSeverity severity)
    {
        StatusInfoBar.Title = "Storage locations";
        StatusInfoBar.Message = message;
        StatusInfoBar.Severity = severity;
        StatusInfoBar.IsOpen = true;
    }

    private static bool Contains(string value, string search) =>
        !string.IsNullOrWhiteSpace(value) &&
        value.Contains(search, StringComparison.OrdinalIgnoreCase);
}
