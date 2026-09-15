using System.Diagnostics;
using System.Text.RegularExpressions;
using SevaDesk.Core.Interfaces;
using SevaDesk.Core.Models;

namespace SevaDesk.Infrastructure.FileManager;

public class FolderManager : IFolderManager
{
    private string _baseDirectory;

    public FolderManager()
    {
        // Try user Desktop; check OneDrive desktop redirection fallback
        var defaultDesktop = Environment.GetFolderPath(Environment.SpecialFolder.Desktop);
        var userProfile = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        var oneDriveDesktop = Path.Combine(userProfile, "OneDrive", "Desktop");

        if (Directory.Exists(oneDriveDesktop))
        {
            _baseDirectory = oneDriveDesktop;
        }
        else if (Directory.Exists(defaultDesktop))
        {
            _baseDirectory = defaultDesktop;
        }
        else
        {
            _baseDirectory = Path.Combine(userProfile, "Desktop");
        }
    }

    public string BaseDirectory => _baseDirectory;

    public void SetBaseDirectory(string newPath)
    {
        if (Directory.Exists(newPath))
        {
            _baseDirectory = newPath;
        }
    }

    private static string SanitizeFolderName(string name)
    {
        var invalidChars = new string(Path.GetInvalidFileNameChars()) + new string(Path.GetInvalidPathChars());
        var sanitized = Regex.Replace(name, $"[{Regex.Escape(invalidChars)}]", "");
        return Regex.Replace(sanitized.Trim(), @"\s+", "_");
    }

    public string GetCustomerFolderPath(string customerName, string customerCode)
    {
        var cleanName = SanitizeFolderName(customerName);
        var cleanId = customerCode.Replace("CUST-", "", StringComparison.OrdinalIgnoreCase).Trim();
        if (string.IsNullOrWhiteSpace(cleanId)) cleanId = customerCode;

        var folderName = $"{cleanName}_{cleanId}";
        var newPath = Path.Combine(_baseDirectory, folderName);

        // Backward compatibility: If older legacy folder with CUST- prefix exists, return it
        var legacyName = $"{cleanName}_{customerCode}";
        var legacyPath = Path.Combine(_baseDirectory, legacyName);
        if (!Directory.Exists(newPath) && Directory.Exists(legacyPath))
        {
            return legacyPath;
        }

        return newPath;
    }

    public string EnsureCustomerWorkingFolder(string customerName, string customerCode)
    {
        var customerDir = GetCustomerFolderPath(customerName, customerCode);
        Directory.CreateDirectory(customerDir);

        // Standard persistent identity and documents subfolder
        var sharedDocsDir = Path.Combine(customerDir, "Shared Docs");
        Directory.CreateDirectory(sharedDocsDir);

        return customerDir;
    }

    public string EnsureApplicationSubfolder(string customerFolderPath, string applicationName)
    {
        if (!Directory.Exists(customerFolderPath))
        {
            Directory.CreateDirectory(customerFolderPath);
        }

        var safeAppName = SanitizeFolderName(applicationName);
        var appPath = Path.Combine(customerFolderPath, safeAppName);
        Directory.CreateDirectory(appPath);
        return appPath;
    }

