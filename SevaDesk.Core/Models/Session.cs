namespace SevaDesk.Core.Models;

public class Session
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string CustomerId { get; set; } = string.Empty;
    public DateTime StartedAt { get; set; } = DateTime.UtcNow;
    public DateTime? EndedAt { get; set; }
    public string Status { get; set; } = "Active"; // Active, Paused, Completed, Archived
    public string? Notes { get; set; }

    public bool IsActive => Status == "Active";
    public bool IsPaused => Status == "Paused";
}
