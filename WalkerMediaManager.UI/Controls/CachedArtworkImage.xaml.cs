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
    private const string DefaultFallbackGlyph = "\uE714";
    private const string DefaultFallbackText = "No Poster Available";

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

    public static readonly DependencyProperty FallbackGlyphProperty = DependencyProperty.Register(
        nameof(FallbackGlyph),
        typeof(string),
        typeof(CachedArtworkImage),
        new PropertyMetadata(DefaultFallbackGlyph, OnFallbackChanged));

    public static readonly DependencyProperty FallbackTextProperty = DependencyProperty.Register(
        nameof(FallbackText),
        typeof(string),
        typeof(CachedArtworkImage),
        new PropertyMetadata(DefaultFallbackText, OnFallbackChanged));

    public static readonly DependencyProperty FallbackDescriptionProperty = DependencyProperty.Register(
        nameof(FallbackDescription),
        typeof(string),
        typeof(CachedArtworkImage),
        new PropertyMetadata(string.Empty, OnFallbackChanged));

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

    public string FallbackGlyph
    {
        get => (string)GetValue(FallbackGlyphProperty);
        set => SetValue(FallbackGlyphProperty, value);
    }

    public string FallbackText
    {
        get => (string)GetValue(FallbackTextProperty);
        set => SetValue(FallbackTextProperty, value);
    }

    public string FallbackDescription
    {
        get => (string)GetValue(FallbackDescriptionProperty);
        set => SetValue(FallbackDescriptionProperty, value);
    }

    public CachedArtworkImage()
    {
        InitializeComponent();
        UpdateFallbackContent();
        Unloaded += (_, _) => _loadCts?.Cancel();
    }

    private static void OnArtworkChanged(
        DependencyObject dependencyObject,
        DependencyPropertyChangedEventArgs eventArgs)
    {
        _ = ((CachedArtworkImage)dependencyObject).LoadArtworkAsync();
    }

    private static void OnFallbackChanged(
        DependencyObject dependencyObject,
        DependencyPropertyChangedEventArgs eventArgs)
    {
        ((CachedArtworkImage)dependencyObject).UpdateFallbackContent();
    }

    private void UpdateFallbackContent()
    {
        if (FallbackIcon is null ||
            FallbackTextBlock is null ||
            FallbackDescriptionBlock is null)
        {
            return;
        }

        FallbackIcon.Glyph = string.IsNullOrWhiteSpace(FallbackGlyph)
            ? DefaultFallbackGlyph
            : FallbackGlyph;

        FallbackTextBlock.Text = string.IsNullOrWhiteSpace(FallbackText)
            ? DefaultFallbackText
            : FallbackText;

        string description = FallbackDescription?.Trim() ?? string.Empty;
        FallbackDescriptionBlock.Text = description;
        FallbackDescriptionBlock.Visibility = string.IsNullOrWhiteSpace(description)
            ? Visibility.Collapsed
            : Visibility.Visible;
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
            StorageFile? file = await ArtworkService.Current.GetArtworkFileAsync(
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
        catch (Exception exception)
        {
            Debug.WriteLine(
                $"Artwork load failed. CacheKey='{CacheKey}', ArtworkPath='{ArtworkPath}'. {exception}");
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
