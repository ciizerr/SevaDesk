namespace SevaDesk.Core.Models;

public class ActiveSessionItem
{
    public Session Session { get; set; } = new();
    public Customer Customer { get; set; } = new();
    public string FolderPath { get; set; } = string.Empty;
    public FolderStats FolderStats { get; set; } = new();

    public string StatusBadge => Session.Status.ToUpperInvariant();
}
