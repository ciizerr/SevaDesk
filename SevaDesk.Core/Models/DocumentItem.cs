namespace SevaDesk.Core.Models;

public class DocumentItem
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string Name { get; set; } = string.Empty;
    public string CustomerName { get; set; } = string.Empty;
    public string Category { get; set; } = "Unorganised"; // Unorganised, Identity, Education, PrintReady
    public string FileSize { get; set; } = "240 KB";
    public string Extension { get; set; } = ".pdf";
    public string Status { get; set; } = "Pending Review";
    public string Glyph { get; set; } = "\uE8A5"; // Document glyph
    public string? FilePath { get; set; }
    public string? CustomerFolderPath { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.Now;
}

public class CompressionPreset
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string TargetSize { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string IconGlyph { get; set; } = "\uEB9F";
}
