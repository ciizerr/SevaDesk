using CommunityToolkit.Mvvm.ComponentModel;

namespace SevaDesk.Core.Models;

public partial class DocumentItem : ObservableObject
{
    public string Id { get; set; } = Guid.NewGuid().ToString();

    [ObservableProperty]
    private string _name = string.Empty;

    public string CustomerName { get; set; } = string.Empty;
    public string? CustomerId { get; set; }
    public string Category { get; set; } = "Unorganised"; // Unorganised, Identity, Education, PrintReady

    [ObservableProperty]
    private string _fileSize = "240 KB";

    [ObservableProperty]
    private string _extension = ".pdf";

    [ObservableProperty]
    private string _status = "Pending Review";

    [ObservableProperty]
    private string _glyph = "\uE8A5"; // Document glyph

    [ObservableProperty]
    private string? _filePath;

    public string? CustomerFolderPath { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.Now;

    [ObservableProperty]
    private bool _isRenaming;

    [ObservableProperty]
    private string _editName = string.Empty;
}

public class CompressionPreset
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string TargetSize { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string IconGlyph { get; set; } = "\uEB9F";
}
