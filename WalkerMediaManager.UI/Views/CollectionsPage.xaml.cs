using System;
using System.Collections.ObjectModel;
using System.Threading.Tasks;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using WalkerMediaManager.UI.Models;
using WalkerMediaManager.UI.Repositories;

namespace WalkerMediaManager.UI.Views;

public sealed partial class CollectionsPage : Page
{
    private readonly CollectionRepository _collectionRepository =
        new();

    private MediaCollection? _collectionBeingEdited;

    public ObservableCollection<MediaCollection> Collections { get; } =
        [];

    public CollectionsPage()
    {
        InitializeComponent();

        Loaded += CollectionsPage_Loaded;
    }

    private async void CollectionsPage_Loaded(
        object sender,
        RoutedEventArgs e)
    {
        await RefreshCollectionsAsync();
    }

    private async void SaveButton_Click(
        object sender,
        RoutedEventArgs e)
    {
        string name = NameBox.Text.Trim();

        if (string.IsNullOrWhiteSpace(name))
        {
            ShowStatus(
                "A collection name is required.",
                InfoBarSeverity.Warning);

            return;
        }

        if (!TryReadCount(
                OwnedCountBox.Text,
                "Titles owned",
                out int ownedCount))
        {
            return;
        }

        if (!TryReadCount(
                TargetCountBox.Text,
                "Total titles",
                out int targetCount))
        {
            return;
        }

        if (ownedCount > targetCount)
        {
            ShowStatus(
                "Titles owned cannot be greater than total titles.",
                InfoBarSeverity.Warning);

            return;
        }

        try
        {
            if (_collectionBeingEdited is null)
            {
                bool alreadyExists =
                    await _collectionRepository.ExistsAsync(name);

                if (alreadyExists)
                {
                    ShowStatus(
                        $"{name} already exists.",
                        InfoBarSeverity.Warning);

                    return;
                }

                MediaCollection collection = new()
                {
                    Name = name,
                    Description = DescriptionBox.Text.Trim(),
                    OwnedCount = ownedCount,
                    TargetCount = targetCount
                };

                collection.Id =
                    await _collectionRepository.AddAsync(collection);

                ShowStatus(
                    $"{collection.Name} was added.",
                    InfoBarSeverity.Success);
            }
            else
            {
                _collectionBeingEdited.Name = name;
                _collectionBeingEdited.Description =
                    DescriptionBox.Text.Trim();

                _collectionBeingEdited.OwnedCount = ownedCount;
                _collectionBeingEdited.TargetCount = targetCount;

                await _collectionRepository.UpdateAsync(
                    _collectionBeingEdited);

                ShowStatus(
                    $"{_collectionBeingEdited.Name} was updated.",
                    InfoBarSeverity.Success);
            }

            ResetForm();
            await RefreshCollectionsAsync();
        }
        catch (Exception exception)
        {
            ShowStatus(
                $"The collection could not be saved: {exception.Message}",
                InfoBarSeverity.Error);
        }
    }

    private void EditButton_Click(
        object sender,
        RoutedEventArgs e)
    {
        if (sender is not Button button ||
            button.Tag is not MediaCollection collection)
        {
            return;
        }

        _collectionBeingEdited = collection;

        FormTitleText.Text = "Edit Collection";
        SaveButton.Content = "Save Changes";
        CancelButton.Visibility = Visibility.Visible;

        NameBox.Text = collection.Name;
        DescriptionBox.Text = collection.Description;
        OwnedCountBox.Text = collection.OwnedCount.ToString();
        TargetCountBox.Text = collection.TargetCount.ToString();

        NameBox.Focus(FocusState.Programmatic);
    }

    private async void DeleteButton_Click(
        object sender,
        RoutedEventArgs e)
    {
        if (sender is not Button button ||
            button.Tag is not MediaCollection collection)
        {
            return;
        }

        ContentDialog dialog = new()
        {
            Title = "Delete collection?",
            Content =
                $"Delete the {collection.Name} collection?",
            PrimaryButtonText = "Delete",
            CloseButtonText = "Cancel",
            DefaultButton = ContentDialogButton.Close,
            XamlRoot = XamlRoot
        };

        ContentDialogResult result =
            await dialog.ShowAsync();

        if (result != ContentDialogResult.Primary)
        {
            return;
        }

        try
        {
            await _collectionRepository.DeleteAsync(
                collection.Id);

            if (_collectionBeingEdited?.Id == collection.Id)
            {
                ResetForm();
            }

            await RefreshCollectionsAsync();

            ShowStatus(
                $"{collection.Name} was deleted.",
                InfoBarSeverity.Success);
        }
        catch (Exception exception)
        {
            ShowStatus(
                $"The collection could not be deleted: {exception.Message}",
                InfoBarSeverity.Error);
        }
    }

    private void CancelButton_Click(
        object sender,
        RoutedEventArgs e)
    {
        ResetForm();
    }

    private bool TryReadCount(
        string text,
        string fieldName,
        out int value)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            value = 0;
            return true;
        }

        if (!int.TryParse(text.Trim(), out value) || value < 0)
        {
            ShowStatus(
                $"{fieldName} must be a non-negative whole number.",
                InfoBarSeverity.Warning);

            return false;
        }

        return true;
    }

    private async Task RefreshCollectionsAsync()
    {
        Collections.Clear();

        foreach (
            MediaCollection collection
            in await _collectionRepository.GetAllAsync())
        {
            Collections.Add(collection);
        }

        CollectionCountText.Text =
            Collections.Count == 1
                ? "1 collection"
                : $"{Collections.Count} collections";
    }

    private void ResetForm()
    {
        _collectionBeingEdited = null;

        FormTitleText.Text = "Add Collection";
        SaveButton.Content = "Add Collection";
        CancelButton.Visibility = Visibility.Collapsed;

        NameBox.Text = string.Empty;
        DescriptionBox.Text = string.Empty;
        OwnedCountBox.Text = string.Empty;
        TargetCountBox.Text = string.Empty;
    }

    private void ShowStatus(
        string message,
        InfoBarSeverity severity)
    {
        StatusInfoBar.Message = message;
        StatusInfoBar.Severity = severity;
        StatusInfoBar.IsOpen = true;
    }
}