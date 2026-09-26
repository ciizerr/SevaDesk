using System;
using System.Collections.Generic;
using CommunityToolkit.Mvvm.ComponentModel;

namespace SevaDesk.Core.Models;

public partial class ActiveSessionItem : ObservableObject
{
    public Session Session { get; set; } = new();
    public Customer Customer { get; set; } = new();
    public string FolderPath { get; set; } = string.Empty;
    public FolderStats FolderStats { get; set; } = new();

    public string Status => Session?.Status ?? "Active";
    public string StatusBadge => Status.ToUpperInvariant();

    public bool IsActive => Status == "Active";
    public bool IsPaused => Status == "Paused";
    public string ToggleActionText => IsPaused ? "Resume" : "Pause";
    public string ToggleActionGlyph => IsPaused ? "\uE768" : "\uE769"; // E768 = Play, E769 = Pause

    public string CustomerContactSummary
    {
        get
        {
            var parts = new List<string>();
            if (!string.IsNullOrWhiteSpace(Customer?.Mobile)) parts.Add(Customer.Mobile);
            if (!string.IsNullOrWhiteSpace(Customer?.Village)) parts.Add(Customer.Village);
            return parts.Count > 0 ? string.Join(" • ", parts) : "No contact details";
        }
    }

    [ObservableProperty]
    private string _elapsedDisplay = "00m 00s";

    [ObservableProperty]
    private ApplicationItem? _linkedApplication;

    public bool HasLinkedApplication => LinkedApplication != null;
    public string LinkedApplicationTitle => LinkedApplication?.Title ?? string.Empty;
    public string LinkedApplicationStatus => LinkedApplication?.Status ?? "Draft";

    partial void OnLinkedApplicationChanged(ApplicationItem? value)
    {
        OnPropertyChanged(nameof(HasLinkedApplication));
        OnPropertyChanged(nameof(LinkedApplicationTitle));
        OnPropertyChanged(nameof(LinkedApplicationStatus));
    }

    public void NotifyLinkedApplicationChanged()
    {
        OnPropertyChanged(nameof(LinkedApplication));
        OnPropertyChanged(nameof(HasLinkedApplication));
        OnPropertyChanged(nameof(LinkedApplicationTitle));
        OnPropertyChanged(nameof(LinkedApplicationStatus));
    }

    public void NotifyStatusChanged()
    {
        OnPropertyChanged(nameof(Status));
        OnPropertyChanged(nameof(StatusBadge));
        OnPropertyChanged(nameof(IsActive));
        OnPropertyChanged(nameof(IsPaused));
        OnPropertyChanged(nameof(ToggleActionText));
        OnPropertyChanged(nameof(ToggleActionGlyph));
    }

    public void UpdateElapsed()
    {
        if (Session == null) return;

        if (IsPaused)
        {
            var ts = TimeSpan.FromSeconds(Math.Max(0, Session.DurationSeconds));
            if (ts.TotalHours >= 1)
            {
                ElapsedDisplay = $"{(int)ts.TotalHours}h {ts.Minutes:D2}m {ts.Seconds:D2}s (Paused)";
            }
            else
            {
                ElapsedDisplay = $"{ts.Minutes:D2}m {ts.Seconds:D2}s (Paused)";
            }
            return;
        }

        var startedUtc = Session.StartedAt.Kind == DateTimeKind.Utc
            ? Session.StartedAt
            : (Session.StartedAt.Kind == DateTimeKind.Unspecified
                ? DateTime.SpecifyKind(Session.StartedAt, DateTimeKind.Utc)
                : Session.StartedAt.ToUniversalTime());

        var diff = TimeSpan.FromSeconds(Math.Max(0, Session.DurationSeconds)) + (DateTime.UtcNow - startedUtc);
        if (diff.TotalSeconds < 0) diff = TimeSpan.Zero;

        if (diff.TotalHours >= 1)
        {
            ElapsedDisplay = $"{(int)diff.TotalHours}h {diff.Minutes:D2}m {diff.Seconds:D2}s";
        }
        else
        {
            ElapsedDisplay = $"{diff.Minutes:D2}m {diff.Seconds:D2}s";
        }
    }
}
