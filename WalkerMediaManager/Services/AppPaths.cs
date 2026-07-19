namespace WalkerMediaManager.Services;

public static class AppPaths
{
    public static string DataDirectory { get; } = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "Walker Media Manager");

    public static string DatabasePath => Path.Combine(DataDirectory, "walker-media.db");
    public static string LogPath => Path.Combine(DataDirectory, "walker-media.log");
}
