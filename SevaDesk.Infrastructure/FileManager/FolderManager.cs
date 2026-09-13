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
        var folderName = $"{cleanName}_{customerCode}";
        return Path.Combine(_baseDirectory, folderName);
    }

    public string EnsureCustomerWorkingFolder(string customerName, string customerCode)
    {
        var customerDir = GetCustomerFolderPath(customerName, customerCode);

        string[] subfolders =
        [
            "00_Unorganised",
            "01_Shared Documents",
            "02_Applications",
            "03_Ready to Print"
        ];

        Directory.CreateDirectory(customerDir);

        foreach (var sub in subfolders)
        {
            var subPath = Path.Combine(customerDir, sub);
            Directory.CreateDirectory(subPath);
        }

        return customerDir;
    }

    public FolderStats GetFolderStats(string folderPath)
    {
        var stats = new FolderStats();
        if (!Directory.Exists(folderPath)) return stats;

        static int CountFiles(string dir)
        {
            try
            {
                if (!Directory.Exists(dir)) return 0;
                return Directory.GetFiles(dir, "*", SearchOption.AllDirectories).Length;
            }
            catch
            {
                return 0;
            }
        }

        stats.UnorganisedCount = CountFiles(Path.Combine(folderPath, "00_Unorganised"));
        stats.SharedDocsCount = CountFiles(Path.Combine(folderPath, "01_Shared Documents"));
        stats.ApplicationsCount = CountFiles(Path.Combine(folderPath, "02_Applications"));
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
}
