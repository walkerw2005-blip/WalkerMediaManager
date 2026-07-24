using System;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using WalkerMediaManager.UI.Models;

namespace WalkerMediaManager.UI.Controls;

public sealed partial class MediaCard : UserControl
{
    public static readonly DependencyProperty MovieProperty =
        DependencyProperty.Register(
            nameof(Movie),
            typeof(Movie),
            typeof(MediaCard),
            new PropertyMetadata(new Movie()));

    public Movie Movie
    {
        get => (Movie)GetValue(MovieProperty);
        set => SetValue(MovieProperty, value);
    }

    public event EventHandler<Movie>? OpenRequested;
    public event EventHandler<Movie>? EditRequested;
    public event EventHandler<Movie>? DeleteRequested;

    public MediaCard()
    {
        InitializeComponent();
    }

    private void OpenButton_Click(object sender, RoutedEventArgs e) =>
        OpenRequested?.Invoke(this, Movie);

    private void EditButton_Click(object sender, RoutedEventArgs e) =>
        EditRequested?.Invoke(this, Movie);

    private void DeleteButton_Click(object sender, RoutedEventArgs e) =>
        DeleteRequested?.Invoke(this, Movie);
}
