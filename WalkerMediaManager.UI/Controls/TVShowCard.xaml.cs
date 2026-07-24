using System;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using WalkerMediaManager.UI.Models;

namespace WalkerMediaManager.UI.Controls;

public sealed partial class TVShowCard : UserControl
{
    public static readonly DependencyProperty ShowProperty =
        DependencyProperty.Register(
            nameof(Show),
            typeof(TVShow),
            typeof(TVShowCard),
            new PropertyMetadata(new TVShow()));

    public TVShow Show
    {
        get => (TVShow)GetValue(ShowProperty);
        set => SetValue(ShowProperty, value);
    }

    public event EventHandler<TVShow>? OpenRequested;
    public event EventHandler<TVShow>? EditRequested;
    public event EventHandler<TVShow>? DeleteRequested;

    public TVShowCard()
    {
        InitializeComponent();
    }

    private void OpenButton_Click(object sender, RoutedEventArgs e) =>
        OpenRequested?.Invoke(this, Show);

    private void EditButton_Click(object sender, RoutedEventArgs e) =>
        EditRequested?.Invoke(this, Show);

    private void DeleteButton_Click(object sender, RoutedEventArgs e) =>
        DeleteRequested?.Invoke(this, Show);
}
