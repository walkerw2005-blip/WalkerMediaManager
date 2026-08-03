using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Data.Sqlite;
using WalkerMediaManager.UI.Data;

namespace WalkerMediaManager.UI.Services;

public static class DatabaseBackupService
{
    private const int MaximumBackupCount = 30;

    public static string BackupFolderPath => ApplicationPaths.BackupFolder;

    public static async Task<string?> CreateDailyBackupAsync(
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        Directory.CreateDirectory(BackupFolderPath);

        string datePrefix = $"walker_{DateTime.Now:yyyy-MM-dd}_";
        string? existingBackup = new DirectoryInfo(BackupFolderPath)
            .EnumerateFiles($"{datePrefix}*.db", SearchOption.TopDirectoryOnly)
            .OrderByDescending(file => file.LastWriteTimeUtc)
            .Select(file => file.FullName)
            .FirstOrDefault();

        if (!string.IsNullOrWhiteSpace(existingBackup))
        {
            DiagnosticsService.Log(
                $"Daily startup backup already exists: {existingBackup}");
            DeleteOldBackups();
            return existingBackup;
        }

        DiagnosticsService.Log("Creating daily startup database backup.");
        return await CreateBackupAsync(cancellationToken);
    }

    public static async Task<string> CreateBackupAsync(
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        string databasePath = Path.GetFullPath(DatabaseService.DatabasePath);
        string backupFolderPath = Path.GetFullPath(BackupFolderPath);

        ApplicationPaths.EnsureDataFolderExists();
        Directory.CreateDirectory(backupFolderPath);

        if (!File.Exists(databasePath))
        {
            string message =
                $"The Walker Media Manager database was not found at '{databasePath}'.";
            DiagnosticsService.Log($"Database backup failed: {message}");
            throw new FileNotFoundException(message, databasePath);
        }

        string timestamp = DateTime.Now.ToString("yyyy-MM-dd_HHmmss");
        string backupPath = GetUniqueBackupPath(backupFolderPath, timestamp);

        try
        {
            // SQLite's backup API creates a consistent snapshot even if the database
            // is currently open elsewhere in the application.
            await using SqliteConnection source =
                new($"Data Source={databasePath};Mode=ReadOnly");
            await using SqliteConnection destination =
                new($"Data Source={backupPath};Mode=ReadWriteCreate");

            await source.OpenAsync(cancellationToken);
            await destination.OpenAsync(cancellationToken);
            source.BackupDatabase(destination);

            if (!File.Exists(backupPath) || new FileInfo(backupPath).Length == 0)
            {
                throw new IOException(
                    $"The backup file was not created correctly at '{backupPath}'.");
            }

            DiagnosticsService.Log($"Database backup created: {backupPath}");
            DeleteOldBackups();
            return backupPath;
        }
        catch (Exception exception)
        {
            TryDeleteIncompleteBackup(backupPath);
            DiagnosticsService.LogException(
                $"Database backup failed. Source: '{databasePath}'. Destination: '{backupPath}'.",
                exception);
            throw;
        }
    }

    private static string GetUniqueBackupPath(
        string backupFolderPath,
        string timestamp)
    {
        string candidate = Path.Combine(
            backupFolderPath,
            $"walker_{timestamp}.db");
        int duplicateNumber = 1;

        while (File.Exists(candidate))
        {
            candidate = Path.Combine(
                backupFolderPath,
                $"walker_{timestamp}_{duplicateNumber}.db");
            duplicateNumber++;
        }

        return candidate;
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

    private static void TryDeleteIncompleteBackup(string backupPath)
    {
        try
        {
            if (File.Exists(backupPath))
            {
                File.Delete(backupPath);
            }
        }
        catch
        {
            // Preserve the original backup exception.
        }
    }
}
