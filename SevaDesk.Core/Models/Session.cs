namespace SevaDesk.Core.Models;

public class Session
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string CustomerId { get; set; } = string.Empty;
    public DateTime StartedAt { get; set; } = DateTime.UtcNow;
    public DateTime? EndedAt { get; set; }
    public string Status { get; set; } = "Active"; // Active, Paused, Completed, Archived
    public string? Notes { get; set; }
    public int DurationSeconds { get; set; }

    public bool IsActive => Status == "Active";
    public bool IsPaused => Status == "Paused";

    public string FormattedDuration
    {
        get
        {
            var totalSec = DurationSeconds;
            if (totalSec <= 0 && EndedAt.HasValue && EndedAt > StartedAt)
            {
                totalSec = (int)(EndedAt.Value - StartedAt).TotalSeconds;
            }
            if (totalSec <= 0) return "0m";

            var ts = TimeSpan.FromSeconds(totalSec);
            if (ts.TotalHours >= 1)
            {
                return $"{(int)ts.TotalHours}h {ts.Minutes}m";
            }
            return $"{ts.Minutes}m {ts.Seconds}s";
        }
    }
}
