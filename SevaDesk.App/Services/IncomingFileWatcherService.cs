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
            var desktop = Environment.GetFolderPath(Environment.SpecialFolder.Desktop);
            if (Directory.Exists(desktop)) directoriesToWatch.Add(desktop);
        }

        if (WatchDocuments)
        {
            var docs = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
            if (Directory.Exists(docs)) directoriesToWatch.Add(docs);
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
            // Ignore temporary, download parts, and system files
            var ext = Path.GetExtension(filePath).ToLowerInvariant();
            if (ext is ".crdownload" or ".tmp" or ".part" or ".download" or ".ini") return;

            var fileName = Path.GetFileName(filePath);
            if (fileName.StartsWith("~") || fileName.StartsWith(".")) return;

            // Ignore files already inside working base directory
            if (filePath.StartsWith(workingBase, StringComparison.OrdinalIgnoreCase)) return;

            lock (_lock)
            {
                if (_recentlyHandled.Contains(filePath)) return;
                _recentlyHandled.Add(filePath);
            }

            // Fire after brief delay so file handle closes
            _ = Task.Run(async () =>
            {
                await Task.Delay(800);
                if (!File.Exists(filePath)) return;

                long fileSize = 0;
                try
                {
                    var fi = new FileInfo(filePath);
                    fileSize = fi.Length;
                }
                catch { }

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

    public async Task<bool> RouteFileToCustomerAsync(string sourceFilePath, string targetCustomerFolderPath, bool deleteSource = false)
    {
        try
        {
            if (!File.Exists(sourceFilePath) || !Directory.Exists(targetCustomerFolderPath)) return false;

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
                    File.Copy(sourceFilePath, destPath);
                }
            });

            return true;
        }
        catch
        {
            return false;
        }
    }

    public void Dispose()
    {
        StopWatchers();
    }
}