    public IEnumerable<string> GetApplicationSubfolders(string customerFolderPath)
    {
        if (!Directory.Exists(customerFolderPath)) yield break;

        var subdirs = Directory.GetDirectories(customerFolderPath);
        foreach (var dir in subdirs)
        {
            var dirName = Path.GetFileName(dir);
            // Skip reserved / common folders
            if (string.Equals(dirName, "Shared Docs", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(dirName, "00_Unorganised", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(dirName, "01_Shared Documents", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(dirName, "02_Applications", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(dirName, "03_Ready to Print", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            yield return dirName;
        }
    }

    public FolderStats GetFolderStats(string folderPath)
    {
        var stats = new FolderStats();
        if (!Directory.Exists(folderPath)) return stats;

        static int CountFiles(string dir, SearchOption option = SearchOption.AllDirectories)
        {
            try
            {
                if (!Directory.Exists(dir)) return 0;
                return Directory.GetFiles(dir, "*", option).Length;
            }
            catch
            {
                return 0;
            }
        }

        // Unorganised count = loose files in customer root directory + legacy 00_Unorganised if present
        int rootLooseFiles = CountFiles(folderPath, SearchOption.TopDirectoryOnly);
        int legacyUnorganised = CountFiles(Path.Combine(folderPath, "00_Unorganised"));
        stats.UnorganisedCount = rootLooseFiles + legacyUnorganised;

        // Shared docs = 'Shared Docs' + legacy '01_Shared Documents'
        int modernShared = CountFiles(Path.Combine(folderPath, "Shared Docs"));
        int legacyShared = CountFiles(Path.Combine(folderPath, "01_Shared Documents"));
        stats.SharedDocsCount = modernShared + legacyShared;

        // Application folders = all other direct subfolders
        int appFiles = 0;
        foreach (var appFolder in GetApplicationSubfolders(folderPath))
        {
            appFiles += CountFiles(Path.Combine(folderPath, appFolder));
        }
        int legacyApps = CountFiles(Path.Combine(folderPath, "02_Applications"));
        stats.ApplicationsCount = appFiles + legacyApps;

        // Ready to print legacy count
        stats.ReadyToPrintCount = CountFiles(Path.Combine(folderPath, "03_Ready to Print"));

        return stats;
    }

    public void OpenFolderInExplorer(string folderPath)
    {
        if (!Directory.Exists(folderPath))
        {
            Directory.CreateDirectory(folderPath);
        }

        Process.Start(new ProcessStartInfo
        {
            FileName = folderPath,
            UseShellExecute = true
        });
    }

    public IEnumerable<FolderFileItem> GetFolderFiles(string folderPath)
    {
        if (!Directory.Exists(folderPath)) return Enumerable.Empty<FolderFileItem>();

        try
        {
            var dirInfo = new DirectoryInfo(folderPath);
            var files = dirInfo.GetFiles()
                .OrderByDescending(f => f.LastWriteTime)
                .Select(f =>
                {
                    var ext = f.Extension.ToLowerInvariant();
                    string glyph = "\uE8A5"; // generic doc
                    string color = "#3B82F6"; // blue

                    if (ext is ".jpg" or ".jpeg" or ".png" or ".webp" or ".bmp")
                    {
                        glyph = "\uEB9F"; // image icon
                        color = "#10B981"; // emerald
                    }
                    else if (ext is ".pdf")
                    {
                        glyph = "\uEA90"; // pdf icon
                        color = "#EF4444"; // red
                    }
                    else if (ext is ".doc" or ".docx")
                    {
                        glyph = "\uE8C1"; // word doc
                        color = "#2563EB"; // blue
                    }
                    else if (ext is ".xls" or ".xlsx" or ".csv")
                    {
                        glyph = "\uE80A"; // spreadsheet
                        color = "#059669"; // green
                    }
                    else if (ext is ".zip" or ".rar" or ".7z")
                    {
                        glyph = "\uE8B7"; // archive
                        color = "#D97706"; // amber
                    }

                    string formattedSize = f.Length < 1024
                        ? $"{f.Length} B"
                        : f.Length < 1024 * 1024
                            ? $"{f.Length / 1024.0:F1} KB"
                            : $"{f.Length / (1024.0 * 1024.0):F1} MB";

                    return new FolderFileItem
                    {
                        FullPath = f.FullName,
                        Name = f.Name,
                        EditName = Path.GetFileNameWithoutExtension(f.Name),
                        Extension = ext,
                        FileSizeBytes = f.Length,
                        FormattedSize = formattedSize,
                        LastModified = f.LastWriteTime,
                        Glyph = glyph,
                        GlyphColor = color
                    };
                });

            return files.ToList();
        }
        catch
        {
            return Enumerable.Empty<FolderFileItem>();
        }
    }

    public bool RenameFile(string oldFullPath, string newFileName, out string newFullPath, out string errorMessage)
    {
        newFullPath = oldFullPath;
        errorMessage = string.Empty;

        if (string.IsNullOrWhiteSpace(oldFullPath) || !File.Exists(oldFullPath))
        {
            errorMessage = "Source file does not exist.";
            return false;
        }

        if (string.IsNullOrWhiteSpace(newFileName))
        {
            errorMessage = "File name cannot be empty.";
            return false;
        }

        var invalidChars = Path.GetInvalidFileNameChars();
        if (newFileName.IndexOfAny(invalidChars) >= 0)
        {
            errorMessage = "File name contains invalid characters.";
            return false;
        }

        try
        {
            var dir = Path.GetDirectoryName(oldFullPath) ?? string.Empty;
            var oldExt = Path.GetExtension(oldFullPath);

            // If operator omitted the extension, preserve the original extension
            var targetFileName = Path.HasExtension(newFileName)
                ? newFileName.Trim()
                : $"{newFileName.Trim()}{oldExt}";

            var targetPath = Path.Combine(dir, targetFileName);

            if (string.Equals(oldFullPath, targetPath, StringComparison.OrdinalIgnoreCase))
            {
                // Exact same path, no-op success
                newFullPath = targetPath;
                return true;
            }

            if (File.Exists(targetPath))
            {
                errorMessage = $"A file named '{targetFileName}' already exists in this folder.";
                return false;
            }

            File.Move(oldFullPath, targetPath);
            newFullPath = targetPath;
            return true;
        }
        catch (Exception ex)
        {
            errorMessage = ex.Message;
            return false;
        }
    }

    public bool DeleteFile(string filePath, out string errorMessage)
    {
        errorMessage = string.Empty;
        if (string.IsNullOrWhiteSpace(filePath) || !File.Exists(filePath))
        {
            errorMessage = "File does not exist.";
            return false;
        }

        try
        {
            File.Delete(filePath);
            return true;
        }
        catch (Exception ex)
        {
            errorMessage = ex.Message;
            return false;
        }
    }

    public void OpenFileWithDefaultApp(string filePath)
    {
        if (string.IsNullOrWhiteSpace(filePath) || !File.Exists(filePath)) return;

        try
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = filePath,
                UseShellExecute = true
            });
        }
        catch
        {
            // Fallback: select in explorer if default open fails
            try
            {
                Process.Start("explorer.exe", $"/select,\"{filePath}\"");
            }
            catch { }
        }
    }
}

