using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Dapper;
using Microsoft.UI.Xaml.Controls;
using SevaDesk.Core.Models;
using SevaDesk.Infrastructure.Database;
using SevaDesk_App.Views.Dialogs;

namespace SevaDesk_App.Services.Maintenance;

/// <summary>
/// On each app startup, scans completed sessions whose backup sync was successful
/// and whose working folder on the Desktop has exceeded the configured TTL.
/// If changes were made in File Explorer, prompts the owner with a review dialog;
/// otherwise silently cleans up the folder to the Windows Recycle Bin.
/// </summary>
public static class WorkingFolderCleanupService
{
    private const string TtlSettingKey = "working_folder_ttl_hours";
    private const int DefaultTtlHours = 24;

    public static int GetTtlHours()
    {
        var raw = AppServices.Database.GetSetting(TtlSettingKey, DefaultTtlHours.ToString());
        return int.TryParse(raw, out var hours) && hours >= 0 ? hours : DefaultTtlHours;
    }

    public static void SetTtlHours(int hours)
    {
        AppServices.Database.SetSetting(TtlSettingKey, hours.ToString());
    }

    /// <summary>
    /// Call this once during app startup (after AppServices.Initialize).
    /// Runs the scan, compares working folders against backup archives,
    /// prompts for any detected Explorer changes, and silently recycles synced folders.
    /// Strictly protects working folders for all currently Active or Paused sessions.
    /// </summary>
    public static async Task RunAsync()
    {
        try
        {
            // 1. Ensure any missing working folders for active/paused sessions are recreated and restored
            await EnsureActiveSessionWorkingFoldersAsync();

            int ttlHours = GetTtlHours();
            if (ttlHours < 0) return; // 0 = immediate, negative = disabled

            var cutoff = DateTime.UtcNow.AddHours(-ttlHours);
            CleanUpOldWalkInTempFolders(cutoff);

            // 2. Query expired completed candidate sessions with customer details
            var candidates = (await GetCandidatesAsync(cutoff)).ToList();
            if (candidates.Count == 0) return;

            // Group candidates by working folder path (case-insensitive)
            // Multiple sessions may reference the same customer working folder
            var folderGroups = candidates
                .GroupBy(c => c.FolderPath, StringComparer.OrdinalIgnoreCase)
                .ToList();

            int silentlyDeleted = 0;
            var silentlyDeletedNames = new List<string>();
            var changedFolders = new List<ChangedFolderCandidate>();

            foreach (var group in folderGroups)
            {
                var folderPath = group.Key;
                var primary = group.First();

                if (!Directory.Exists(folderPath))
                {
                    // Folder is already absent from disk; clear DB references
                    foreach (var item in group)
                    {
                        await ClearFolderPathAsync(item.SessionId);
                    }
                    continue;
                }

                // If customer name is missing, delete safely to Recycle Bin
                if (string.IsNullOrWhiteSpace(primary.CustomerName))
                {
                    if (AppServices.FolderManager.DeleteToRecycleBin(folderPath, out _))
                    {
                        silentlyDeleted++;
                        silentlyDeletedNames.Add(Path.GetFileName(folderPath));
                        foreach (var item in group)
                        {
                            await ClearFolderPathAsync(item.SessionId);
                        }
                    }
                    continue;
                }

                // Compare working directory against backup directory
                var diff = AppServices.FolderManager.CompareWorkingToBackup(primary.CustomerName, primary.CustomerCode);

                if (!diff.HasChanges)
                {
                    // 100% in sync! Silently move to Recycle Bin
                    if (AppServices.FolderManager.DeleteToRecycleBin(folderPath, out _))
                    {
                        silentlyDeleted++;
                        silentlyDeletedNames.Add(Path.GetFileName(folderPath));
                        foreach (var item in group)
                        {
                            await ClearFolderPathAsync(item.SessionId);
                        }
                    }
                }
                else
                {
                    // Working folder has unsynced changes made by the cafe owner in File Explorer!
                    changedFolders.Add(new ChangedFolderCandidate
                    {
                        SessionId = string.Join(",", group.Select(g => g.SessionId)),
                        CustomerId = primary.CustomerId,
                        CustomerName = primary.CustomerName,
                        CustomerCode = primary.CustomerCode,
                        WorkingFolderPath = folderPath,
                        Diff = diff,
                        ChosenAction = ReviewFolderChoice.UpdateBackupAndClean
                    });
                }
            }

            // 3. If any changed folders exist, present the review dialog to the owner
            if (changedFolders.Count > 0)
            {
                await ShowReviewDialogAndProcessAsync(changedFolders);
            }

            // 4. Show summary toast for silently deleted folders if no review dialog was shown
            if (silentlyDeleted > 0 && changedFolders.Count == 0)
            {
                var summary = silentlyDeleted == 1
                    ? $"🗂 Working folder cleaned up: {silentlyDeletedNames[0]}"
                    : $"🗂 {silentlyDeleted} working folders cleaned up after backup sync";

                ToastService.Instance.ShowInfo(summary);
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[WorkingFolderCleanupService] RunAsync error: {ex.Message}");
        }
    }

    private static async Task ShowReviewDialogAndProcessAsync(List<ChangedFolderCandidate> changedFolders)
    {
        var mainWindow = MainWindow.Instance;
        if (mainWindow == null) return;

        // Give the app ~2.5 seconds to settle on startup so windows and any onboarding dialogs complete
        await Task.Delay(2500);

        // Wait until XamlRoot is ready
        int waited = 0;
        while ((mainWindow.Content?.XamlRoot == null) && waited < 15000)
        {
            await Task.Delay(500);
            waited += 500;
        }

        if (mainWindow.Content?.XamlRoot == null) return;

        int syncedAndCleaned = 0;
        int discardedAndCleaned = 0;
        int keptOnDesktop = 0;

        for (int i = 0; i < changedFolders.Count; i++)
        {
            var candidate = changedFolders[i];
            ReviewFolderChoice choice = ReviewFolderChoice.KeepOnDesktop;
            bool dialogShown = false;

            for (int attempt = 0; attempt < 10; attempt++)
            {
                var tcs = new TaskCompletionSource<(bool Success, ReviewFolderChoice Choice)>();

                mainWindow.DispatcherQueue.TryEnqueue(async () =>
                {
                    try
                    {
                        var dialog = new WorkingFolderReviewDialog(candidate, i, changedFolders.Count)
                        {
                            XamlRoot = mainWindow.Content.XamlRoot
                        };

                        await dialog.ShowAsync();
                        tcs.SetResult((true, dialog.ResultChoice));
                    }
                    catch (Exception ex)
                    {
                        System.Diagnostics.Debug.WriteLine($"[WorkingFolderCleanupService] Review dialog attempt failed: {ex.Message}");
                        tcs.SetResult((false, ReviewFolderChoice.KeepOnDesktop));
                    }
                });

                var (success, resultChoice) = await tcs.Task;
                if (success)
                {
                    dialogShown = true;
                    choice = resultChoice;
                    break;
                }

                await Task.Delay(1500);
            }

            var sessionIds = candidate.SessionId.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

            if (!dialogShown)
            {
                // Could not display dialog; postpone safely
                foreach (var sid in sessionIds)
                {
                    await PostponeCleanupAsync(sid);
                }
                continue;
            }

            switch (choice)
            {
                case ReviewFolderChoice.UpdateBackupAndClean:
                    try
                    {
                        // 1. Mirror changes into backup
                        await AppServices.FolderManager.SyncToBackupAsync(candidate.CustomerName, candidate.CustomerCode);
                        // 2. Recycle working folder
                        if (AppServices.FolderManager.DeleteToRecycleBin(candidate.WorkingFolderPath, out _))
                        {
                            foreach (var sid in sessionIds)
                            {
                                await ClearFolderPathAsync(sid);
                            }
                            syncedAndCleaned++;
                        }
                        else
                        {
                            foreach (var sid in sessionIds)
                            {
                                await PostponeCleanupAsync(sid);
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        System.Diagnostics.Debug.WriteLine($"[WorkingFolderCleanupService] SyncToBackup failed: {ex.Message}");
                        foreach (var sid in sessionIds)
                        {
                            await PostponeCleanupAsync(sid);
                        }
                    }
                    break;

                case ReviewFolderChoice.KeepOnDesktop:
                    foreach (var sid in sessionIds)
                    {
                        await PostponeCleanupAsync(sid);
                    }
                    keptOnDesktop++;
                    break;

                case ReviewFolderChoice.DeleteWithoutBackup:
                    try
                    {
                        if (AppServices.FolderManager.DeleteToRecycleBin(candidate.WorkingFolderPath, out _))
                        {
                            foreach (var sid in sessionIds)
                            {
                                await ClearFolderPathAsync(sid);
                            }
                            discardedAndCleaned++;
                        }
                        else
                        {
                            foreach (var sid in sessionIds)
                            {
                                await PostponeCleanupAsync(sid);
                            }
                        }
                    }
                    catch
                    {
                        foreach (var sid in sessionIds)
                        {
                            await PostponeCleanupAsync(sid);
                        }
                    }
                    break;
            }
        }

        var summaryParts = new List<string>();
        if (syncedAndCleaned > 0) summaryParts.Add($"{syncedAndCleaned} synced & cleaned");
        if (discardedAndCleaned > 0) summaryParts.Add($"{discardedAndCleaned} discarded");
        if (keptOnDesktop > 0) summaryParts.Add($"{keptOnDesktop} kept on Desktop");

        if (summaryParts.Count > 0)
        {
            ToastService.Instance.ShowSuccess($"🗂 Folder cleanup: {string.Join(", ", summaryParts)}");
        }
    }

    /// <summary>
    /// Re-ensures that all currently Active or Paused sessions have their working folders on Desktop.
    /// If a folder is missing on startup, recreates it and restores files from the Backup archive.
    /// </summary>
    public static async Task<int> EnsureActiveSessionWorkingFoldersAsync()
    {
        int restoredCount = 0;
        try
        {
            var activeItems = await AppServices.Sessions.GetActiveSessionsAsync();
            foreach (var item in activeItems)
            {
                if (item.Customer == null || string.IsNullOrWhiteSpace(item.Customer.Name)) continue;
                var folderPath = AppServices.FolderManager.GetCustomerFolderPath(item.Customer.Name, item.Customer.Code);
                if (!Directory.Exists(folderPath))
                {
                    AppServices.FolderManager.EnsureCustomerWorkingFolder(item.Customer.Name, item.Customer.Code);
                    await AppServices.FolderManager.SyncToWorkingAsync(item.Customer.Name, item.Customer.Code);
                    restoredCount++;
                }
            }
        }
        catch { }
        return restoredCount;
    }

    private class CandidateFolderInfo
    {
        public string SessionId { get; set; } = string.Empty;
        public string CustomerId { get; set; } = string.Empty;
        public string CustomerName { get; set; } = string.Empty;
        public string CustomerCode { get; set; } = string.Empty;
        public string FolderPath { get; set; } = string.Empty;
        public DateTime SyncedAt { get; set; }
    }

    private static async Task<IEnumerable<CandidateFolderInfo>> GetCandidatesAsync(DateTime cutoff)
    {
        try
        {
            using var conn = AppServices.Database.CreateConnection();
            await conn.OpenAsync();

            // Collect all protected folder paths belonging to currently Active or Paused sessions
            var activeRows = await conn.QueryAsync(
                @"SELECT s.working_folder_path, c.name, c.code
                  FROM sessions s
                  JOIN customers c ON s.customer_id = c.id
                  WHERE s.status IN ('Active', 'Paused')",
                commandTimeout: 5);

            var protectedPaths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var a in activeRows)
            {
                string? wfp = a.working_folder_path as string;
                if (!string.IsNullOrWhiteSpace(wfp))
                {
                    try { protectedPaths.Add(Path.GetFullPath(wfp).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)); } catch { }
                }

                string? cName = a.name as string;
                string? cCode = a.code as string;
                if (!string.IsNullOrWhiteSpace(cName))
                {
                    try
                    {
                        var folder = AppServices.FolderManager.GetCustomerFolderPath(cName, cCode ?? string.Empty);
                        protectedPaths.Add(Path.GetFullPath(folder).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar));
                    }
                    catch { }
                }
            }

            var rows = await conn.QueryAsync(
                @"SELECT s.id, s.customer_id, s.working_folder_path, s.backup_synced_at, c.name AS customer_name, c.code AS customer_code
                  FROM sessions s
                  LEFT JOIN customers c ON s.customer_id = c.id
                  WHERE s.status = 'Completed'
                    AND s.working_folder_path IS NOT NULL
                    AND s.backup_synced_at IS NOT NULL
                    AND s.backup_synced_at != ''",
                commandTimeout: 5);

            var result = new List<CandidateFolderInfo>();
            foreach (var row in rows)
            {
                string? path = row.working_folder_path as string;
                string? syncedStr = row.backup_synced_at as string;
                string? id = row.id as string;
                string? customerId = row.customer_id as string;
                string? customerName = row.customer_name as string;
                string? customerCode = row.customer_code as string;

                if (string.IsNullOrEmpty(path) || string.IsNullOrEmpty(syncedStr) || string.IsNullOrEmpty(id))
                    continue;

                // Skip walk-in sessions here; Walk-in_Temp is managed by CleanUpOldWalkInTempFolders
                if (path.Contains("Walk-in_Temp", StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(customerName, "Walk-in Customer", StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(customerCode, "CUST-0000", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                // STRICT IMMUNITY: Never delete a folder that belongs to any currently Active or Paused session!
                try
                {
                    var norm = Path.GetFullPath(path).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
                    if (protectedPaths.Contains(norm))
                    {
                        continue;
                    }
                }
                catch { }

                if (!DateTime.TryParse(syncedStr, null, DateTimeStyles.RoundtripKind, out var syncedAt))
                    continue;

                if (syncedAt <= cutoff)
                {
                    result.Add(new CandidateFolderInfo
                    {
                        SessionId = id,
                        CustomerId = customerId ?? string.Empty,
                        CustomerName = customerName ?? string.Empty,
                        CustomerCode = customerCode ?? string.Empty,
                        FolderPath = path,
                        SyncedAt = syncedAt
                    });
                }
            }
            return result;
        }
        catch
        {
            return Array.Empty<CandidateFolderInfo>();
        }
    }

    private static async Task ClearFolderPathAsync(string sessionId)
    {
        if (string.IsNullOrWhiteSpace(sessionId)) return;
        try
        {
            using var conn = AppServices.Database.CreateConnection();
            await conn.OpenAsync();
            await conn.ExecuteAsync(
                "UPDATE sessions SET working_folder_path = NULL WHERE id = @Id",
                new { Id = sessionId });
        }
        catch { }
    }

    private static async Task PostponeCleanupAsync(string sessionId)
    {
        if (string.IsNullOrWhiteSpace(sessionId)) return;
        try
        {
            using var conn = AppServices.Database.CreateConnection();
            await conn.OpenAsync();
            var nowStr = DateTime.UtcNow.ToString("o");
            await conn.ExecuteAsync(
                "UPDATE sessions SET backup_synced_at = @SyncedAt WHERE id = @Id",
                new { Id = sessionId, SyncedAt = nowStr });
        }
        catch { }
    }

    private static void CleanUpOldWalkInTempFolders(DateTime cutoff)
    {
        try
        {
            var baseDir = AppServices.FolderManager.BaseDirectory;
            var walkInRoot = Path.Combine(baseDir, "Walk-in_Temp");
            if (!Directory.Exists(walkInRoot)) return;

            var subdirs = Directory.GetDirectories(walkInRoot);
            foreach (var dir in subdirs)
            {
                var folderName = Path.GetFileName(dir);
                if (DateTime.TryParseExact(folderName, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var folderDate))
                {
                    if (folderDate.Date < cutoff.Date)
                    {
                        try
                        {
                            AppServices.FolderManager.DeleteToRecycleBin(dir, out _);
                        }
                        catch { /* skip if locked */ }
                    }
                }
            }
        }
        catch { }
    }
}
