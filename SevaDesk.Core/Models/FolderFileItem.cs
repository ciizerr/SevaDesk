using CommunityToolkit.Mvvm.ComponentModel;

namespace SevaDesk.Core.Models;

public partial class FolderFileItem : ObservableObject
{
    public string FullPath { get; set; } = string.Empty;

    [ObservableProperty]
    private string _name = string.Empty;

    [ObservableProperty]
    private bool _isRenaming = false;

    [ObservableProperty]
    private string _editName = string.Empty;

    public string Extension { get; set; } = string.Empty;
    public long FileSizeBytes { get; set; }
    public string FormattedSize { get; set; } = string.Empty;
    public DateTime LastModified { get; set; }
    public string FormattedDate => LastModified.ToString("dd MMM yyyy, hh:mm tt");

    public string Glyph { get; set; } = "\uE8A5";
    public string GlyphColor { get; set; } = "#3B82F6";

    public bool IsImage => Extension.Equals(".jpg", StringComparison.OrdinalIgnoreCase) ||
                           Extension.Equals(".jpeg", StringComparison.OrdinalIgnoreCase) ||
                           Extension.Equals(".png", StringComparison.OrdinalIgnoreCase) ||
                           Extension.Equals(".webp", StringComparison.OrdinalIgnoreCase) ||
                           Extension.Equals(".bmp", StringComparison.OrdinalIgnoreCase);

    public bool IsPdf => Extension.Equals(".pdf", StringComparison.OrdinalIgnoreCase);
}

public partial class SubfolderItem : ObservableObject
{
    public string DisplayName { get; set; } = string.Empty;
    public string RelativePath { get; set; } = string.Empty; // "" for Root, "Shared Docs", etc.
    public string FullPath { get; set; } = string.Empty;
    public string Glyph { get; set; } = "\uE8B7";
    public string AccentColor { get; set; } = "#F59E0B";
    public string Description { get; set; } = string.Empty;

    [ObservableProperty]
    private int _fileCount;

    [ObservableProperty]
    private bool _isSelected;
}

public class FolderGroup
{
    public string FolderName { get; set; } = string.Empty;
    public string FolderPath { get; set; } = string.Empty;
    public string Glyph { get; set; } = "\uE8B7";
    public string AccentColor { get; set; } = "#F59E0B";
    public List<FolderFileItem> Files { get; set; } = new();
    public int FileCount => Files.Count;
}
