using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using WalkerMediaManager.UI.Data;

namespace WalkerMediaManager.UI.Services;

public static class DatabaseBackupService
{
    private const int MaximumBackupCount = 30;
    private const string BackupFolderName = "Backups";

    public static string BackupFolderPath
    {
        get
        {
            string databaseFolder =
                Path.GetDirectoryName(DatabaseService.DatabasePath)
                ?? throw new InvalidOperationException(
                    "The database folder could not be determined.");

            return Path.Combine(databaseFolder, BackupFolderName);
        }
    }

    public static async Task<string?> CreateBackupAsync(
        CancellationToken cancellationToken = default)
    {
        string databasePath = DatabaseService.DatabasePath;

        if (!File.Exists(databasePath))
        {
            DiagnosticsService.Log(
                $"Database backup skipped because the database does not exist: {databasePath}");

            return null;
        }

        Directory.CreateDirectory(BackupFolderPath);

        string timestamp = DateTime.Now.ToString("yyyy-MM-dd_HHmmss");
        string backupPath = Path.Combine(
            BackupFolderPath,
            $"walker_{timestamp}.db");

        int duplicateNumber = 1;

        while (File.Exists(backupPath))
        {
            backupPath = Path.Combine(
                BackupFolderPath,
                $"walker_{timestamp}_{duplicateNumber}.db");

            duplicateNumber++;
        }

        await using FileStream source = new(
            databasePath,
            FileMode.Open,
            FileAccess.Read,
            FileShare.ReadWrite,
            bufferSize: 81920,
            useAsync: true);

        await using FileStream destination = new(
            backupPath,
            FileMode.CreateNew,
            FileAccess.Write,
            FileShare.None,
            bufferSize: 81920,
            useAsync: true);

        await source.CopyToAsync(destination, cancellationToken);
        await destination.FlushAsync(cancellationToken);

        DiagnosticsService.Log(
            $"Database backup created: {backupPath}");

        DeleteOldBackups();

        return backupPath;
    }

    private static void DeleteOldBackups()
    {
        try
        {
            if (!Directory.Exists(BackupFolderPath))
            {
                return;
            }

            List<FileInfo> backups = new DirectoryInfo(BackupFolderPath)
                .EnumerateFiles("walker_*.db", SearchOption.TopDirectoryOnly)
                .OrderByDescending(file => file.CreationTimeUtc)
                .ThenByDescending(file => file.LastWriteTimeUtc)
                .ToList();

            foreach (FileInfo oldBackup in backups.Skip(MaximumBackupCount))
            {
                try
                {
                    oldBackup.Delete();

                    DiagnosticsService.Log(
                        $"Old database backup deleted: {oldBackup.FullName}");
                }
                catch (Exception exception)
                {
                    DiagnosticsService.LogException(
                        $"Could not delete old database backup '{oldBackup.FullName}'.",
                        exception);
                }
            }
        }
        catch (Exception exception)
        {
            DiagnosticsService.LogException(
                "Database backup retention cleanup failed.",
                exception);
        }
    }
}
