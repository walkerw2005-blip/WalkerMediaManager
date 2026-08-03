using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Navigation;
using WalkerMediaManager.UI.Models;
using WalkerMediaManager.UI.Repositories;
using WalkerMediaManager.UI.Services;

namespace WalkerMediaManager.UI.Views;

public sealed partial class WishlistPage : Page
{
    private readonly WishlistRepository _wishlistRepository = new();
    private readonly MovieRepository _movieRepository = new();
    private readonly OwnedCopyRepository _ownedCopyRepository = new();
    private readonly SmartBuyRepository _smartBuyRepository = new();
    private readonly List<WishlistItem> _allItems = [];

    private WishlistItem? _itemBeingEdited;
    private int? _requestedItemId;

    public ObservableCollection<WishlistItem> WishlistItems { get; } = [];

    public WishlistPage()
    {
        InitializeComponent();
        Loaded += WishlistPage_Loaded;
    }

    protected override void OnNavigatedTo(NavigationEventArgs e)
    {
        base.OnNavigatedTo(e);
        _requestedItemId = e.Parameter is int itemId && itemId > 0 ? itemId : null;
    }

    private async void WishlistPage_Loaded(object sender, RoutedEventArgs e)
    {
        await RefreshWishlistAsync();

        if (_requestedItemId is int itemId)
        {
            WishlistItem? requestedItem = await _wishlistRepository.GetByIdAsync(itemId);
            if (requestedItem is not null)
            {
                BeginEditing(requestedItem);
                WishlistListView.ScrollIntoView(WishlistItems.FirstOrDefault(item => item.Id == itemId));
            }

            _requestedItemId = null;
        }
    }

    private async void SaveButton_Click(object sender, RoutedEventArgs e)
    {
        string title = TitleBox.Text.Trim();
        if (string.IsNullOrWhiteSpace(title))
        {
            ShowStatus("A title is required.", InfoBarSeverity.Warning);
            return;
        }

        try
        {
            if (_itemBeingEdited is null)
            {
                WishlistItem item = new()
                {
                    Title = title,
                    PreferredFormat = GetSelectedFormat(),
                    Priority = GetSelectedPriority()
                };

                item.Id = await _wishlistRepository.AddAsync(item);
                ShowStatus($"{item.Title} was added to your wishlist.", InfoBarSeverity.Success);
            }
            else
            {
                _itemBeingEdited.Title = title;
                _itemBeingEdited.PreferredFormat = GetSelectedFormat();
                _itemBeingEdited.Priority = GetSelectedPriority();
                await _wishlistRepository.UpdateAsync(_itemBeingEdited);
                ShowStatus($"{_itemBeingEdited.Title} was updated.", InfoBarSeverity.Success);
            }

            ResetForm();
            await RefreshWishlistAsync();
        }
        catch (Exception exception)
        {
            ShowStatus($"The wishlist item could not be saved: {exception.Message}", InfoBarSeverity.Error);
        }
    }

    private void SearchBox_TextChanged(AutoSuggestBox sender, AutoSuggestBoxTextChangedEventArgs args)
    {
        if (args.Reason == AutoSuggestionBoxTextChangeReason.UserInput)
        {
            ApplySearch();
        }
    }

    private void WishlistListView_ItemClick(object sender, ItemClickEventArgs e)
    {
        if (e.ClickedItem is WishlistItem item)
        {
            BeginEditing(item);
        }
    }

