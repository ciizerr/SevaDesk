using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using SevaDesk.Core.Models;
using SevaDesk_App.Services;

namespace SevaDesk_App.ViewModels.Pages;

/// <summary>
/// Wraps a Customer for the customers list, loading expensive stats lazily on hover.
/// </summary>
public partial class CustomerRowModel : ObservableObject
{
    public Customer Customer { get; }

    [ObservableProperty]
    private bool _statsLoaded;

    [ObservableProperty]
    private int _visitCount;

    [ObservableProperty]
    private string _lastVisitText = "—";

    [ObservableProperty]
    private int _fileCount;

    [ObservableProperty]
    private bool _isLoadingStats;

    public CustomerRowModel(Customer customer)
    {
        Customer = customer;
    }

    /// <summary>Called when the row is hovered. Loads stats once then caches.</summary>
    public async Task LoadStatsAsync()
    {
        if (StatsLoaded || IsLoadingStats) return;
        IsLoadingStats = true;
        try
        {
            // Session count + last visit date
            var sessions = (await AppServices.Sessions.GetCustomerSessionsAsync(Customer.Id)).ToList();
            VisitCount = sessions.Count;
            var latest = sessions.OrderByDescending(s => s.StartedAt).FirstOrDefault();
            LastVisitText = latest != null ? latest.StartedAt.ToLocalTime().ToString("dd MMM yy") : "No visits";

            // File count (fast filesystem scan — checks working folder, falls back to backup)
            var folderPath = AppServices.FolderManager.GetEffectiveCustomerFolderPath(Customer.Name, Customer.Code, out _);
            var stats = AppServices.FolderManager.GetFolderStats(folderPath);
            FileCount = stats.TotalFiles;

            StatsLoaded = true;
        }
        catch { }
        finally
        {
            IsLoadingStats = false;
        }
    }
}
