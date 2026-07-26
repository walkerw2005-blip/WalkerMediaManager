using Microsoft.UI.Xaml;
using System;
using WalkerMediaManager.UI.Data;
using WalkerMediaManager.UI.Services;

namespace WalkerMediaManager.UI
{
    /// <summary>
    /// Provides application-specific behavior to supplement the default Application class.
    /// </summary>
    public partial class App : Application
    {
        private Window? _window;

        public App()
        {
            InitializeComponent();
            UnhandledException += App_UnhandledException;

            try
            {
                DiagnosticsService.Log("Application starting.");
                DatabaseService.Initialize();
                DiagnosticsService.Log($"Database initialized successfully: {DatabaseService.DatabasePath}");
            }
            catch (Exception exception)
            {
                DiagnosticsService.LogException("Database initialization failed.", exception);
                throw;
            }
        }

        private void App_UnhandledException(object sender, Microsoft.UI.Xaml.UnhandledExceptionEventArgs e)
        {
            DiagnosticsService.LogException("Unhandled application exception.", e.Exception);
        }

        protected override void OnLaunched(LaunchActivatedEventArgs args)
        {
            _window = new MainWindow();
            _window.Activate();
        }
    }
}
