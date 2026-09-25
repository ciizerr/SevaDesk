using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Threading.Tasks;
using Dapper;
using SevaDesk.Infrastructure.Database;

namespace SevaDesk_App.Services.Maintenance;

/// <summary>
/// On each app startup, scans completed sessions whose backup sync was successful
/// and whose working folder on the Desktop has exceeded the configured TTL.
/// Deletes the working folder and shows a toast summary.
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
    /// Runs the scan + delete in background; fires a toast if folders were cleaned.
    /// </summary>
    public static async Task RunAsync()
    {
        try
        {
            int ttlHours = GetTtlHours();
            if (ttlHours < 0) return; // 0 = immediate, negative = disabled

            var cutoff = DateTime.UtcNow.AddHours(-ttlHours);
            var candidates = await GetCandidatesAsync(cutoff);

            int deleted = 0;
            var deletedNames = new List<string>();

            foreach (var (sessionId, folderPath, syncedAt) in candidates)
            {
                if (!Directory.Exists(folderPath)) continue;
                try
                {
                    Directory.Delete(folderPath, recursive: true);
                    deleted++;
                    deletedNames.Add(Path.GetFileName(folderPath));

                    // Clear folder path from DB so we don't attempt again
                    await ClearFolderPathAsync(sessionId);
                }
                catch { /* skip if locked */ }
            }

            if (deleted > 0)
            {
                var summary = deleted == 1
                    ? $"🗂 Working folder cleaned up: {deletedNames[0]}"
                    : $"🗂 {deleted} working folders cleaned up after backup sync";

                ToastService.Instance.ShowInfo(summary);
            }
        }
        catch { /* never crash startup */ }
    }

    private static async Task<IEnumerable<(string SessionId, string FolderPath, DateTime SyncedAt)>> GetCandidatesAsync(DateTime cutoff)
    {
        try
        {
            using var conn = AppServices.Database.CreateConnection();
            await conn.OpenAsync();

            var rows = await conn.QueryAsync(
                @"SELECT id, working_folder_path, backup_synced_at
                  FROM sessions
                  WHERE status = 'Completed'
                    AND working_folder_path IS NOT NULL
                    AND backup_synced_at IS NOT NULL
                    AND backup_synced_at != ''",
                commandTimeout: 5);

            var result = new List<(string, string, DateTime)>();
            foreach (var row in rows)
            {
                string? path = row.working_folder_path as string;
                string? syncedStr = row.backup_synced_at as string;
                string? id = row.id as string;

                if (string.IsNullOrEmpty(path) || string.IsNullOrEmpty(syncedStr) || string.IsNullOrEmpty(id))
                    continue;

                if (!DateTime.TryParse(syncedStr, null, System.Globalization.DateTimeStyles.RoundtripKind, out var syncedAt))
                    continue;

                if (syncedAt <= cutoff)
                {
                    result.Add((id, path, syncedAt));
                }
            }
            return result;
        }
        catch
        {
            return Array.Empty<(string, string, DateTime)>();
        }
    }

    private static async Task ClearFolderPathAsync(string sessionId)
    {
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
}
