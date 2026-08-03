using System;
using System.IO;

namespace WalkerMediaManager.UI.Services;

/// <summary>
/// Provides the single authoritative location for Walker Media Manager data.
/// </summary>
public static class ApplicationPaths
{
    public static string DataFolder { get; } = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "WalkerMediaManager");

    public static string DatabasePath => Path.Combine(DataFolder, "walker.db");

    public static string SettingsPath => Path.Combine(DataFolder, "settings.json");

    public static string BackupFolder => Path.Combine(DataFolder, "Backups");

    public static string LogFolder => Path.Combine(DataFolder, "Logs");

    public static void EnsureDataFolderExists() => Directory.CreateDirectory(DataFolder);
}
