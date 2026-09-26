using System.Collections.ObjectModel;
using System.Text.Json;

namespace SevaDesk_App.Services;

public record IncomingFileItem(string FilePath, string FileName, string SourceFolder, long FileSize, DateTime DetectedAt);

public sealed class IncomingFileWatcherService : IDisposable
{
    private static readonly Lazy<IncomingFileWatcherService> _lazy = new(() => new IncomingFileWatcherService());
    public static IncomingFileWatcherService Instance => _lazy.Value;

    private readonly List<FileSystemWatcher> _watchers = [];
    private readonly HashSet<string> _recentlyHandled = [];
    private readonly object _lock = new();

    public event Action<IncomingFileItem>? FileDetected;

    public bool WatchDownloads { get; set; } = true;
    public bool WatchDesktop { get; set; } = true;
    public bool WatchDocuments { get; set; } = true;

    public ObservableCollection<string> CustomFolders { get; } = [];

    private IncomingFileWatcherService()
    {
        LoadSettings();
    }

    public void Initialize()
    {
        RestartWatchers();
    }

    public void LoadSettings()
    {
        try
        {
            var downloadsStr = AppServices.Database.GetSetting("watch_downloads", "true");
            WatchDownloads = bool.TryParse(downloadsStr, out var wd) ? wd : true;

            var desktopStr = AppServices.Database.GetSetting("watch_desktop", "true");
            WatchDesktop = bool.TryParse(desktopStr, out var dt) ? dt : true;

            var docsStr = AppServices.Database.GetSetting("watch_documents", "true");
            WatchDocuments = bool.TryParse(docsStr, out var dc) ? dc : true;

            var customJson = AppServices.Database.GetSetting("watch_custom_folders", "[]") ?? "[]";
            var customList = JsonSerializer.Deserialize<List<string>>(customJson) ?? [];

            CustomFolders.Clear();
            foreach (var folder in customList)
            {
                if (Directory.Exists(folder))
                {
                    CustomFolders.Add(folder);
                }
            }
        }
        catch { }
    }

    public void SaveSettings()
    {
        try
        {
            AppServices.Database.SetSetting("watch_downloads", WatchDownloads.ToString().ToLowerInvariant());
            AppServices.Database.SetSetting("watch_desktop", WatchDesktop.ToString().ToLowerInvariant());
            AppServices.Database.SetSetting("watch_documents", WatchDocuments.ToString().ToLowerInvariant());
            AppServices.Database.SetSetting("watch_custom_folders", JsonSerializer.Serialize(CustomFolders.ToList()));
        }
        catch { }
    }

    public void RestartWatchers()
    {
        StopWatchers();

        var directoriesToWatch = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        if (WatchDownloads)
        {
            var userProfile = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
            var downloads = Path.Combine(userProfile, "Downloads");
            if (Directory.Exists(downloads)) directoriesToWatch.Add(downloads);
        }

        if (WatchDesktop)
        {
            var userProfile = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
            var defaultDesktop = Environment.GetFolderPath(Environment.SpecialFolder.Desktop);
            var localDesktop = Path.Combine(userProfile, "Desktop");
            var oneDriveDesktop = Path.Combine(userProfile, "OneDrive", "Desktop");

            if (Directory.Exists(defaultDesktop)) directoriesToWatch.Add(defaultDesktop);
            if (Directory.Exists(localDesktop)) directoriesToWatch.Add(localDesktop);
            if (Directory.Exists(oneDriveDesktop)) directoriesToWatch.Add(oneDriveDesktop);
        }

        if (WatchDocuments)
        {
            var docs = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
            if (Directory.Exists(docs)) directoriesToWatch.Add(docs);
            var userProfile = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
            var oneDriveDocs = Path.Combine(userProfile, "OneDrive", "Documents");
            if (Directory.Exists(oneDriveDocs)) directoriesToWatch.Add(oneDriveDocs);
        }

        foreach (var custom in CustomFolders)
        {
            if (Directory.Exists(custom)) directoriesToWatch.Add(custom);
        }

        var workingBase = AppServices.FolderManager.BaseDirectory;

        foreach (var dir in directoriesToWatch)
        {
            try
            {
                var watcher = new FileSystemWatcher(dir)
                {
                    NotifyFilter = NotifyFilters.FileName | NotifyFilters.LastWrite | NotifyFilters.Size,
                    IncludeSubdirectories = false,
                    EnableRaisingEvents = true
                };

                watcher.Created += (s, e) => OnFileCreated(e.FullPath, dir, workingBase);
                watcher.Renamed += (s, e) => OnFileCreated(e.FullPath, dir, workingBase);

                _watchers.Add(watcher);
            }
            catch { }
        }
    }

