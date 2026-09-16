using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SevaDesk.Core.Models;
using SevaDesk_App.Services;

namespace SevaDesk_App.ViewModels.Pages;

public partial class DocumentsViewModel : StatusViewModel
{
    [ObservableProperty]
    private ObservableCollection<DocumentItem> _pendingDocuments = [];

    [ObservableProperty]
    private ObservableCollection<DocumentItem> _processedDocuments = [];

    [ObservableProperty]
    private ObservableCollection<CompressionPreset> _compressionPresets = [];

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasSelectedDocument))]
    private DocumentItem? _selectedDocument;

    public bool HasSelectedDocument => SelectedDocument != null;

    partial void OnSelectedDocumentChanged(DocumentItem? value)
    {
        RefreshCustomerSubfolders();
    }

    [ObservableProperty]
    private ObservableCollection<SubfolderItem> _customerSubfolders = [];

    [ObservableProperty]
    private int _unorganisedCount;

    public DocumentsViewModel()
    {
        InitializePresets();
        _ = LoadDocumentsAsync();
        AppServices.FileWatcher.FileDetected += OnIncomingFileDetected;
    }

    private void OnIncomingFileDetected(IncomingFileItem item)
    {
        MainWindow.Instance?.DispatcherQueue.TryEnqueue(async () =>
        {
            await LoadDocumentsAsync();
        });
    }

    private void InitializePresets()
    {
        CompressionPresets.Clear();
        CompressionPresets.Add(new CompressionPreset
        {
            Id = "ssc_photo",
            Name = "Passport Photo (SSC/UPSC)",
            TargetSize = "20 KB – 50 KB",
            Description = "Dimensions 3.5cm x 4.5cm, JPEG format",
            IconGlyph = "\uEB9F"
        });
        CompressionPresets.Add(new CompressionPreset
        {
            Id = "govt_sign",
            Name = "Applicant Signature",
            TargetSize = "10 KB – 20 KB",
            Description = "Dimensions 4.0cm x 2.0cm, clear black ink",
            IconGlyph = "\uEDC6"
        });
        CompressionPresets.Add(new CompressionPreset
        {
            Id = "pdf_doc",
            Name = "Govt Portal PDF Upload",
            TargetSize = "< 300 KB",
            Description = "High legibility grayscale/color A4 PDF",
            IconGlyph = "\uE8A5"
        });
        CompressionPresets.Add(new CompressionPreset
        {
            Id = "pan_doc",
            Name = "PAN / NSDL Format",
            TargetSize = "< 2 MB (200 DPI)",
            Description = "Color scan for Form 49A supporting docs",
            IconGlyph = "\uE749"
        });
    }

    [RelayCommand]
    public async Task LoadDocumentsAsync()
    {
        PendingDocuments.Clear();
        ProcessedDocuments.Clear();

        var activeSessions = (await AppServices.Sessions.GetActiveSessionsAsync()).ToList();
        var scannedPaths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        // 1. Scan folders for active sessions
        foreach (var s in activeSessions)
        {
            if (Directory.Exists(s.FolderPath))
            {
                scannedPaths.Add(s.FolderPath);
                ScanCustomerFolder(s.FolderPath, $"{s.Customer.Name} ({s.Customer.Code})");
            }
        }

        // 2. Also scan any other customer folders in BaseDirectory
        var baseDir = AppServices.FolderManager.BaseDirectory;
        if (Directory.Exists(baseDir))
        {
            var customerDirs = Directory.GetDirectories(baseDir);
            foreach (var dir in customerDirs)
            {
                if (!scannedPaths.Contains(dir))
                {
                    var dirName = Path.GetFileName(dir);
                    ScanCustomerFolder(dir, dirName);
                }
            }
        }

        UnorganisedCount = PendingDocuments.Count;
        SelectedDocument = PendingDocuments.FirstOrDefault();
    }

    private void ScanCustomerFolder(string customerFolderPath, string customerDisplayName)
    {
        try
        {
            var dirInfo = new DirectoryInfo(customerFolderPath);

            // A. Loose files directly in customer root (Unorganised / pending triage)
            foreach (var file in dirInfo.GetFiles())
            {
                var ext = file.Extension.ToLowerInvariant();
                var glyph = ext switch
                {
                    ".pdf" => "\uE8A5",
                    ".jpg" or ".jpeg" or ".png" or ".webp" or ".bmp" => "\uEB9F",
                    ".doc" or ".docx" or ".txt" => "\uE8C1",
                    _ => "\uE8A5"
                };

                string sizeStr = file.Length > 1024 * 1024
                    ? $"{file.Length / (1024.0 * 1024.0):F1} MB"
                    : $"{Math.Max(1, file.Length / 1024)} KB";

                PendingDocuments.Add(new DocumentItem
                {
                    Id = file.FullName,
                    Name = file.Name,
                    CustomerName = customerDisplayName,
                    Category = "Loose Files",
                    FileSize = sizeStr,
                    Extension = ext,
                    Status = "Needs Triage",
                    Glyph = glyph,
                    FilePath = file.FullName,
                    CustomerFolderPath = customerFolderPath,
                    CreatedAt = file.CreationTime
                });
            }

            // B. Subfolders
            foreach (var subDir in dirInfo.GetDirectories())
            {
                if (string.Equals(subDir.Name, "00_Unorganised", StringComparison.OrdinalIgnoreCase))
                {
                    foreach (var file in subDir.GetFiles())
                    {
                        var ext = file.Extension.ToLowerInvariant();
                        var glyph = ext switch
                        {
                            ".pdf" => "\uE8A5",
                            ".jpg" or ".jpeg" or ".png" or ".webp" or ".bmp" => "\uEB9F",
                            ".doc" or ".docx" or ".txt" => "\uE8C1",
                            _ => "\uE8A5"
                        };

                        string sizeStr = file.Length > 1024 * 1024
                            ? $"{file.Length / (1024.0 * 1024.0):F1} MB"
                            : $"{Math.Max(1, file.Length / 1024)} KB";

                        PendingDocuments.Add(new DocumentItem
                        {
                            Id = file.FullName,
                            Name = file.Name,
                            CustomerName = customerDisplayName,
                            Category = "Unorganised",
                            FileSize = sizeStr,
                            Extension = ext,
                            Status = "Needs Triage",
                            Glyph = glyph,
                            FilePath = file.FullName,
                            CustomerFolderPath = customerFolderPath,
                            CreatedAt = file.CreationTime
                        });
                    }
                    continue;
                }

                foreach (var file in subDir.GetFiles())
                {
                    var ext = file.Extension.ToLowerInvariant();
                    var glyph = ext switch
                    {
                        ".pdf" => "\uE8A5",
                        ".jpg" or ".jpeg" or ".png" or ".webp" or ".bmp" => "\uEB9F",
                        ".doc" or ".docx" => "\uE8C1",
                        _ => "\uE8A5"
                    };

                    string sizeStr = file.Length > 1024 * 1024
                        ? $"{file.Length / (1024.0 * 1024.0):F1} MB"
                        : $"{Math.Max(1, file.Length / 1024)} KB";

                    ProcessedDocuments.Add(new DocumentItem
                    {
                        Id = file.FullName,
                        Name = file.Name,
                        CustomerName = customerDisplayName,
                        Category = subDir.Name,
                        FileSize = sizeStr,
                        Extension = ext,
                        Status = "Organized",
                        Glyph = glyph,
                        FilePath = file.FullName,
                        CustomerFolderPath = customerFolderPath,
                        CreatedAt = file.CreationTime
                    });
                }
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[DocumentsViewModel] Scan error in {customerFolderPath}: {ex.Message}");
        }
    }

    public string? GetCustomerRootFolder(DocumentItem? doc)
    {
        if (doc == null) return null;

        if (!string.IsNullOrWhiteSpace(doc.CustomerFolderPath) && Directory.Exists(doc.CustomerFolderPath))
        {
            return doc.CustomerFolderPath;
        }

        if (!string.IsNullOrWhiteSpace(doc.FilePath))
        {
            var dir = Path.GetDirectoryName(doc.FilePath);
            if (!string.IsNullOrWhiteSpace(dir))
            {
                var baseDir = AppServices.FolderManager.BaseDirectory.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
                var current = dir;
                while (!string.IsNullOrEmpty(current))
                {
                    var parent = Path.GetDirectoryName(current)?.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
                    if (string.Equals(parent, baseDir, StringComparison.OrdinalIgnoreCase))
                    {
                        return current;
                    }
                    current = Path.GetDirectoryName(current);
                }
                return dir;
            }
        }

        return null;
    }

    public void RefreshCustomerSubfolders()
    {
        CustomerSubfolders.Clear();
        if (SelectedDocument == null) return;

        var customerRoot = GetCustomerRootFolder(SelectedDocument);
        if (string.IsNullOrWhiteSpace(customerRoot) || !Directory.Exists(customerRoot)) return;

        // 1. Standard Shared Docs subfolder (always primary for core docs)
        var sharedPath = Path.Combine(customerRoot, "Shared Docs");
        int sharedCount = 0;
        if (Directory.Exists(sharedPath))
        {
            try { sharedCount = Directory.GetFiles(sharedPath, "*", SearchOption.TopDirectoryOnly).Length; } catch { }
        }
        CustomerSubfolders.Add(new SubfolderItem
        {
            DisplayName = "Shared Docs",
            RelativePath = "Shared Docs",
            FullPath = sharedPath,
            Glyph = "\uE8A5",
            AccentColor = "#0284C7",
            Description = "Shared Docs",
            FileCount = sharedCount
        });

        // 2. All actual customer subfolders on disk
        try
        {
            var dirs = Directory.GetDirectories(customerRoot);
            foreach (var dir in dirs.OrderBy(d => Path.GetFileName(d)))
            {
                var dirName = Path.GetFileName(dir);
                if (string.Equals(dirName, "Shared Docs", StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(dirName, "00_Unorganised", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                int count = 0;
                try { count = Directory.GetFiles(dir, "*", SearchOption.TopDirectoryOnly).Length; } catch { }

                CustomerSubfolders.Add(new SubfolderItem
                {
                    DisplayName = dirName.Replace('_', ' '),
                    RelativePath = dirName,
                    FullPath = dir,
                    Glyph = "\uED25",
                    AccentColor = "#8B5CF6",
                    Description = dirName,
                    FileCount = count
                });
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[DocumentsViewModel] Error reading subfolders: {ex.Message}");
        }
    }

    [RelayCommand]
    public async Task MoveDocumentToSubfolderAsync(string subfolderRelativePath)
    {
        if (SelectedDocument == null || string.IsNullOrWhiteSpace(SelectedDocument.FilePath)) return;

        var sourcePath = SelectedDocument.FilePath;
        if (!File.Exists(sourcePath))
        {
            ShowWarning($"File '{SelectedDocument.Name}' no longer exists on disk.");
            await LoadDocumentsAsync();
            return;
        }

        try
        {
            var customerRoot = GetCustomerRootFolder(SelectedDocument);
            if (string.IsNullOrEmpty(customerRoot))
            {
                ShowError("Cannot determine customer working folder.");
                return;
            }

            var destDir = Path.Combine(customerRoot, subfolderRelativePath);
            if (!Directory.Exists(destDir))
            {
                Directory.CreateDirectory(destDir);
            }

            var destPath = Path.Combine(destDir, SelectedDocument.Name);
            if (File.Exists(destPath))
            {
                var baseName = Path.GetFileNameWithoutExtension(SelectedDocument.Name);
                var ext = Path.GetExtension(SelectedDocument.Name);
                destPath = Path.Combine(destDir, $"{baseName}_{DateTime.Now:HHmmss}{ext}");
            }

            File.Move(sourcePath, destPath);
            var movedDocName = SelectedDocument.Name;
            ShowSuccess($"'{movedDocName}' moved to '{subfolderRelativePath}'.");

            await LoadDocumentsAsync();
        }
        catch (Exception ex)
        {
            ShowError($"Failed to move file: {ex.Message}");
        }
    }

    [RelayCommand]
    public async Task MoveDocumentAsync(string targetCategory)
    {
        await MoveDocumentToSubfolderAsync(targetCategory);
    }

    [RelayCommand]
    public void OpenFile()
    {
        if (SelectedDocument != null && !string.IsNullOrWhiteSpace(SelectedDocument.FilePath) && File.Exists(SelectedDocument.FilePath))
        {
            AppServices.FolderManager.OpenFileWithDefaultApp(SelectedDocument.FilePath);
        }
    }

    [RelayCommand]
    public void ApplyPreset(CompressionPreset preset)
    {
        if (SelectedDocument == null) return;

        ShowInfo($"Preset '{preset.Name}' applied to {SelectedDocument.Name} (Target: {preset.TargetSize}).");
        SelectedDocument.Status = $"Target: {preset.TargetSize}";
    }

    [RelayCommand]
    public void OpenInExplorer()
    {
        if (SelectedDocument != null && !string.IsNullOrWhiteSpace(SelectedDocument.FilePath) && File.Exists(SelectedDocument.FilePath))
        {
            var dir = Path.GetDirectoryName(SelectedDocument.FilePath);
            if (!string.IsNullOrWhiteSpace(dir))
            {
                AppServices.FolderManager.OpenFolderInExplorer(dir);
                return;
            }
        }

        var basePath = AppServices.FolderManager.BaseDirectory;
        AppServices.FolderManager.OpenFolderInExplorer(basePath);
    }
}
