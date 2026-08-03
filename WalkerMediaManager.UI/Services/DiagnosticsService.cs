using System;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;

namespace WalkerMediaManager.UI.Services;

public static class DiagnosticsService
{
    private const long MaximumLogSizeBytes = 5 * 1024 * 1024;
    private static readonly object SyncRoot = new();
    private static bool _sessionStarted;

    public static string LogFolderPath => ApplicationPaths.LogFolder;

    public static string LogFilePath =>
        Path.Combine(LogFolderPath, "walker-media-manager.log");

    public static void StartSession()
    {
        lock (SyncRoot)
        {
            if (_sessionStarted)
            {
                return;
            }

            _sessionStarted = true;
            RotateLogIfNeeded();

            WriteLine("============================================================");
            WriteLine("Walker Media Manager session started.");
            WriteLine($"Version: {GetApplicationVersion()}");
            WriteLine($"Process architecture: {RuntimeInformation.ProcessArchitecture}");
            WriteLine($"OS: {RuntimeInformation.OSDescription}");
            WriteLine($"Base directory: {AppContext.BaseDirectory}");
            WriteLine($"Data directory: {ApplicationPaths.DataFolder}");
        }
    }

    public static void EndSession()
    {
        Log("Walker Media Manager session ended normally.");
    }

    public static void Log(string message)
    {
        try
        {
            lock (SyncRoot)
            {
                RotateLogIfNeeded();
                WriteLine(message);
            }
        }
        catch
        {
            // Diagnostics must never crash the application.
        }
    }

    public static void LogException(string context, Exception exception)
    {
        ArgumentNullException.ThrowIfNull(exception);
        Log($"ERROR: {context}{Environment.NewLine}{exception}");
    }

    private static string GetApplicationVersion()
    {
        Assembly assembly = typeof(DiagnosticsService).Assembly;
        return assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()
                   ?.InformationalVersion
               ?? assembly.GetName().Version?.ToString()
               ?? "Unknown";
    }

    private static void RotateLogIfNeeded()
    {
        Directory.CreateDirectory(LogFolderPath);

        if (!File.Exists(LogFilePath))
        {
            return;
        }

        FileInfo logFile = new(LogFilePath);
        if (logFile.Length < MaximumLogSizeBytes)
        {
            return;
        }

        string archivedLogPath = Path.Combine(
            LogFolderPath,
            $"walker-media-manager-{DateTime.Now:yyyyMMdd-HHmmss}.log");

        File.Move(LogFilePath, archivedLogPath, overwrite: false);
    }

    private static void WriteLine(string message)
    {
        Directory.CreateDirectory(LogFolderPath);
        string line =
            $"[{DateTimeOffset.Now:yyyy-MM-dd HH:mm:ss.fff zzz}] {message}{Environment.NewLine}";

        File.AppendAllText(LogFilePath, line);
    }
}
