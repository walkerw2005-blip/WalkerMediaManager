using Microsoft.UI.Xaml;
using WalkerMediaManager.Data;
using WalkerMediaManager.Services;

namespace WalkerMediaManager;

public partial class App : Application
{
    public static SettingsService SettingsService { get; } = new();
    public static DatabaseService DatabaseService { get; } = new();
    public static PlexService PlexService { get; } = new();
    public static UpdateService UpdateService { get; } = new();
    public static CredentialService CredentialService { get; } = new();

    private Window? _window;

    public App()
    {
        InitializeComponent();
        UnhandledException += (_, args) =>
        {
            LogService.Write($"Unhandled error: {args.Exception}");
            args.Handled = true;
        };
    }

    protected override async void OnLaunched(LaunchActivatedEventArgs args)
    {
        try
        {
            await DatabaseService.InitializeAsync();
            _window = new MainWindow();
            _window.Activate();
        }
        catch (Exception ex)
        {
            LogService.Write($"Startup failure: {ex}");
            throw;
        }
    }
}
