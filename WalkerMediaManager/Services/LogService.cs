namespace WalkerMediaManager.Services;

public static class LogService
{
    private static readonly object Gate = new();

    public static void Write(string message)
    {
        try
        {
            lock (Gate)
            {
                Directory.CreateDirectory(AppPaths.DataDirectory);
                File.AppendAllText(AppPaths.LogPath, $"{DateTimeOffset.Now:u} {message}{Environment.NewLine}");
            }
        }
        catch
        {
            // Logging must never crash the application.
        }
    }
}
