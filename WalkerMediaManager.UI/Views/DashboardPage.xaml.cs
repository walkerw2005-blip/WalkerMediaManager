using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Navigation;
using WalkerMediaManager.UI.Repositories;

namespace WalkerMediaManager.UI.Views;

public sealed partial class DashboardPage : Page
{
    private readonly MovieRepository _movieRepository = new();
    private readonly TVShowRepository _tvShowRepository = new();
    private readonly WishlistRepository _wishlistRepository = new();

    public DashboardPage()
    {
        InitializeComponent();
    }

    protected override async void OnNavigatedTo(
        NavigationEventArgs e)
    {
        base.OnNavigatedTo(e);

        int movieCount = await _movieRepository.CountAsync();
        int tvShowCount = await _tvShowRepository.CountAsync();
        int wishlistCount = await _wishlistRepository.CountAsync();

        MovieCountText.Text = movieCount.ToString();
        TvSeriesCountText.Text = tvShowCount.ToString();
        OwnedTitleCountText.Text =
            (movieCount + tvShowCount).ToString();

        WishlistCountText.Text = wishlistCount.ToString();
    }

    private void OpenSmartBuy_Click(
        object sender,
        RoutedEventArgs e)
    {
        Frame.Navigate(typeof(SmartBuyPage));
    }
}