    private async void MarkPurchasedButton_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not Button button || button.Tag is not WishlistItem item)
        {
            return;
        }

        ContentDialog confirmationDialog = new()
        {
            Title = "Mark as purchased?",
            Content = $"Add {item.Title} to your owned movies and remove it from the wishlist?",
            PrimaryButtonText = "Mark Purchased",
            CloseButtonText = "Cancel",
            DefaultButton = ContentDialogButton.Primary,
            XamlRoot = XamlRoot
        };

        if (await confirmationDialog.ShowAsync() != ContentDialogResult.Primary)
        {
            return;
        }

        try
        {
            bool alreadyOwned = await _smartBuyRepository.ExactMovieExistsAsync(item.Title, item.Year);
            if (alreadyOwned)
            {
                ShowStatus($"{item.Title} is already in your movie collection.", InfoBarSeverity.Warning);
                return;
            }

            Movie movie = new()
            {
                Title = item.Title,
                ReleaseYear = item.Year,
                Rating = string.Empty,
                Runtime = 0,
                Genre = string.Empty,
                Director = string.Empty,
                Format = string.IsNullOrWhiteSpace(item.PreferredFormat) ? "DVD" : item.PreferredFormat,
                Owned = true,
                PlexGuid = string.Empty,
                TMDbId = item.TMDbId,
                IMDbId = string.Empty
            };

            int movieId = await _movieRepository.AddAsync(movie);

            string purchasedFormat = string.IsNullOrWhiteSpace(item.PreferredFormat)
                ? "DVD"
                : item.PreferredFormat.Trim();

            await _ownedCopyRepository.AddAsync(new OwnedCopy
            {
                MovieId = movieId,
                Format = purchasedFormat,
                Store = item.PreferredStore,
                PurchasePrice = item.TargetPrice,
                Notes = item.Notes,
                IsDigital = !string.Equals(purchasedFormat, "Digital", StringComparison.OrdinalIgnoreCase),
                IsFavorite = true
            });

            await _wishlistRepository.DeleteAsync(item.Id);

            if (_itemBeingEdited?.Id == item.Id)
            {
                ResetForm();
            }

            await RefreshWishlistAsync();
            ShowStatus($"{item.Title} was moved to your movie collection.", InfoBarSeverity.Success);
        }
        catch (Exception exception)
        {
            ShowStatus($"The purchase could not be recorded: {exception.Message}", InfoBarSeverity.Error);
        }
    }

    private void EditButton_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button button && button.Tag is WishlistItem item)
        {
            BeginEditing(item);
        }
    }

    private void BeginEditing(WishlistItem item)
    {
        _itemBeingEdited = item;
        SaveButton.Content = "Save Changes";
        CancelButton.Visibility = Visibility.Visible;
        TitleBox.Text = item.Title;
        SelectFormat(item.PreferredFormat);
        PriorityComboBox.SelectedIndex = item.Priority switch
        {
            1 => 0,
            3 => 2,
            _ => 1
        };
        TitleBox.Focus(FocusState.Programmatic);
    }

    private async void RemoveButton_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not Button button || button.Tag is not WishlistItem item)
        {
            return;
        }

        ContentDialog dialog = new()
        {
            Title = "Remove wishlist item?",
            Content = $"Remove {item.Title} from your wishlist?",
            PrimaryButtonText = "Remove",
            CloseButtonText = "Cancel",
            DefaultButton = ContentDialogButton.Close,
            XamlRoot = XamlRoot
        };

        if (await dialog.ShowAsync() != ContentDialogResult.Primary)
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
            ShowStatus($"{item.Title} was removed from your wishlist.", InfoBarSeverity.Success);
        }
        catch (Exception exception)
        {
            ShowStatus($"The wishlist item could not be removed: {exception.Message}", InfoBarSeverity.Error);
        }
    }

    private void CancelButton_Click(object sender, RoutedEventArgs e) => ResetForm();

    private string GetSelectedFormat()
    {
        if (FormatComboBox.SelectedItem is ComboBoxItem item)
        {
            return item.Tag?.ToString()?.Trim() ?? item.Content?.ToString()?.Trim() ?? "DVD";
        }

        return "DVD";
    }

    private void SelectFormat(string format)
    {
        string desired = string.IsNullOrWhiteSpace(format) ? "DVD" : format.Trim();
        for (int index = 0; index < FormatComboBox.Items.Count; index++)
        {
            if (FormatComboBox.Items[index] is ComboBoxItem item &&
                string.Equals(item.Tag?.ToString(), desired, StringComparison.OrdinalIgnoreCase))
            {
                FormatComboBox.SelectedIndex = index;
                return;
            }
        }

        FormatComboBox.SelectedIndex = 4;
    }

    private int GetSelectedPriority()
    {
        if (PriorityComboBox.SelectedItem is ComboBoxItem item &&
            int.TryParse(item.Tag?.ToString(), out int priority))
        {
            return priority;
        }

        return 2;
    }

    private async Task RefreshWishlistAsync()
    {
        _allItems.Clear();
        _allItems.AddRange(await _wishlistRepository.GetAllAsync());
        ApplySearch();
    }

    private void ApplySearch()
    {
        string searchText = SearchBox.Text.Trim();
        string normalizedSearch = MediaIdentityService.NormalizeTitle(searchText);

        IEnumerable<WishlistItem> filtered = _allItems;
        if (!string.IsNullOrWhiteSpace(searchText))
        {
            filtered = filtered.Where(item =>
                MediaIdentityService.NormalizeTitle(item.Title).Contains(normalizedSearch, StringComparison.Ordinal) ||
                (item.Year > 0 && item.Year.ToString().Contains(searchText, StringComparison.OrdinalIgnoreCase)) ||
                item.PreferredFormat.Contains(searchText, StringComparison.OrdinalIgnoreCase));
        }

        WishlistItems.Clear();
        foreach (WishlistItem item in filtered)
        {
            WishlistItems.Add(item);
        }

        WishlistCountText.Text = WishlistItems.Count == 1
            ? "1 wishlist item"
            : $"{WishlistItems.Count} wishlist items";

        bool hasItems = WishlistItems.Count > 0;
        WishlistListView.Visibility = hasItems ? Visibility.Visible : Visibility.Collapsed;
        EmptyStatePanel.Visibility = hasItems ? Visibility.Collapsed : Visibility.Visible;
        EmptyStateMessageText.Text = _allItems.Count == 0
            ? "Add movies or TV shows using the form above."
            : "No wishlist items match your search.";
    }

    private void ResetForm()
    {
        _itemBeingEdited = null;
        SaveButton.Content = "Add to Wishlist";
        CancelButton.Visibility = Visibility.Collapsed;
        TitleBox.Text = string.Empty;
        FormatComboBox.SelectedIndex = 0;
        PriorityComboBox.SelectedIndex = 1;
    }

    private void ShowStatus(string message, InfoBarSeverity severity)
    {
        StatusInfoBar.Message = message;
        StatusInfoBar.Severity = severity;
        StatusInfoBar.IsOpen = true;
    }
}
