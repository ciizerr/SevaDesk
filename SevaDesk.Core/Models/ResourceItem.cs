using CommunityToolkit.Mvvm.ComponentModel;

namespace SevaDesk.Core.Models;

public partial class ResourceItem : ObservableObject
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string Title { get; set; } = string.Empty;
    public string Category { get; set; } = "Forms"; // Forms, Affidavits, Notices, Rates
    public string FileType { get; set; } = "PDF";
    public string FileSize { get; set; } = "1.2 MB";
    public string? FilePath { get; set; }
    public string Glyph { get; set; } = "\uE8A5";

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(StarForeground))]
    private bool _isFavorite;

    public string StarForeground => IsFavorite ? "#F59E0B" : "#808080";
    public DateTime LastModified { get; set; } = DateTime.Now;
}
