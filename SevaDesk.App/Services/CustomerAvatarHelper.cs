using System;
using System.IO;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Imaging;

namespace SevaDesk_App.Services;

public static class CustomerAvatarHelper
{
    public static string? ResolvePhotoPath(string? photoPath)
    {
        if (string.IsNullOrWhiteSpace(photoPath))
            return null;

        if (File.Exists(photoPath))
            return photoPath;

        try
        {
            var backupDir = AppServices.FolderManager?.BackupDirectory;
            if (string.IsNullOrWhiteSpace(backupDir) || !Directory.Exists(backupDir))
                return null;

            var baseDir = AppServices.FolderManager?.BaseDirectory;
            if (!string.IsNullOrWhiteSpace(baseDir) && photoPath.StartsWith(baseDir, StringComparison.OrdinalIgnoreCase))
            {
                var relative = Path.GetRelativePath(baseDir, photoPath);
                var candidate = Path.Combine(backupDir, relative);
                if (File.Exists(candidate))
                    return candidate;
            }

            // Fallback: match by customer folder name and relative file path
            var fileName = Path.GetFileName(photoPath);
            var parentDir = Path.GetDirectoryName(photoPath);
            if (!string.IsNullOrEmpty(parentDir))
            {
                var parentName = Path.GetFileName(parentDir);
                if (parentName.Equals("Shared Docs", StringComparison.OrdinalIgnoreCase))
                {
                    var custDir = Path.GetDirectoryName(parentDir);
                    if (!string.IsNullOrEmpty(custDir))
                    {
                        var custFolderName = Path.GetFileName(custDir);
                        var candidate = Path.Combine(backupDir, custFolderName, "Shared Docs", fileName);
                        if (File.Exists(candidate)) return candidate;
                    }
                }
                else
                {
                    var candidate = Path.Combine(backupDir, parentName, fileName);
                    if (File.Exists(candidate)) return candidate;
                }
            }
        }
        catch { }

        return null;
    }

    public static ImageSource? GetProfilePicture(string? photoPath)
    {
        var resolved = ResolvePhotoPath(photoPath);
        if (string.IsNullOrWhiteSpace(resolved) || !File.Exists(resolved))
            return null;

        try
        {
            var bitmap = new BitmapImage
            {
                UriSource = new Uri(resolved),
                DecodePixelWidth = 160
            };
            return bitmap;
        }
        catch
        {
            return null;
        }
    }

    /// <summary>Visible when string is non-empty, Collapsed otherwise.</summary>
    public static Visibility VisibleIfNotEmpty(string? value) =>
        !string.IsNullOrWhiteSpace(value) ? Visibility.Visible : Visibility.Collapsed;

    /// <summary>Visible when string is null/empty, Collapsed otherwise.</summary>
    public static Visibility VisibleIfEmpty(string? value) =>
        string.IsNullOrWhiteSpace(value) ? Visibility.Visible : Visibility.Collapsed;

    /// <summary>Converts bool to Visibility (true → Visible).</summary>
    public static Visibility BoolToVisible(bool value) =>
        value ? Visibility.Visible : Visibility.Collapsed;

    /// <summary>Converts bool to Visibility (true → Collapsed).</summary>
    public static Visibility BoolToCollapsed(bool value) =>
        value ? Visibility.Collapsed : Visibility.Visible;
}
