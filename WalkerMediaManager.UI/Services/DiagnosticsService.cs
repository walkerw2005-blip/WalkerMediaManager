using System;
using System.IO;

namespace WalkerMediaManager.UI.Services;

public static class DiagnosticsService
{
    private static readonly object SyncRoot = new();

    public static string LogFolderPath => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "WalkerMediaManager",
        "Logs");

    public static string LogFilePath => Path.Combine(LogFolderPath, "walker-media-manager.log");

    public static void Log(string message)
    {
        try
        {
            Directory.CreateDirectory(LogFolderPath);
            string line = $"[{DateTimeOffset.Now:yyyy-MM-dd HH:mm:ss zzz}] {message}{Environment.NewLine}";
            lock (SyncRoot)
            {
                File.AppendAllText(LogFilePath, line);
            }
        }
        catch
        {
            // Diagnostics must never crash the application.
        }
    }

    public static void LogException(string context, Exception exception)
    {
        Log($"ERROR: {context}{Environment.NewLine}{exception}");
    }
}
