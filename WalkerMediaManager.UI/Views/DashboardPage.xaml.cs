using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace WalkerMediaManager.UI.Views;

public sealed partial class DashboardPage : Page
{
    public DashboardPage()
    {
        InitializeComponent();
    }

    private void OpenSmartBuy_Click(object sender, RoutedEventArgs e)
    {
        Frame.Navigate(typeof(SmartBuyPage));
    }
}