    public void StopWatchers()
    {
        foreach (var w in _watchers)
        {
            try
            {
                w.EnableRaisingEvents = false;
                w.Dispose();
            }
            catch { }
        }
        _watchers.Clear();
    }

    private void OnFileCreated(string filePath, string sourceFolder, string workingBase)
    {
        try
        {
            // Ignore temporary, download parts, shortcuts, and system files
            var ext = Path.GetExtension(filePath).ToLowerInvariant();
            if (ext is ".crdownload" or ".tmp" or ".part" or ".download" or ".ini" or ".lnk") return;

            var fileName = Path.GetFileName(filePath);
            if (fileName.StartsWith("~") || fileName.StartsWith(".") || fileName.Equals("desktop.ini", StringComparison.OrdinalIgnoreCase)) return;

            // Only ignore if the file is inside a customer directory (i.e. child directory of workingBase)
            // Loose files directly on Desktop/workingBase must NOT be ignored!
            var parentDir = Path.GetDirectoryName(filePath);
            if (!string.IsNullOrEmpty(parentDir) && !string.IsNullOrEmpty(workingBase))
            {
                var normParent = parentDir.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
                var normBase = workingBase.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);

                if (normParent.StartsWith(normBase, StringComparison.OrdinalIgnoreCase) &&
                    !normParent.Equals(normBase, StringComparison.OrdinalIgnoreCase))
                {
                    // File is inside a customer folder (e.g. workingBase\CustomerName_0001\...)
                    return;
                }
            }

            lock (_lock)
            {
                if (_recentlyHandled.Contains(filePath)) return;
                _recentlyHandled.Add(filePath);
            }

            // Fire after brief delay so file write lock closes
            _ = Task.Run(async () =>
            {
                long fileSize = 0;
                bool fileReady = false;

                // Wait up to 2.4s for download write locks to release
                for (int attempt = 0; attempt < 6; attempt++)
                {
                    await Task.Delay(400);
                    if (!File.Exists(filePath)) return;

                    try
                    {
                        using var fs = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
                        fileSize = fs.Length;
                        fileReady = true;
                        break;
                    }
                    catch (IOException)
                    {
                        // File still locked by browser / WhatsApp writer
                    }
                    catch
                    {
                        break;
                    }
                }

                if (!fileReady && !File.Exists(filePath)) return;

                var item = new IncomingFileItem(filePath, fileName, sourceFolder, fileSize, DateTime.Now);
                FileDetected?.Invoke(item);

                // Clean cache after 15 seconds
                await Task.Delay(15000);
                lock (_lock)
                {
                    _recentlyHandled.Remove(filePath);
                }
            });
        }
        catch { }
    }

    public string? AutoRouteSessionId { get; private set; }
    public string? AutoRouteCustomerName { get; private set; }
    public (string SourcePath, string DestPath, string SessionId, string CustomerName)? LastAutoMovedFile { get; private set; }

    public void SetAutoRoute(string sessionId, string customerName)
    {
        AutoRouteSessionId = sessionId;
        AutoRouteCustomerName = customerName;
    }

    public void ClearAutoRoute()
    {
        AutoRouteSessionId = null;
        AutoRouteCustomerName = null;
    }

    public void ClearAutoRouteIfSession(string sessionId)
    {
        if (AutoRouteSessionId == sessionId)
        {
            ClearAutoRoute();
        }
    }

    public async Task<bool> RouteFileToCustomerAsync(string sourceFilePath, string targetCustomerFolderPath, bool deleteSource = true)
    {
        var (success, _) = await RouteFileToCustomerExAsync(sourceFilePath, targetCustomerFolderPath, deleteSource);
        return success;
    }

    public async Task<(bool Success, string DestPath)> RouteFileToCustomerExAsync(string sourceFilePath, string targetCustomerFolderPath, bool deleteSource = true)
    {
        try
        {
            if (!File.Exists(sourceFilePath)) return (false, string.Empty);
            if (!Directory.Exists(targetCustomerFolderPath))
            {
                Directory.CreateDirectory(targetCustomerFolderPath);
            }

            var fileName = Path.GetFileName(sourceFilePath);
            var destPath = Path.Combine(targetCustomerFolderPath, fileName);

            // Handle duplicate file names safely
            int counter = 1;
            var nameWithoutExt = Path.GetFileNameWithoutExtension(fileName);
            var ext = Path.GetExtension(fileName);
            while (File.Exists(destPath))
            {
                destPath = Path.Combine(targetCustomerFolderPath, $"{nameWithoutExt}_{counter}{ext}");
                counter++;
            }

            await Task.Run(() =>
            {
                if (deleteSource)
                {
                    File.Move(sourceFilePath, destPath);
                }
                else
                {
                    File.Copy(sourceFilePath, destPath, overwrite: false);
                }
            });

            return (true, destPath);
        }
        catch
        {
            return (false, string.Empty);
        }
    }

    public async Task<(bool Success, string DestPath)> RouteFileToSessionWithUndoTrackingAsync(string sourceFilePath, string targetCustomerFolderPath, string customerName, string sessionId = "")
    {
        var (success, destPath) = await RouteFileToCustomerExAsync(sourceFilePath, targetCustomerFolderPath, deleteSource: true);
        if (success)
        {
            LastAutoMovedFile = (sourceFilePath, destPath, sessionId, customerName);
        }
        return (success, destPath);
    }

    public async Task<(bool Success, string DestPath)> RouteFileWithRenameAndUndoTrackingAsync(
        string sourceFilePath,
        string targetCustomerFolderPath,
        string targetBaseName,
        string customerName,
        string sessionId = "")
    {
        try
        {
            if (!File.Exists(sourceFilePath)) return (false, string.Empty);
            if (!Directory.Exists(targetCustomerFolderPath))
            {
                Directory.CreateDirectory(targetCustomerFolderPath);
            }

            var ext = Path.GetExtension(sourceFilePath);
            var uniqueFileName = SmartTagHelper.GenerateUniqueFileName(targetCustomerFolderPath, targetBaseName, ext);
            var destPath = Path.Combine(targetCustomerFolderPath, uniqueFileName);

            await Task.Run(() => File.Move(sourceFilePath, destPath));

            LastAutoMovedFile = (sourceFilePath, destPath, sessionId, customerName);
            return (true, destPath);
        }
        catch
        {
            return (false, string.Empty);
        }
    }

    public async Task<(bool Success, string NewDestPath)> RenameAutoMovedFileAsync(string currentDestPath, string targetBaseName)
    {
        try
        {
            if (!File.Exists(currentDestPath)) return (false, string.Empty);
            var folder = Path.GetDirectoryName(currentDestPath) ?? string.Empty;
            var ext = Path.GetExtension(currentDestPath);
            var uniqueFileName = SmartTagHelper.GenerateUniqueFileName(folder, targetBaseName, ext);
            var newDestPath = Path.Combine(folder, uniqueFileName);

            if (string.Equals(currentDestPath, newDestPath, StringComparison.OrdinalIgnoreCase))
            {
                return (true, currentDestPath);
            }

            await Task.Run(() => File.Move(currentDestPath, newDestPath));

            if (LastAutoMovedFile.HasValue)
            {
                var current = LastAutoMovedFile.Value;
                if (string.Equals(current.DestPath, currentDestPath, StringComparison.OrdinalIgnoreCase))
                {
                    LastAutoMovedFile = (current.SourcePath, newDestPath, current.SessionId, current.CustomerName);
                }
            }

            return (true, newDestPath);
        }
        catch
        {
            return (false, string.Empty);
        }
    }

    public async Task<(bool Success, string RestoredPath)> UndoLastAutoMoveAsync()
    {
        if (LastAutoMovedFile == null) return (false, string.Empty);
        var (sourcePath, destPath, sessionId, customerName) = LastAutoMovedFile.Value;
        LastAutoMovedFile = null;
        ClearAutoRoute();

        if (!File.Exists(destPath)) return (false, string.Empty);

        try
        {
            var sourceDir = Path.GetDirectoryName(sourcePath);
            if (!string.IsNullOrEmpty(sourceDir) && !Directory.Exists(sourceDir))
            {
                Directory.CreateDirectory(sourceDir);
            }

            await Task.Run(() => File.Move(destPath, sourcePath, overwrite: true));
            return (true, sourcePath);
        }
        catch
        {
            return (false, string.Empty);
        }
    }

    public void Dispose()
    {
        StopWatchers();
    }
}
