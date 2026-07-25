using System;
using System.Diagnostics;
using System.Threading;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media.Imaging;
using WalkerMediaManager.UI.Services;
using Windows.Storage;
using Windows.Storage.Streams;

namespace WalkerMediaManager.UI.Controls;

public sealed partial class CachedArtworkImage : UserControl
{
    private CancellationTokenSource? _loadCts;

    public static readonly DependencyProperty ArtworkPathProperty = DependencyProperty.Register(
        nameof(ArtworkPath),
        typeof(string),
        typeof(CachedArtworkImage),
        new PropertyMetadata(string.Empty, OnArtworkChanged));

    public static readonly DependencyProperty CacheKeyProperty = DependencyProperty.Register(
        nameof(CacheKey),
        typeof(string),
        typeof(CachedArtworkImage),
        new PropertyMetadata(string.Empty, OnArtworkChanged));

    public string ArtworkPath
    {
        get => (string)GetValue(ArtworkPathProperty);
        set => SetValue(ArtworkPathProperty, value);
    }

    public string CacheKey
    {
        get => (string)GetValue(CacheKeyProperty);
        set => SetValue(CacheKeyProperty, value);
    }

    public CachedArtworkImage()
    {
        InitializeComponent();
        Unloaded += (_, _) => _loadCts?.Cancel();
    }

    private static void OnArtworkChanged(
        DependencyObject dependencyObject,
        DependencyPropertyChangedEventArgs eventArgs)
    {
        _ = ((CachedArtworkImage)dependencyObject).LoadArtworkAsync();
    }

    private async System.Threading.Tasks.Task LoadArtworkAsync()
    {
        _loadCts?.Cancel();
        _loadCts?.Dispose();
        _loadCts = new CancellationTokenSource();
        CancellationToken token = _loadCts.Token;

        ArtworkImage.Source = null;
        ArtworkImage.Visibility = Visibility.Collapsed;
        FallbackPanel.Visibility = Visibility.Visible;
        LoadingRing.IsActive = !string.IsNullOrWhiteSpace(ArtworkPath);
        LoadingRing.Visibility = LoadingRing.IsActive
            ? Visibility.Visible
            : Visibility.Collapsed;

        try
        {
            StorageFile? file = await ArtworkCacheService.Current.GetArtworkFileAsync(
                ArtworkPath,
                CacheKey,
                token);

            if (file is null || token.IsCancellationRequested)
            {
                return;
            }

            using IRandomAccessStream stream = await file.OpenAsync(FileAccessMode.Read);
            BitmapImage bitmap = new();
            await bitmap.SetSourceAsync(stream);

            if (token.IsCancellationRequested)
            {
                return;
            }

            ArtworkImage.Source = bitmap;
            ArtworkImage.Visibility = Visibility.Visible;
            FallbackPanel.Visibility = Visibility.Collapsed;
        }
        catch (OperationCanceledException)
        {
            // Expected when a recycled card begins loading different artwork.
        }
        catch (Exception ex)
        {
            Debug.WriteLine(
                $"Artwork load failed. CacheKey='{CacheKey}', ArtworkPath='{ArtworkPath}'. {ex}");
        }
        finally
        {
            if (!token.IsCancellationRequested)
            {
                LoadingRing.IsActive = false;
                LoadingRing.Visibility = Visibility.Collapsed;
            }
        }
    }
}
