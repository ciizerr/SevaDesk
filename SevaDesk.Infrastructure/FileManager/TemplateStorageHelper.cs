using System;
using System.IO;

namespace SevaDesk.Infrastructure.FileManager;

public static class TemplateStorageHelper
{
    private static Func<string?>? _backupDirectoryProvider;

    public static void Initialize(Func<string?> backupDirectoryProvider)
    {
        _backupDirectoryProvider = backupDirectoryProvider;
    }

    /// <summary>
    /// Gets the root folder for offline templates and forms, following the user's chosen hierarchy.
    /// Prefers the configured backup directory; falls back to %LOCALAPPDATA%\SevaDesk\Offline Templates &amp; Forms.
    /// </summary>
    public static string GetTemplatesRootDirectory()
    {
        string? backupDir = null;
        try
        {
            backupDir = _backupDirectoryProvider?.Invoke();
        }
        catch { }

        if (!string.IsNullOrWhiteSpace(backupDir) && Directory.Exists(backupDir))
        {
            var path = Path.Combine(backupDir, "Offline Templates & Forms");
            Directory.CreateDirectory(path);
            return path;
        }

        var appData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        var fallbackPath = Path.Combine(appData, "SevaDesk", "Offline Templates & Forms");
        Directory.CreateDirectory(fallbackPath);
        return fallbackPath;
    }

    /// <summary>
    /// Gets or creates the category subfolder (e.g., "Offline Templates &amp; Forms\Affidavits").
    /// </summary>
    public static string GetCategoryDirectory(string category)
    {
        var root = GetTemplatesRootDirectory();
        var cleanCategory = string.IsNullOrWhiteSpace(category) ? "Blank Forms" : category.Trim();
        foreach (var c in Path.GetInvalidFileNameChars())
        {
            cleanCategory = cleanCategory.Replace(c, '_');
        }

        var catPath = Path.Combine(root, cleanCategory);
        Directory.CreateDirectory(catPath);
        return catPath;
    }

    /// <summary>
    /// Copies a chosen file into the Category-Based Hierarchy ([Backup Directory]\Offline Templates &amp; Forms\[Category]\[FileName].[ext]).
    /// Returns the target full file path.
    /// </summary>
    public static string StoreTemplateFile(string sourceFilePath, string category, string? preferredTitle = null)
    {
        if (!File.Exists(sourceFilePath))
            throw new FileNotFoundException("Selected document file does not exist.", sourceFilePath);

        var targetCategoryDir = GetCategoryDirectory(category);
        var extension = Path.GetExtension(sourceFilePath);

        var baseName = !string.IsNullOrWhiteSpace(preferredTitle)
            ? preferredTitle.Trim()
            : Path.GetFileNameWithoutExtension(sourceFilePath);

        foreach (var c in Path.GetInvalidFileNameChars())
        {
            baseName = baseName.Replace(c, '_');
        }

        var targetPath = Path.Combine(targetCategoryDir, $"{baseName}{extension}");

        // If a file with the same name already exists and is not the identical path, append unique tag
        if (File.Exists(targetPath) && !string.Equals(Path.GetFullPath(sourceFilePath), Path.GetFullPath(targetPath), StringComparison.OrdinalIgnoreCase))
        {
            var tag = DateTime.Now.ToString("yyyyMMdd_HHmmss");
            targetPath = Path.Combine(targetCategoryDir, $"{baseName}_{tag}{extension}");
        }

        File.Copy(sourceFilePath, targetPath, overwrite: true);
        return targetPath;
    }

    /// <summary>
    /// Helper to format raw byte count into human readable KB / MB string.
    /// </summary>
    public static string FormatFileSize(long bytes)
    {
        if (bytes < 1024)
            return $"{bytes} B";
        if (bytes < 1024 * 1024)
            return $"{bytes / 1024.0:F0} KB";
        return $"{bytes / (1024.0 * 1024.0):F1} MB";
    }
}
