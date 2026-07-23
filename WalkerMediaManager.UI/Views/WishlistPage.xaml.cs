using System;
using System.Collections.ObjectModel;
using System.Threading.Tasks;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using WalkerMediaManager.UI.Models;
using WalkerMediaManager.UI.Repositories;

namespace WalkerMediaManager.UI.Views;

public sealed partial class WishlistPage : Page
{
    private readonly WishlistRepository _wishlistRepository = new();

    private WishlistItem? _itemBeingEdited;

    public ObservableCollection<WishlistItem> WishlistItems { get; } =
        [];

    public WishlistPage()
    {
        InitializeComponent();

        Loaded += WishlistPage_Loaded;
    }

    private async void WishlistPage_Loaded(
        object sender,
        RoutedEventArgs e)
    {
        await RefreshWishlistAsync();
    }

    private async void SaveButton_Click(
        object sender,
        RoutedEventArgs e)
    {
        string title = TitleBox.Text.Trim();

        if (string.IsNullOrWhiteSpace(title))
        {
            ShowStatus(
                "A title is required.",
                InfoBarSeverity.Warning);

            return;
        }

        int priority = GetSelectedPriority();

        try
        {
            if (_itemBeingEdited is null)
            {
                bool alreadyExists =
                    await _wishlistRepository.ExistsAsync(title);

                if (alreadyExists)
                {
                    ShowStatus(
                        $"{title} is already on your wishlist.",
                        InfoBarSeverity.Warning);

                    return;
                }

                WishlistItem item = new()
                {
                    Title = title,
                    Priority = priority
                };

                item.Id =
                    await _wishlistRepository.AddAsync(item);

                ShowStatus(
                    $"{item.Title} was added to your wishlist.",
                    InfoBarSeverity.Success);
            }
            else
            {
                _itemBeingEdited.Title = title;
                _itemBeingEdited.Priority = priority;

                await _wishlistRepository.UpdateAsync(
                    _itemBeingEdited);

                ShowStatus(
                    $"{_itemBeingEdited.Title} was updated.",
                    InfoBarSeverity.Success);
            }

            ResetForm();

            await RefreshWishlistAsync();
        }
        catch (Exception exception)
        {
            ShowStatus(
                $"The wishlist item could not be saved: {exception.Message}",
                InfoBarSeverity.Error);
        }
    }

    private void EditButton_Click(
        object sender,
        RoutedEventArgs e)
    {
        if (sender is not Button button ||
            button.Tag is not WishlistItem item)
        {
            return;
        }

        _itemBeingEdited = item;

        FormTitleText.Text = "Edit Wishlist Item";
        SaveButton.Content = "Save Changes";
        CancelButton.Visibility = Visibility.Visible;

        TitleBox.Text = item.Title;

        PriorityComboBox.SelectedIndex =
            item.Priority switch
            {
                1 => 0,
                3 => 2,
                _ => 1
            };

        TitleBox.Focus(FocusState.Programmatic);
    }

    private async void RemoveButton_Click(
        object sender,
        RoutedEventArgs e)
    {
        if (sender is not Button button ||
            button.Tag is not WishlistItem item)
        {
            return;
        }

        ContentDialog dialog = new()
        {
            Title = "Remove wishlist item?",
            Content =
                $"Remove {item.Title} from your wishlist?",
            PrimaryButtonText = "Remove",
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
            await _wishlistRepository.DeleteAsync(item.Id);

            if (_itemBeingEdited?.Id == item.Id)
            {
                ResetForm();
            }

            await RefreshWishlistAsync();

            ShowStatus(
                $"{item.Title} was removed from your wishlist.",
                InfoBarSeverity.Success);
        }
        catch (Exception exception)
        {
            ShowStatus(
                $"The wishlist item could not be removed: {exception.Message}",
                InfoBarSeverity.Error);
        }
    }

    private void CancelButton_Click(
        object sender,
        RoutedEventArgs e)
    {
        ResetForm();
    }

    private int GetSelectedPriority()
    {
        if (PriorityComboBox.SelectedItem is not ComboBoxItem item)
        {
            return 2;
        }

        return int.TryParse(
            item.Tag?.ToString(),
            out int priority)
                ? priority
                : 2;
    }

    private async Task RefreshWishlistAsync()
    {
        WishlistItems.Clear();

        foreach (
            WishlistItem item
            in await _wishlistRepository.GetAllAsync())
        {
            WishlistItems.Add(item);
        }

        WishlistCountText.Text =
            WishlistItems.Count == 1
                ? "1 wishlist item"
                : $"{WishlistItems.Count} wishlist items";
    }

    private void ResetForm()
    {
        _itemBeingEdited = null;

        FormTitleText.Text = "Add Wishlist Item";
        SaveButton.Content = "Add to Wishlist";
        CancelButton.Visibility = Visibility.Collapsed;

        TitleBox.Text = string.Empty;
        PriorityComboBox.SelectedIndex = 1;
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