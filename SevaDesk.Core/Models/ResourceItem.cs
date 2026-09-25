using System.IO;
using CommunityToolkit.Mvvm.ComponentModel;

namespace SevaDesk.Core.Models;

public partial class ResourceItem : ObservableObject
{
    public string Id { get; set; } = Guid.NewGuid().ToString();

    [ObservableProperty]
    private string _title = string.Empty;

    [ObservableProperty]
    private string _category = "Blank Forms"; // Forms, Affidavits, Guidelines, Notices

    [ObservableProperty]
    private string _fileType = "PDF";

    [ObservableProperty]
    private string _fileSize = "1.2 MB";

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasFile))]
    [NotifyPropertyChangedFor(nameof(FileStatusText))]
    [NotifyPropertyChangedFor(nameof(FileLocationDisplay))]
    private string? _filePath;

    [ObservableProperty]
    private string _glyph = "\uE8A5";

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(StarForeground))]
    private bool _isFavorite;

    public string StarForeground => IsFavorite ? "#F59E0B" : "#808080";
    public DateTime LastModified { get; set; } = DateTime.Now;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(RequiredDocsList))]
    [NotifyPropertyChangedFor(nameof(HasRequiredDocs))]
    private string? _requiredDocs;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasNotes))]
    private string? _notes;

    public bool HasFile => !string.IsNullOrWhiteSpace(FilePath) && File.Exists(FilePath);
    public string FileStatusText => HasFile ? "Document Ready" : "File Not Linked";
    public string FileLocationDisplay => HasFile ? (Path.GetFileName(FilePath) ?? "Document file") : "No document file linked yet";
    public bool HasRequiredDocs => !string.IsNullOrWhiteSpace(RequiredDocs);
    public bool HasNotes => !string.IsNullOrWhiteSpace(Notes);

    public IEnumerable<string> RequiredDocsList =>
        string.IsNullOrWhiteSpace(RequiredDocs)
            ? []
            : RequiredDocs.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
}
