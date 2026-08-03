using Microsoft.UI.Xaml;
using System;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using WalkerMediaManager.UI.Data;
using WalkerMediaManager.UI.Services;

namespace WalkerMediaManager.UI;

public partial class App : Application
{
    private Window? _window;
    private int _fatalErrorInProgress;

    public App()
    {
        InitializeComponent();
        UnhandledException += App_UnhandledException;
        AppDomain.CurrentDomain.UnhandledException += CurrentDomain_UnhandledException;
        TaskScheduler.UnobservedTaskException += TaskScheduler_UnobservedTaskException;
    }

    protected override void OnLaunched(LaunchActivatedEventArgs args)
    {
        try
        {
            DiagnosticsService.StartSession();
            DiagnosticsService.Log("Application launch started.");

            ApplicationPaths.EnsureDataFolderExists();
            DiagnosticsService.Log("Application data folders verified.");

            DatabaseService.Initialize();
            DiagnosticsService.Log($"Database initialized: {DatabaseService.DatabasePath}");

            _window = new MainWindow();
            _window.Closed += MainWindow_Closed;
            _window.Activate();
            DiagnosticsService.Log("Main window activated.");
        }
        catch (Exception exception)
        {
            HandleFatalError("Fatal startup failure.", exception);
        }
    }

    private void App_UnhandledException(
        object sender,
        Microsoft.UI.Xaml.UnhandledExceptionEventArgs e)
    {
        // Mark handled long enough to log and show a useful message. The app is then
        // closed because continuing after an unknown UI exception can corrupt state.
        e.Handled = true;
        HandleFatalError("Unhandled WinUI exception.", e.Exception);
    }

    private static void CurrentDomain_UnhandledException(
        object? sender,
        System.UnhandledExceptionEventArgs e)
    {
        if (e.ExceptionObject is Exception exception)
        {
            DiagnosticsService.LogException(
                $"Unhandled AppDomain exception. IsTerminating={e.IsTerminating}.",
                exception);
        }
        else
        {
            DiagnosticsService.Log(
                $"Unhandled AppDomain exception. IsTerminating={e.IsTerminating}: " +
                e.ExceptionObject);
        }
    }

    private static void TaskScheduler_UnobservedTaskException(
        object? sender,
        UnobservedTaskExceptionEventArgs e)
    {
        DiagnosticsService.LogException("Unobserved task exception.", e.Exception);
        e.SetObserved();
    }

    private void MainWindow_Closed(object sender, WindowEventArgs args)
    {
        DiagnosticsService.EndSession();
    }

    private void HandleFatalError(string context, Exception exception)
    {
        if (Interlocked.Exchange(ref _fatalErrorInProgress, 1) != 0)
        {
            return;
        }

        DiagnosticsService.LogException(context, exception);
        ShowFatalStartupMessage(exception);

        try
        {
            _window?.Close();
        }
        catch (Exception closeException)
        {
            DiagnosticsService.LogException(
                "The application window could not be closed after a fatal error.",
                closeException);
        }

        Environment.Exit(1);
    }

    private static void ShowFatalStartupMessage(Exception exception)
    {
        string message =
            "Walker Media Manager encountered a serious error and must close.\n\n" +
            exception.Message +
            "\n\nA diagnostic log was written to:\n" +
            DiagnosticsService.LogFilePath;

        MessageBox(IntPtr.Zero, message, "Walker Media Manager", 0x00000010);
    }

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern int MessageBox(
        IntPtr hWnd,
        string text,
        string caption,
        uint type);
}
