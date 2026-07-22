using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Navigation;
using WalkerMediaManager.UI.Repositories;

namespace WalkerMediaManager.UI.Views;

public sealed partial class DashboardPage : Page
{
    private readonly MovieRepository _movieRepository = new();

    public DashboardPage()
    {
        InitializeComponent();
    }

    protected override async void OnNavigatedTo(
        NavigationEventArgs e)
    {
        base.OnNavigatedTo(e);

        int movieCount = await _movieRepository.CountAsync();

        MovieCountText.Text = movieCount.ToString();

        OwnedTitleCountText.Text =
            (movieCount + 57).ToString();
    }

    private void OpenSmartBuy_Click(
        object sender,
        RoutedEventArgs e)
    {
        Frame.Navigate(typeof(SmartBuyPage));
    }
}